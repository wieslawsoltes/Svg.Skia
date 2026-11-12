#!/usr/bin/env python3
"""Validate all workload/backend shards without pooling observations across runners."""
from __future__ import annotations

import argparse
import hashlib
import json
import math
import re
from collections import Counter
from pathlib import Path
from typing import Any

from metrics import comparisons, metric_label, validate_packages
from quality import verify_bridge
from workload_plan import BACKENDS, WORKLOADS, diagnostic_plan, primary_plan, require_run_coverage


SETTINGS = ("viewport", "device_scale_factor", "repeats", "steps", "idle_ms", "warmup_policy",
            "graphics_description", "metric_limits", "python")
EXPECTED_SETTINGS = {"viewport": {"width": 1440, "height": 960}, "device_scale_factor": 1,
                     "steps": 120, "idle_ms": 2000}


def read_json(path: Path) -> dict[str, Any]:
    def invalid_constant(value: str) -> None:
        raise ValueError(f"Nonfinite JSON value in {path}: {value}")

    def finite_float(value: str) -> float:
        number = float(value)
        if not math.isfinite(number):
            invalid_constant(value)
        return number

    value = json.loads(path.read_text(encoding="utf-8"), parse_constant=invalid_constant, parse_float=finite_float)
    if not isinstance(value, dict):
        raise ValueError(f"Expected a JSON object in {path}")
    return value


def package_identity(packages: list[dict[str, Any]], require_reference: bool) -> list[dict[str, Any]]:
    validate_packages(packages)
    roles = {package["role"] for package in packages}
    if roles not in ({"baseline", "candidate"}, {"baseline", "candidate", "reference"}):
        raise ValueError("The complete comparison requires baseline, candidate and an optional reference package")
    if require_reference and "reference" not in roles:
        raise ValueError("This comparison explicitly requires the pre-migration reference package")
    for package in packages:
        generation = "uno6" if package["role"] == "reference" else "uno7"
        if package.get("runtime_generation") != generation:
            raise ValueError(f"Unexpected runtime generation for {package['role']}")
        if not re.fullmatch(r"[0-9a-f]{40}", package.get("source_sha", "")):
            raise ValueError("Every package must record its exact source commit")
        if not re.fullmatch(r"[0-9a-f]{64}", package.get("index_sha256", "")):
            raise ValueError("Every package must record the published index digest")
    if len({package["source_sha"] for package in packages}) != len(packages):
        raise ValueError("Package roles must identify distinct revisions")
    candidate = next(package for package in packages if package["role"] == "candidate")
    if candidate.get("supports_render_diagnostics") is not True:
        raise ValueError("The complete comparison requires the candidate's separate diagnostic pass")
    # Local extraction paths vary by runner. All actual manifest fields, including
    # source/build metadata and asset hashes, must match for each package role.
    return sorted(({key: value for key, value in package.items() if key != "path"}
                   for package in packages), key=lambda package: package["role"])


def validate_quality(rows: list[dict[str, Any]], backend: str, workload: str, repeats: int) -> None:
    keys = Counter((row["backend"], row["workload"], row["trial"]) for row in rows)
    if keys != Counter((backend, workload, trial) for trial in range(1, repeats + 1)):
        raise ValueError("Every shard requires all planned baseline/candidate pixel comparisons")
    for row in rows:
        for state in ("before", "after", "restored"):
            pixels = row[state]
            groups = (({"document": pixels["changed_pixel_fraction"]}, {"document"}, 0.001),
                      (pixels["ruler_changed_pixel_fraction"], {"horizontal", "vertical"}, 0.001),
                      (pixels["chrome_font_changed_pixel_fraction"],
                       {"menu_labels", "toolbar_icons", "panel_icons"}, 0.02))
            for values, expected, limit in groups:
                if set(values) != expected or any(not isinstance(value, (int, float))
                                                 or not math.isfinite(value) or not 0 <= value <= limit
                                                 for value in values.values()):
                    raise ValueError(f"Invalid or failed {state} pixel comparison: {row}")


def validate_proof(proof: dict[str, Any], candidate_sha: str) -> None:
    if (proof.get("production_source_equivalent") is not True
            or proof.get("production_changes") != []
            or proof.get("package_source_sha") != candidate_sha
            or proof.get("execution_mode") != "InterpreterAndAOT"
            or not re.fullmatch(r"[0-9a-f]{40}", proof.get("harness_head_sha", ""))
            or proof.get("allowed_difference_prefixes") != ["tests/", "docs/", ".github/"]
            or not isinstance(proof.get("changed_paths"), list)
            or any(not isinstance(path, str) or not path.startswith(("tests/", "docs/", ".github/"))
                   for path in proof["changed_paths"])):
        raise ValueError("Invalid candidate source-equivalence proof")


