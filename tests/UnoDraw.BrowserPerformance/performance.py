#!/usr/bin/env python3
"""Compare published UnoDraw builds through real browser input and graphics work."""
from __future__ import annotations

import argparse
import contextlib
import json
import math
import platform
import shutil
import sys
import time
from pathlib import Path
from typing import Any

from PIL import Image, ImageChops
from playwright.sync_api import Page, sync_playwright

sys.path.insert(0, str(Path(__file__).resolve().parents[1] / "UnoDraw.BrowserSmoke"))
from backend import launch_arguments, renderer_query  # noqa: E402
from smoke import (READY, artwork_pixels, artwork_transition, assert_artwork,  # noqa: E402
                   chrome_font_changes, record_backend, ruler_frame_changes, serve,
                   wait_for_artwork_transition)
from metrics import comparisons, summarize, validate_packages, verify_hover_isolation  # noqa: E402
from quality import bridge_query  # noqa: E402
from selection_readiness import require_cleared_selection  # noqa: E402
from workload_plan import (BACKENDS, WORKLOADS, diagnostic_plan, primary_plan,  # noqa: E402
                           require_run_coverage)


VIEWPORT = {"width": 1440, "height": 960}
DOCUMENT_REGION = (100, 160, 1050, 880)
WARMUP_INPUTS = 120
WARMUP_MIN_CALLBACKS = 60
WARMUP_MAX_INPUTS = 600
WARMUP_BATCH = 60


def next_frame(page: Page) -> None:
    # This observer schedules no Uno invalidation. One real input per display
    # opportunity models an interactive drag; delayed frames slow the workload.
    page.evaluate("() => globalThis.__unoDrawBenchmark.nextFrame()")


def park_pointer(page: Page) -> None:
    # Rulers intentionally retain the canvas's last world pointer after exit.
    # Match that state as well as the camera when comparing restored ruler pixels.
    page.mouse.move(450, 430)
    page.mouse.move(760, 16)


def screenshot_changed(before: Path, after: Path) -> int:
    with Image.open(before) as first, Image.open(after) as second:
        if first.size != second.size:
            raise AssertionError("Screenshot dimensions changed during a workload")
        difference = ImageChops.difference(first.convert("RGB").crop(DOCUMENT_REGION),
                                          second.convert("RGB").crop(DOCUMENT_REGION)).convert("L")
        return sum(count for level, count in enumerate(difference.histogram()) if level > 12)


def settled_screenshot(page: Page, output: Path) -> None:
    """Wait on observed pixels, never add a startup sleep to a measured sample."""
    previous = output.with_name(output.stem + "-previous.png")
    deadline = time.monotonic() + 30
    stable = 0
    while time.monotonic() < deadline:
        page.screenshot(path=str(output))
        try:
            assert_artwork(artwork_pixels(output))
            if previous.exists():
                artwork_transition(previous, output, restore=True)
                if any(value > 0.001 for value in ruler_frame_changes(previous, output).values()):
                    raise AssertionError("Rulers are still changing")
                if any(value > 0.02 for value in chrome_font_changes(previous, output).values()):
                    raise AssertionError("Fonts are still changing")
                stable += 1
                if stable >= 2:
                    previous.unlink(missing_ok=True)
                    return
        except AssertionError:
            stable = 0
        shutil.copyfile(output, previous)
        next_frame(page)
    raise AssertionError("The initial document, rulers and fonts did not settle")


def wait_for_edited_artwork(page: Page, before: Path, after: Path) -> dict[str, Any]:
    """Observe deselection and settled edited pixels after the timed sample ends."""
    previous = after.with_name(after.stem + "-selection-previous.png")
    deadline = time.monotonic() + 30
    started = time.monotonic()
    stable = 0
    captures = 0
    failure = "No completed edited frame"
    while time.monotonic() < deadline:
        page.screenshot(path=str(after))
        captures += 1
        try:
            selection = require_cleared_selection(before, after)
            result = artwork_transition(before, after, restore=False)
            if previous.exists():
                artwork_transition(previous, after, restore=True)
                stable += 1
                if stable >= 2:
                    previous.unlink(missing_ok=True)
                    result["selection_clear_readiness"] = {
                        **selection, "captures": captures,
                        "elapsed_ms_outside_timed_sample": (time.monotonic() - started) * 1000,
                    }
                    return result
        except AssertionError as error:
            stable = 0
            failure = str(error)
        shutil.copyfile(after, previous)
        next_frame(page)
    raise AssertionError(f"Edited artwork did not settle after clearing selection: {failure}")


