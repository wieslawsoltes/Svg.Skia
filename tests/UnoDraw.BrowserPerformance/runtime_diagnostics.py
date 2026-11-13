#!/usr/bin/env python3
"""Compare renderer command evidence from pinned packages with explicit log configuration."""
from __future__ import annotations

import argparse
import contextlib
import functools
import http.server
import json
import platform
import re
import threading
import time
from pathlib import Path
from typing import Any, Iterator
from urllib.parse import urlsplit

from playwright.sync_api import sync_playwright

from pinned_package import RECEIPT, inventory, verify_package
from runtime_diagnostic_contracts import MODES, config_response, summarize_logs, verify_observation
from performance import (VIEWPORT, common_pan_warmup, cpu_metrics, cross_build_quality, launch_arguments,
                         measure_workload, next_frame, park_pointer, renderer_evidence, settled_screenshot,
                         summarize, verify_workload, wait_for_artwork_transition, warm_workload)
from quality import bridge_query, raw_comparison, verify_bridge
from smoke import READY, SiteHandler


@contextlib.contextmanager
def serve_configured(site: Path, evidence: Path) -> Iterator[tuple[str, dict[str, Any]]]:
    original = (site / "uno-config.js").read_bytes()
    served, proof = config_response(original)
    evidence.mkdir(parents=True, exist_ok=True)
    (evidence / "uno-config.original.js").write_bytes(original)
    (evidence / "uno-config.served.js").write_bytes(served)
    proof["request_count"] = 0

    class ConfiguredHandler(SiteHandler):
        def do_GET(self) -> None:
            if urlsplit(self.path).path != "/uno-config.js":
                super().do_GET()
                return
            proof["request_count"] += 1
            self.send_response(200)
            self.send_header("Content-Type", "text/javascript; charset=utf-8")
            self.send_header("Content-Length", str(len(served)))
            self.end_headers()
            self.wfile.write(served)

        def do_HEAD(self) -> None:
            if urlsplit(self.path).path != "/uno-config.js":
                super().do_HEAD()
                return
            self.send_response(200)
            self.send_header("Content-Type", "text/javascript; charset=utf-8")
            self.send_header("Content-Length", str(len(served)))
            self.end_headers()

    server = http.server.ThreadingHTTPServer(("127.0.0.1", 0), functools.partial(ConfiguredHandler, directory=str(site)))
    server.daemon_threads = True
    thread = threading.Thread(target=server.serve_forever, name="unodraw-runtime-diagnostic-http", daemon=True)
    thread.start()
    try:
        yield f"http://127.0.0.1:{server.server_port}/", proof
    finally:
        server.shutdown()
        server.server_close()
        thread.join(timeout=5)
        (evidence / "configuration-proof.json").write_text(json.dumps(proof, indent=2) + "\n", encoding="utf-8")


