#!/usr/bin/env python3
"""Compare old/new UnoDraw UI pixels at native high DPI on both real graphics APIs."""
from __future__ import annotations

import argparse
import contextlib
import json
import sys
import time
from pathlib import Path
from typing import Any, Callable

from playwright.sync_api import Page, sync_playwright

sys.path.insert(0, str(Path(__file__).resolve().parents[1] / "UnoDraw.BrowserPerformance"))
from bridge_quality import capture, require_document_change  # noqa: E402
from performance import next_frame, park_pointer, renderer_evidence  # noqa: E402
from quality import raw_comparison, verify_bridge  # noqa: E402
from backend import launch_arguments, renderer_query  # noqa: E402
from smoke import READY, artwork_transition, serve  # noqa: E402
from contracts import (BACKENDS, DESKTOP, DPRS, EXECUTION_MODES, ROLES, STATE_NAMES, compare_states,  # noqa: E402
                       load_packages, matrix_failures, save_ruler_strips)


def run_case(browser: Any, url: str, package: dict[str, Any], backend: str,
             dpr: float, output: Path) -> dict[str, Any]:
    report: dict[str, Any] = {"role": package["role"], "source_sha": package["source_sha"],
                              "execution_mode": package["execution_mode"],
                              "backend": backend, "dpr": dpr, "passed": False,
                              "console": [], "page_errors": [], "resource_errors": [], "states": []}
    context = browser.new_context(viewport=DESKTOP, device_scale_factor=dpr, locale="en-US",
                                  timezone_id="UTC", color_scheme="dark", reduced_motion="no-preference")
    instrumentation = Path(__file__).resolve().parents[1] / "UnoDraw.BrowserPerformance/instrumentation.js"
    context.add_init_script(path=str(instrumentation))
    page = context.new_page()
    page.on("console", lambda message: report["console"].append(
        {"type": message.type, "text": message.text, "location": message.location}))
    page.on("pageerror", lambda error: report["page_errors"].append(str(error)))
    page.on("response", lambda response: report["resource_errors"].append(
        {"url": response.url, "status": response.status})
        if response.status >= 400 and not response.url.endswith("/favicon.ico") else None)
    page.on("requestfailed", lambda request: report["resource_errors"].append(
        {"url": request.url, "failure": request.failure}) if request.failure != "net::ERR_ABORTED" else None)

    def snapshot(name: str, condition: Callable[[Path], Any] | None = None) -> None:
        park_pointer(page)
        state = capture(page, output, name, DESKTOP, dpr, condition=condition)
        state["ruler_strips"] = save_ruler_strips(output / state["raw"], dpr)
        report["states"].append(state)

    try:
        page.goto(url + renderer_query(backend), wait_until="domcontentloaded", timeout=120_000)
        page.wait_for_function(READY, timeout=120_000)
        report["environment"] = page.evaluate("""() => ({
            viewport: {width: innerWidth, height: innerHeight}, device_pixel_ratio: devicePixelRatio,
            cross_origin_isolated: crossOriginIsolated, user_agent: navigator.userAgent,
            browser_fonts: [...document.fonts].map(font => ({family: font.family,
                weight: font.weight, style: font.style, status: font.status}))
        })""")
        if report["environment"]["viewport"] != DESKTOP or report["environment"]["device_pixel_ratio"] != dpr:
            raise AssertionError("The fixed viewport/native-DPR contract changed")
        if report["environment"]["cross_origin_isolated"]:
            raise AssertionError("Quality hosting must match GitHub Pages without COOP/COEP")
        # READY can precede the first scheduled paint. Observe completed artwork
        # before sending focus/keyboard input or checking the active context.
        capture(page, output, "startup", DESKTOP, dpr)
        report["renderer"] = renderer_evidence(page, report["console"], package, backend)
        persistent = package["role"] == "candidate" and backend == "webgpu"
        if persistent:
            verify_bridge(report["console"], "persistent")
        page.mouse.click(470, 430)
        page.keyboard.press("V")
        page.keyboard.press("Control+Shift+A")
        page.keyboard.press("Control+0")
        snapshot("01-desktop")
        fitted = output / "01-desktop-css.png"

        page.keyboard.press("Control+1")
        snapshot("02-actual-size", lambda path: artwork_transition(fitted, path, restore=False))
        page.keyboard.press("Control+0")
        # Start from the fitted camera in both builds, then exercise a fractional
        # translation so native-DPR ruler alignment is not tested only on integers.
        capture(page, output, "pan-start", DESKTOP, dpr,
                condition=lambda path: artwork_transition(fitted, path, restore=True))
        page.mouse.move(450, 430)
        page.mouse.down(button="middle")
        for step in range(1, 9):
            page.mouse.move(450 + 37.25 * step / 8, 430 - 18.5 * step / 8)
            next_frame(page)
        page.mouse.up(button="middle")
        snapshot("03-pan", lambda path: artwork_transition(fitted, path, restore=False))

        page.keyboard.press("Control+0")
        snapshot("04-refitted", lambda path: artwork_transition(fitted, path, restore=True))
        report["refit_restore"] = raw_comparison(output / "01-desktop.png", output / "04-refitted.png", DESKTOP, dpr)
        page.keyboard.press("Control+A")
        snapshot("05-selection", lambda path: require_document_change(fitted, path, 50))
        if persistent:
            verify_bridge(report["console"], "persistent")
        allowed = set(report["renderer"].get("expected_console_error_indices", []))
        errors = [entry for index, entry in enumerate(report["console"])
                  if entry["type"] == "error" and index not in allowed
                  and not entry["location"].get("url", "").endswith("/favicon.ico")]
        if errors or report["page_errors"] or report["resource_errors"]:
            raise AssertionError("Old/new render quality produced runtime/resource errors")
        report["passed"] = True
    except Exception as error:
        report["failure"] = f"{type(error).__name__}: {error}"
        with contextlib.suppress(Exception):
            page.screenshot(path=str(output / "failure.png"), scale="device", timeout=5000)
    finally:
        with contextlib.suppress(Exception):
            context.close()
    return report


