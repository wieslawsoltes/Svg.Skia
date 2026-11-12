#!/usr/bin/env python3
"""Measure legacy and combined texture transport in one pinned AOT application."""
from __future__ import annotations

import argparse
import contextlib
import json
import platform
import re
import time
from pathlib import Path
from typing import Any

from playwright.sync_api import sync_playwright

from bridge_experiment_contracts import load_candidate
from metrics import metric_label
from performance import launch_arguments, run_trial, serve
from pinned_package import verify_package
from quality import raw_comparison, verify_bridge
from transport_experiment_contracts import (BRIDGES, REPEATS, REPORT_KIND, STATES, VIEWPORT,
                                            compare_metrics, trial_plan, verify_native_pairs,
                                            verify_report_identity, verify_runs)


def save_report(report: dict[str, Any], output: Path) -> None:
    report["duration_seconds"] = time.time() - report["started_unix"]
    (output / "report.json").write_text(json.dumps(report, indent=2) + "\n", encoding="utf-8")
    lines = ["# Same-snapshot texture transport comparison", "",
             f"Application snapshot: `{report['application_source_sha']}`. "
             f"Harness revision: `{report['harness_source_sha']}`.", "",
             "Both modes use the same verified Release AOT package, retained editor UI, full-resolution SVG painter "
             "and WebGPU compositor. Only `canvasBridge=legacy` versus `canvasBridge=composite` differs. "
             "The legacy control uses Uno's existing SKCanvasElement texture upload; the composite path restores "
             "cached document pixels and uploads combined interaction feedback into its persistent target.", "",
             "This isolates texture transport and composition within an explicit historical application snapshot. "
             "It is separate from the original baseline-versus-current-head performance matrix, "
             "and cannot establish current-head eligibility or recovery by multiplying ratios from other jobs.", "",
             "Each of six primary trials has a fresh browser, the unchanged full common pan warmup, "
             "121 real selection inputs and the original visible-overlay/restore checks. "
             "Trial order alternates. Two separate instrumented passes require zero document paints "
             "and positive interaction paints. All nine before/after/restored native-pixel pairs must pass "
             "the original luminance 12 / 0.001 gate.", "",
             "SwiftShader implements the graphics APIs in software. Elapsed task time can include blocking; "
             "ThreadTime separately measures active renderer-thread CPU and excludes GPU/worker CPU. "
             "Callbacks and submissions are not physical presentation or proven input-to-new-pixels latency.", "",
             "| Metric | Legacy median | Composite median | Composite / legacy |",
             "| --- | ---: | ---: | ---: |"]
    for row in report["metric_comparisons"]:
        ratio = row["composite_over_legacy"]
        formatted = f"{ratio:.4f}" if ratio is not None else "n/a"
        lines.append(f"| {metric_label(row['metric'])} | {row['medians']['legacy']:.3f} | "
                     f"{row['medians']['composite']:.3f} | {formatted} |")
    lines += ["", "## Separate diagnostics", "",
              "| Transport | Phase | Calls | Callback elapsed ms | Allocated bytes |",
              "| --- | --- | ---: | ---: | ---: |"]
    for run in report["diagnostic_runs"]:
        for phase in run.get("managed_render_diagnostics", {}).get("after", {}).get("phases", []):
            lines.append(f"| {run['canvas_bridge']} | {phase['phase']} | {phase['count']} | "
                         f"{phase['totalMilliseconds']:.3f} | {phase['allocatedBytes']} |")
    lines += ["", "Post-window screenshots, validation and restore durations remain in each raw trial report. "
              "They include automation/image analysis, are excluded from primary timing, and are not a matched "
              "queue-drain or physical-presentation measurement.", "",
              "Result: " + ("passed all source, trial and native-pixel contracts" if report["passed"]
                              else report.get("failure", "incomplete; no speedup conclusion")), ""]
    (output / "summary.md").write_text("\n".join(lines), encoding="utf-8")


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--package", type=Path, required=True)
    parser.add_argument("--output", type=Path, required=True)
    parser.add_argument("--harness-sha", required=True)
    args = parser.parse_args()
    if not re.fullmatch(r"[0-9a-f]{40}", args.harness_sha):
        parser.error("Record the exact checked-out harness commit")
    package_path, output = args.package.resolve(), args.output.resolve()
    if output == package_path or package_path in output.parents or output in package_path.parents:
        parser.error("The output and served package must be separate directory trees")
    # This consumes a fixed, independently identified historical AOT artifact.
    # Do not call current-head source-equivalence validation or override its result.
    package = verify_package(package_path, role="candidate")
    if package != load_candidate(package_path):
        raise ValueError("The verified snapshot differs from the actual AOT candidate manifest")
    receipt = json.loads((package_path / "pinned-package-receipt.json").read_text(encoding="utf-8"))
    output.mkdir(parents=True, exist_ok=True)
    args.steps, args.idle_ms = 120, 2000
    report: dict[str, Any] = {
        "schema": 1, "report_kind": REPORT_KIND, "primary_comparison": False, "passed": False,
        "started_unix": time.time(), "application_source_sha": package["source_sha"],
        "harness_source_sha": args.harness_sha, "package": package, "artifact_receipt": receipt,
        "workload": "selection", "bridges": list(BRIDGES), "repeats": REPEATS, "steps": args.steps,
        "viewport": VIEWPORT, "device_scale_factor": 1,
        "runner": {"platform": platform.platform(), "machine": platform.machine()},
        "graphics_description": "SwiftShader software graphics; not physical GPU throughput",
        "snapshot_scope": "Immutable historical application; no current-head source-equivalence claim",
        "primary_runs": [], "diagnostic_runs": [], "quality_comparisons": [], "metric_comparisons": [],
        "served_package_integrity_rechecked": False,
    }
    verify_report_identity(report)
    save_report(report, output)
    try:
        with serve(package_path / "wwwroot") as url, sync_playwright() as playwright:
            arguments = launch_arguments("webgpu")

            def execute(trial: int, bridge: str, diagnostics: bool) -> dict[str, Any]:
                relative = Path("diagnostic" if diagnostics else "primary") / f"trial-{trial}" / bridge
                directory = output / relative
                directory.mkdir(parents=True, exist_ok=True)
                browser = None
                result: dict[str, Any] = {
                    "passed": False, "role": "candidate", "source_sha": package["source_sha"],
                    "backend": "webgpu", "workload": "selection", "trial": trial,
                    "canvas_bridge": bridge, "diagnostic_pass": diagnostics,
                }
                try:
                    browser = playwright.chromium.launch(headless=True, args=arguments)
                    result = run_trial(browser, url, package, "webgpu", "selection", trial, args,
                                       directory, diagnostics=diagnostics, canvas_bridge=bridge)
                    result["browser_version"] = browser.version
                    result["browser_launch_arguments"] = arguments
                    if result["passed"]:
                        verify_bridge(result["console"], bridge)
                except Exception as error:
                    result.update({"passed": False, "failure": f"{type(error).__name__}: {error}"})
                finally:
                    if browser:
                        with contextlib.suppress(Exception):
                            browser.close()
                result["artifact_directory"] = str(relative)
                (directory / "report.json").write_text(json.dumps(result, indent=2) + "\n", encoding="utf-8")
                print(f"selection {bridge} trial {trial} diagnostic={diagnostics}: "
                      + ("passed" if result["passed"] else result["failure"]), flush=True)
                return result

            for trial, bridge in trial_plan():
                report["primary_runs"].append(execute(trial, bridge, False))
                save_report(report, output)
            for bridge in BRIDGES:
                report["diagnostic_runs"].append(execute(1, bridge, True))
                save_report(report, output)
        verify_runs(report["primary_runs"], package["source_sha"])
        verify_runs(report["diagnostic_runs"], package["source_sha"], diagnostics=True)
        all_runs = report["primary_runs"] + report["diagnostic_runs"]
        if (len({run["browser_version"] for run in all_runs}) != 1
                or len({tuple(run["browser_launch_arguments"]) for run in all_runs}) != 1):
            raise ValueError("Primary and diagnostic trials changed browser configuration")
        for trial in range(1, REPEATS + 1):
            modes = {run["canvas_bridge"]: run for run in report["primary_runs"] if run["trial"] == trial}
            for state in STATES:
                row: dict[str, Any] = {"trial": trial, "state": state, "passed": False}
                try:
                    row.update(raw_comparison(output / modes["legacy"]["artifact_directory"] / f"{state}.png",
                                              output / modes["composite"]["artifact_directory"] / f"{state}.png",
                                              VIEWPORT, 1))
                    row["passed"] = True
                except Exception as error:
                    row["failure"] = f"{type(error).__name__}: {error}"
                report["quality_comparisons"].append(row)
        verify_native_pairs(report["quality_comparisons"])
        if verify_package(package_path, role="candidate") != package:
            raise ValueError("The served immutable application changed during measurement")
        report["served_package_integrity_rechecked"] = True
        report["metric_comparisons"] = compare_metrics(report["primary_runs"])
        report["passed"] = True
    except Exception as error:
        report["failure"] = f"{type(error).__name__}: {error}"
    finally:
        save_report(report, output)
    return 0 if report["passed"] else 1


if __name__ == "__main__":
    raise SystemExit(main())