def cpu_metrics(session: Any) -> dict[str, float]:
    return {entry["name"]: entry["value"] for entry in session.send("Performance.getMetrics")["metrics"]}


def managed_snapshot(page: Page, reset: bool) -> dict[str, Any]:
    snapshot = page.evaluate("""async reset => {
        const exports = await globalThis.Module.getAssemblyExports('UnoDraw');
        const snapshot = exports?.UnoDraw?.BrowserRenderDiagnostics?.Snapshot;
        if (typeof snapshot !== 'function') throw new Error('Managed render diagnostics export is unavailable');
        return JSON.parse(snapshot(reset));
    }""", reset)
    if snapshot.get("enabled") is not True:
        raise AssertionError("The separate managed diagnostic pass did not enable its counters")
    return snapshot


def renderer_evidence(page: Page, console: list[dict[str, Any]], package: dict[str, Any],
                      backend: str) -> dict[str, Any]:
    contexts = page.evaluate("() => globalThis.__unoDrawBenchmark.selectCanvas()")
    selected = [entry for entry in contexts if entry["target"]]
    expected = {"webgpu"} if backend == "webgpu" else {"webgl", "webgl2", "experimental-webgl"}
    if len(selected) != 1 or selected[0]["kind"] not in expected:
        raise AssertionError(f"The visible application canvas did not use {backend}: {contexts}")
    if not selected[0]["draw_calls"] and not selected[0]["presentations"]:
        raise AssertionError("The application canvas context exists but has produced no observed graphics work")
    result: dict[str, Any] = {"application_contexts": contexts}
    if package["runtime_generation"] == "uno7":
        evidence = {"console": console, "backend_selection": {}, "backend_mode": backend}
        record_backend(page, evidence, "startup", 0)
        result.update(evidence["backend_selection"]["startup"])
    else:
        # Uno 6 predates the provider negotiation message. Require the actual
        # connected WebGL canvas plus its Skia host, rather than pretending the
        # Uno 7 message exists or accepting a detached capability-probe canvas.
        host = page.evaluate("() => typeof globalThis.Uno?.UI?.Runtime?.Skia?.BrowserRenderer")
        if host not in {"function", "object"} or backend != "skia-preferred":
            raise AssertionError("The pre-migration comparison requires Uno 6's Skia/WebGL host")
        result["legacy_skia_host"] = host
        result["expected_console_error_indices"] = []
    return result


def common_pan_warmup(page: Page) -> dict[str, Any]:
    """Exercise real input beyond the shared runtime's 50-frame retention threshold."""
    page.mouse.move(450, 430)
    page.mouse.down(button="middle")
    page.evaluate("() => globalThis.__unoDrawBenchmark.start('common-warmup-pan', ['pointermove'])")
    offered = 0
    completed = 0
    while offered < WARMUP_MAX_INPUTS and (offered < WARMUP_INPUTS or completed < WARMUP_MIN_CALLBACKS):
        for _ in range(WARMUP_BATCH):
            offered += 1
            angle = offered * 2 * math.pi / WARMUP_BATCH
            page.mouse.move(450 + 48 * math.sin(angle), 430 + 24 * (1 - math.cos(angle)))
            next_frame(page)
        completed = page.evaluate("() => globalThis.__unoDrawBenchmark.progress().completed_render_callbacks")
    page.mouse.up(button="middle")
    for _ in range(2):
        next_frame(page)
    raw = page.evaluate("() => globalThis.__unoDrawBenchmark.stop()")
    completed = len(raw["completed_render_callback_ms"])
    if raw["input_events"] < WARMUP_INPUTS or completed < WARMUP_MIN_CALLBACKS:
        raise AssertionError(f"Real pan warmup did not produce enough input and graphics callbacks: "
                             f"{raw['input_events']} trusted moves, {completed} completed callbacks")
    return {"offered_pointer_moves": offered, "trusted_pointer_moves": raw["input_events"],
            "completed_graphics_callbacks": completed,
            "graphics_active_animation_intervals": len(raw["graphics_active_intervals_ms"]),
            "elapsed_ms": raw["ended_ms"] - raw["started_ms"],
            "included_in_primary_metrics": False}