def merge_reports(shards: Path, *, require_reference: bool = False,
                  expected_repeats: int = 3) -> tuple[dict[str, Any], dict[str, dict[str, Any]]]:
    if not 2 <= expected_repeats <= 20:
        raise ValueError("Use 2–20 repetitions, matching the selected workflow input")
    reports = []
    for path in sorted(shards.rglob("report.json")):
        value = read_json(path)
        if "schema" in value and "packages" in value and "runs" in value:
            reports.append((path, value))
    expected_shards = {(backend, workload) for backend in BACKENDS for workload in WORKLOADS}
    actual_shards = Counter((report.get("shard", {}).get("backend"),
                             report.get("shard", {}).get("workload")) for _, report in reports)
    if actual_shards != Counter(expected_shards):
        raise ValueError(f"Require exactly 12 distinct workload/backend shards; found {dict(actual_shards)}")

    identity = None
    settings = None
    browser_versions: set[str] = set()
    launch_arguments: dict[str, list[str]] = {}
    proofs: dict[str, dict[str, Any]] = {}
    merged: dict[str, Any] = {"schema": 1, "merged_shards": True, "passed": False,
                              "selected_workloads": list(WORKLOADS), "selected_backends": list(BACKENDS),
                              "expected_diagnostic_runs": 12,
                              "shards": [], "runs": [], "diagnostic_runs": [],
                              "comparisons": [], "cross_build_quality": []}
    for path, report in reports:
        backend, workload = report["shard"]["backend"], report["shard"]["workload"]
        label = f"{backend}/{workload}"
        if report.get("schema") != 1 or report.get("passed") is not True:
            raise ValueError(f"Shard {label} did not finish successfully")
        if report.get("selected_workloads") != [workload] or report.get("selected_backends") != [backend]:
            raise ValueError(f"Shard {label} did not execute its declared selection")
        actual_settings = {key: report[key] for key in SETTINGS}
        if (actual_settings["repeats"] != expected_repeats
                or any(actual_settings[key] != value for key, value in EXPECTED_SETTINGS.items())):
            raise ValueError(f"Shard {label} changed the fixed measurement settings")
        actual_identity = package_identity(report["packages"], require_reference)
        if identity is None:
            identity, settings = actual_identity, actual_settings
            merged.update(actual_settings)
            merged["packages"] = identity
            merged["expected_primary_runs"] = len(primary_plan(identity, expected_repeats))
            merged["expected_quality_comparisons"] = 12 * expected_repeats
        elif actual_identity != identity or actual_settings != settings:
            raise ValueError(f"Shard {label} has inconsistent packages, toolchain, fixture or measurement settings")

        primary = primary_plan(report["packages"], expected_repeats, (workload,), (backend,))
        diagnostic = diagnostic_plan(report["packages"], (workload,), (backend,))
        if (report.get("expected_primary_runs") != len(primary)
                or report.get("expected_diagnostic_runs") != len(diagnostic)):
            raise ValueError(f"Shard {label} has incorrect declared run counts")
        require_run_coverage(report["runs"], primary, f"{label} primary")
        require_run_coverage(report["diagnostic_runs"], diagnostic, f"{label} diagnostic")
        sources = {package["role"]: package["source_sha"] for package in report["packages"]}
        for diagnostic_pass, runs in ((False, report["runs"]), (True, report["diagnostic_runs"])):
            for run in runs:
                if run.get("source_sha") != sources[run["role"]] or run.get("diagnostic_pass") is not diagnostic_pass:
                    raise ValueError(f"Shard {label} contains a run from the wrong source or measurement pass")
                if not isinstance(run.get("browser_version"), str) or not run["browser_version"]:
                    raise ValueError(f"Shard {label} does not record its browser version")
                browser_versions.add(run["browser_version"])
                arguments = run["browser_launch_arguments"]
                if not isinstance(arguments, list) or any(not isinstance(value, str) for value in arguments):
                    raise ValueError(f"Shard {label} has invalid browser launch settings")
                if backend in launch_arguments and launch_arguments[backend] != arguments:
                    raise ValueError(f"Shard {label} changed its backend's browser launch settings")
                launch_arguments[backend] = arguments
                if not isinstance(run.get("raw_browser_observations"), dict) or not run["raw_browser_observations"]:
                    raise ValueError(f"Shard {label} is missing its raw browser observations")
                if diagnostic_pass and not isinstance(run.get("managed_render_diagnostics"), dict):
                    raise ValueError(f"Shard {label} is missing its separate managed diagnostics")
                if run["role"] == "candidate" and backend == "webgpu":
                    verify_bridge(run["console"], "persistent")

        # Recompute only to check each shard's own stored comparison. The output
        # retains its original rows; timestamps and frame distributions are never
        # joined across runners, nor are percentiles computed from pooled frames.
        if report["comparisons"] != comparisons(report["runs"]) or not report["comparisons"]:
            raise ValueError(f"Shard {label} has missing or inconsistent within-runner comparisons")
        validate_quality(report["cross_build_quality"], backend, workload, expected_repeats)
        for key in ("runs", "diagnostic_runs", "comparisons", "cross_build_quality"):
            merged[key].extend(report[key])
        entry: dict[str, Any] = {"backend": backend, "workload": workload,
                                 "source_report": path.relative_to(shards).as_posix(),
                                 "report_sha256": hashlib.sha256(path.read_bytes()).hexdigest(),
                                 "primary_runs": len(primary), "diagnostic_runs": len(diagnostic),
                                 "platform": report["platform"],
                                 "started_unix": report["started_unix"],
                                 "duration_seconds": report["duration_seconds"]}
        proof_path = path.with_name("source-equivalence.json")
        if proof_path.is_file():
            proof = read_json(proof_path)
            validate_proof(proof, sources["candidate"])
            if proofs and proof != next(iter(proofs.values())):
                raise ValueError("Source-equivalence proofs do not identify the same package run and harness source")
            name = f"source-equivalence/{backend}-{workload}.json"
            proofs[name] = proof
            entry["source_equivalence"] = name
        merged["shards"].append(entry)

    if len(browser_versions) != 1 or (proofs and len(proofs) != 12):
        raise ValueError("All shards must use the same browser and provide consistent source-equivalence evidence")
    require_run_coverage(merged["runs"], primary_plan(identity, expected_repeats), "complete primary")
    require_run_coverage(merged["diagnostic_runs"], diagnostic_plan(identity), "complete diagnostic")
    merged["browser_version"] = next(iter(browser_versions))
    merged["artifact_policy"] = ("Raw run records are unchanged. Resolve each artifact_directory relative to its "
                                 "original shard's source_report directory; full PNGs and raw reports remain in "
                                 "the separately retained shard artifacts.")
    merged["aggregation_policy"] = ("Concatenate verified within-runner comparisons only. No pooled frames, "
                                    "percentiles or timestamps across runners.")
    merged["passed"] = True
    return merged, proofs


