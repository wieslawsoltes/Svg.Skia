"""Attribution for the direct 96e baseline versus an explicitly opt-in candidate."""
from __future__ import annotations

import math
import re
import statistics
from typing import Any

from legacy_baseline_packages import BASELINE_SHA
from metrics import verify_interaction_isolation
from quality import verify_bridge


REPORT_KIND = "direct_baseline_legacy_composite_experiment"
BRIDGE = "legacy-composite"
REPORT_KINDS = {
    BRIDGE: REPORT_KIND,
    "legacy": "direct_baseline_original_upload_experiment",
    "composite": "direct_baseline_persistent_composite_experiment",
    "single-canvas": "direct_baseline_original_topology_experiment",
}
WORKLOADS = ("brush-hover", "selection", "pan", "zoom", "edit")
VIEWPORT = {"width": 1440, "height": 960}


def trial_plan() -> list[tuple[int, str]]:
    roles = ("baseline", "candidate")
    return [(trial, role) for trial in range(1, 4)
            for role in (roles if trial % 2 else tuple(reversed(roles)))]


def verify_report_identity(report: dict[str, Any]) -> None:
    bridge = report.get("candidate_bridge")
    if (bridge not in REPORT_KINDS or report.get("report_kind") != REPORT_KINDS[bridge]
            or report.get("primary_comparison") is not False or report.get("candidate_default") is not False
            or report.get("baseline_source_sha") != BASELINE_SHA or report.get("workload") not in WORKLOADS
            or report.get("repeats") != 3 or report.get("steps") != 120
            or report.get("viewport") != VIEWPORT or report.get("device_scale_factor") != 1
            or report.get("expected_primary_runs") != 6 or report.get("expected_candidate_diagnostics") != 1
            or not re.fullmatch(r"[0-9a-f]{40}", report.get("harness_source_sha", ""))
            or not re.fullmatch(r"[0-9a-f]{40}", report.get("candidate_source_sha", ""))
            or report.get("candidate_source_sha") == BASELINE_SHA):
        raise ValueError("The direct opt-in comparison requires its distinct source and workload identity")


