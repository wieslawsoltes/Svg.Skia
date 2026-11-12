#!/usr/bin/env python3
"""Compare SVG document canvas paths through the production source editor."""
from __future__ import annotations

import argparse
import contextlib
import hashlib
import json
import re
import shutil
import subprocess
import sys
import time
from pathlib import Path
from typing import Any, Callable

from PIL import Image
from playwright.sync_api import Page, sync_playwright

sys.path.insert(0, str(Path(__file__).resolve().parents[1] / "UnoDraw.BrowserPerformance"))
from bridge_quality import capture, command  # noqa: E402
from performance import next_frame, park_pointer, renderer_evidence  # noqa: E402
from quality import bridge_query, validate_graphite_coverage, verify_bridge  # noqa: E402
from backend import launch_arguments  # noqa: E402
from smoke import READY, serve  # noqa: E402
from candidate_package import verify_package  # noqa: E402
from svg_fixture_contracts import (LIVE_CANDIDATE_BRIDGES, DESKTOP, DPRS, SCENES, STATES, Fixture,  # noqa: E402
                                   compare_scene, inspect_scene, load_fixtures, matrix_dprs, matrix_failures,
                                   normalize_newlines, require_camera_change, require_source_readback, scene_bridges)


EXECUTION_MODES = ("InterpreterAndAOT", "Interpreter")
SOURCE_EDITOR = (720, 420)
APPLY_BUTTON = (580, 742)


def digest(path: Path) -> str:
    return hashlib.sha256(path.read_bytes()).hexdigest()


def load_package(directory: Path, execution_mode: str) -> dict[str, Any]:
    package = json.loads((directory / "benchmark-package.json").read_text(encoding="utf-8"))
    if package.get("role") != "candidate" or package.get("runtime_generation") != "uno7":
        raise ValueError("Both bridge selections must use the same Uno 7 candidate package")
    if execution_mode not in EXECUTION_MODES or package.get("configuration") != "Release" or package.get("execution_mode") != execution_mode:
        raise ValueError(f"SVG quality requires a published Release {execution_mode} artifact")
    if not re.fullmatch(r"[0-9a-f]{40}", package.get("source_sha", "")):
        raise ValueError("The candidate package must identify its exact source commit")
    if not package.get("dotnet_sdk") or not package.get("dotnet_workload_version"):
        raise ValueError("The candidate package must record its exact SDK and workload")
    index = directory / "wwwroot/index.html"
    if not index.is_file() or digest(index) != package.get("index_sha256"):
        raise ValueError("The served application index does not match its package manifest")
    package["path"] = str(directory)
    return package


def verify_scene_bridge(console: list[dict[str, Any]], bridge: str, *,
                        graphite_coverage: str | None = None) -> None:
    # Source replacement and camera changes repaint the document. They do not
    # guarantee an overlay-only frame that could reuse its cached pixels. The
    # original-host composite must still prove a completed copy into Uno's canvas;
    # persistent-texture composition must prove its combined RGBA upload. Overlay
    # cache reuse is required separately by Welcome and timing workloads.
    verify_bridge(console, bridge, require_reuse=bridge not in {"composite", "legacy-composite"},
                  graphite_coverage=graphite_coverage)


def source_hashes() -> dict[str, str]:
    directory = Path(__file__).resolve().parent
    root = directory.parents[1]
    files = [directory / name for name in ("svg_bridge_quality.py", "svg_fixture_contracts.py", "test_svg_fixture_contracts.py",
                                           "merge_svg_shards.py", "test_svg_shards.py", "verify_source.py")]
    files += sorted((directory / "fixtures/svg-bridge").iterdir())
    files += [root / "tests/Svg.Editor.Skia.UnitTests/RgbaCanvasEquivalenceTests.cs"]
    files += [root / "tests/UnoDraw.BrowserPerformance" / name
              for name in ("instrumentation.js", "bridge_quality.py", "performance.py", "quality.py",
                           "selection_readiness.py", "metrics.py", "candidate_package.py", "pinned_package.py")]
    files += [root / "tests/UnoDraw.BrowserSmoke" / name for name in ("backend.py", "smoke.py", "requirements.txt")]
    return {str(path.relative_to(root)): digest(path) for path in files}


