#!/usr/bin/env python3
"""Compare persistent WebGPU canvas upload against the same build's legacy bridge."""
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
from backend import launch_arguments  # noqa: E402
from smoke import (READY, artwork_pixels, artwork_transition, assert_artwork, assert_rendered,  # noqa: E402
                   assert_workspace_affordances, changed_pixels, record_backend, serve,
                   workspace_affordance_pixels)
from performance import next_frame, screenshot_changed
from quality import assert_native_size, raw_comparison, verify_bridge


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
        for red, green, blue in region.get_flattened_data():
            counts["indigo"] += 40 <= red <= 170 and blue > red + 45 and blue > green + 45
            counts["green"] += green > red + 20 and green > blue + 5
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


def run_case(browser: Any, url: str, bridge: str, dpr: float, output: Path) -> dict[str, Any]:
    output.mkdir(parents=True, exist_ok=True)
    report: dict[str, Any] = {"bridge": bridge, "dpr": dpr, "passed": False,
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
        page.goto(url + ("?canvasBridge=legacy" if bridge == "legacy" else ""),
                  wait_until="domcontentloaded", timeout=120_000)
        page.wait_for_function(READY, timeout=120_000)
        report["actual_device_pixel_ratio"] = page.evaluate("devicePixelRatio")
        if report["actual_device_pixel_ratio"] != dpr:
            raise AssertionError("The requested device pixel ratio was not applied")
        record_backend(page, report, "startup", 0)
        report["states"].append(capture(page, output, "01-desktop", DESKTOP, dpr))
        verify_bridge(report["console"], bridge)
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
        verify_bridge(report["console"], bridge)
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
    args = parser.parse_args()
    package, output = args.package.resolve(), args.output.resolve()
    if not (package / "wwwroot/index.html").is_file():
        parser.error("--package must contain a published wwwroot/index.html")
    if output == package or package in output.parents:
        parser.error("--output must be outside the published application")
    output.mkdir(parents=True, exist_ok=True)
    report: dict[str, Any] = {"passed": False, "graphics": "SwiftShader WebGPU API correctness; not hardware performance",
                              "started_unix": time.time(), "cases": [], "comparisons": [],
                              "package": json.loads((package / "benchmark-package.json").read_text(encoding="utf-8"))}
    try:
        with serve(package / "wwwroot") as url, sync_playwright() as playwright:
            arguments = launch_arguments("webgpu")
            browser = playwright.chromium.launch(headless=True, args=arguments)
            try:
                report["browser_version"] = browser.version
                report["browser_launch_arguments"] = arguments
                for dpr in DPRS:
                    cases = {}
                    for bridge in ("legacy", "persistent"):
                        directory = output / f"dpr-{dpr}" / bridge
                        result = run_case(browser, url, bridge, dpr, directory)
                        report["cases"].append(result)
                        cases[bridge] = result
                        print(f"Bridge {bridge} DPR {dpr}: " + ("passed" if result["passed"] else result["failure"]), flush=True)
                        if not result["passed"]:
                            raise AssertionError(result["failure"])
                    legacy = {state["name"]: state for state in cases["legacy"]["states"]}
                    for state in cases["persistent"]["states"]:
                        expected = legacy[state["name"]]
                        row = raw_comparison(output / f"dpr-{dpr}" / "legacy" / expected["raw"],
                                             output / f"dpr-{dpr}" / "persistent" / state["raw"],
                                             state["viewport"], dpr, navigator=state["navigator"])
                        row["state"] = state["name"]
                        report["comparisons"].append(row)
            finally:
                browser.close()
        report["passed"] = True
    except Exception as error:
        report["failure"] = f"{type(error).__name__}: {error}"
    finally:
        report["duration_seconds"] = time.time() - report["started_unix"]
        (output / "report.json").write_text(json.dumps(report, indent=2) + "\n", encoding="utf-8")
    return 0 if report["passed"] else 1


if __name__ == "__main__":
    raise SystemExit(main())
