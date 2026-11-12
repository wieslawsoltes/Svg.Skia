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

from quality import verify_bridge
from metrics import require_graphite_queue_drain, verify_hover_isolation


CANDIDATE_BRIDGES = ("webgl", "composite", "graphite")
WORKLOADS = ("pan", "zoom", "brush-hover")
STATES = ("before", "after", "restored")


def bridge_pair(candidate_bridge: str = "webgl") -> tuple[str, str]:
    if candidate_bridge not in CANDIDATE_BRIDGES:
        raise ValueError("Compare persistent with one supported experimental bridge")
    return "persistent", candidate_bridge


def verify_experiment_workload(workload: str, candidate_bridge: str) -> None:
    bridge_pair(candidate_bridge)
    supported = ("pan", "zoom") if candidate_bridge in {"webgl", "graphite"} else ("brush-hover",)
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
    return [(trial + 1, bridge) for trial in range(repeats)
            for bridge in (bridges if trial % 2 == 0 else tuple(reversed(bridges)))]


def verify_runs(runs: list[dict[str, Any]], repeats: int, source_sha: str,
                workload: str, *, diagnostics: bool = False, candidate_bridge: str = "webgl") -> None:
    verify_experiment_workload(workload, candidate_bridge)
    bridges = bridge_pair(candidate_bridge)
    expected = Counter(trial_plan(repeats, candidate_bridge) if not diagnostics
                       else [(1, bridge) for bridge in bridges])
    actual = Counter((run.get("trial"), run.get("canvas_bridge")) for run in runs)
    if actual != expected:
        raise ValueError("The same-artifact bridge comparison is incomplete or contains duplicate trials")
    for run in runs:
        if (run.get("passed") is not True or run.get("source_sha") != source_sha
                or run.get("workload") != workload or run.get("backend") != "webgpu"
                or run.get("diagnostic_pass") is not diagnostics):
            raise ValueError("A bridge trial failed or changed source, workload, backend or diagnostic mode")
        metrics = run.get("metrics", {})
        if any(metrics.get(key) != value for key, value in {
                "operations": 120, "input_events": 120, "inputs_without_later_graphics": 0,
                "inputs_without_completed_render_callback": 0}.items()):
            raise ValueError("Each bridge trial requires all 120 trusted inputs and no unobserved input tail")
        if (metrics.get("completed_render_callbacks", 0) <= 0
                or metrics.get("webgpu_canvas_submissions", 0) <= 0):
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
        if diagnostics and workload == "brush-hover":
            verify_hover_isolation(run.get("managed_render_diagnostics", {}).get("after", {"phases": []}))
        verify_bridge(run["console"], run["canvas_bridge"])


def compare_metrics(runs: list[dict[str, Any]], candidate_bridge: str = "webgl") -> list[dict[str, Any]]:
    bridges = bridge_pair(candidate_bridge)
    if any(run.get("canvas_bridge") not in bridges for run in runs):
        raise ValueError("Metric ratios cannot mix different experimental bridge pairs")
    rows = []
    optional = {"thread_cpu_ms", "thread_cpu_ms_per_operation"}
    for metric in ("cpu_task_ms_per_operation", "thread_cpu_ms", "thread_cpu_ms_per_operation",
                   "elapsed_ms", "operations_per_second"):
        values = {bridge: [(run["metrics"].get(metric) if metric in optional else run["metrics"][metric])
                           for run in runs if run["canvas_bridge"] == bridge]
                  for bridge in bridges}
        if not values["persistent"] or len(values["persistent"]) != len(values[candidate_bridge]):
            raise ValueError("Metric ratios require matching persistent and experimental trials")
        if metric in optional and any(value is None for samples in values.values() for value in samples):
            continue
        if any(value is None or not math.isfinite(value) or value < 0
               for samples in values.values() for value in samples):
            raise ValueError("Metric ratios require finite measured values")
        medians = {bridge: statistics.median(samples) for bridge, samples in values.items()}
        ratio = medians[candidate_bridge] / medians["persistent"] if medians["persistent"] else None
        row = {"metric": metric, "trial_values": values, "medians": medians,
               "candidate_bridge": candidate_bridge, "candidate_over_persistent": ratio}
        if candidate_bridge == "webgl":
            row["webgl_over_persistent"] = ratio
        rows.append(row)
    return rows