def read_editor(page: Page) -> dict[str, Any] | None:
    # Uno's browser TextBox uses a focused native textarea for multiline input.
    # Reading this public DOM value verifies real input; no model/export is called.
    return page.evaluate("""() => {
        const editor = document.activeElement;
        if (!(editor instanceof HTMLTextAreaElement)) return null;
        return {value: editor.value, length: editor.value.length,
            selection_start: editor.selectionStart, selection_end: editor.selectionEnd,
            tag: editor.tagName, id: editor.id};
    }""")


def focus_source_editor(page: Page) -> dict[str, Any]:
    deadline = time.monotonic() + 15
    while time.monotonic() < deadline:
        page.mouse.click(*SOURCE_EDITOR)
        value = read_editor(page)
        if value and re.search(r"<(?:\w+:)?svg(?:\s|>)", value["value"]):
            return value
        next_frame(page)
    raise AssertionError("The production SVG source dialog did not expose its focused multiline editor")


def edit_source(page: Page, fixture: Fixture, directory: Path) -> dict[str, Any]:
    (directory / "canonical.svg").write_text(fixture.source, encoding="utf-8")
    (directory / "browser-input.svg").write_text(fixture.browser_source, encoding="utf-8")
    opened, reopened = directory / "source-open", directory / "source-reopen"
    opened.mkdir(exist_ok=True)
    reopened.mkdir(exist_ok=True)
    command(page, "Edit SVG source", opened)
    before = focus_source_editor(page)
    (directory / "source-before.svg").write_text(before["value"], encoding="utf-8")
    page.screenshot(path=str(directory / "source-before.png"), scale="device")
    page.keyboard.press("Control+A")
    selected = read_editor(page)
    if not selected or selected["selection_start"] != 0 or selected["selection_end"] != selected["length"]:
        raise AssertionError("Select All did not select the actual source editor text")
    page.keyboard.insert_text(fixture.browser_source)
    deadline = time.monotonic() + 15
    inserted = None
    while time.monotonic() < deadline:
        inserted = read_editor(page)
        if inserted and normalize_newlines(inserted["value"]) == normalize_newlines(fixture.browser_source):
            break
        next_frame(page)
    else:
        raise AssertionError("The native source editor did not read back the exact inserted fixture")
    (directory / "source-inserted.svg").write_text(inserted["value"], encoding="utf-8")
    page.screenshot(path=str(directory / "source-inserted.png"), scale="device")
    # Native Skia controls may have no accessible DOM button. These coordinates
    # are the observed 1440×960 production dialog's primary action, not a test UI.
    button = page.get_by_role("button", name="Apply SVG", exact=True)
    if button.count() == 1 and button.is_visible():
        button.click()
        apply_method = "accessible Apply SVG button"
    else:
        page.mouse.click(*APPLY_BUTTON)
        apply_method = "observed production dialog primary-button coordinates"
    next_frame(page)
    # Reopening must first show the command palette. A failed Apply that leaves
    # the original modal editor open cannot masquerade as a committed readback.
    command(page, "Edit SVG source", reopened)
    committed = focus_source_editor(page)
    (directory / "source-committed.svg").write_text(committed["value"], encoding="utf-8")
    page.screenshot(path=str(directory / "source-committed.png"), scale="device")
    proof = require_source_readback(committed["value"], fixture)
    page.keyboard.press("Escape")
    page.wait_for_function("() => !(document.activeElement instanceof HTMLTextAreaElement)", timeout=15_000)
    next_frame(page)
    proof.update({"exact_inserted_readback": True, "input_sha256": fixture.browser_sha256,
                  "input_tag": inserted["tag"], "input_id": inserted["id"],
                  "apply_method": apply_method, "reopened_after_apply": True})
    return proof