def brush_transition_warmup(page: Page, fitted: Path, before: Path) -> dict[str, Any]:
    """Complete the post-fit layer transition and clear its real preview before measurement."""
    preview = before.with_name("brush-transition-preview.png")
    bounds = (425, 405, 475, 455)

    def changed(path: Path) -> int:
        with Image.open(fitted) as first, Image.open(path) as second:
            difference = ImageChops.difference(first.convert("RGB").crop(bounds),
                                              second.convert("RGB").crop(bounds)).convert("L")
            return sum(count for level, count in enumerate(difference.histogram()) if level > 12)

    page.mouse.move(450, 430)
    deadline = time.monotonic() + 15
    preview_pixels = 0
    while time.monotonic() < deadline:
        page.screenshot(path=str(preview))
        preview_pixels = changed(preview)
        if preview_pixels >= 20:
            break
        next_frame(page)
    else:
        raise AssertionError("The post-fit brush preview did not appear during its untimed transition")
    page.mouse.move(760, 16)
    deadline = time.monotonic() + 15
    area = (bounds[2] - bounds[0]) * (bounds[3] - bounds[1])
    while time.monotonic() < deadline:
        page.screenshot(path=str(before))
        fraction = changed(before) / area
        if fraction <= 0.001:
            artwork_transition(fitted, before, restore=True)
            if not any(value > 0.001 for value in ruler_frame_changes(fitted, before).values()):
                return {"preview_changed_pixels": preview_pixels, "cleared_changed_pixel_fraction": fraction,
                        "region": list(bounds), "included_in_primary_metrics": False}
        next_frame(page)
    raise AssertionError("The warmed brush preview did not clear before the measured interval")


def warm_workload(page: Page, workload: str) -> None:
    page.mouse.click(470, 430)
    page.keyboard.press("Control+Shift+A")
    if workload == "brush-hover":
        # Blob Brush has a visible circular hover preview without editing the SVG.
        page.keyboard.press("Shift+B")
        for x in (450, 460):
            page.mouse.move(x, 430)
            next_frame(page)
    elif workload == "pan":
        page.mouse.move(450, 430)
        page.mouse.down(button="middle")
        for x in (458, 450):
            page.mouse.move(x, 430)
            next_frame(page)
        page.mouse.up(button="middle")
    elif workload == "zoom":
        page.mouse.move(600, 470)
        for delta in (-12, 12):
            page.mouse.wheel(0, delta)
            next_frame(page)
    elif workload in {"selection", "edit"}:
        page.keyboard.press("Control+A")
        next_frame(page)
        if workload == "edit":
            page.keyboard.press("ArrowRight")
            next_frame(page)
            page.keyboard.press("Control+Z")
            next_frame(page)
        page.keyboard.press("Control+Shift+A")
    page.keyboard.press("Control+0")
    page.keyboard.press("Control+Shift+A")
    park_pointer(page)


