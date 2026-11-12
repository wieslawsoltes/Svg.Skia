#!/usr/bin/env python3
"""Measure two document rasterizers in the same published AOT application."""
from __future__ import annotations

import argparse
import contextlib
import json
import platform
import time
from pathlib import Path
from typing import Any

from playwright.sync_api import sync_playwright

from bridge_experiment_contracts import (BRIDGES, STATES, WORKLOADS, compare_metrics,
                                         load_candidate, trial_plan, verify_runs)
from performance import VIEWPORT, launch_arguments, run_trial, serve
from quality import raw_comparison, verify_bridge


def save_report(report: dict[str, Any], output: Path) -> None:
    report["duration_seconds"] = time.time() - report["started_unix"]
    (output / "report.json").write_text(json.dumps(report, indent=2) + "\n", encoding="utf-8")
    lines = ["# Same-artifact document rasterizer experiment", "",
             f"Source: `{report['candidate_source_sha']}`; workload: {report['workload']}; "
             f"independent repeats per bridge: {report['repeats']}.", "",
             "Both modes use this exact AOT package, full device resolution and the same SVG painter. "
             "Only canvasBridge=persistent/webgl differs. All matched trials run on this runner, "
             "with alternating order and a fresh browser for every trial. "
             "SwiftShader implements the graphics APIs in software; these are not physical-GPU FPS results.", "",
             "The WebGL mode rasterizes the document with Ganesh and copies the browser canvas into WebGPU. "
             "Chromium 143 on Linux performs browser-side readback for that copy. "
             "The copy count does not measure actual bytes transferred or display presentation.", "",
             "| Metric | Persistent CPU median | WebGL median | WebGL / CPU |",
             "| --- | ---: | ---: | ---: |"]
    for row in report.get("metric_comparisons", []):
        ratio = row["webgl_over_persistent"]
        lines.append(f"| {row['metric']} | {row['medians']['persistent']:.3f} | "
                     f"{row['medians']['webgl']:.3f} | {ratio:.3f} |" if ratio is not None else
                     f"| {row['metric']} | {row['medians']['persistent']:.3f} | "
                     f"{row['medians']['webgl']:.3f} | n/a |")
    lines += ["", "## Separate diagnostic passes", "",
              "Instrumented callback costs are excluded from the primary timing ratios.", "",
              "| Bridge | Phase | Calls | CPU ms | Allocated bytes |",
              "| --- | --- | ---: | ---: | ---: |"]
    for run in report["bridge_diagnostics"]:
        for phase in run.get("managed_render_diagnostics", {}).get("after", {}).get("phases", []):
            lines.append(f"| {run['canvas_bridge']} | {phase['phase']} | {phase['count']} | "
                         f"{phase['totalMilliseconds']:.3f} | {phase['allocatedBytes']} |")
    lines += ["", "Result: " + ("passed all trial and native-pixel requirements" if report["passed"]
                                  else report.get("failure", "incomplete; no speedup conclusion")), ""]
    (output / "summary.md").write_text("\n".join(lines), encoding="utf-8")


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--package", type=Path, required=True)
    parser.add_argument("--output", type=Path, required=True)
    parser.add_argument("--workload", choices=WORKLOADS, required=True)
    parser.add_argument("--repeats", type=int, default=3)
    args = parser.parse_args()
    plan = trial_plan(args.repeats)
    package = load_candidate(args.package.resolve())
    output = args.output.resolve()
    if output == args.package.resolve() or args.package.resolve() in output.parents:
        parser.error("The experiment output must be outside the served package")
    output.mkdir(parents=True, exist_ok=True)
    args.steps, args.idle_ms = 120, 2000
    report: dict[str, Any] = {
        "schema": 1, "report_kind": "same_candidate_bridge_experiment", "primary_comparison": False,
        "passed": False, "started_unix": time.time(), "package": package,
        "candidate_source_sha": package["source_sha"], "workload": args.workload,
        "repeats": args.repeats, "steps": args.steps, "viewport": VIEWPORT, "device_scale_factor": 1,
        "runner": {"platform": platform.platform(), "machine": platform.machine()},
        "graphics_description": "SwiftShader software GPU implementations; not physical GPU throughput",
        "bridge_runs": [], "bridge_diagnostics": [], "quality_comparisons": [], "metric_comparisons": [],
    }
    save_report(report, output)
    try:
        with serve(Path(package["path"]) / "wwwroot") as url, sync_playwright() as playwright:
            arguments = launch_arguments("webgpu")

            def execute(trial: int, bridge: str, diagnostics: bool) -> dict[str, Any]:
                relative = Path("diagnostic" if diagnostics else "primary") / f"trial-{trial}" / bridge
                directory = output / relative
                directory.mkdir(parents=True, exist_ok=True)
                browser = None
                result: dict[str, Any] = {"source_sha": package["source_sha"], "backend": "webgpu",
                                          "workload": args.workload, "trial": trial, "canvas_bridge": bridge,
                                          "diagnostic_pass": diagnostics}
                try:
                    browser = playwright.chromium.launch(headless=True, args=arguments)
                    result = run_trial(browser, url, package, "webgpu", args.workload, trial, args,
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
                print(f"{args.workload} {bridge} trial {trial} diagnostic={diagnostics}: "
                      + ("passed" if result["passed"] else result["failure"]), flush=True)
                return result

            for trial, bridge in plan:
                report["bridge_runs"].append(execute(trial, bridge, False))
                save_report(report, output)
            for bridge in BRIDGES:
                report["bridge_diagnostics"].append(execute(1, bridge, True))
                save_report(report, output)

        verify_runs(report["bridge_runs"], args.repeats, package["source_sha"], args.workload)
        verify_runs(report["bridge_diagnostics"], args.repeats, package["source_sha"], args.workload,
                    diagnostics=True)
        if len({run["browser_version"] for run in report["bridge_runs"] + report["bridge_diagnostics"]}) != 1:
            raise ValueError("The two modes did not use the same browser version")
        for trial in range(1, args.repeats + 1):
            pair = {run["canvas_bridge"]: run for run in report["bridge_runs"] if run["trial"] == trial}
            for state in STATES:
                before = output / pair["persistent"]["artifact_directory"] / f"{state}.png"
                after = output / pair["webgl"]["artifact_directory"] / f"{state}.png"
                comparison = {"trial": trial, "state": state, "passed": False}
                try:
                    comparison.update(raw_comparison(before, after, VIEWPORT, 1))
                    comparison["passed"] = True
                except Exception as error:
                    comparison["failure"] = f"{type(error).__name__}: {error}"
                report["quality_comparisons"].append(comparison)
        if any(row["passed"] is not True for row in report["quality_comparisons"]):
            raise ValueError("The same-artifact bridge comparison changed native pixels above the original tolerance")
        report["metric_comparisons"] = compare_metrics(report["bridge_runs"])
        report["passed"] = True
    except Exception as error:
        report["failure"] = f"{type(error).__name__}: {error}"
    finally:
        save_report(report, output)
    return 0 if report["passed"] else 1


if __name__ == "__main__":
    raise SystemExit(main())
