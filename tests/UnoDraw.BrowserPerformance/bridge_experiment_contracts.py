"""Strict attribution for a same-artifact CPU/WebGL raster experiment."""
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


BRIDGES = ("persistent", "webgl")
WORKLOADS = ("pan", "zoom")
STATES = ("before", "after", "restored")


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


def trial_plan(repeats: int) -> list[tuple[int, str]]:
    if not 2 <= repeats <= 20:
        raise ValueError("Use 2–20 matched independent repetitions")
    return [(trial + 1, bridge) for trial in range(repeats)
            for bridge in (BRIDGES if trial % 2 == 0 else tuple(reversed(BRIDGES)))]


def verify_runs(runs: list[dict[str, Any]], repeats: int, source_sha: str,
                workload: str, *, diagnostics: bool = False) -> None:
    expected = Counter(trial_plan(repeats) if not diagnostics else [(1, bridge) for bridge in BRIDGES])
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
        if run["canvas_bridge"] == "webgl" and metrics.get("graphics_api_calls", {}).get(
                "webgpu.copyExternalImageToTexture", 0) <= 0:
            raise ValueError("The WebGL trial did not copy a document image during measurement")
        verify_bridge(run["console"], run["canvas_bridge"])


def compare_metrics(runs: list[dict[str, Any]]) -> list[dict[str, Any]]:
    rows = []
    for metric in ("cpu_task_ms_per_operation", "elapsed_ms", "operations_per_second"):
        values = {bridge: [run["metrics"][metric] for run in runs if run["canvas_bridge"] == bridge]
                  for bridge in BRIDGES}
        if not values["persistent"] or len(values["persistent"]) != len(values["webgl"]):
            raise ValueError("Metric ratios require matching CPU and WebGL trials")
        if any(value is None or not math.isfinite(value) or value < 0
               for samples in values.values() for value in samples):
            raise ValueError("Metric ratios require finite measured values")
        medians = {bridge: statistics.median(samples) for bridge, samples in values.items()}
        rows.append({"metric": metric, "trial_values": values, "medians": medians,
                     "webgl_over_persistent": (medians["webgl"] / medians["persistent"]
                                               if medians["persistent"] else None)})
    return rows