def measure_workload(page: Page, workload: str, steps: int, idle_ms: int) -> int:
    if workload == "idle":
        # Idle is a separate observation of unnecessary background rendering.
        # Its deliberate quiet window is never included in active-workload FPS.
        page.evaluate("ms => new Promise(resolve => setTimeout(resolve, ms))", idle_ms)
        return 0
    if workload in {"pan", "brush-hover"}:
        for index in range(1, steps + 1):
            fraction = index / steps
            page.mouse.move(450 + 96 * fraction + 32 * math.sin(fraction * 2 * math.pi),
                            430 + 24 * math.sin(fraction * 4 * math.pi))
            next_frame(page)
        if workload == "pan":
            page.mouse.up(button="middle")
    elif workload == "zoom":
        previous = 0.0
        for index in range(1, steps + 1):
            fraction = index / steps
            log_scale = 0.3 * math.sin(fraction * 2 * math.pi) + 0.2 * fraction
            page.mouse.wheel(0, -(log_scale - previous) / 0.0015)
            previous = log_scale
            next_frame(page)
    elif workload == "selection":
        steps |= 1  # End with a selected document so a no-op cannot pass the pixel check.
        for index in range(steps):
            page.keyboard.press("Control+A" if index % 2 == 0 else "Control+Shift+A")
            next_frame(page)
    elif workload == "edit":
        for _ in range(steps):
            page.keyboard.press("ArrowRight")
            next_frame(page)
    return steps


def verify_workload(page: Page, workload: str, operations: int, before: Path,
                    output: Path) -> dict[str, Any]:
    after = output / "after.png"
    if workload == "edit":
        # Remove selection outlines before requiring changed SVG interiors.
        page.keyboard.press("Control+Shift+A")
        result = wait_for_edited_artwork(page, before, after)
    elif workload in {"pan", "zoom"}:
        result = wait_for_artwork_transition(page, before, after, restore=False)
    elif workload in {"selection", "brush-hover"}:
        page.screenshot(path=str(after))
        changed = screenshot_changed(before, after)
        minimum = 20 if workload == "brush-hover" else 50
        if changed < minimum:
            raise AssertionError(f"{workload} produced no visible interaction overlay: {changed} changed pixels")
        result = {"changed_document_pixels": changed, "minimum_changed_pixels": minimum}
    else:
        page.screenshot(path=str(after))
        result = artwork_transition(before, after, restore=True)
    if workload == "edit":
        for _ in range(operations):
            page.keyboard.press("Control+Z")
    if workload == "brush-hover":
        page.keyboard.press("V")
    page.keyboard.press("Control+Shift+A")
    page.keyboard.press("Control+0")
    park_pointer(page)
    result["restored"] = wait_for_artwork_transition(page, before, output / "restored.png",
                                                    restore=True, require_settled_rulers=True)
    return result


