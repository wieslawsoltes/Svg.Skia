#!/usr/bin/env python3
"""Compare a selected WebGPU canvas bridge with the same build's legacy bridge."""
from __future__ import annotations

import argparse
import contextlib
import json
import shutil
import sys
import time
from pathlib import Path
from typing import Any, Callable

from PIL import Image
from playwright.sync_api import Page, sync_playwright

sys.path.insert(0, str(Path(__file__).resolve().parents[1] / "UnoDraw.BrowserSmoke"))
from backend import launch_arguments, renderer_query  # noqa: E402
from smoke import (READY, artwork_pixels, artwork_transition, assert_artwork, assert_rendered,  # noqa: E402
                   assert_workspace_affordances, changed_pixels, record_backend, serve,
                   workspace_affordance_pixels)
from performance import next_frame, screenshot_changed
from quality import (LIVE_BRIDGES, STATE_NAMES, assert_native_size, bridge_query, compare_bridge_states,
                     is_welcome_green, matrix_failures, raw_comparison, validate_graphite_coverage, verify_bridge)


DESKTOP = {"width": 1440, "height": 960}
COMPACT = {"width": 390, "height": 844}
DPRS = (1, 1.25, 2)


def require_document_change(before: Path, after: Path, minimum: int) -> None:
    changed = screenshot_changed(before, after)
    if changed < minimum:
        raise AssertionError(f"The interaction overlay did not change enough document pixels: {changed}")


def navigator_artwork(path: Path) -> dict[str, int]:
    with Image.open(path) as image:
        region = image.convert("RGB").crop((1100, 120, 1410, 420))
        counts = {"indigo": 0, "green": 0, "peach": 0}
        for pixel in region.get_flattened_data():
            red, green, blue = pixel
            counts["indigo"] += 40 <= red <= 170 and blue > red + 45 and blue > green + 45
            counts["green"] += is_welcome_green(pixel)
            counts["peach"] += red > 235 and 150 <= green <= 235 and 100 <= blue <= 190 and red > green + 15 and green > blue + 12
        return counts


def require_navigator(path: Path) -> None:
    counts = navigator_artwork(path)
    if any(value < 25 for value in counts.values()):
        raise AssertionError(f"Navigator preview artwork is missing: {counts}")


def require_navigator_hidden(path: Path) -> None:
    counts = navigator_artwork(path)
    if all(value >= 25 for value in counts.values()):
        raise AssertionError("Navigator preview is still visible after opening Properties")


def capture(page: Page, directory: Path, name: str, viewport: dict[str, int], dpr: float,
            *, navigator: bool = False, condition: Callable[[Path], Any] | None = None) -> dict[str, Any]:
    css = directory / f"{name}-css.png"
    raw = directory / f"{name}.png"
    previous = directory / f"{name}-previous.png"
    deadline = time.monotonic() + 30
    stable = 0
    failure = "No completed frame"
    while time.monotonic() < deadline:
        # CSS screenshots are only semantic/readiness evidence. The A/B image
        # below and every quantitative quality comparison retain device pixels.
        page.screenshot(path=str(css), scale="css")
        page.screenshot(path=str(raw), scale="device")
        try:
            if viewport == DESKTOP:
                assert_artwork(artwork_pixels(css))
                if navigator:
                    require_navigator(css)
            else:
                assert_rendered(css)
                if viewport == COMPACT:
                    assert_workspace_affordances(workspace_affordance_pixels(css))
            if condition:
                condition(css)
            with Image.open(raw) as image:
                assert_native_size(image.size, viewport, dpr)
            if previous.exists():
                raw_comparison(previous, raw, viewport, dpr, navigator=navigator)
                stable += 1
                if stable >= 2:
                    previous.unlink(missing_ok=True)
                    return {"name": name, "viewport": viewport, "dpr": dpr,
                            "navigator": navigator, "raw": raw.name, "readiness_css": css.name}
        except AssertionError as error:
            stable = 0
            failure = str(error)
        shutil.copyfile(raw, previous)
        next_frame(page)
    raise AssertionError(f"{name} did not settle: {failure}")


def command(page: Page, text: str, directory: Path) -> None:
    before = directory / "command-before.png"
    opened = directory / "command-opened.png"
    page.screenshot(path=str(before), scale="css")
    page.keyboard.press("Control+Shift+F12")
    deadline = time.monotonic() + 15
    while time.monotonic() < deadline:
        page.screenshot(path=str(opened), scale="css")
        if changed_pixels(before, opened) >= 2000:
            break
        next_frame(page)
    else:
        raise AssertionError("Command search did not open")
    page.keyboard.type(text)
    next_frame(page)
    page.keyboard.press("Enter")
    next_frame(page)