def verify_runs(runs: list[dict[str, Any]], source_sha: str, workload: str, *, diagnostics: bool = False,
                candidate_bridge: str = BRIDGE) -> None:
    expected = [(1, "candidate")] if diagnostics else trial_plan()
    if (candidate_bridge not in REPORT_KINDS or workload not in WORKLOADS
            or [(run.get("trial"), run.get("role")) for run in runs] != expected):
        raise ValueError("The direct comparison is incomplete, duplicated or outside its alternating plan")
    browsers, launches = set(), set()
    for run in runs:
        candidate = run["role"] == "candidate"
        if (run.get("passed") is not True or run.get("source_sha") != (source_sha if candidate else BASELINE_SHA)
                or run.get("backend") != "webgpu" or run.get("workload") != workload
                or run.get("diagnostic_pass") is not diagnostics or run.get("graphite_coverage") is not None
                or run.get("canvas_bridge") != (candidate_bridge if candidate else None)
                or run.get("page_errors") != [] or run.get("resource_errors") != []):
            raise ValueError("A direct trial changed its package, mode, workload or error status")
        environment, renderer = run.get("environment", {}), run.get("renderer", {})
        if (environment.get("viewport") != VIEWPORT or environment.get("device_pixel_ratio") != 1
                or environment.get("cross_origin_isolated") is not False):
            raise ValueError("The direct comparison changed native resolution or hosting policy")
        observed_provider = ("single_canvas_native_factory" in renderer
                             or any(row.get("provider") in {"SingleCanvasWebGpuProvider", "SingleCanvasSkiaProvider"}
                                    for row in renderer.get("negotiations", []))
                             or any(entry.get("text", "").startswith("UnoDraw single-canvas graphics observation:")
                                    or "Graphics backend 'SingleCanvasWebGpuProvider'" in entry.get("text", "")
                                    or "Graphics backend 'SingleCanvasSkiaProvider'" in entry.get("text", "")
                                    for entry in run.get("console", [])))
        if observed_provider:
            if not candidate or candidate_bridge != "single-canvas":
                raise ValueError("Only the explicit single-canvas candidate may use an observed provider")
            console = run.get("console", [])
            start = renderer.get("console_start_index")
            end = renderer.get("console_end_index")
            if (type(start) is not int or start != 0 or type(end) is not int
                    or not 0 < end <= len(console)):
                raise ValueError("The observed provider requires its original completed-startup console boundary")
            # The live driver has already loaded the shared smoke/provider module.
            # Archived native-provider reports do not acquire a new dependency.
            from backend import verify_backend
            proof = verify_backend(console[:end], renderer.get("capabilities", {}), "webgpu",
                                   allow_single_canvas_provider=True)
            if "single_canvas_native_factory" not in proof:
                raise ValueError("The decorated native factory must be observed during completed startup")
            completion_proof = verify_backend(console, renderer.get("capabilities", {}), "webgpu",
                                              allow_single_canvas_provider=True)
            if completion_proof != proof:
                raise ValueError("The native provider observation changed after completed startup")
            if any(renderer.get(key) != value for key, value in proof.items()):
                raise ValueError("Stored native factory proof differs from the original startup observation")
        elif not any(row.get("provider") == "WebGpuGraphicsProvider" and row.get("context") == "WebGpu"
                     for row in renderer.get("negotiations", [])):
            raise ValueError("The direct trial did not prove its actual WebGPU compositor")
        if not any(row.get("kind") == "webgpu" and row.get("connected") is True and row.get("target") is True
                   and type(row.get("presentations")) is int and row["presentations"] > 0
                   for row in renderer.get("application_contexts", [])):
            raise ValueError("The direct trial has no connected application WebGPU canvas")
        warmup = run.get("common_warmup", {})
        offered, trusted = warmup.get("offered_pointer_moves"), warmup.get("trusted_pointer_moves")
        if (type(offered) is not int or offered < 120 or type(trusted) is not int or offered != trusted
                or type(warmup.get("completed_graphics_callbacks")) is not int
                or warmup["completed_graphics_callbacks"] < 60 or warmup.get("included_in_primary_metrics") is not False):
            raise ValueError("Every direct trial requires the original full common pan warmup outside timing")
        metrics = run.get("metrics", {})
        operations = 121 if workload == "selection" else 120
        if any(type(metrics.get(key)) is not int or metrics[key] != value for key, value in {
                "operations": operations, "input_events": operations, "inputs_without_later_graphics": 0,
                "inputs_without_completed_render_callback": 0}.items()):
            raise ValueError("The direct comparison requires every trusted operation and zero unobserved input tails")
        if any(type(metrics.get(key)) is not int or metrics[key] <= 0
               for key in ("completed_render_callbacks", "webgpu_canvas_submissions")):
            raise ValueError("A direct trial did not complete observed graphics work")
        if (metrics.get("graphics_api_calls", {}).get("webgpu.copyExternalImageToTexture", 0) != 0
                or metrics.get("graphite_document_submissions") not in (None, 0)):
            raise ValueError("Another GPU document transport contaminated the direct comparison")
        if candidate:
            # Each explicit mode must prove its exclusive observed selection.
            # Original-topology painting has no feedback cache/reuse claim.
            verify_bridge(run.get("console", []), candidate_bridge,
                          require_reuse=workload in {"brush-hover", "selection"})
        elif any(entry.get("text", "").startswith(("UnoDraw canvas bridge", "UnoDraw legacy-composite", "UnoDraw Graphite"))
                 for entry in run.get("console", [])):
            raise ValueError("The exact baseline unexpectedly selected a new opt-in canvas transport")
        if workload == "selection":
            visual = run.get("quality", {})
            restored = visual.get("restored", {})
            rulers = restored.get("ruler_changed_pixel_fraction", {})
            fractions = [restored.get("changed_pixel_fraction"), *rulers.values()]
            if (visual.get("minimum_changed_pixels") != 50 or type(visual.get("changed_document_pixels")) is not int
                    or visual["changed_document_pixels"] < 50 or set(rulers) != {"horizontal", "vertical"}
                    or any(type(value) not in (int, float) or not math.isfinite(value) or not 0 <= value <= 0.001
                           for value in fractions)):
                raise ValueError("Selection changed the original visible-overlay or restore gate")
        if diagnostics:
            snapshot = run.get("managed_render_diagnostics", {}).get("after", {"phases": []})
            if candidate_bridge == "single-canvas":
                # Repainting the complete original canvas is this control's
                # defining behavior. It must not borrow the retained-layer proof.
                for phase in ("Document", "Interaction"):
                    counts = [row.get("count") for row in snapshot.get("phases", []) if row.get("phase") == phase]
                    if len(counts) != 1 or type(counts[0]) is not int or counts[0] <= 0:
                        raise ValueError("Original-topology diagnostics must actually paint both original layers")
            elif workload in {"brush-hover", "selection"}:
                verify_interaction_isolation(snapshot, workload)
            else:
                documents = [row.get("count") for row in snapshot.get("phases", []) if row.get("phase") == "Document"]
                if len(documents) != 1 or type(documents[0]) is not int or documents[0] <= 0:
                    raise ValueError("Camera/edit diagnostics must actually repaint the document")
        elif "managed_render_diagnostics" in run or "runtime_phase_logs" in run:
            raise ValueError("Candidate diagnostics cannot become primary timing samples")
        browser, launch = run.get("browser_version"), run.get("browser_launch_arguments")
        if not isinstance(browser, str) or not browser or not isinstance(launch, list) or not launch:
            raise ValueError("Every direct trial requires actual browser version and launch arguments")
        browsers.add(browser)
        launches.add(tuple(launch))
    if len(browsers) != 1 or len(launches) != 1:
        raise ValueError("The direct comparison changed its browser configuration")


def compare_metrics(runs: list[dict[str, Any]]) -> list[dict[str, Any]]:
    if ([(run.get("trial"), run.get("role")) for run in runs] != trial_plan()
            or any(run.get("diagnostic_pass") is not False for run in runs)):
        raise ValueError("Direct ratios require all six ordered primary samples")
    rows = []
    optional = {"thread_cpu_ms", "thread_cpu_ms_per_operation"}
    for metric in ("elapsed_ms", "operations_per_second", "cpu_task_ms_per_operation", "thread_cpu_ms",
                   "thread_cpu_ms_per_operation", "completed_render_callbacks", "webgpu_canvas_submissions"):
        values = {role: [run["metrics"].get(metric) for run in runs if run["role"] == role]
                  for role in ("baseline", "candidate")}
        if metric in optional and any(value is None for samples in values.values() for value in samples):
            continue
        if any(type(value) not in (int, float) or not math.isfinite(value) or value < 0
               for samples in values.values() for value in samples):
            raise ValueError("Direct ratios require finite measured samples")
        medians = {role: statistics.median(samples) for role, samples in values.items()}
        rows.append({"metric": metric, "trial_values": values, "medians": medians,
                     "opt_in_candidate_over_baseline": medians["candidate"] / medians["baseline"]
                     if medians["baseline"] else None})
    return rows