def run_trial(browser: Any, url: str, package: dict[str, Any], backend: str, workload: str,
              trial: int, args: Any, output: Path, *, diagnostics: bool = False,
              canvas_bridge: str | None = None) -> dict[str, Any]:
    if canvas_bridge is not None and (backend != "webgpu" or package["runtime_generation"] != "uno7"):
        raise ValueError("Explicit canvas bridges require the Uno 7 WebGPU compositor")
    output.mkdir(parents=True, exist_ok=True)
    result: dict[str, Any] = {"role": package["role"], "source_sha": package["source_sha"],
                              "backend": backend, "workload": workload, "trial": trial,
                              "diagnostic_pass": diagnostics,
                              "passed": False, "console": [], "page_errors": [], "resource_errors": []}
    if canvas_bridge is not None:
        result["canvas_bridge"] = canvas_bridge
    context = browser.new_context(viewport=VIEWPORT, device_scale_factor=1, locale="en-US",
                                  timezone_id="UTC", color_scheme="dark", reduced_motion="no-preference")
    context.add_init_script(path=str(Path(__file__).with_name("instrumentation.js")))
    page = context.new_page()
    page.on("console", lambda message: result["console"].append(
        {"type": message.type, "text": message.text, "location": message.location}))
    page.on("pageerror", lambda error: result["page_errors"].append(str(error)))
    page.on("response", lambda response: result["resource_errors"].append(
        {"url": response.url, "status": response.status})
        if response.status >= 400 and not response.url.endswith("/favicon.ico") else None)
    page.on("requestfailed", lambda request: result["resource_errors"].append(
        {"url": request.url, "failure": request.failure}) if request.failure != "net::ERR_ABORTED" else None)
    try:
        query = renderer_query(backend) if package["runtime_generation"] == "uno7" else ""
        if canvas_bridge is not None:
            query += ("&" if query else "?") + bridge_query(canvas_bridge)
        if diagnostics:
            query += ("&" if query else "?") + "renderDiagnostics=1&perfDiagnostics=1"
        page.goto(url + query, wait_until="domcontentloaded", timeout=120_000)
        page.wait_for_function(READY, timeout=120_000)
        result["environment"] = page.evaluate("""() => ({
            viewport: {width: innerWidth, height: innerHeight}, device_pixel_ratio: devicePixelRatio,
            cross_origin_isolated: crossOriginIsolated, user_agent: navigator.userAgent,
            hardware_concurrency: navigator.hardwareConcurrency,
            browser_fonts: [...document.fonts].map(font => ({family: font.family, weight: font.weight,
                style: font.style, status: font.status}))
        })""")
        if result["environment"]["viewport"] != VIEWPORT or result["environment"]["device_pixel_ratio"] != 1:
            raise AssertionError("The fixed viewport/DPR contract changed")
        if result["environment"]["cross_origin_isolated"]:
            raise AssertionError("Benchmark hosting must match GitHub Pages without COOP/COEP")
        # READY ends font/startup deferral but can precede the first scheduled
        # render callback. Require actual settled artwork before inspecting the
        # context's completed graphics work; no startup delay is invented.
        settled_screenshot(page, output / "startup.png")
        result["renderer"] = renderer_evidence(page, result["console"], package, backend)
        park_pointer(page)
        settled_screenshot(page, output / "initial.png")
        result["common_warmup"] = common_pan_warmup(page)
        warm_workload(page, workload)
        before = output / "before.png"
        if workload == "brush-hover":
            result["hover_transition_warmup"] = brush_transition_warmup(page, output / "initial.png", before)
        else:
            wait_for_artwork_transition(page, output / "initial.png", before,
                                       restore=True, require_settled_rulers=True)
        if workload == "brush-hover":
            page.mouse.move(450, 430)
        elif workload == "pan":
            page.mouse.move(450, 430)
            page.mouse.down(button="middle")
        elif workload == "zoom":
            page.mouse.move(600, 470)
        elif workload == "edit":
            page.keyboard.press("Control+A")
        for _ in range(2):
            next_frame(page)
        session = context.new_cdp_session(page)
        session.send("Performance.enable")
        before_managed = managed_snapshot(page, reset=True) if diagnostics else None
        diagnostic_console_start = len(result["console"])
        before_cpu = cpu_metrics(session)
        input_types = {"idle": [], "brush-hover": ["pointermove"], "pan": ["pointermove"], "zoom": ["wheel"],
                       "selection": ["keydown"], "edit": ["keydown"]}[workload]
        page.evaluate("args => globalThis.__unoDrawBenchmark.start(...args)", [workload, input_types])
        operations = measure_workload(page, workload, args.steps, args.idle_ms)
        # Drain the naturally scheduled final input and composition opportunity.
        # No artificial application invalidation or screenshot occurs in this interval.
        for _ in range(2):
            next_frame(page)
        raw = page.evaluate("() => globalThis.__unoDrawBenchmark.stop()")
        after_cpu = cpu_metrics(session)
        if diagnostics:
            after_managed = managed_snapshot(page, reset=False)
            result["managed_render_diagnostics"] = {
                "before_reset": before_managed, "after": after_managed,
                "thread_allocated_bytes_delta_including_observer": (
                    after_managed["threadAllocatedBytes"] - before_managed["threadAllocatedBytes"]),
                "gc_collections_delta": {name: after_managed[name] - before_managed[name]
                                         for name in ("gen0Collections", "gen1Collections", "gen2Collections")},
            }
            result["runtime_phase_logs"] = [entry["text"] for entry in result["console"][diagnostic_console_start:]
                                            if any(marker in entry["text"] for marker in
                                                   ("[frame-phases]", "[webgpu-frame]", "[webgpu-stats]"))]
            if workload == "brush-hover":
                verify_hover_isolation(after_managed)
                result["hover_layer_isolation"] = "Document count is zero; Interaction count increased"
        result["metrics"] = summarize(raw, before_cpu, after_cpu, operations)
        result["raw_browser_observations"] = raw
        result["cdp_cpu_before"] = before_cpu
        result["cdp_cpu_after"] = after_cpu
        if workload != "idle" and (raw["input_events"] == 0 or not raw["graphics_active_intervals_ms"]):
            raise AssertionError("Active workload did not produce both trusted input and graphics work")
        result["quality"] = verify_workload(page, workload, operations, before, output)
        allowed = set(result["renderer"].get("expected_console_error_indices", []))
        errors = [entry for index, entry in enumerate(result["console"])
                  if entry["type"] == "error" and index not in allowed
                  and not entry["location"].get("url", "").endswith("/favicon.ico")]
        if errors or result["page_errors"] or result["resource_errors"]:
            raise AssertionError("Runtime/resource errors occurred; inspect the per-trial report")
        result["passed"] = True
    except Exception as error:
        result["failure"] = f"{type(error).__name__}: {error}"
        with contextlib.suppress(Exception):
            page.screenshot(path=str(output / "failure.png"), timeout=5000)
    finally:
        context.close()
        (output / "report.json").write_text(json.dumps(result, indent=2) + "\n", encoding="utf-8")
    return result


