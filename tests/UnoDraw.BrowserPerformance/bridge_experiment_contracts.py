"""Strict attribution for a same-artifact opt-in canvas bridge experiment."""
from __future__ import annotations

import hashlib
import json
import math
import re
import statistics
from collections import Counter
from pathlib import Path
from typing import Any

from quality import validate_graphite_coverage, verify_bridge
from metrics import require_graphite_queue_drain, verify_interaction_isolation


# Keep archived WebGL report validation reproducible; only LIVE_CANDIDATE_BRIDGES
# are executable options in the current driver.
CANDIDATE_BRIDGES = ("webgl", "composite", "legacy-composite", "graphite")
LIVE_CANDIDATE_BRIDGES = ("composite", "legacy-composite", "graphite")
WORKLOADS = ("pan", "zoom", "brush-hover", "selection")
STATES = ("before", "after", "restored")
LEGACY_COMPOSITION_REPORT_KIND = "same_candidate_legacy_composition_experiment"


def bridge_pair(candidate_bridge: str = "webgl") -> tuple[str, str]:
    if candidate_bridge not in CANDIDATE_BRIDGES:
        raise ValueError("Compare the declared control with one supported experimental bridge")
    return ("legacy" if candidate_bridge == "legacy-composite" else "persistent"), candidate_bridge


def report_kind(candidate_bridge: str) -> str:
    bridge_pair(candidate_bridge)
    return (LEGACY_COMPOSITION_REPORT_KIND if candidate_bridge == "legacy-composite"
            else "same_candidate_bridge_experiment")


def verify_report_identity(report: dict[str, Any]) -> None:
    candidate_bridge = report.get("candidate_bridge")
    bridges = bridge_pair(candidate_bridge)
    if (report.get("report_kind") != report_kind(candidate_bridge)
            or report.get("primary_comparison") is not False or report.get("bridges") != list(bridges)):
        raise ValueError("The bridge experiment requires its own declared control and report kind")
    if candidate_bridge == "legacy-composite":
        if (report.get("repeats") != 3 or report.get("steps") != 120
                or report.get("viewport") != {"width": 1440, "height": 960}
                or report.get("device_scale_factor") != 1
                or not re.fullmatch(r"[0-9a-f]{40}", report.get("harness_source_sha", ""))):
            raise ValueError("Legacy composition requires six primary trials, fixed native pixels and the exact harness commit")
        verify_experiment_workload(report.get("workload"), candidate_bridge)


def verify_experiment_workload(workload: str, candidate_bridge: str) -> None:
    bridge_pair(candidate_bridge)
    supported = ("pan", "zoom") if candidate_bridge in {"webgl", "graphite"} else ("brush-hover", "selection")
    if workload not in supported:
        raise ValueError(f"The {candidate_bridge} experiment requires one of: {', '.join(supported)}")


def load_candidate(directory: Path) -> dict[str, Any]:
    package = json.loads((directory / "benchmark-package.json").read_text(encoding="utf-8"))
    if (package.get("schema") != 2 or package.get("role") != "candidate"
            or package.get("configuration") != "Release"
            or package.get("execution_mode") != "InterpreterAndAOT"
            or package.get("runtime_generation") != "uno7"
            or not package.get("dotnet_sdk") or not package.get("dotnet_workload_version")
            or not re.fullmatch(r"[0-9a-f]{40}", package.get("source_sha", ""))
            or not re.fullmatch(r"[0-9a-f]{64}", package.get("fixture_sha256", ""))):
        raise ValueError("The bridge experiment requires one fully identified Uno 7 Release AOT candidate")
    if hashlib.sha256((directory / "wwwroot/index.html").read_bytes()).hexdigest() != package.get("index_sha256"):
        raise ValueError("The candidate's published index does not match its manifest")
    if package.get("supports_render_diagnostics") is not True:
        raise ValueError("The bridge experiment requires separate managed paint diagnostics")
    return {**package, "path": str(directory.resolve())}


def trial_plan(repeats: int, candidate_bridge: str = "webgl") -> list[tuple[int, str]]:
    if not 2 <= repeats <= 20:
        raise ValueError("Use 2–20 matched independent repetitions")
    bridges = bridge_pair(candidate_bridge)
    if candidate_bridge == "legacy-composite" and repeats != 3:
        raise ValueError("Legacy composition requires three repetitions per mode and two separate diagnostic passes")
    return [(trial + 1, bridge) for trial in range(repeats)
            for bridge in (bridges if trial % 2 == 0 else tuple(reversed(bridges)))]