def write_summary(report: dict[str, Any], output: Path) -> None:
    lines = ["# UnoDraw old/new high-DPI rendering quality", "",
             "Native pixels on SwiftShader WebGPU and Skia/WebGL; this is a correctness gate, not a performance measurement.", "",
             f"Both packages must use Release `{report['execution_mode']}`.", "",
             "| Build | Backend | DPR | Complete case |", "| --- | --- | ---: | --- |"]
    for case in report["cases"]:
        lines.append(f"| {case['role']} | {case['backend']} | {case['dpr']} | {'passed' if case['passed'] else 'failed'} |")
    lines += ["", "| Backend | DPR | State | Native comparison |", "| --- | ---: | --- | --- |"]
    for row in report["comparisons"]:
        lines.append(f"| {row['backend']} | {row['device_pixel_ratio']} | {row['state']} | {'passed' if row['passed'] else 'failed'} |")
    if report["failures"]:
        lines += ["", "## Failures", "", *["- " + failure for failure in report["failures"]]]
    lines += ["", "Full device-pixel PNGs, isolated native ruler strips, per-case reports, and package provenance accompany this report.", ""]
    (output / "summary.md").write_text("\n".join(lines), encoding="utf-8")


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--packages", type=Path, required=True)
    parser.add_argument("--output", type=Path, required=True)
    parser.add_argument("--execution-mode", choices=EXECUTION_MODES, default="InterpreterAndAOT",
                        help="Required mode for both correctness-test packages; Interpreter never supplies performance results")
    args = parser.parse_args()
    output = args.output.resolve()
    output.mkdir(parents=True, exist_ok=True)
    report: dict[str, Any] = {"schema": 1, "passed": False, "started_unix": time.time(),
                              "execution_mode": args.execution_mode,
                              "graphics": "SwiftShader API correctness; not physical GPU performance",
                              "packages": [], "cases": [], "comparisons": [], "failures": [],
                              "device_pixel_ratios": DPRS, "backends": BACKENDS,
                              "expected_case_count": len(ROLES) * len(BACKENDS) * len(DPRS),
                              "expected_states": STATE_NAMES}
    try:
        packages = load_packages(args.packages.resolve(), expected_execution_mode=args.execution_mode)
        report["packages"] = list(packages.values())
        if any(Path(package["path"]) == output or Path(package["path"]) in output.parents for package in packages.values()):
            raise ValueError("Quality artifacts must be outside the served application packages")
        with contextlib.ExitStack() as stack, sync_playwright() as playwright:
            urls = {role: stack.enter_context(serve(Path(package["path"]) / "wwwroot")) for role, package in packages.items()}
            for backend in BACKENDS:
                arguments = launch_arguments(backend)
                for dpr in DPRS:
                    cases = {}
                    for role in ROLES:
                        relative = Path(backend) / f"dpr-{dpr}" / role
                        directory = output / relative
                        directory.mkdir(parents=True, exist_ok=True)
                        browser = None
                        try:
                            browser = playwright.chromium.launch(headless=True, args=arguments)
                            result = run_case(browser, urls[role], packages[role], backend, dpr, directory)
                            result["browser_version"] = browser.version
                        except Exception as error:
                            result = {"role": role, "source_sha": packages[role]["source_sha"],
                                      "execution_mode": packages[role]["execution_mode"],
                                      "backend": backend, "dpr": dpr, "passed": False, "states": [],
                                      "failure": f"{type(error).__name__}: {error}"}
                        finally:
                            if browser:
                                with contextlib.suppress(Exception):
                                    browser.close()
                        result["artifact_directory"] = str(relative)
                        result["browser_launch_arguments"] = arguments
                        (directory / "report.json").write_text(json.dumps(result, indent=2) + "\n", encoding="utf-8")
                        report["cases"].append(result)
                        cases[role] = result
                        print(f"Render quality {role} {backend} DPR {dpr}: "
                              + ("passed" if result["passed"] else result["failure"]), flush=True)

                    def compare(before: dict[str, Any], after: dict[str, Any]) -> dict[str, Any]:
                        return raw_comparison(output / cases["baseline"]["artifact_directory"] / before["raw"],
                                              output / cases["candidate"]["artifact_directory"] / after["raw"], DESKTOP, dpr)

                    rows = compare_states(cases, backend, dpr, compare)
                    report["comparisons"].extend(rows)
                    for row in rows:
                        if not row["passed"]:
                            print(f"Render comparison {backend} DPR {dpr} {row['state']}: {row['failure']}", flush=True)
        report["failures"] = matrix_failures(report["cases"], report["comparisons"])
        report["passed"] = not report["failures"]
    except Exception as error:
        report["failures"].append(f"{type(error).__name__}: {error}")
    finally:
        report["duration_seconds"] = time.time() - report["started_unix"]
        (output / "report.json").write_text(json.dumps(report, indent=2) + "\n", encoding="utf-8")
        write_summary(report, output)
    return 0 if report["passed"] else 1


if __name__ == "__main__":
    raise SystemExit(main())