def capture_scene(page: Page, directory: Path, name: str, fixture: Fixture, dpr: float,
                  condition: Callable[[dict[str, Any]], None] | None = None) -> dict[str, Any]:
    raw = directory / f"{name}.png"
    previous = directory / f"{name}-previous.png"
    previous_readiness = None
    stable = 0
    failure = "No completed frame"
    deadline = time.monotonic() + 30
    park_pointer(page)
    while time.monotonic() < deadline:
        page.screenshot(path=str(raw), scale="device")
        try:
            readiness = inspect_scene(raw, fixture, dpr)
            if condition:
                condition(readiness)
            if previous_readiness is not None:
                compare_scene(previous, raw, fixture, dpr, previous_readiness, readiness)
                stable += 1
                if stable >= 2:
                    previous.unlink(missing_ok=True)
                    body = directory / f"{name}-body.png"
                    with Image.open(raw) as image:
                        image.crop(readiness["body_physical_bounds"]).save(body)
                    return {"name": name, "scene": fixture.name, "viewport": DESKTOP, "dpr": dpr,
                            "raw": raw.name, "body_raw": body.name, "readiness": readiness}
            previous_readiness = readiness
        except AssertionError as error:
            stable = 0
            previous_readiness = None
            failure = str(error)
        shutil.copyfile(raw, previous)
        next_frame(page)
    raise AssertionError(f"{fixture.name} {name} did not settle: {failure}")


def errors_since(logs: dict[str, Any], starts: dict[str, int]) -> bool:
    errors = [entry for entry in logs["console"][starts["console"]:] if entry["type"] == "error"
              and not entry["location"].get("url", "").endswith("/favicon.ico")]
    return bool(errors or logs["page_errors"][starts["page_errors"]:] or logs["resource_errors"][starts["resource_errors"]:])


def create_page(browser: Any, dpr: float, logs: dict[str, Any]) -> tuple[Any, Page]:
    context = browser.new_context(viewport=DESKTOP, device_scale_factor=dpr, locale="en-US",
                                  timezone_id="UTC", color_scheme="dark", reduced_motion="no-preference")
    instrumentation = Path(__file__).resolve().parents[1] / "UnoDraw.BrowserPerformance/instrumentation.js"
    context.add_init_script(path=str(instrumentation))
    page = context.new_page()
    page.on("console", lambda message: logs["console"].append(
        {"type": message.type, "text": message.text, "location": message.location}))
    page.on("pageerror", lambda error: logs["page_errors"].append(str(error)))
    page.on("response", lambda response: logs["resource_errors"].append(
        {"url": response.url, "status": response.status})
        if response.status >= 400 and not response.url.endswith("/favicon.ico") else None)
    page.on("requestfailed", lambda request: logs["resource_errors"].append(
        {"url": request.url, "failure": request.failure}) if request.failure != "net::ERR_ABORTED" else None)
    return context, page


