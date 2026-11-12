"""Partition complete comparisons without splitting a revision pair across runners."""
from __future__ import annotations

from collections import Counter
from typing import Any


WORKLOADS = ("idle", "brush-hover", "pan", "zoom", "selection", "edit")
BACKENDS = ("webgpu", "skia-preferred")


def _selection(values: tuple[str, ...], allowed: tuple[str, ...], name: str) -> None:
    if not values or len(set(values)) != len(values) or any(value not in allowed for value in values):
        raise ValueError(f"{name} must contain unique supported values")


def available_backends(package: dict[str, Any], requested: tuple[str, ...]) -> tuple[str, ...]:
    generation = package["runtime_generation"]
    if generation not in {"uno6", "uno7"}:
        raise ValueError(f"Unknown runtime generation: {generation}")
    return tuple(backend for backend in requested if generation == "uno7" or backend == "skia-preferred")


def primary_plan(packages: list[dict[str, Any]], repeats: int,
                 workloads: tuple[str, ...] = WORKLOADS,
                 backends: tuple[str, ...] = BACKENDS) -> list[dict[str, Any]]:
    """Keep the original rotating revision and alternating backend order."""
    _selection(workloads, WORKLOADS, "Workloads")
    _selection(backends, BACKENDS, "Backends")
    if not packages or repeats < 1 or len({package["role"] for package in packages}) != len(packages):
        raise ValueError("A plan requires unique package roles and positive repetitions")
    result = []
    for trial in range(repeats):
        ordered = packages[trial % len(packages):] + packages[:trial % len(packages)]
        for workload in workloads:
            for package in ordered:
                selected = available_backends(package, backends)
                if trial % 2:
                    selected = tuple(reversed(selected))
                for backend in selected:
                    result.append({"role": package["role"], "backend": backend,
                                   "workload": workload, "trial": trial + 1})
    return result


def diagnostic_plan(packages: list[dict[str, Any]], workloads: tuple[str, ...] = WORKLOADS,
                    backends: tuple[str, ...] = BACKENDS) -> list[dict[str, Any]]:
    _selection(workloads, WORKLOADS, "Workloads")
    _selection(backends, BACKENDS, "Backends")
    candidate = next(package for package in packages if package["role"] == "candidate")
    if not candidate.get("supports_render_diagnostics"):
        return []
    return [{"role": "candidate", "backend": backend, "workload": workload, "trial": 1}
            for backend in available_backends(candidate, backends) for workload in workloads]


def run_key(run: dict[str, Any]) -> tuple[str, str, str, int]:
    return run["role"], run["backend"], run["workload"], run["trial"]


def require_run_coverage(runs: list[dict[str, Any]], plan: list[dict[str, Any]], label: str) -> None:
    expected = Counter(map(run_key, plan))
    actual = Counter(map(run_key, runs))
    if actual != expected:
        missing = list((expected - actual).elements())
        unexpected = list((actual - expected).elements())
        raise ValueError(f"Incomplete {label} coverage: missing={missing}, unexpected or duplicate={unexpected}")
    if any(run.get("passed") is not True for run in runs):
        raise ValueError(f"A {label} run did not pass its runtime and quality checks")
