"""Pure aggregation for browser observations; never equates rAF with a rendered frame."""
from __future__ import annotations

import math
import statistics
from typing import Any


def distribution(values: list[float]) -> dict[str, float | int | None]:
    if any(not math.isfinite(value) or value < 0 for value in values):
        raise ValueError("Timing samples must be finite and nonnegative")
    ordered = sorted(values)

    def quantile(fraction: float) -> float | None:
        if not ordered:
            return None
        position = (len(ordered) - 1) * fraction
        lower, upper = math.floor(position), math.ceil(position)
        return ordered[lower] + (ordered[upper] - ordered[lower]) * (position - lower)

    return {"count": len(ordered), "p50": quantile(0.5), "p95": quantile(0.95),
            "p99": quantile(0.99), "max": max(ordered) if ordered else None}


def intervals(timestamps: list[float]) -> list[float]:
    if any(not math.isfinite(value) for value in timestamps):
        raise ValueError("Timestamps must be finite")
    result = [right - left for left, right in zip(timestamps, timestamps[1:])]
    if any(value < 0 for value in result):
        raise ValueError("Timestamps must be monotonically increasing")
    return result


def summarize(sample: dict[str, Any], cpu_before: dict[str, float], cpu_after: dict[str, float],
              operations: int) -> dict[str, Any]:
    elapsed = sample["ended_ms"] - sample["started_ms"]
    if elapsed <= 0 or not math.isfinite(elapsed):
        raise ValueError("A measurement must have a positive finite duration")
    result: dict[str, Any] = {
        "elapsed_ms": elapsed,
        "operations": operations,
        "operations_per_second": operations * 1000 / elapsed if operations else None,
        "raf_interval_ms": distribution(intervals(sample["raf_ms"])),
        "graphics_active_animation_intervals": len(sample["graphics_active_intervals_ms"]),
        "graphics_active_interval_ms": distribution(intervals(sample["graphics_active_intervals_ms"])),
        "application_raf_callback_ms": distribution(sample["application_raf_callback_ms"]),
        "completed_render_callbacks": len(sample["completed_render_callback_ms"]),
        "completed_render_callback_ms": distribution(sample["completed_render_callback_ms"]),
        "completed_render_callback_interval_ms": distribution(intervals(sample["completed_render_callback_end_ms"])),
        "input_to_completed_render_callback_ms": distribution(sample["input_to_completed_render_callback_ms"]),
        "input_to_next_graphics_ms": distribution(sample["input_to_next_graphics_ms"]),
        "webgpu_canvas_submissions": len(sample["webgpu_canvas_submit_ms"]),
        "webgl_flush_calls": len(sample["webgl_flush_ms"]),
        "input_events": sample["input_events"],
        "inputs_without_later_graphics": sample["inputs_without_later_graphics"],
        "inputs_without_completed_render_callback": sample["inputs_without_completed_render_callback"],
        "graphics_api_calls": sample["calls"],
        "supplied_upload_buffer_bytes": sample["supplied_upload_buffer_bytes"],
        "upload_calls_without_buffer_size": sample["upload_calls_without_buffer_size"],
        "physical_presented_frames": None,
        "managed_allocated_bytes": None,
        "cpu_ms": {},
    }
    for name in ("TaskDuration", "ScriptDuration", "LayoutDuration", "RecalcStyleDuration"):
        if name in cpu_before and name in cpu_after:
            result["cpu_ms"][name] = (cpu_after[name] - cpu_before[name]) * 1000
    result["cpu_task_ms_per_operation"] = (
        result["cpu_ms"].get("TaskDuration", 0) / operations
        if operations and "TaskDuration" in result["cpu_ms"] else None)
    result["js_heap_used_delta_bytes"] = (
        cpu_after["JSHeapUsedSize"] - cpu_before["JSHeapUsedSize"]
        if "JSHeapUsedSize" in cpu_before and "JSHeapUsedSize" in cpu_after else None)
    return result


def validate_packages(packages: list[dict[str, Any]]) -> None:
    if not {"baseline", "candidate"}.issubset({package["role"] for package in packages}):
        raise ValueError("Both baseline and candidate packages are required")
    if len({package["role"] for package in packages}) != len(packages):
        raise ValueError("Package roles must be unique")
    if len({package["fixture_sha256"] for package in packages}) != 1:
        raise ValueError("All packages must embed the identical Welcome.svg fixture")
    if len({package["dotnet_sdk"] for package in packages}) != 1:
        raise ValueError("All packages must use the same .NET SDK for a controlled comparison")
    for package in packages:
        if package["configuration"] != "Release" or package["execution_mode"] != "InterpreterAndAOT":
            raise ValueError("Comparisons require the published Release InterpreterAndAOT artifact")


def verify_hover_isolation(snapshot: dict[str, Any]) -> None:
    phases = {phase["phase"]: phase for phase in snapshot["phases"]}
    if phases.get("Document", {}).get("count") != 0 or phases.get("Interaction", {}).get("count", 0) <= 0:
        raise AssertionError("Brush hover must repaint the interaction layer without repainting the SVG document")


def comparisons(runs: list[dict[str, Any]]) -> list[dict[str, Any]]:
    """Compare medians of per-trial metrics, not a pooled distribution of frames."""
    groups: dict[tuple[str, str, str], list[dict[str, Any]]] = {}
    for run in runs:
        groups.setdefault((run["role"], run["backend"], run["workload"]), []).append(run["metrics"])
    rows = []
    fields = {
        "cpu_task_ms_per_operation": lambda value: value["cpu_task_ms_per_operation"],
        "raf_p95_ms": lambda value: value["raf_interval_ms"]["p95"],
        "graphics_active_interval_p95_ms": lambda value: value["graphics_active_interval_ms"]["p95"],
        "input_to_next_graphics_p95_ms": lambda value: value["input_to_next_graphics_ms"]["p95"],
        "application_raf_callback_p95_ms": lambda value: value["application_raf_callback_ms"]["p95"],
        "completed_render_callback_p95_ms": lambda value: value["completed_render_callback_ms"]["p95"],
        "input_to_completed_render_callback_p95_ms": lambda value: value["input_to_completed_render_callback_ms"]["p95"],
        "operations_per_second": lambda value: value["operations_per_second"],
    }
    for (role, backend, workload), candidates in sorted(groups.items()):
        if role != "candidate":
            continue
        for baseline_role in ("baseline", "reference"):
            baseline = groups.get((baseline_role, backend, workload))
            if not baseline:
                continue
            for metric, read in fields.items():
                before = [read(value) for value in baseline if read(value) is not None]
                after = [read(value) for value in candidates if read(value) is not None]
                if not before or not after:
                    continue
                before_median, after_median = statistics.median(before), statistics.median(after)
                rows.append({"baseline_role": baseline_role, "backend": backend, "workload": workload,
                             "metric": metric, "baseline_median": before_median,
                             "candidate_median": after_median,
                             "candidate_over_baseline": after_median / before_median if before_median else None,
                             "baseline_trials": before, "candidate_trials": after})
    return rows