def run_case(browser: Any, url: str, bridge: str, dpr: float, output: Path, *,
             graphite_coverage: str | None = None) -> dict[str, Any]:
    validate_graphite_coverage(bridge, graphite_coverage)
    query = renderer_query("webgpu") + "&" + bridge_query(bridge, graphite_coverage=graphite_coverage)
    output.mkdir(parents=True, exist_ok=True)
    report: dict[str, Any] = {"bridge": bridge, "graphite_coverage": graphite_coverage, "dpr": dpr, "passed": False,
                              "backend_mode": "webgpu", "backend_selection": {},
                              "console": [], "page_errors": [], "resource_errors": [], "states": []}
    context = browser.new_context(viewport=DESKTOP, device_scale_factor=dpr, locale="en-US",
                                  timezone_id="UTC", color_scheme="dark", reduced_motion="no-preference")
    context.add_init_script(path=str(Path(__file__).with_name("instrumentation.js")))
    page = context.new_page()
    page.on("console", lambda message: report["console"].append(
        {"type": message.type, "text": message.text, "location": message.location}))
    page.on("pageerror", lambda error: report["page_errors"].append(str(error)))
    page.on("response", lambda response: report["resource_errors"].append(
        {"url": response.url, "status": response.status})
        if response.status >= 400 and not response.url.endswith("/favicon.ico") else None)
    page.on("requestfailed", lambda request: report["resource_errors"].append(
        {"url": request.url, "failure": request.failure}) if request.failure != "net::ERR_ABORTED" else None)
    try:
        page.goto(url + query,
                  wait_until="domcontentloaded", timeout=120_000)
        page.wait_for_function(READY, timeout=120_000)
        report["actual_device_pixel_ratio"] = page.evaluate("devicePixelRatio")
        if report["actual_device_pixel_ratio"] != dpr:
            raise AssertionError("The requested device pixel ratio was not applied")
        report["states"].append(capture(page, output, "01-desktop", DESKTOP, dpr))
        record_backend(page, report, "startup", 0, allow_single_canvas_provider=bridge == "single-canvas")
        # Cached composition first reuses its backdrop during the later overlay
        # states. Startup must already prove the requested upload path; completion
        # additionally requires actual cache reuse, never just a preference.
        verify_bridge(report["console"], bridge, require_reuse=False, graphite_coverage=graphite_coverage)
        fitted = output / "01-desktop-css.png"
        page.keyboard.press("Control+1")
        report["states"].append(capture(page, output, "02-actual-size", DESKTOP, dpr,
                                        condition=lambda path: artwork_transition(fitted, path, restore=False)))
        page.keyboard.press("Control+0")
        report["states"].append(capture(page, output, "03-refitted", DESKTOP, dpr,
                                        condition=lambda path: artwork_transition(fitted, path, restore=True)))

        page.mouse.click(470, 430)
        page.keyboard.press("Control+Shift+A")
        page.keyboard.press("Shift+B")
        page.mouse.move(650, 480)
        report["states"].append(capture(page, output, "03b-brush-overlay", DESKTOP, dpr,
                                        condition=lambda path: require_document_change(fitted, path, 20)))
        page.keyboard.press("V")
        page.keyboard.press("Control+A")
        page.mouse.move(760, 16)
        report["states"].append(capture(page, output, "03c-selection-overlay", DESKTOP, dpr,
                                        condition=lambda path: require_document_change(fitted, path, 50)))
        page.keyboard.press("Control+Shift+A")

        command(page, "Navigator panel", output)
        report["states"].append(capture(page, output, "04-navigator-visible", DESKTOP, dpr, navigator=True))
        command(page, "Properties panel", output)
        report["states"].append(capture(page, output, "05-navigator-hidden", DESKTOP, dpr,
                                        condition=lambda path: (artwork_transition(fitted, path, restore=True),
                                                                require_navigator_hidden(path))))
        command(page, "Navigator panel", output)
        report["states"].append(capture(page, output, "06-navigator-shown-again", DESKTOP, dpr, navigator=True))
        report["navigator_restore"] = raw_comparison(output / "04-navigator-visible.png",
                                                     output / "06-navigator-shown-again.png", DESKTOP, dpr,
                                                     navigator=True)

        page.set_viewport_size(COMPACT)
        report["states"].append(capture(page, output, "07-compact", COMPACT, dpr))
        # A real resize collapses the editor's available content area. This
        # exercises zero/near-zero surface lifetime without mutating app styles.
        page.set_viewport_size({"width": 80, "height": 80})
        for _ in range(3):
            next_frame(page)
        page.screenshot(path=str(output / "08-small-window.png"), scale="device")
        page.set_viewport_size(DESKTOP)
        report["states"].append(capture(page, output, "09-desktop-restored", DESKTOP, dpr, navigator=True,
                                        condition=lambda path: artwork_transition(fitted, path, restore=True)))
        report["resize_restore"] = raw_comparison(output / "06-navigator-shown-again.png",
                                                  output / "09-desktop-restored.png", DESKTOP, dpr,
                                                  navigator=True)
        verify_bridge(report["console"], bridge, graphite_coverage=graphite_coverage)
        errors = [entry for entry in report["console"] if entry["type"] == "error"
                  and not entry["location"].get("url", "").endswith("/favicon.ico")]
        if errors or report["page_errors"] or report["resource_errors"]:
            raise AssertionError("Bridge A/B produced runtime/resource errors")
        report["passed"] = True
    except Exception as error:
        report["failure"] = f"{type(error).__name__}: {error}"
        with contextlib.suppress(Exception):
            page.screenshot(path=str(output / "failure.png"), scale="device", timeout=5000)
    finally:
        context.close()
        (output / "report.json").write_text(json.dumps(report, indent=2) + "\n", encoding="utf-8")
    return report


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--package", type=Path, required=True)
    parser.add_argument("--output", type=Path, required=True)
    parser.add_argument("--candidate-bridge", choices=tuple(bridge for bridge in LIVE_BRIDGES if bridge != "legacy"),
                        default="persistent",
                        help="Compare the selected implementation with the same package's legacy raster bridge")
    args = parser.parse_args()
    package, output = args.package.resolve(), args.output.resolve()
    if not (package / "wwwroot/index.html").is_file():
        parser.error("--package must contain a published wwwroot/index.html")
    if output == package or package in output.parents:
        parser.error("--output must be outside the published application")
    output.mkdir(parents=True, exist_ok=True)
    report: dict[str, Any] = {"passed": False, "graphics": "SwiftShader WebGPU API correctness; not hardware performance",
                              "started_unix": time.time(), "cases": [], "comparisons": [],
                              "expected_case_count": len(DPRS) * 2, "expected_states": list(STATE_NAMES),
                              "failures": [], "candidate_bridge": args.candidate_bridge,
                              "graphite_coverage": None,
                              "package": json.loads((package / "benchmark-package.json").read_text(encoding="utf-8"))}
    try:
        with serve(package / "wwwroot") as url, sync_playwright() as playwright:
            arguments = launch_arguments("webgpu")
            report["browser_launch_arguments"] = arguments
            for dpr in DPRS:
                cases = {}
                for bridge in ("legacy", args.candidate_bridge):
                    coverage = None
                    directory = output / f"dpr-{dpr}" / bridge
                    directory.mkdir(parents=True, exist_ok=True)
                    browser = None
                    try:
                        # A browser/context failure in one case must not hide the
                        # other independent bridge/DPR cases.
                        browser = playwright.chromium.launch(headless=True, args=arguments)
                        report["browser_version"] = browser.version
                        result = run_case(browser, url, bridge, dpr, directory, graphite_coverage=coverage)
                        result["browser_version"] = browser.version
                    except Exception as error:
                        result = {"bridge": bridge, "graphite_coverage": coverage, "dpr": dpr, "passed": False, "states": [],
                                  "failure": f"{type(error).__name__}: {error}"}
                    finally:
                        if browser:
                            with contextlib.suppress(Exception):
                                browser.close()
                    (directory / "report.json").write_text(json.dumps(result, indent=2) + "\n", encoding="utf-8")
                    report["cases"].append(result)
                    cases[bridge] = result
                    print(f"Bridge {bridge} DPR {dpr}: " + ("passed" if result["passed"] else result["failure"]), flush=True)

                def compare(expected: dict[str, Any], state: dict[str, Any]) -> dict[str, Any]:
                    if any(expected[key] != state[key] for key in ("viewport", "dpr", "navigator")):
                        raise AssertionError("The two bridge states have different viewport/DPR/Navigator conditions")
                    return raw_comparison(output / f"dpr-{dpr}" / "legacy" / expected["raw"],
                                          output / f"dpr-{dpr}" / args.candidate_bridge / state["raw"],
                                          state["viewport"], dpr, navigator=state["navigator"])

                rows = compare_bridge_states(cases, dpr, compare, candidate_bridge=args.candidate_bridge)
                report["comparisons"].extend(rows)
                for row in rows:
                    if not row["passed"]:
                        print(f"Bridge comparison DPR {dpr} {row['state']}: {row['failure']}", flush=True)
        report["failures"] = matrix_failures(report["cases"], report["comparisons"], DPRS,
                                               candidate_bridge=args.candidate_bridge)
        report["passed"] = not report["failures"]
        if report["failures"]:
            report["failure"] = f"{len(report['failures'])} case/state failures; first: {report['failures'][0]}"
    except Exception as error:
        report["failure"] = f"{type(error).__name__}: {error}"
        report["failures"].append(report["failure"])
    finally:
        report["duration_seconds"] = time.time() - report["started_unix"]
        (output / "report.json").write_text(json.dumps(report, indent=2) + "\n", encoding="utf-8")
    return 0 if report["passed"] else 1


if __name__ == "__main__":
    raise SystemExit(main())
