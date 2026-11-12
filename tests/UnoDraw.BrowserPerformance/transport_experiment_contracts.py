"""Attribution gates for one immutable application's texture transport comparison."""
from __future__ import annotations

import math
import statistics
from collections import Counter
from typing import Any

from metrics import verify_interaction_isolation
from quality import verify_bridge


REPORT_KIND = "same_snapshot_texture_transport_experiment"
BRIDGES = ("legacy", "composite")
STATES = ("before", "after", "restored")
REPEATS = 3
OPERATIONS = 121
VIEWPORT = {"width": 1440, "height": 960}


def trial_plan() -> list[tuple[int, str]]:
    return [(trial, bridge) for trial in range(1, REPEATS + 1)
            for bridge in (BRIDGES if trial % 2 else tuple(reversed(BRIDGES)))]


def verify_report_identity(report: dict[str, Any]) -> None:
    if (report.get("report_kind") != REPORT_KIND or report.get("primary_comparison") is not False
            or report.get("workload") != "selection" or report.get("bridges") != list(BRIDGES)
            or report.get("repeats") != REPEATS or report.get("steps") != 120
            or report.get("viewport") != VIEWPORT or report.get("device_scale_factor") != 1):
        raise ValueError("Texture transport evidence requires its own complete same-snapshot report identity")


def verify_runs(runs: list[dict[str, Any]], source_sha: str, *, diagnostics: bool = False) -> None:
    expected = [(1, bridge) for bridge in BRIDGES] if diagnostics else trial_plan()
    actual = [(run.get("trial"), run.get("canvas_bridge")) for run in runs]
    if actual != expected:
        raise ValueError("Transport trials are incomplete, duplicated or not in the declared alternating order")
    browsers = set()
    launch_arguments = set()
    for run in runs:
        if (run.get("passed") is not True or run.get("source_sha") != source_sha
                or run.get("role") != "candidate" or run.get("backend") != "webgpu"
                or run.get("workload") != "selection" or run.get("diagnostic_pass") is not diagnostics
                or run.get("graphite_coverage") is not None):
            raise ValueError("A transport trial failed or changed application source, workload or diagnostics")
        if run.get("page_errors") != [] or run.get("resource_errors") != []:
            raise ValueError("Transport trials require explicit empty page/resource error lists")
        environment = run.get("environment", {})
        if (environment.get("viewport") != VIEWPORT or environment.get("device_pixel_ratio") != 1
                or environment.get("cross_origin_isolated") is not False):
            raise ValueError("The transport comparison changed its viewport, DPR or hosting policy")
        renderer = run.get("renderer", {})
        if not any(row.get("provider") == "WebGpuGraphicsProvider" and row.get("context") == "WebGpu"
                   for row in renderer.get("negotiations", [])):
            raise ValueError("The transport trial did not prove the actual WebGPU compositor")
        if not any(row.get("kind") == "webgpu" and row.get("connected") is True
                   and row.get("target") is True and row.get("presentations", 0) > 0
                   for row in renderer.get("application_contexts", [])):
            raise ValueError("No connected application WebGPU canvas performed observed graphics work")
        warmup = run.get("common_warmup", {})
        offered, trusted = warmup.get("offered_pointer_moves"), warmup.get("trusted_pointer_moves")
        if (type(offered) is not int or offered < 120 or type(trusted) is not int or trusted != offered
                or type(warmup.get("completed_graphics_callbacks")) is not int
                or warmup["completed_graphics_callbacks"] < 60
                or warmup.get("included_in_primary_metrics") is not False):
            raise ValueError("Every transport trial requires the full observed common pan warmup outside timing")
        metrics = run.get("metrics", {})
        if any(type(metrics.get(key)) is not int or metrics[key] != expected_value for key, expected_value in {
                "operations": OPERATIONS, "input_events": OPERATIONS,
                "inputs_without_later_graphics": 0, "inputs_without_completed_render_callback": 0}.items()):
            raise ValueError("Selection requires all 121 trusted inputs and zero unobserved tails")
        if any(type(metrics.get(key)) is not int or metrics[key] <= 0
               for key in ("completed_render_callbacks", "webgpu_canvas_submissions")):
            raise ValueError("The transport trial did not complete application graphics work")
        if (metrics.get("graphics_api_calls", {}).get("webgpu.copyExternalImageToTexture", 0) != 0
                or metrics.get("graphite_document_submissions") not in (None, 0)):
            raise ValueError("Another document GPU renderer contaminated the texture transport comparison")
        quality = run.get("quality", {})
        changed = quality.get("changed_document_pixels")
        if quality.get("minimum_changed_pixels") != 50 or type(changed) is not int or changed < 50:
            raise ValueError("Selection did not retain the existing visible-overlay readiness proof")
        restored = quality.get("restored", {})
        rulers = restored.get("ruler_changed_pixel_fraction", {})
        fractions = [restored.get("changed_pixel_fraction"), *rulers.values()]
        if (set(rulers) != {"horizontal", "vertical"}
                or any(type(value) not in (int, float) or not math.isfinite(value)
                       or not 0 <= value <= 0.001 for value in fractions)):
            raise ValueError("Selection did not restore the original document and ruler pixel gates")
        if diagnostics:
            verify_interaction_isolation(run.get("managed_render_diagnostics", {}).get("after", {"phases": []}),
                                         "selection")
        elif "managed_render_diagnostics" in run or "runtime_phase_logs" in run:
            raise ValueError("Instrumented diagnostics cannot become primary transport timing")
        verify_bridge(run.get("console", []), run["canvas_bridge"])
        browser = run.get("browser_version")
        arguments = run.get("browser_launch_arguments")
        if not isinstance(browser, str) or not browser or not isinstance(arguments, list) or not arguments:
            raise ValueError("Every transport trial requires its actual browser version and launch arguments")
        browsers.add(browser)
        launch_arguments.add(tuple(arguments))
    if len(browsers) != 1 or len(launch_arguments) != 1:
        raise ValueError("The two transports did not use the same browser configuration")