def run_session(browser: Any, url: str, package: dict[str, Any], bridge: str, dpr: float,
                fixtures: list[Fixture], output: Path, *,
                graphite_coverage: str | None = None) -> list[dict[str, Any]]:
    validate_graphite_coverage(bridge, graphite_coverage)
    query = bridge_query(bridge, graphite_coverage=graphite_coverage)
    logs: dict[str, Any] = {"console": [], "page_errors": [], "resource_errors": []}
    context = page = None
    cases = []
    reload_required = True
    navigation_console = 0
    navigation_starts = {key: 0 for key in logs}
    environment: dict[str, Any] = {}
    try:
        for fixture in fixtures:
            directory = output / fixture.name
            directory.mkdir(parents=True, exist_ok=True)
            case: dict[str, Any] = {"scene": fixture.name, "bridge": bridge, "dpr": dpr, "passed": False,
                                    "graphite_coverage": graphite_coverage,
                                    "source_sha": package["source_sha"], "execution_mode": package["execution_mode"],
                                    "canonical_sha256": fixture.sha256, "input_sha256": fixture.browser_sha256,
                                    "states": [], "browser_version": browser.version}
            starts = {key: len(value) for key, value in logs.items()}
            try:
                if reload_required:
                    # A failed scene may leave a modal or recovery checkpoint.
                    # A fresh context gives the next scene a clean startup while
                    # retaining the failed case and keeping one browser session.
                    if context:
                        with contextlib.suppress(Exception):
                            context.close()
                    context, page = create_page(browser, dpr, logs)
                    navigation_console = len(logs["console"])
                    navigation_starts = {key: len(value) for key, value in logs.items()}
                    page.goto(url + "?" + query,
                              wait_until="domcontentloaded", timeout=120_000)
                    page.wait_for_function(READY, timeout=120_000)
                    environment = page.evaluate("""() => ({viewport: {width: innerWidth, height: innerHeight},
                        device_pixel_ratio: devicePixelRatio, cross_origin_isolated: crossOriginIsolated,
                        user_agent: navigator.userAgent, browser_fonts: [...document.fonts].map(font =>
                            ({family: font.family, weight: font.weight, style: font.style, status: font.status}))})""")
                    if environment["viewport"] != DESKTOP or environment["device_pixel_ratio"] != dpr or environment["cross_origin_isolated"]:
                        raise AssertionError("The fixed native-DPR/GitHub Pages hosting contract changed")
                    capture(page, directory, "startup", DESKTOP, dpr)
                    renderer_evidence(page, logs["console"][navigation_console:], package, "webgpu")
                    verify_scene_bridge(logs["console"][navigation_console:], bridge, graphite_coverage=graphite_coverage)
                    reload_required = False
                case["environment"] = environment
                case["source_edit"] = edit_source(page, fixture, directory)
                page.mouse.click(1075, 915)
                page.keyboard.press("V")
                page.keyboard.press("Control+Shift+A")
                page.keyboard.press("Control+0")
                fitted = capture_scene(page, directory, STATES[0], fixture, dpr)
                case["states"].append(fitted)
                page.mouse.move(450, 430)
                page.mouse.down(button="middle")
                try:
                    for step in range(1, 9):
                        page.mouse.move(450 + 37.25 * step / 8, 430 - 18.5 * step / 8)
                        next_frame(page)
                finally:
                    page.mouse.up(button="middle")
                panned = capture_scene(page, directory, STATES[1], fixture, dpr,
                    lambda readiness: require_camera_change(fitted["readiness"], readiness, dpr, STATES[1]))
                case["states"].append(panned)
                page.mouse.move(601.25, 470.75)
                page.mouse.wheel(0, -73.25)
                next_frame(page)
                case["states"].append(capture_scene(page, directory, STATES[2], fixture, dpr,
                    lambda readiness: require_camera_change(panned["readiness"], readiness, dpr, STATES[2])))
                case["renderer"] = renderer_evidence(page, logs["console"][navigation_console:], package, "webgpu")
                verify_scene_bridge(logs["console"][navigation_console:], bridge, graphite_coverage=graphite_coverage)
                if errors_since(logs, navigation_starts):
                    raise AssertionError("The SVG scene produced runtime/resource errors")
                case["passed"] = True
            except Exception as error:
                case["failure"] = f"{type(error).__name__}: {error}"
                reload_required = True
                with contextlib.suppress(Exception):
                    page.screenshot(path=str(directory / "failure.png"), scale="device", timeout=5000)
            finally:
                # Keep each case independently reviewable, including initialization
                # evidence once per navigation and any later fallback notification.
                case["console"] = list(logs["console"][navigation_console:])
                case["page_errors"] = list(logs["page_errors"][starts["page_errors"]:])
                case["resource_errors"] = list(logs["resource_errors"][starts["resource_errors"]:])
                (directory / "report.json").write_text(json.dumps(case, indent=2) + "\n", encoding="utf-8")
                cases.append(case)
                print(f"SVG {fixture.name} {bridge} DPR {dpr}: " + ("passed" if case["passed"] else case["failure"]), flush=True)
    finally:
        if context:
            with contextlib.suppress(Exception):
                context.close()
    return cases


def compare_cases(cases: list[dict[str, Any]], fixtures: list[Fixture], output: Path, *,
                  candidate_bridge: str = "webgl", dprs: tuple[float, ...] = DPRS) -> list[dict[str, Any]]:
    dprs = matrix_dprs(dprs)
    bridges = scene_bridges(candidate_bridge)
    indexed = {(case["scene"], case["bridge"], case["dpr"]): case for case in cases}
    rows = []
    for fixture in fixtures:
        for dpr in dprs:
            for name in STATES:
                row: dict[str, Any] = {"scene": fixture.name, "dpr": dpr, "state": name, "passed": False}
                try:
                    matched = []
                    for bridge in bridges:
                        case = indexed.get((fixture.name, bridge, dpr), {})
                        if case.get("passed") is not True or case.get("canonical_sha256") != fixture.sha256 or case.get("input_sha256") != fixture.browser_sha256:
                            raise AssertionError(f"The {bridge} scene case did not complete with the identical SVG")
                        states = [state for state in case["states"] if state["name"] == name]
                        if len(states) != 1 or states[0].get("viewport") != DESKTOP or states[0].get("dpr") != dpr or states[0].get("scene") != fixture.name:
                            raise AssertionError(f"Required native scene state is missing or mismatched in {bridge}")
                        matched.append((output / case["artifact_directory"] / states[0]["raw"], states[0]["readiness"]))
                    row.update(compare_scene(matched[0][0], matched[1][0], fixture, dpr, matched[0][1], matched[1][1]))
                    row["passed"] = True
                except Exception as error:
                    row["failure"] = f"{type(error).__name__}: {error}"
                rows.append(row)
    return rows