def cross_build_quality(runs: list[dict[str, Any]], output: Path) -> list[dict[str, Any]]:
    result = []
    # Preserve the exact current-versus-candidate quality boundary. Legacy Uno
    # glyph output is reported separately and is not an invented Uno 7 baseline.
    by_key = {(run["role"], run["backend"], run["workload"], run["trial"]): run for run in runs}
    for key, candidate in by_key.items():
        if key[0] != "candidate" or not candidate["passed"]:
            continue
        baseline = by_key.get(("baseline", *key[1:]))
        if not baseline or not baseline["passed"]:
            continue
        row: dict[str, Any] = {"backend": key[1], "workload": key[2], "trial": key[3]}
        for state in ("before", "after", "restored"):
            first = output / baseline["artifact_directory"] / f"{state}.png"
            second = output / candidate["artifact_directory"] / f"{state}.png"
            row[state] = artwork_transition(first, second, restore=True)
            rulers = ruler_frame_changes(first, second)
            fonts = chrome_font_changes(first, second)
            if any(value > 0.001 for value in rulers.values()):
                raise AssertionError(f"{key[1]} {key[2]} trial {key[3]} {state} changed ruler pixels: {rulers}")
            if any(value > 0.02 for value in fonts.values()):
                raise AssertionError(f"{key[1]} {key[2]} trial {key[3]} {state} changed chrome font pixels: {fonts}")
            row[state]["ruler_changed_pixel_fraction"] = rulers
            row[state]["chrome_font_changed_pixel_fraction"] = fonts
        result.append(row)
    return result


