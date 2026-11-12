#!/usr/bin/env python3
"""Directly compare the fixed baseline with an explicitly identified candidate canvas mode."""
from __future__ import annotations

import argparse
import contextlib
import json
import platform
import re
import subprocess
import sys
import time
from pathlib import Path
from typing import Any

from PIL import Image
from playwright.sync_api import sync_playwright

from legacy_baseline_contracts import (BRIDGE, REPORT_KINDS, VIEWPORT, WORKLOADS, compare_metrics,
                                       trial_plan, verify_report_identity, verify_runs)
from legacy_baseline_packages import BASELINE_SHA, RECEIPT, ROLES, fetch_descriptors, verify_package
from merge_shards import validate_proof, validate_quality
from metrics import metric_label, validate_packages
from performance import cross_build_quality, launch_arguments, run_trial, serve

sys.path.insert(0, str(Path(__file__).resolve().parents[1] / "UnoDraw.BrowserRenderQuality"))
from verify_source import verify_source  # noqa: E402


def save_report(report: dict[str, Any], output: Path) -> None:
    report["duration_seconds"] = time.time() - report["started_unix"]
    (output / "report.json").write_text(json.dumps(report, indent=2) + "\n", encoding="utf-8")
    bridge = report["candidate_bridge"]
    if bridge not in REPORT_KINDS:
        raise ValueError("The summary requires a supported, explicitly identified canvas mode")
    diagnostic_description = (
        "One separate candidate diagnostic pass requires positive Document and Interaction paints for every workload. "
        "The original single-canvas topology repaints both layers; it has no retained document-pixel cache claim. "
        "Its initialization message proves completion of the original painter, not host upload or presentation."
        if bridge == "single-canvas" else
        "One separate candidate diagnostic pass validates zero Document paints and positive Interaction paints "
        "for hover/selection. " + (
        "Original upload keeps the editor's separate interaction surface; it has no combined pixel-cache reuse claim."
        if bridge == "legacy" else
        "The combined canvas must also report actual document-pixel cache reuse for hover/selection.") + " "
        "Pan, zoom and edit instead require actual document painting; "
        "their camera/source changes do not guarantee an overlay-only cache hit.")
    lines = [f"# Direct baseline versus opt-in {bridge}", "",
             f"Baseline: `{report['baseline_source_sha']}`. Candidate: `{report['candidate_source_sha']}`. "
             f"Harness: `{report['harness_source_sha']}`. Workload: `{report['workload']}`.", "",
             "This distinct experiment compares the fixed baseline's original default path with the candidate's explicit "
             f"`canvasBridge={bridge}` path. The current candidate default is unchanged and is not measured here. "
             "These results cannot enter the current-default aggregate or independently promote an opt-in renderer.", "",
             "Both immutable packages come from the identified performance workflow run. Their archive digests, full served "
             "file inventories, source/build/font manifests and candidate production-source equivalence are verified. "
             "Every trial uses a fresh browser on this runner and the original full common pan warmup outside timing. "
             "Three paired trials alternate order. Selection requires 121 real trusted inputs; other workloads require 120. "
             "All input tails must be observed, and all nine before/after/restored native pairs retain the original "
             "document/ruler 0.001 and chrome-font 0.02 thresholds above luminance 12 at 1440×960 DPR 1.", "",
             diagnostic_description + " Every mode must prove exclusive selection and its own completed-painter or "
             "successful-upload requirements. The baseline predates managed diagnostics. "
             "Diagnostic timings are excluded from the primary ratios.", "",
             "SwiftShader implements the graphics APIs in software. Elapsed duration includes waits. Renderer ThreadTime "
             "excludes GPU/worker CPU. Callback completion and queue submission do not prove physical display presentation "
             "or input-to-new-pixels latency. Post-window capture and restore durations remain separately recorded in each trial.", "",
             "| Metric | Baseline median | Opt-in candidate median | Candidate / baseline |",
             "| --- | ---: | ---: | ---: |"]
    for row in report["metric_comparisons"]:
        ratio = row["opt_in_candidate_over_baseline"]
        rendered = f"{ratio:.4f}" if ratio is not None else "n/a"
        lines.append(f"| {metric_label(row['metric'])} | {row['medians']['baseline']:.3f} | "
                     f"{row['medians']['candidate']:.3f} | {rendered} |")
    lines += ["", "Result: " + ("passed source, workload and original native-quality requirements" if report["passed"]
                                  else report.get("failure", "incomplete; no recovery claim")), ""]
    (output / "summary.md").write_text("\n".join(lines), encoding="utf-8")


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--packages", type=Path, required=True)
    parser.add_argument("--package-run-id", type=int, required=True)
    parser.add_argument("--harness-sha", required=True)
    parser.add_argument("--workload", choices=WORKLOADS, required=True)
    parser.add_argument("--candidate-bridge", choices=tuple(REPORT_KINDS), default=BRIDGE)
    parser.add_argument("--output", type=Path, required=True)
    args = parser.parse_args()
    repository = Path(__file__).resolve().parents[2]
    if (not re.fullmatch(r"[0-9a-f]{40}", args.harness_sha)
            or subprocess.check_output(["git", "rev-parse", "HEAD"], cwd=repository, text=True).strip() != args.harness_sha):
        parser.error("The supplied harness SHA must identify the actual checked-out commit")
    packages_root, output = args.packages.resolve(), args.output.resolve()
    if output == packages_root or packages_root in output.parents or output in packages_root.parents:
        parser.error("The served packages and output must be separate directory trees")
    # Fetch fresh GitHub descriptors independently of the on-disk receipts. Hold
    # these immutable digests for both pre- and post-measurement verification.
    descriptors = fetch_descriptors(args.package_run_id)
    packages = {role: verify_package(packages_root / role, role, descriptors) for role in ROLES}
    validate_packages(list(packages.values()))
    output.mkdir(parents=True, exist_ok=True)
    source_proof = verify_source(repository, packages_root / "candidate", output / "source-audit", str(args.package_run_id))
    validate_proof(source_proof, packages["candidate"]["source_sha"])
    if source_proof["harness_head_sha"] != args.harness_sha:
        raise ValueError("The source proof does not identify this exact harness revision")
    args.steps, args.idle_ms = 120, 2000
    report: dict[str, Any] = {
        "schema": 1, "report_kind": REPORT_KINDS[args.candidate_bridge], "primary_comparison": False,
        "candidate_default": False, "candidate_bridge": args.candidate_bridge, "baseline_source_sha": BASELINE_SHA,
        "candidate_source_sha": packages["candidate"]["source_sha"], "harness_source_sha": args.harness_sha,
        "package_run_id": args.package_run_id, "artifact_descriptors": descriptors, "packages": packages,
        "package_receipts": {role: json.loads((packages_root / role / RECEIPT).read_text()) for role in ROLES},
        "candidate_source_equivalence": source_proof, "workload": args.workload,
        "repeats": 3, "steps": 120, "viewport": VIEWPORT, "device_scale_factor": 1,
        "expected_primary_runs": 6, "expected_candidate_diagnostics": 1, "expected_native_pairs": 9,
        "runner": {"platform": platform.platform(), "machine": platform.machine()},
        "started_unix": time.time(), "passed": False, "served_package_integrity_rechecked": False,
        "primary_runs": [], "candidate_diagnostics": [], "quality_comparisons": [], "metric_comparisons": [],
    }
    verify_report_identity(report)
    save_report(report, output)
    try:
        with contextlib.ExitStack() as stack, sync_playwright() as playwright:
            urls = {role: stack.enter_context(serve(Path(package["path"]) / "wwwroot")) for role, package in packages.items()}
            arguments = launch_arguments("webgpu")

            def execute(trial: int, role: str, diagnostics: bool) -> dict[str, Any]:
                relative = Path("diagnostic" if diagnostics else "primary") / f"trial-{trial}" / role
                directory = output / relative
                directory.mkdir(parents=True, exist_ok=True)
                browser = None
                result: dict[str, Any] = {"role": role, "trial": trial, "workload": args.workload,
                                          "source_sha": packages[role]["source_sha"], "passed": False,
                                          "diagnostic_pass": diagnostics}
                try:
                    browser = playwright.chromium.launch(headless=True, args=arguments)
                    result = run_trial(browser, urls[role], packages[role], "webgpu", args.workload, trial, args,
                                       directory, diagnostics=diagnostics,
                                       canvas_bridge=args.candidate_bridge if role == "candidate" else None)
                    result["browser_version"] = browser.version
                    result["browser_launch_arguments"] = arguments
                except Exception as error:
                    result.update({"passed": False, "failure": f"{type(error).__name__}: {error}"})
                finally:
                    if browser:
                        with contextlib.suppress(Exception):
                            browser.close()
                result["artifact_directory"] = str(relative)
                (directory / "report.json").write_text(json.dumps(result, indent=2) + "\n", encoding="utf-8")
                print(f"{args.workload} {role} trial {trial} diagnostic={diagnostics}: "
                      + ("passed" if result["passed"] else result["failure"]), flush=True)
                return result

            for trial, role in trial_plan():
                report["primary_runs"].append(execute(trial, role, False))
                save_report(report, output)
            report["candidate_diagnostics"].append(execute(1, "candidate", True))
            save_report(report, output)

        verify_runs(report["primary_runs"], report["candidate_source_sha"], args.workload,
                    candidate_bridge=args.candidate_bridge)
        verify_runs(report["candidate_diagnostics"], report["candidate_source_sha"], args.workload, diagnostics=True,
                    candidate_bridge=args.candidate_bridge)
        all_runs = report["primary_runs"] + report["candidate_diagnostics"]
        if (len({run["browser_version"] for run in all_runs}) != 1
                or len({tuple(run["browser_launch_arguments"]) for run in all_runs}) != 1):
            raise ValueError("The primary and diagnostic trials changed browser configuration")
        for run in report["primary_runs"]:
            for state in ("before", "after", "restored"):
                with Image.open(output / run["artifact_directory"] / (state + ".png")) as capture:
                    if capture.size != (1440, 960):
                        raise ValueError("The direct native comparison must not resize a screenshot")
        report["quality_comparisons"] = cross_build_quality(report["primary_runs"], output)
        validate_quality(report["quality_comparisons"], "webgpu", args.workload, 3)
        for role in ROLES:
            if verify_package(packages_root / role, role, descriptors) != packages[role]:
                raise ValueError("The served package identity changed during measurement")
        report["served_package_integrity_rechecked"] = True
        report["metric_comparisons"] = compare_metrics(report["primary_runs"])
        verify_report_identity(report)
        report["passed"] = True
    except Exception as error:
        report["failure"] = f"{type(error).__name__}: {error}"
    finally:
        save_report(report, output)
    return 0 if report["passed"] else 1


if __name__ == "__main__":
    raise SystemExit(main())