def write_summary(report: dict[str, Any], output: Path) -> None:
    lines = ["# UnoDraw complete browser performance comparison", ""]
    if not report["passed"]:
        lines += ["Validation failed: " + report["failure"], "",
                  "No complete performance result is claimed; inspect the individual shard artifacts."]
    else:
        lines += [f"Validated 12 shards: {report['expected_primary_runs']} primary trials, "
                  f"12 separate diagnostic passes and {report['expected_quality_comparisons']} "
                  "matched baseline/candidate screenshot comparisons.",
                  "", report["graphics_description"], "",
                  f"Each row retains medians of {report['repeats']} trials from one runner. Different workloads/backends "
                  "are separate shards; their frame distributions and timestamps are never pooled. "
                  "Task wall duration includes waits; ThreadTime separately measures active renderer-thread CPU. "
                  "Neither callback completion nor these counters proves physical presentation or input-to-photon latency. "
                  "Ratios below 1 favor the candidate for time costs, while throughput favors ratios above 1.", "",
                  "| Reference | Backend | Workload | Metric | Reference median | Candidate median | Ratio |",
                  "| --- | --- | --- | --- | ---: | ---: | ---: |"]
        for row in report["comparisons"]:
            ratio = f"{row['candidate_over_baseline']:.3f}" if row["candidate_over_baseline"] is not None else "n/a"
            lines.append(f"| {row['baseline_role']} | {row['backend']} | {row['workload']} | {metric_label(row['metric'])} "
                         f"| {row['baseline_median']:.3f} | {row['candidate_median']:.3f} | {ratio} |")
        lines += ["", "## Original shard artifacts", "", report["artifact_policy"], "",
                  "| Backend | Workload | Original report path |", "| --- | --- | --- |"]
        for shard in report["shards"]:
            lines.append(f"| {shard['backend']} | {shard['workload']} | `{shard['source_report']}` |")
    (output / "summary.md").write_text("\n".join(lines) + "\n", encoding="utf-8")


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--shards", type=Path, required=True, help="Directory containing all 12 downloaded shard artifacts")
    parser.add_argument("--output", type=Path, required=True)
    parser.add_argument("--repeats", type=int, default=3,
                        help="Required repetitions per revision, matching the selected workflow input")
    parser.add_argument("--require-reference", action="store_true",
                        help="Require the pre-migration reference for every selected repetition")
    args = parser.parse_args()
    shards, output = args.shards.resolve(), args.output.resolve()
    if not shards.is_dir() or output == shards or shards in output.parents:
        parser.error("Use an existing shard directory and an output directory outside it")
    output.mkdir(parents=True, exist_ok=True)
    proofs: dict[str, dict[str, Any]] = {}
    try:
        report, proofs = merge_reports(shards, require_reference=args.require_reference, expected_repeats=args.repeats)
    except Exception as error:
        report = {"schema": 1, "merged_shards": True, "passed": False,
                  "failure": f"{type(error).__name__}: {error}"}
    for name, proof in proofs.items():
        path = output / name
        path.parent.mkdir(parents=True, exist_ok=True)
        path.write_text(json.dumps(proof, indent=2) + "\n", encoding="utf-8")
    (output / "report.json").write_text(json.dumps(report, indent=2) + "\n", encoding="utf-8")
    write_summary(report, output)
    print("Complete shard comparison passed" if report["passed"] else report["failure"])
    return 0 if report["passed"] else 1


if __name__ == "__main__":
    raise SystemExit(main())
