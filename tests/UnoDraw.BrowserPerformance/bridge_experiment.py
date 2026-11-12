#!/usr/bin/env python3
"""Compare an opt-in bridge with its declared control in the same published AOT application."""
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

from bridge_experiment_contracts import (LIVE_CANDIDATE_BRIDGES, LIVE_WORKLOADS, STATES, bridge_pair,
                                         compare_metrics, load_candidate, report_kind, trial_plan,
                                         verify_experiment_workload, verify_report_identity, verify_runs)
from performance import VIEWPORT, launch_arguments, run_trial, serve
from metrics import metric_label
from quality import raw_comparison, verify_bridge
from transport_experiment_contracts import verify_native_pairs


def save_report(report: dict[str, Any], output: Path) -> None:
    report["duration_seconds"] = time.time() - report["started_unix"]
    (output / "report.json").write_text(json.dumps(report, indent=2) + "\n", encoding="utf-8")
    candidate_bridge = report["candidate_bridge"]
    control = bridge_pair(candidate_bridge)[0]
    lines = ["# Same-artifact canvas bridge experiment", "",
             f"Source: `{report['candidate_source_sha']}`; workload: {report['workload']}; "
             f"candidate bridge: `{candidate_bridge}`; independent repeats per bridge: {report['repeats']}.", "",
             "Both modes use this exact AOT package, full device resolution and the same SVG painter. "
             f"The selected canvasBridge={control}/{candidate_bridge} and any explicitly requested Graphite coverage "
             "are the only rendering options that differ. All matched trials run on this runner, "
             "with alternating order and a fresh browser for every trial. "
             "SwiftShader implements the graphics APIs in software; these are not physical-GPU FPS results.", "",
             "Task wall duration includes blocking inside browser calls. ThreadTime separately measures "
             "active CPU on the renderer thread; it excludes GPU/worker CPU. Missing thread counters are "
             "null and do not produce a ratio from an incomplete pair of trial sets.", ""]
    if candidate_bridge == "webgl":
        # Retain summaries of archived experiments; the live CLI rejects this mode.
        lines += ["The WebGL mode rasterizes the document with Ganesh and copies the browser canvas into WebGPU. "
                  "Chromium 143 on Linux performs browser-side readback for that copy. "
                  "The copy count does not measure actual bytes transferred or display presentation.", ""]
    elif candidate_bridge in {"composite", "legacy-composite"}:
        if candidate_bridge == "legacy-composite":
            lines += [f"Harness revision: `{report.get('harness_source_sha', 'unrecorded')}`. "
                      "This distinct same-candidate legacy composition report compares Uno's original uploader "
                      "in both modes. The candidate combines cached document pixels and interaction feedback "
                      "through the original host's raster surface; initialization requires a completed host copy, "
                      "and cache reuse requires an actual completed copy of reused pixels. "
                      "No persistent or alternate GPU document transport may initialize.", "",
                      "The report isolates the opt-in composition change within this exact application package. "
                      "It does not establish recovery against the older baseline or current-default eligibility. "
                      "That requires a direct, independently complete baseline-versus-candidate comparison.", ""]
        else:
            lines += ["The composite mode restores exact full-resolution cached BGRA document pixels before "
                      "painting interaction feedback and uploading one combined canvas texture. It tests the cost "
                      "of the additional retained interaction texture.", ""]
        lines += ["Both modes must retain the document during this workload; actual cache-reuse console evidence "
                  "and separate managed counters are required. Every primary and diagnostic trial keeps the full "
                  "common pan warmup outside its measurement window.", ""]
        if report["workload"] == "selection":
            lines += ["The existing selection driver turns 120 configured steps into 121 real alternating selection "
                      "commands, ending with a visible selection. Each trial requires all 121 operations and trusted "
                      "inputs, zero unobserved tails, the existing visible-overlay proof and exact original restore gates. "
                      "Both separate diagnostic passes require zero document paints and positive interaction paints.", ""]
    elif candidate_bridge == "graphite":
        coverage = report.get("graphite_coverage")
        lines += [f"Graphite coverage: `{coverage}` (requires verified actual native context evidence)." if coverage is not None else
                  "Graphite coverage: unspecified; the artifact's default applies without an explicit coverage claim.", ""]
        lines += ["Graphite records and submits the document directly into the Uno texture on the shared WebGPU queue. "
                  "Each measured trial requires a positive delta of successful document submissions and no external-image copies. "
                  "The document counter is separate from visible-canvas submissions and does not prove physical presentation. "
                  "Aggregate writeTexture calls can still belong to auxiliary CPU surfaces.", ""]
        lines += ["| Trial | Diagnostic pass | Graphite document submits | Visible-canvas submits | External-image copies |",
                  "| ---: | --- | ---: | ---: | ---: |"]
        for run in report.get("bridge_runs", []) + report.get("bridge_diagnostics", []):
            if run.get("canvas_bridge") != "graphite":
                continue
            observed = run.get("metrics", {})
            values = [observed.get("graphite_document_submissions"), observed.get("webgpu_canvas_submissions"),
                      observed["graphics_api_calls"].get("webgpu.copyExternalImageToTexture", 0)
                      if "graphics_api_calls" in observed else None]
            formatted = [str(value) if value is not None else "n/a" for value in values]
            lines.append(f"| {run['trial']} | {run.get('diagnostic_pass', False)} | {' | '.join(formatted)} |")
        lines.append("")
    lines += [f"| Metric | {control} median | {candidate_bridge} median | Candidate / {control} |",
             "| --- | ---: | ---: | ---: |"]
    for row in report.get("metric_comparisons", []):
        ratio = row["candidate_over_" + control]
        lines.append(f"| {metric_label(row['metric'])} | {row['medians'][control]:.3f} | "
                     f"{row['medians'][candidate_bridge]:.3f} | {ratio:.3f} |" if ratio is not None else
                     f"| {metric_label(row['metric'])} | {row['medians'][control]:.3f} | "
                     f"{row['medians'][candidate_bridge]:.3f} | n/a |")
    lines += ["", "## Separate diagnostic passes", "",
              "Instrumented callback elapsed times are excluded from the primary timing ratios and "
              "are not active-thread CPU measurements.", "",
              "| Bridge | Phase | Calls | Callback elapsed ms | Allocated bytes |",
              "| --- | --- | ---: | ---: | ---: |"]
    for run in report["bridge_diagnostics"]:
        for phase in run.get("managed_render_diagnostics", {}).get("after", {}).get("phases", []):
            lines.append(f"| {run['canvas_bridge']} | {phase['phase']} | {phase['count']} | "
                         f"{phase['totalMilliseconds']:.3f} | {phase['allocatedBytes']} |")
    lines += ["", "## Post-window diagnostics", "",
              "Final capture includes screenshot/readiness and native-image validation. Restore includes real restore "
              "commands and their screenshot validation. Both include automation and image analysis; neither is a CPU "
              "or physical-presentation measurement. The optional Graphite queue drain occurs first and is reported "
              "separately; other modes have no matched drain, so no drain or post-window ratio is calculated.", "",
              "| Bridge | Trial | Diagnostic pass | Final capture/validation ms | Restore/validation ms | Graphite queue drain ms |",
              "| --- | ---: | --- | ---: | ---: | ---: |"]
    for run in report.get("bridge_runs", []) + report.get("bridge_diagnostics", []):
        quality = run.get("quality", {})
        drain = run.get("graphite_queue_drain", {})
        values = [quality.get("final_capture_validation_elapsed_ms"), quality.get("restore_validation_elapsed_ms"),
                  drain.get("result", {}).get("duration_ms") if drain.get("available") else None]
        formatted = [f"{value:.3f}" if value is not None else "n/a" for value in values]
        lines.append(f"| {run['canvas_bridge']} | {run['trial']} | {run.get('diagnostic_pass', False)} "
                     f"| {' | '.join(formatted)} |")
    lines += ["", "Result: " + ("passed all trial and native-pixel requirements" if report["passed"]
                                  else report.get("failure", "incomplete; no speedup conclusion")), ""]
    (output / "summary.md").write_text("\n".join(lines), encoding="utf-8")


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--package", type=Path, required=True)
    parser.add_argument("--output", type=Path, required=True)
    parser.add_argument("--workload", choices=LIVE_WORKLOADS, required=True)
    parser.add_argument("--candidate-bridge", choices=LIVE_CANDIDATE_BRIDGES, default="composite",
                        help="Compare persistent or original-uploader composition during hover/selection")
    parser.add_argument("--harness-sha", help="Exact checked-out harness revision; required for legacy composition")
    parser.add_argument("--repeats", type=int, default=3)
    args = parser.parse_args()
    try:
        verify_experiment_workload(args.workload, args.candidate_bridge)
        plan = trial_plan(args.repeats, args.candidate_bridge)
        if (args.harness_sha is not None or args.candidate_bridge == "legacy-composite") and not re.fullmatch(
                r"[0-9a-f]{40}", args.harness_sha or ""):
            raise ValueError("Record the exact checked-out harness commit with --harness-sha")
    except ValueError as error:
        parser.error(str(error))
    bridges = bridge_pair(args.candidate_bridge)
    package = load_candidate(args.package.resolve())
    output = args.output.resolve()
    if output == args.package.resolve() or args.package.resolve() in output.parents or output in args.package.resolve().parents:
        parser.error("The experiment output and served package must be separate directory trees")
    output.mkdir(parents=True, exist_ok=True)
    args.steps, args.idle_ms = 120, 2000
    report: dict[str, Any] = {
        "schema": 1, "report_kind": report_kind(args.candidate_bridge), "primary_comparison": False,
        "passed": False, "started_unix": time.time(), "package": package,
        "candidate_source_sha": package["source_sha"], "workload": args.workload,
        "harness_source_sha": args.harness_sha,
        "candidate_bridge": args.candidate_bridge, "bridges": list(bridges),
        "graphite_coverage": None,
        "repeats": args.repeats, "steps": args.steps, "viewport": VIEWPORT, "device_scale_factor": 1,
        "runner": {"platform": platform.platform(), "machine": platform.machine()},
        "graphics_description": "SwiftShader software GPU implementations; not physical GPU throughput",
        "bridge_runs": [], "bridge_diagnostics": [], "quality_comparisons": [], "metric_comparisons": [],
    }
    verify_report_identity(report)
    save_report(report, output)
    try:
        with serve(Path(package["path"]) / "wwwroot") as url, sync_playwright() as playwright:
            arguments = launch_arguments("webgpu")

            def execute(trial: int, bridge: str, diagnostics: bool) -> dict[str, Any]:
                coverage = None
                relative = Path("diagnostic" if diagnostics else "primary") / f"trial-{trial}" / bridge
                directory = output / relative
                directory.mkdir(parents=True, exist_ok=True)
                browser = None
                result: dict[str, Any] = {"source_sha": package["source_sha"], "backend": "webgpu",
                                          "workload": args.workload, "trial": trial, "canvas_bridge": bridge,
                                          "diagnostic_pass": diagnostics, "graphite_coverage": coverage}
                try:
                    browser = playwright.chromium.launch(headless=True, args=arguments)
                    result = run_trial(browser, url, package, "webgpu", args.workload, trial, args,
                                       directory, diagnostics=diagnostics, canvas_bridge=bridge,
                                       graphite_coverage=coverage)
                    result["browser_version"] = browser.version
                    result["browser_launch_arguments"] = arguments
                    if result["passed"]:
                        verify_bridge(result["console"], bridge, graphite_coverage=coverage)
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
            for bridge in bridges:
                report["bridge_diagnostics"].append(execute(1, bridge, True))
                save_report(report, output)

        verify_runs(report["bridge_runs"], args.repeats, package["source_sha"], args.workload,
                    candidate_bridge=args.candidate_bridge)
        verify_runs(report["bridge_diagnostics"], args.repeats, package["source_sha"], args.workload,
                    diagnostics=True, candidate_bridge=args.candidate_bridge)
        all_runs = report["bridge_runs"] + report["bridge_diagnostics"]
        if (len({run["browser_version"] for run in all_runs}) != 1
                or len({tuple(run["browser_launch_arguments"]) for run in all_runs}) != 1):
            raise ValueError("The two modes did not use the same browser configuration")
        for trial in range(1, args.repeats + 1):
            pair = {run["canvas_bridge"]: run for run in report["bridge_runs"] if run["trial"] == trial}
            for state in STATES:
                before = output / pair[bridges[0]]["artifact_directory"] / f"{state}.png"
                after = output / pair[args.candidate_bridge]["artifact_directory"] / f"{state}.png"
                comparison = {"trial": trial, "state": state, "passed": False}
                try:
                    comparison.update(raw_comparison(before, after, VIEWPORT, 1))
                    comparison["passed"] = True
                except Exception as error:
                    comparison["failure"] = f"{type(error).__name__}: {error}"
                report["quality_comparisons"].append(comparison)
        if any(row["passed"] is not True for row in report["quality_comparisons"]):
            raise ValueError("The same-artifact bridge comparison changed native pixels above the original tolerance")
        if args.candidate_bridge == "legacy-composite":
            verify_native_pairs(report["quality_comparisons"])
            if package != load_candidate(args.package.resolve()):
                raise ValueError("The same-artifact package identity changed during measurement")
        verify_report_identity(report)
        report["metric_comparisons"] = compare_metrics(report["bridge_runs"], args.candidate_bridge)
        report["passed"] = True
    except Exception as error:
        report["failure"] = f"{type(error).__name__}: {error}"
    finally:
        save_report(report, output)
    return 0 if report["passed"] else 1


if __name__ == "__main__":
    raise SystemExit(main())