def verify_native_pairs(comparisons: list[dict[str, Any]]) -> None:
    expected = Counter((trial, state) for trial in range(1, REPEATS + 1) for state in STATES)
    if Counter((row.get("trial"), row.get("state")) for row in comparisons) != expected:
        raise ValueError("The transport native-pixel matrix is incomplete or duplicated")
    for row in comparisons:
        fractions = row.get("changed_pixel_fractions", {})
        if (row.get("passed") is not True or row.get("physical_size") != [1440, 960]
                or row.get("device_pixel_ratio") != 1 or row.get("resized_for_comparison") is not False
                or row.get("luminance_threshold") != 12 or row.get("maximum_changed_fraction") != 0.001
                or set(fractions) != {"whole_frame", "document", "horizontal_ruler", "vertical_ruler"}
                or any(type(value) not in (int, float) or not math.isfinite(value)
                       or not 0 <= value <= 0.001 for value in fractions.values())):
            raise ValueError("Transport pixels failed or changed the original native-resolution gate")


def compare_metrics(runs: list[dict[str, Any]]) -> list[dict[str, Any]]:
    if [(run.get("trial"), run.get("canvas_bridge")) for run in runs] != trial_plan():
        raise ValueError("Metric ratios require the complete ordered primary comparison")
    if any(run.get("diagnostic_pass") is not False for run in runs):
        raise ValueError("Diagnostic samples cannot produce primary timing ratios")
    rows = []
    optional = {"thread_cpu_ms", "thread_cpu_ms_per_operation"}
    for metric in ("elapsed_ms", "operations_per_second", "cpu_task_ms_per_operation",
                   "thread_cpu_ms", "thread_cpu_ms_per_operation", "completed_render_callbacks",
                   "webgpu_canvas_submissions"):
        values = {bridge: [run["metrics"].get(metric) for run in runs if run["canvas_bridge"] == bridge]
                  for bridge in BRIDGES}
        if metric in optional and any(value is None for samples in values.values() for value in samples):
            continue
        if any(type(value) not in (int, float) or not math.isfinite(value) or value < 0
               for samples in values.values() for value in samples):
            raise ValueError("Transport ratios require finite nonnegative measured values")
        medians = {bridge: statistics.median(samples) for bridge, samples in values.items()}
        rows.append({"metric": metric, "trial_values": values, "medians": medians,
                     "composite_over_legacy": medians["composite"] / medians["legacy"]
                     if medians["legacy"] else None})
    return rows
