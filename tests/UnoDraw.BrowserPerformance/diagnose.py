#!/usr/bin/env python3
"""Run one explicitly instrumented candidate workload without claiming a speedup."""
from __future__ import annotations

import argparse
import json
import time
from pathlib import Path
from typing import Any

from playwright.sync_api import sync_playwright

from metrics import validate_packages
from performance import VIEWPORT, launch_arguments, run_trial, serve
from quality import verify_bridge
from workload_plan import BACKENDS, WORKLOADS


def write_summary(report: dict[str, Any], output: Path) -> None:
    lines = ["# UnoDraw candidate rendering diagnostics", "",
             f"Backend: {report['backend']}; workload: {report['workload']}; "
             f"candidate source: `{report['candidate_source_sha']}`.", "",
             "This separate instrumented pass uses the same real-input warmup, 120 operations and quality checks. "
             "It is not a before/after performance comparison. SwiftShader implements the graphics APIs in software; "
             "paint callback costs exclude deferred compositor/GPU work.", ""]
    run = report.get("diagnostic_run", {})
    snapshot = run.get("managed_render_diagnostics", {}).get("after", {})
    if snapshot:
        lines += ["| Paint callback | Calls | Total CPU ms | Max CPU ms | Allocated bytes |",
                  "| --- | ---: | ---: | ---: | ---: |"]
        for phase in snapshot["phases"]:
            lines.append(f"| {phase['phase']} | {phase['count']} | {phase['totalMilliseconds']:.3f} "
                         f"| {phase['maxMilliseconds']:.3f} | {phase['allocatedBytes']} |")
    phase_logs = run.get("runtime_phase_logs", [])
    if phase_logs:
        lines += ["", "## Uno runtime phase and WebGPU diagnostics", "", "```text"]
        lines += phase_logs
        lines += ["```"]
    if not report["passed"]:
        lines += ["", "Diagnostic run failed: " + report.get("failure", "The workload did not pass")]
    (output / "summary.md").write_text("\n".join(lines) + "\n", encoding="utf-8")


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--packages", type=Path, required=True)
    parser.add_argument("--output", type=Path, required=True)
    parser.add_argument("--backend", choices=BACKENDS, required=True)
    parser.add_argument("--workload", choices=WORKLOADS, required=True)
    args = parser.parse_args()
    # Keep the primary driver's workload and warmup intact; only select its
    # existing separate diagnostic mode. There is no reduced quick workload.
    args.steps, args.idle_ms = 120, 2000
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
    candidate = next(package for package in packages if package["role"] == "candidate")
    report: dict[str, Any] = {
        "schema": 1, "report_kind": "candidate_render_diagnostics", "passed": False,
        "primary_comparison": False, "started_unix": time.time(), "packages": packages,
        "candidate_source_sha": candidate["source_sha"], "backend": args.backend,
        "workload": args.workload, "steps": args.steps, "idle_ms": args.idle_ms,
        "viewport": VIEWPORT, "device_scale_factor": 1,
        "graphics_description": "SwiftShader software GPU API implementations; not physical GPU throughput",
    }
    try:
        if not candidate.get("supports_render_diagnostics"):
            raise AssertionError("The candidate package does not contain the opt-in managed diagnostics export")
        with serve(Path(candidate["path"]) / "wwwroot") as url, sync_playwright() as playwright:
            arguments = launch_arguments(args.backend)
            browser = playwright.chromium.launch(headless=True, args=arguments)
            try:
                directory = Path("candidate") / args.backend / args.workload
                run = run_trial(browser, url, candidate, args.backend, args.workload, 1,
                                args, output / directory, diagnostics=True)
                run["artifact_directory"] = str(directory)
                run["browser_version"] = browser.version
                run["browser_launch_arguments"] = arguments
                # A singular diagnostic_run cannot satisfy the performance
                # merger's shard report or primary-runs coverage contract.
                report["diagnostic_run"] = run
                if not run["passed"]:
                    raise AssertionError(run["failure"])
                if args.backend == "webgpu":
                    verify_bridge(run["console"], "persistent")
                report["passed"] = True
            finally:
                browser.close()
    except Exception as error:
        report["failure"] = f"{type(error).__name__}: {error}"
    finally:
        report["duration_seconds"] = time.time() - report["started_unix"]
        (output / "report.json").write_text(json.dumps(report, indent=2) + "\n", encoding="utf-8")
        write_summary(report, output)
    print(f"candidate diagnostic {args.backend} {args.workload}: "
          + ("passed" if report["passed"] else report["failure"]), flush=True)
    return 0 if report["passed"] else 1


if __name__ == "__main__":
    raise SystemExit(main())