def run_case(playwright: Any, package: dict[str, Any], mode: str, bridge: str | None,
             output: Path, args: Any) -> dict[str, Any]:
    output.mkdir(parents=True, exist_ok=True)
    site = Path(package["path"]) / "wwwroot"
    before_inventory = inventory(site)
    result: dict[str, Any] = {"role": package["role"], "source_sha": package["source_sha"],
                              "mode": mode, "backend": "webgpu", "workload": "selection", "trial": 1,
                              "canvas_bridge": bridge, "diagnostic_pass": True, "primary_comparison": False,
                              "passed": False, "console": [], "page_errors": [], "resource_errors": []}
    arguments = launch_arguments("webgpu")
    browser = None
    page = None
    try:
        with serve_configured(site, output / "configuration") as (url, proof):
            result["configuration_override"] = proof
            browser = playwright.chromium.launch(headless=True, args=arguments)
            result["browser_version"] = browser.version
            result["browser_launch_arguments"] = arguments
            context = browser.new_context(viewport=VIEWPORT, device_scale_factor=1, locale="en-US",
                                          timezone_id="UTC", color_scheme="dark", reduced_motion="no-preference")
            context.add_init_script(path=str(Path(__file__).with_name("instrumentation.js")))
            page = context.new_page()
            page.on("console", lambda message: result["console"].append(
                {"type": message.type, "text": message.text, "location": message.location}))
            page.on("pageerror", lambda error: result["page_errors"].append(str(error)))
            page.on("response", lambda response: result["resource_errors"].append(
                {"url": response.url, "status": response.status})
                if response.status >= 400 and not response.url.endswith("/favicon.ico") else None)
            page.on("requestfailed", lambda request: result["resource_errors"].append(
                {"url": request.url, "failure": request.failure}) if request.failure != "net::ERR_ABORTED" else None)
            page.goto(url + ("?" + bridge_query(bridge) if bridge else ""), wait_until="domcontentloaded", timeout=120_000)
            page.wait_for_function(READY, timeout=120_000)
            result["environment"] = page.evaluate("""() => ({
                viewport: {width: innerWidth, height: innerHeight}, device_pixel_ratio: devicePixelRatio,
                cross_origin_isolated: crossOriginIsolated, user_agent: navigator.userAgent,
                hardware_concurrency: navigator.hardwareConcurrency,
                browser_fonts: [...document.fonts].map(font => ({family: font.family, weight: font.weight,
                    style: font.style, status: font.status}))
            })""")
            if (result["environment"]["viewport"] != VIEWPORT or result["environment"]["device_pixel_ratio"] != 1
                    or result["environment"]["cross_origin_isolated"]):
                raise AssertionError("The diagnostic viewport/DPR/hosting contract changed")
            settled_screenshot(page, output / "startup.png")
            result["renderer"] = renderer_evidence(page, result["console"], package, "webgpu")
            park_pointer(page)
            settled_screenshot(page, output / "initial.png")
            result["common_warmup"] = common_pan_warmup(page)
            warm_workload(page, "selection")
            before = output / "before.png"
            wait_for_artwork_transition(page, output / "initial.png", before, restore=True, require_settled_rulers=True)
            for _ in range(2):
                next_frame(page)
            session = context.new_cdp_session(page)
            session.send("Performance.enable", {"timeDomain": "timeTicks"})
            result["cdp_time_domain"] = "timeTicks"
            console_start = len(result["console"])
            before_cpu = cpu_metrics(session)
            page.evaluate("args => globalThis.__unoDrawBenchmark.start(...args)", ["selection", ["keydown"]])
            operations = measure_workload(page, "selection", args.steps, args.idle_ms)
            for _ in range(2):
                next_frame(page)
            raw = page.evaluate("() => globalThis.__unoDrawBenchmark.stop()")
            after_cpu = cpu_metrics(session)
            console_end = len(result["console"])
            result["console_measurement_range"] = [console_start, console_end]
            result["runtime_phase_logs"] = [entry["text"] for entry in result["console"][console_start:console_end]
                                             if entry["text"].startswith(("[frame-phases]", "[webgpu-frame]", "[webgpu-stats]"))]
            result["runtime_log_summary"] = summarize_logs(result["runtime_phase_logs"])
            result["raw_browser_observations"] = raw
            result["cdp_cpu_before"], result["cdp_cpu_after"] = before_cpu, after_cpu
            result["metrics"] = summarize(raw, before_cpu, after_cpu, operations)
            result["timing_scope"] = "Instrumented diagnostics only; no throughput ratio or primary performance qualification"
            result["quality"] = verify_workload(page, "selection", operations, before, output)
            # A sparse native log cannot identify a selected versus cleared UI.
            # Observe both canonical states outside the 121-input window, proving
            # the pixels against that mode's existing after/before captures.
            result["canonical_states"] = []
            for name, key, reference in (("selected", "Control+A", "after"),
                                         ("cleared", "Control+Shift+A", "before")):
                state_start = len(result["console"])
                page.keyboard.press(key)
                for _ in range(2):
                    next_frame(page)
                capture = output / f"canonical-{name}.png"
                settled_screenshot(page, capture)
                comparison = raw_comparison(output / f"{reference}.png", capture, VIEWPORT, 1)
                state_logs = [entry["text"] for entry in result["console"][state_start:]
                              if entry["text"].startswith(("[webgpu-frame]", "[webgpu-stats]"))]
                frames = [line for line in state_logs if line.startswith("[webgpu-frame]")]
                stats = [line for line in state_logs if line.startswith("[webgpu-stats]")]
                if not frames or not stats:
                    raise AssertionError(f"No actual native frame proves the canonical {name} state")
                result["canonical_states"].append({"name": name, "reference": reference + ".png",
                                                    "capture": capture.name, "native_comparison": comparison,
                                                    "runtime_phase_logs": state_logs,
                                                    "last_frame": frames[-1], "last_stats": stats[-1],
                                                    "included_in_measured_window": False})
            if bridge is not None:
                verify_bridge(result["console"], bridge)
            allowed = set(result["renderer"].get("expected_console_error_indices", []))
            errors = [entry for index, entry in enumerate(result["console"])
                      if entry["type"] == "error" and index not in allowed
                      and not entry["location"].get("url", "").endswith("/favicon.ico")]
            if errors or result["page_errors"] or result["resource_errors"]:
                raise AssertionError("Runtime/resource errors occurred during the diagnostic")
            context.close()
            proof["on_disk_inventory_unchanged"] = inventory(site) == before_inventory
        verify_package(Path(package["path"]), package["role"])
        verify_observation(result)
        result["passed"] = True
    except Exception as error:
        result["failure"] = f"{type(error).__name__}: {error}"
        if page is not None:
            with contextlib.suppress(Exception):
                page.screenshot(path=str(output / "failure.png"), timeout=5000)
    finally:
        if browser is not None:
            with contextlib.suppress(Exception):
                browser.close()
        (output / "report.json").write_text(json.dumps(result, indent=2) + "\n", encoding="utf-8")
    return result