def write_summary(report: dict[str, Any], output: Path) -> None:
    lines = ["# UnoDraw browser performance comparison", "",
             "Graphics: " + report["graphics_description"], "",
             "The table compares medians of repeated trials on the same runner. "
             "Ratios below 1 favor the candidate for latency/CPU; throughput favors ratios above 1. "
             "rAF intervals are scheduling observations, not rendered FPS. "
             "Graphics-active intervals and input-to-next-graphics are CPU submission observations, "
             "not display presentation or input-to-photon latency.", "",
             "| Reference | Backend | Workload | Metric | Reference median | Candidate median | Ratio |",
             "| --- | --- | --- | --- | ---: | ---: | ---: |"]
    for row in report.get("comparisons", []):
        ratio = f"{row['candidate_over_baseline']:.3f}" if row["candidate_over_baseline"] is not None else "n/a"
        lines.append(f"| {row['baseline_role']} | {row['backend']} | {row['workload']} | {row['metric']} "
                     f"| {row['baseline_median']:.3f} | {row['candidate_median']:.3f} | {ratio} |")
    diagnostics = [run for run in report.get("diagnostic_runs", []) if "managed_render_diagnostics" in run]
    if diagnostics:
        lines += ["", "## Candidate diagnostics — separate instrumented pass", "",
                  "These callback costs and allocations are outside the primary comparison. "
                  "They exclude deferred compositor/GPU work; runtime phase logs are in the JSON report.", "",
                  "| Backend | Workload | Paint callback | Calls | Total CPU ms | Max CPU ms | Allocated bytes |",
                  "| --- | --- | --- | ---: | ---: | ---: | ---: |"]
        for run in diagnostics:
            for phase in run["managed_render_diagnostics"]["after"]["phases"]:
                lines.append(f"| {run['backend']} | {run['workload']} | {phase['phase']} | {phase['count']} "
                             f"| {phase['totalMilliseconds']:.3f} | {phase['maxMilliseconds']:.3f} "
                             f"| {phase['allocatedBytes']} |")
    if not report["passed"]:
        lines += ["", "Comparison failed: " + report.get("failure", "one or more runtime/quality checks failed")]
    (output / "summary.md").write_text("\n".join(lines) + "\n", encoding="utf-8")


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--packages", type=Path, required=True, help="Directory containing published package manifests")
    parser.add_argument("--output", type=Path, required=True)
    parser.add_argument("--repeats", type=int, default=3)
    parser.add_argument("--steps", type=int, default=120)
    parser.add_argument("--idle-ms", type=int, default=2000)
    parser.add_argument("--workload", choices=WORKLOADS,
                        help="Run one workload with every selected revision and repetition on this runner")
    parser.add_argument("--backend", choices=BACKENDS,
                        help="Run one actual backend; the Uno 6 reference only supports skia-preferred")
    parser.add_argument("--graphics", choices=("swiftshader", "hardware"), default="swiftshader")
    parser.add_argument("--executable", help="Optional local Chromium executable for a hardware run")
    parser.add_argument("--diagnostics", action="store_true", help="After primary trials, run separate candidate-only managed/Uno diagnostics")
    args = parser.parse_args()
    if not 2 <= args.repeats <= 20 or not 30 <= args.steps <= 1000 or not 1000 <= args.idle_ms <= 10_000:
        parser.error("Use 2–20 repeats, 30–1000 steps and 1000–10000 idle milliseconds")
    output = args.output.resolve()
    output.mkdir(parents=True, exist_ok=True)
    packages = []
    for path in sorted(args.packages.resolve().rglob("benchmark-package.json")):
        package = json.loads(path.read_text(encoding="utf-8"))
        package["path"] = str(path.parent)
        packages.append(package)
    validate_packages(packages)
    if any(Path(package["path"]) == output or Path(package["path"]) in output.parents for package in packages):
        parser.error("Diagnostics must be outside the served application packages")
    selected_workloads = (args.workload,) if args.workload else WORKLOADS
    selected_backends = (args.backend,) if args.backend else BACKENDS
    primary_tasks = primary_plan(packages, args.repeats, selected_workloads, selected_backends)
    diagnostic_tasks = (diagnostic_plan(packages, selected_workloads, selected_backends)
                        if args.diagnostics else [])
    report: dict[str, Any] = {
        "schema": 1, "passed": False, "started_unix": time.time(), "packages": packages,
        "viewport": VIEWPORT, "device_scale_factor": 1, "repeats": args.repeats, "steps": args.steps,
        "idle_ms": args.idle_ms, "python": platform.python_version(), "platform": platform.platform(),
        "shard": {"workload": args.workload, "backend": args.backend},
        "selected_workloads": list(selected_workloads), "selected_backends": list(selected_backends),
        "expected_primary_runs": len(primary_tasks), "expected_diagnostic_runs": len(diagnostic_tasks),
        "warmup_policy": {"minimum_real_pan_inputs": WARMUP_INPUTS,
                          "minimum_completed_graphics_callbacks": WARMUP_MIN_CALLBACKS,
                          "maximum_real_pan_inputs": WARMUP_MAX_INPUTS, "additional_batch_size": WARMUP_BATCH,
                          "canonical_pointer_reset_css": [[450, 430], [760, 16]],
                          "reason": "Both pinned Uno versions retain eligible stable visual subtrees after 50 paint frames",
                          "included_in_primary_metrics": False},
        "graphics_description": ("SwiftShader software GPU API implementations; not physical GPU throughput"
                                 if args.graphics == "swiftshader" else
                                 "Requested hardware; inspect the recorded actual adapters before interpreting GPU performance"),
        "metric_limits": {
            "raf": "Animation callback cadence; not rendered or presented frames",
            "graphics_active_intervals": "Animation intervals containing application-canvas drawing/submission API calls",
            "input_to_next_graphics": "Trusted input dispatch to next observed graphics work; coalesced and not causal presentation latency",
            "completed_render_callback": "Successful application rAF callback drawing/submitting to the main canvas; excludes earlier NativeDispatcher recording; may replay an older record, not input-to-new-pixels or physical presentation",
            "cpu": "CDP main-thread task/script/layout time including small observer and automation overheads",
            "heap": "JavaScript used-heap difference; not an allocation count and not the managed WASM heap",
            "physical_presented_frames": "Unavailable; never inferred from rAF",
            "managed_allocated_bytes": "Unavailable without application instrumentation",
        }, "runs": [], "diagnostic_runs": [],
    }
    try:
        with contextlib.ExitStack() as stack, sync_playwright() as playwright:
            urls = {package["role"]: stack.enter_context(serve(Path(package["path"]) / "wwwroot"))
                    for package in packages}
            by_role = {package["role"]: package for package in packages}
            for task in primary_tasks:
                package = by_role[task["role"]]
                backend, workload, trial = task["backend"], task["workload"], task["trial"]
                arguments = launch_arguments(backend) if args.graphics == "swiftshader" else []
                browser = playwright.chromium.launch(headless=True, args=arguments,
                                                     executable_path=args.executable)
                try:
                    directory = Path(package["role"]) / backend / workload / f"trial-{trial}"
                    run = run_trial(browser, urls[package["role"]], package, backend, workload,
                                    trial, args, output / directory)
                    run["artifact_directory"] = str(directory)
                    run["browser_version"] = browser.version
                    run["browser_launch_arguments"] = arguments
                    report["runs"].append(run)
                    print(f"{package['role']} {backend} {workload} trial {trial}: "
                          + ("passed" if run["passed"] else run["failure"]), flush=True)
                    if not run["passed"]:
                        raise AssertionError(run["failure"])
                finally:
                    browser.close()
            require_run_coverage(report["runs"], primary_tasks, "primary")
            report["cross_build_quality"] = cross_build_quality(report["runs"], output)
            report["comparisons"] = comparisons(report["runs"])
            if args.diagnostics:
                candidate = next(package for package in packages if package["role"] == "candidate")
                if not candidate.get("supports_render_diagnostics"):
                    report["diagnostics_unavailable"] = "The selected candidate does not contain the opt-in managed diagnostics export"
                else:
                    for task in diagnostic_tasks:
                        backend, workload = task["backend"], task["workload"]
                        arguments = launch_arguments(backend) if args.graphics == "swiftshader" else []
                        browser = playwright.chromium.launch(headless=True, args=arguments,
                                                             executable_path=args.executable)
                        try:
                            directory = Path("diagnostics") / backend / workload
                            run = run_trial(browser, urls["candidate"], candidate, backend, workload,
                                            1, args, output / directory, diagnostics=True)
                            run["artifact_directory"] = str(directory)
                            run["browser_version"] = browser.version
                            run["browser_launch_arguments"] = arguments
                            report["diagnostic_runs"].append(run)
                            print(f"candidate diagnostic {backend} {workload}: "
                                  + ("passed" if run["passed"] else run["failure"]), flush=True)
                            if not run["passed"]:
                                raise AssertionError(run["failure"])
                        finally:
                            browser.close()
            require_run_coverage(report["diagnostic_runs"], diagnostic_tasks, "diagnostic")
            report["passed"] = True
    except Exception as error:
        report["failure"] = f"{type(error).__name__}: {error}"
    finally:
        report["duration_seconds"] = time.time() - report["started_unix"]
        (output / "report.json").write_text(json.dumps(report, indent=2) + "\n", encoding="utf-8")
        write_summary(report, output)
    return 0 if report["passed"] else 1


if __name__ == "__main__":
    raise SystemExit(main())