def verify_legacy_composition_environment(runs: list[dict[str, Any]], *, diagnostics: bool) -> None:
    """Prove the original uploader experiment kept its full workload and compositor."""
    browsers, arguments = set(), set()
    for run in runs:
        if run.get("role") != "candidate" or run.get("page_errors") != [] or run.get("resource_errors") != []:
            raise ValueError("Legacy composition requires candidate identity and explicit empty runtime error lists")
        environment = run.get("environment", {})
        if (environment.get("viewport") != {"width": 1440, "height": 960}
                or environment.get("device_pixel_ratio") != 1 or environment.get("cross_origin_isolated") is not False):
            raise ValueError("Legacy composition changed the native viewport, DPR or hosting policy")
        renderer = run.get("renderer", {})
        if not any(row.get("provider") == "WebGpuGraphicsProvider" and row.get("context") == "WebGpu"
                   for row in renderer.get("negotiations", [])):
            raise ValueError("Legacy composition did not prove the actual WebGPU compositor")
        if not any(row.get("kind") == "webgpu" and row.get("connected") is True and row.get("target") is True
                   and type(row.get("presentations")) is int and row["presentations"] > 0
                   for row in renderer.get("application_contexts", [])):
            raise ValueError("Legacy composition did not observe a connected application WebGPU canvas")
        warmup = run.get("common_warmup", {})
        offered, trusted = warmup.get("offered_pointer_moves"), warmup.get("trusted_pointer_moves")
        if (type(offered) is not int or offered < 120 or type(trusted) is not int or offered != trusted
                or type(warmup.get("completed_graphics_callbacks")) is not int
                or warmup["completed_graphics_callbacks"] < 60
                or warmup.get("included_in_primary_metrics") is not False):
            raise ValueError("Every legacy composition trial requires the full common pan warmup outside timing")
        metrics = run.get("metrics", {})
        if (metrics.get("graphics_api_calls", {}).get("webgpu.copyExternalImageToTexture", 0) != 0
                or metrics.get("graphite_document_submissions") not in (None, 0)):
            raise ValueError("Another GPU document path contaminated legacy composition")
        if not diagnostics and ("managed_render_diagnostics" in run or "runtime_phase_logs" in run):
            raise ValueError("Instrumented diagnostics cannot become primary legacy composition timing")
        browser, launch = run.get("browser_version"), run.get("browser_launch_arguments")
        if (not isinstance(browser, str) or not browser or not isinstance(launch, list)
                or not launch or any(not isinstance(item, str) for item in launch)):
            raise ValueError("Legacy composition requires the actual browser version and launch arguments")
        browsers.add(browser)
        arguments.add(tuple(launch))
    if len(browsers) != 1 or len(arguments) != 1:
        raise ValueError("Legacy composition trials changed the browser configuration")