def write_summary(report: dict[str, Any], output: Path) -> None:
    control, candidate = scene_bridges(report.get("candidate_bridge", "webgl"))
    dprs = ", ".join(str(value) for value in report.get("device_pixel_ratios", DPRS))
    lines = ["# SVG document bridge quality", "",
             f"One candidate package, `{control}` versus `{candidate}` document rendering, "
             f"at native DPR {dprs}.", "",
             "Eight unchanged SVG bodies are edited through the production source dialog and verified after Apply. "
             "Each case checks fit, fractional pan and fractional zoom. The original scene body is compared separately "
             "at the unchanged 0.1% above luminance 12 threshold; calibration colors over white and dark mattes must be within 2 RGB levels.", "",
             f"Execution mode: `{report['execution_mode']}`. SwiftShader correctness evidence; no hardware performance claim.", ""]
    if report.get("report_kind") == "svg_document_quality_dpr_aggregate":
        lines += ["This aggregate reopens the original source readbacks and native PNGs from all three DPR portions. "
                  "The full matrix requires 48 complete cases and 72 recomputed native comparisons with matching provenance.", ""]
    elif report.get("full_matrix") is False:
        lines += ["This is one complete device-scale portion: two sessions, 16 cases and 24 native comparisons. "
                  "It does not qualify the full matrix. All three DPR portions must pass the strict native-image merger.", ""]
    if candidate == "graphite":
        lines += [f"Explicit Graphite coverage: `{report.get('graphite_coverage') or 'unspecified'}`; unspecified means the artifact's default "
                  "without an explicit coverage claim. Requested coverage requires matching actual-context evidence in every case.", ""]
    if candidate == "legacy-composite":
        lines += ["Each composite case requires a successful copy into the original Uno canvas host. "
                  "These source/camera checks do not claim overlay cache reuse; that is validated separately by Welcome and timing workloads.", ""]
    if candidate == "composite":
        lines += ["Each composite case requires a successful combined document/interaction upload through the persistent RGBA texture path. "
                  "These source/camera checks do not claim overlay cache reuse; that is validated separately by Welcome and timing workloads.", ""]
    lines += ["| Scene | Bridge | DPR | Complete case |", "| --- | --- | ---: | --- |"]
    for case in report["cases"]:
        lines.append(f"| {case['scene']} | {case['bridge']} | {case['dpr']} | {'passed' if case['passed'] else 'failed'} |")
    lines += ["", "| Scene | DPR | State | Native comparison |", "| --- | ---: | --- | --- |"]
    for row in report["comparisons"]:
        lines.append(f"| {row['scene']} | {row['dpr']} | {row['state']} | {'passed' if row['passed'] else 'failed'} |")
    if report["failures"]:
        lines += ["", "## Failures", "", *["- " + failure for failure in report["failures"]]]
    lines += ["", "Full native PNGs, body crops, exact input and committed source, bridge logs, package metadata and test-file hashes accompany this report.", ""]
    (output / "summary.md").write_text("\n".join(lines), encoding="utf-8")


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--package", type=Path, required=True, help="Single candidate package used by both bridge modes")
    parser.add_argument("--output", type=Path, required=True)
    parser.add_argument("--execution-mode", choices=EXECUTION_MODES, default="InterpreterAndAOT")
    parser.add_argument("--candidate-bridge", choices=LIVE_CANDIDATE_BRIDGES, default="legacy-composite",
                        help="Compare this document canvas mode with its paired original CPU control")
    parser.add_argument("--dpr", type=float, choices=DPRS,
                        help="Run both modes and all eight scenes at one DPR; only the three-shard aggregate qualifies the full matrix")
    args = parser.parse_args()
    bridges = scene_bridges(args.candidate_bridge)
    dprs = matrix_dprs(DPRS if args.dpr is None else (args.dpr,))
    package_directory, output = args.package.resolve(), args.output.resolve()
    if output == package_directory or package_directory in output.parents or output in package_directory.parents:
        parser.error("--output must be outside the published application")
    output.mkdir(parents=True, exist_ok=True)
    report: dict[str, Any] = {"schema": 1, "passed": False, "started_unix": time.time(),
                              "report_kind": "svg_document_quality" if args.dpr is None else "svg_document_quality_dpr_shard",
                              "full_matrix": args.dpr is None, "full_matrix_passed": False,
                              "execution_mode": args.execution_mode, "cases": [], "comparisons": [], "failures": [],
                              "candidate_bridge": args.candidate_bridge, "bridges": list(bridges),
                              "graphite_coverage": None,
                              "graphics": "SwiftShader browser graphics API correctness; not hardware performance",
                              "expected_session_count": 2 * len(dprs), "expected_case_count": 16 * len(dprs),
                              "expected_comparison_count": 24 * len(dprs),
                              "expected_scenes": SCENES, "expected_states": STATES, "device_pixel_ratios": dprs}
    try:
        package = load_package(package_directory, args.execution_mode)
        receipt_path = output / "package-artifact.json"
        receipt = None
        if args.dpr is not None:
            report.update({"package_verified_before_browser": False, "package_verified_after_browser": False})
            receipt = json.loads(receipt_path.read_text(encoding="utf-8"))
            if verify_package(package_directory, receipt) != package:
                raise AssertionError("The DPR shard package differs from its immutable archive receipt")
            report.update({"package_verified_before_browser": True, "package_artifact_sha256": digest(receipt_path)})
        manifest, fixtures = load_fixtures()
        report.update({"package": package, "fixture_manifest": manifest, "test_file_sha256": source_hashes()})
        report["harness_source_sha"] = subprocess.check_output(
            ["git", "rev-parse", "HEAD"], cwd=Path(__file__).resolve().parent, text=True).strip()
        arguments = launch_arguments("webgpu")
        report["browser_launch_arguments"] = arguments
        with serve(package_directory / "wwwroot") as url, sync_playwright() as playwright:
            for dpr in dprs:
                for bridge in bridges:
                    coverage = None
                    relative = Path(f"dpr-{dpr}") / bridge
                    directory = output / relative
                    directory.mkdir(parents=True, exist_ok=True)
                    browser = None
                    try:
                        browser = playwright.chromium.launch(headless=True, args=arguments)
                        if report.setdefault("browser_version", browser.version) != browser.version:
                            raise AssertionError("The SVG browser version changed between sessions")
                        cases = run_session(browser, url, package, bridge, dpr, fixtures, directory,
                                            graphite_coverage=coverage)
                    except Exception as error:
                        cases = [{"scene": fixture.name, "bridge": bridge, "dpr": dpr, "passed": False,
                                  "graphite_coverage": coverage,
                                  "states": [], "failure": f"{type(error).__name__}: {error}"} for fixture in fixtures]
                    finally:
                        if browser:
                            with contextlib.suppress(Exception):
                                browser.close()
                    for case in cases:
                        case["artifact_directory"] = str(relative / case["scene"])
                        report["cases"].append(case)
        report["comparisons"] = compare_cases(report["cases"], fixtures, output,
                                              candidate_bridge=args.candidate_bridge, dprs=dprs)
        report["failures"] = matrix_failures(report["cases"], report["comparisons"],
                                             candidate_bridge=args.candidate_bridge, dprs=dprs)
        if receipt is not None:
            if verify_package(package_directory, receipt) != package or digest(receipt_path) != report["package_artifact_sha256"]:
                raise AssertionError("The DPR shard package or receipt changed during browser verification")
            report["package_verified_after_browser"] = True
        report["passed"] = not report["failures"]
        report["full_matrix_passed"] = report["full_matrix"] and report["passed"]
    except Exception as error:
        report["failures"].append(f"{type(error).__name__}: {error}")
    finally:
        report["duration_seconds"] = time.time() - report["started_unix"]
        (output / "report.json").write_text(json.dumps(report, indent=2) + "\n", encoding="utf-8")
        write_summary(report, output)
    return 0 if report["passed"] else 1


if __name__ == "__main__":
    raise SystemExit(main())