def save(report: dict[str, Any], output: Path) -> None:
    (output / "report.json").write_text(json.dumps(report, indent=2) + "\n", encoding="utf-8")
    lines = ["# Pinned UnoDraw runtime diagnostics", "", report["scope"], "",
             f"Harness revision: `{report['harness_source_sha']}`.", "",
             "Only the served uno-config.js response is appended with logging configuration. "
             "Original and served hashes are recorded. This is a modified served configuration, with unchanged "
             "application binaries, runtime files, fonts and other assets.", "",
             "| Mode | Completed | Ops median | Emitted median | Scissor changes median |", "| --- | --- | ---: | ---: | ---: |"]
    for run in report["diagnostic_runs"]:
        fields = run.get("runtime_log_summary", {}).get("fields", {})
        values = [fields.get(key, {}).get("median", "—") for key in ("ops", "emitted", "scissorChanges")]
        lines.append(f"| {run['mode']} | {run['passed']} | " + " | ".join(map(str, values)) + " |")
        if not run["passed"]:
            lines += ["", f"{run['mode']}: {run.get('failure', 'incomplete')}"]
    lines += ["", "Native stats use one completed frame per logging interval. Frame-phase logs retain their "
              "60-recording average. Gap measures time between recordings; it does not identify GPU execution time.", "",
              "Result: " + ("all diagnostic and unchanged pixel gates passed" if report["passed"]
                             else report.get("failure", "still running; no qualification"))]
    (output / "summary.md").write_text("\n".join(lines) + "\n", encoding="utf-8")


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--packages", type=Path, required=True)
    parser.add_argument("--output", type=Path, required=True)
    parser.add_argument("--harness-sha", required=True)
    args = parser.parse_args()
    if not re.fullmatch(r"[0-9a-f]{40}", args.harness_sha):
        parser.error("Record the exact checked-out diagnostic harness commit")
    args.steps, args.idle_ms = 120, 2000
    packages = {role: verify_package(args.packages / role, role) for role in ("baseline", "candidate")}
    output = args.output.resolve()
    if any(output == Path(package["path"]) or Path(package["path"]) in output.parents
           or output in Path(package["path"]).parents for package in packages.values()):
        parser.error("Diagnostic output must be outside both served packages")
    output.mkdir(parents=True, exist_ok=True)
    report: dict[str, Any] = {"schema": 1, "report_kind": "runtime_configuration_diagnostics",
                              "primary_comparison": False, "current_source_qualification": False, "passed": False,
                              "scope": "Historical 96e5 baseline and d0 AOT candidate command evidence; not a speedup test or current-source qualification.",
                              "harness_source_sha": args.harness_sha,
                              "runner": {"platform": platform.platform(), "machine": platform.machine()},
                              "packages": packages, "steps": 120, "actual_selection_operations": 121,
                              "started_unix": time.time(), "diagnostic_runs": [], "quality_comparisons": []}
    for role in packages:
        report.setdefault("artifact_receipts", {})[role] = json.loads((args.packages / role / RECEIPT).read_text())
    save(report, output)
    try:
        with sync_playwright() as playwright:
            for mode, role, bridge in MODES:
                run = run_case(playwright, packages[role], mode, bridge, output / mode, args)
                run["artifact_directory"] = mode
                report["diagnostic_runs"].append(run)
                save(report, output)
                print(f"{mode}: " + ("passed" if run["passed"] else run["failure"]), flush=True)
        runs = {run["mode"]: run for run in report["diagnostic_runs"]}
        for mode, _, _ in MODES[1:]:
            row: dict[str, Any] = {"before": "baseline-original", "after": mode, "passed": False}
            try:
                if not runs["baseline-original"]["passed"] or not runs[mode]["passed"]:
                    raise ValueError("Missing completed diagnostic pair")
                row["comparison"] = cross_build_quality([runs["baseline-original"], runs[mode]], output)
                if len(row["comparison"]) != 1:
                    raise ValueError("The original document/ruler/font gate produced no complete pair")
                row["passed"] = True
            except Exception as error:
                row["failure"] = f"{type(error).__name__}: {error}"
            report["quality_comparisons"].append(row)
        for mode in ("candidate-persistent", "candidate-composite"):
            for state in ("before", "after", "restored"):
                row = {"before": "candidate-legacy", "after": mode, "state": state, "passed": False}
                try:
                    row.update(raw_comparison(output / "candidate-legacy" / f"{state}.png",
                                              output / mode / f"{state}.png", VIEWPORT, 1))
                    row["passed"] = True
                except Exception as error:
                    row["failure"] = f"{type(error).__name__}: {error}"
                report["quality_comparisons"].append(row)
        if any(not run["passed"] for run in report["diagnostic_runs"]) or any(not row["passed"] for row in report["quality_comparisons"]):
            raise AssertionError("A diagnostic or original native-pixel boundary failed; inspect individual rows")
        if (len({run["browser_version"] for run in report["diagnostic_runs"]}) != 1
                or len({tuple(run["browser_launch_arguments"]) for run in report["diagnostic_runs"]}) != 1):
            raise ValueError("Diagnostic modes used different browser versions or launch arguments")
        report["passed"] = True
    except Exception as error:
        report["failure"] = f"{type(error).__name__}: {error}"
    finally:
        report["duration_seconds"] = time.time() - report["started_unix"]
        save(report, output)
    return 0 if report["passed"] else 1


if __name__ == "__main__":
    raise SystemExit(main())