def verify_runs(runs: list[dict[str, Any]], repeats: int, source_sha: str,
                workload: str, *, diagnostics: bool = False, candidate_bridge: str = "webgl",
                graphite_coverage: str | None = None) -> None:
    verify_experiment_workload(workload, candidate_bridge)
    validate_graphite_coverage(candidate_bridge, graphite_coverage)
    bridges = bridge_pair(candidate_bridge)
    expected = Counter(trial_plan(repeats, candidate_bridge) if not diagnostics
                       else [(1, bridge) for bridge in bridges])
    actual = Counter((run.get("trial"), run.get("canvas_bridge")) for run in runs)
    if actual != expected:
        raise ValueError("The same-artifact bridge comparison is incomplete or contains duplicate trials")
    if candidate_bridge == "legacy-composite":
        ordered = [(1, bridge) for bridge in bridges] if diagnostics else trial_plan(repeats, candidate_bridge)
        if [(run.get("trial"), run.get("canvas_bridge")) for run in runs] != ordered:
            raise ValueError("Legacy composition requires the complete declared alternating trial order")
        verify_legacy_composition_environment(runs, diagnostics=diagnostics)
    for run in runs:
        expected_coverage = graphite_coverage if run.get("canvas_bridge") == "graphite" else None
        if (run.get("passed") is not True or run.get("source_sha") != source_sha
                or run.get("workload") != workload or run.get("backend") != "webgpu"
                or run.get("diagnostic_pass") is not diagnostics
                or run.get("graphite_coverage") != expected_coverage):
            raise ValueError("A bridge trial failed or changed source, workload, backend, diagnostic mode or coverage")
        metrics = run.get("metrics", {})
        # The unchanged selection driver makes the configured even step count
        # odd, ending with visible selection feedback that a no-op cannot pass.
        operations = 121 if workload == "selection" else 120
        if any(type(metrics.get(key)) is not int or metrics[key] != value for key, value in {
                "operations": operations, "input_events": operations, "inputs_without_later_graphics": 0,
                "inputs_without_completed_render_callback": 0}.items()):
            raise ValueError(f"Each {workload} bridge trial requires {operations} operations and trusted inputs "
                             "with no unobserved input tail")
        if any(type(metrics.get(key)) is not int or metrics[key] <= 0
               for key in ("completed_render_callbacks", "webgpu_canvas_submissions")):
            raise ValueError("Each bridge trial must complete observed application graphics work")
        if run["canvas_bridge"] == "webgl" and metrics.get("graphics_api_calls", {}).get(
                "webgpu.copyExternalImageToTexture", 0) <= 0:
            raise ValueError("The WebGL trial did not copy a document image during measurement")
        if run["canvas_bridge"] == "graphite":
            submissions = metrics.get("graphite_document_submissions")
            if isinstance(submissions, bool) or not isinstance(submissions, int) or submissions <= 0:
                raise ValueError("The Graphite trial did not submit document commands during measurement")
            if metrics.get("graphics_api_calls", {}).get("webgpu.copyExternalImageToTexture", 0) != 0:
                raise ValueError("The direct Graphite trial unexpectedly copied an external document image")
            require_graphite_queue_drain(run.get("graphite_queue_drain", {}), required=True)
        if workload == "selection":
            visual = run.get("quality", {})
            changed = visual.get("changed_document_pixels")
            if visual.get("minimum_changed_pixels") != 50 or type(changed) is not int or changed < 50:
                raise ValueError("Selection must retain the existing visible-overlay readiness proof")
            restored = visual.get("restored", {})
            rulers = restored.get("ruler_changed_pixel_fraction", {})
            fractions = [restored.get("changed_pixel_fraction"), *rulers.values()]
            if (set(rulers) != {"horizontal", "vertical"}
                    or any(type(value) not in (int, float) or not math.isfinite(value)
                           or not 0 <= value <= 0.001 for value in fractions)):
                raise ValueError("Selection must restore the original document and ruler pixel checks")
        if diagnostics and workload in {"brush-hover", "selection"}:
            verify_interaction_isolation(
                run.get("managed_render_diagnostics", {}).get("after", {"phases": []}), workload)
        verify_bridge(run["console"], run["canvas_bridge"], graphite_coverage=expected_coverage)


def compare_metrics(runs: list[dict[str, Any]], candidate_bridge: str = "webgl") -> list[dict[str, Any]]:
    bridges = bridge_pair(candidate_bridge)
    control = bridges[0]
    if any(run.get("canvas_bridge") not in bridges for run in runs):
        raise ValueError("Metric ratios cannot mix different experimental bridge pairs")
    if candidate_bridge == "legacy-composite":
        if ([(run.get("trial"), run.get("canvas_bridge")) for run in runs] != trial_plan(3, candidate_bridge)
                or any(run.get("diagnostic_pass") is not False for run in runs)):
            raise ValueError("Legacy composition ratios require the complete ordered primary comparison")
    rows = []
    optional = {"thread_cpu_ms", "thread_cpu_ms_per_operation"}
    for metric in ("cpu_task_ms_per_operation", "thread_cpu_ms", "thread_cpu_ms_per_operation",
                   "elapsed_ms", "operations_per_second"):
        values = {bridge: [(run["metrics"].get(metric) if metric in optional else run["metrics"][metric])
                           for run in runs if run["canvas_bridge"] == bridge]
                  for bridge in bridges}
        if not values[control] or len(values[control]) != len(values[candidate_bridge]):
            raise ValueError("Metric ratios require matching control and experimental trials")
        if metric in optional and any(value is None for samples in values.values() for value in samples):
            continue
        if any(type(value) not in (int, float) or not math.isfinite(value) or value < 0
               for samples in values.values() for value in samples):
            raise ValueError("Metric ratios require finite measured values")
        medians = {bridge: statistics.median(samples) for bridge, samples in values.items()}
        ratio = medians[candidate_bridge] / medians[control] if medians[control] else None
        row = {"metric": metric, "trial_values": values, "medians": medians,
               "candidate_bridge": candidate_bridge, "candidate_over_" + control: ratio}
        if candidate_bridge == "webgl":
            row["webgl_over_persistent"] = ratio
        rows.append(row)
    return rows
