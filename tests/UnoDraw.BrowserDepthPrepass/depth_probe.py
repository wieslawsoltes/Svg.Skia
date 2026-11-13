#!/usr/bin/env python3
"""Supplemental depth-attachment A/B; never an unchanged-package primary matrix.

This adapter reuses the exact original run_trial, full warmup, 121-input
selection workload and native-pixel gates. Only the served configuration response
changes; all package files remain tied to the original GitHub archive. A symmetric
API observer proves actual depth-bearing render passes in each fresh browser.
"""
from __future__ import annotations

import argparse
import contextlib
import functools
import hashlib
import http.server
import json
import math
import os
import platform
import re
import statistics
import subprocess
import sys
import threading
import time
from pathlib import Path
from typing import Any, Iterator
from urllib.parse import urlsplit

FLAG = "UNO_WEBGPU_NO_DEPTH_OCCLUSION"
MODES = ("depth-enabled", "depth-disabled")
VALUES = dict(zip(MODES, ("0", "1")))
PREFIX = "UnoDraw supplemental depth configuration: "
VIEWPORT = {"width": 1440, "height": 960}
STATES = ("before", "after", "restored")
PINNED_SOURCE = "22d33286feccb2d53484744d9ecf7f9f6d17e72d"
PINNED_PACKAGE_RUN = 43316652725
PINNED_ARTIFACT = {
    "id": 13991596503,
    "name": "unodraw-performance-package-candidate",
    "archive_sha256": "29cf773ff4c44261a9915091a308c03f765c9167f9cc9698328727989dae438d",
    "archive_bytes": 175653292,
}
PINNED_MANIFEST = "1e849730b42c90593736368540a11708d958d791c98c5077510a7225e0697d95"
PINNED_INVENTORY = "5bd834eab5fd87fcd700481ca44adc46e795338668df2c44932c38b81a35da6e"
ADAPTER_FILES = (
    "tests/UnoDraw.BrowserDepthPrepass/depth_probe.py",
    "tests/UnoDraw.BrowserDepthPrepass/depth_observer.js",
)


def digest(data: bytes) -> str:
    return hashlib.sha256(data).hexdigest()


def trial_plan() -> list[tuple[int, str]]:
    return [(trial, mode) for trial in range(1, 4)
            for mode in (MODES if trial % 2 else tuple(reversed(MODES)))]


def verify_source_pins(source: str, harness: str, bridge: str) -> None:
    if source != PINNED_SOURCE or harness != PINNED_SOURCE or bridge != "single-canvas":
        raise ValueError("Use the exact 22d application and primary harness with explicit single-canvas")


def verify_package_pin(receipt: dict[str, Any]) -> None:
    expected = {"workflow_run_id": PINNED_PACKAGE_RUN, "workflow_head_sha": PINNED_SOURCE,
                "package_source_sha": PINNED_SOURCE, "artifact": PINNED_ARTIFACT,
                "manifest_sha256": PINNED_MANIFEST, "wwwroot_inventory_sha256": PINNED_INVENTORY,
                "served_file_count": 907}
    if not isinstance(receipt, dict) or any(receipt.get(key) != value for key, value in expected.items()):
        raise ValueError("Use the original immutable 22d AOT artifact, manifest and served-file inventory")


def adapter_identity(repository: Path) -> dict[str, Any]:
    """Record the new adapter separately; never claim it is the application source."""
    head = subprocess.check_output(["git", "rev-parse", "HEAD"], cwd=repository, text=True).strip()
    if not re.fullmatch(r"[0-9a-f]{40}", head):
        raise ValueError("The diagnostic adapter must have an identified checkout")
    files = {}
    for name in ADAPTER_FILES:
        original = subprocess.check_output(["git", "show", head + ":" + name], cwd=repository)
        path = repository / name
        if path.is_symlink() or path.read_bytes() != original:
            raise ValueError("The diagnostic adapter or workflow changed after checkout")
        files[name] = digest(original)
    return {"source_sha": head, "files_sha256": files,
            "workflow_run_id": os.environ.get("GITHUB_RUN_ID"),
            "workflow_head_sha": os.environ.get("GITHUB_SHA"),
            "application_source_equivalence_claimed": False}


def source_inventory(directory: Path) -> dict[str, str]:
    result = {}
    for path in sorted(directory.rglob("*")):
        if path.is_symlink():
            raise ValueError("The harness snapshot must not contain symlinks")
        if path.is_file():
            result[path.relative_to(directory).as_posix()] = digest(path.read_bytes())
    return result


def snapshot_harness(repository: Path, commit: str, destination: Path) -> dict[str, str]:
    if not re.fullmatch(r"[0-9a-f]{40}", commit):
        raise ValueError("Pin the complete harness commit SHA")
    actual = subprocess.check_output(["git", "rev-parse", "--verify", commit + "^{commit}"],
                                     cwd=repository, text=True).strip()
    if actual != commit:
        raise ValueError("The requested harness commit does not resolve exactly")
    roots = ("tests/UnoDraw.BrowserPerformance", "tests/UnoDraw.BrowserSmoke")
    entries = subprocess.check_output(["git", "ls-tree", "-r", "-z", commit, "--", *roots], cwd=repository)
    if destination.exists():
        raise ValueError("Extract the committed harness into a new isolated snapshot")
    destination.mkdir(parents=True)
    expected = {}
    for entry in entries.split(b"\0"):
        if not entry:
            continue
        info, name = entry.split(b"\t", 1)
        mode, kind, object_sha = info.decode().split()
        path = Path(name.decode())
        if (path.is_absolute() or ".." in path.parts or path.as_posix().encode() != name
                or not any(path.as_posix().startswith(root + "/") for root in roots)):
            raise ValueError("The committed harness path is outside its source roots")
        if path.suffix not in {".py", ".js"}:
            continue
        if mode not in {"100644", "100755"} or kind != "blob":
            raise ValueError("The committed harness must contain ordinary source files")
        data = subprocess.check_output(["git", "cat-file", "blob", object_sha], cwd=repository)
        if hashlib.sha1(b"blob " + str(len(data)).encode() + b"\0" + data).hexdigest() != object_sha:
            raise ValueError("The harness source bytes do not match their committed Git object")
        target = destination / path
        target.parent.mkdir(parents=True, exist_ok=True)
        with target.open("xb") as stream:
            stream.write(data)
        expected[path.as_posix()] = digest(data)
    required = ("tests/UnoDraw.BrowserPerformance/performance.py",
                "tests/UnoDraw.BrowserPerformance/instrumentation.js",
                "tests/UnoDraw.BrowserSmoke/smoke.py", "tests/UnoDraw.BrowserSmoke/backend.py")
    if any(path not in expected for path in required) or source_inventory(destination) != expected:
        raise ValueError("The complete committed harness source snapshot is unavailable")
    return expected


def configure(original: bytes, mode: str) -> tuple[bytes, dict[str, Any]]:
    if mode not in VALUES:
        raise ValueError("Use a declared depth configuration")
    text = original.decode("utf-8-sig")
    if (len(re.findall(r"(?m)^let config = \{\};\s*$", text)) != 1
            or len(re.findall(r"(?m)^export \{ config \};\s*$", text)) != 1
            or not text.rstrip().endswith("export { config };")
            or text.count("config.environmentVariables = config.environmentVariables || {};") != 1
            or any(key in text for key in (FLAG, PREFIX, "UNO_LOG_FRAME_PHASES", "UNO_WEBGPU_STATS"))):
        raise ValueError("Expected the original generated configuration without diagnostic flags")
    settings = {FLAG: VALUES[mode]}
    proof_message = PREFIX + json.dumps(settings, sort_keys=True)
    appendix = ("\n// Supplemental HTTP response override; application package files are unchanged.\n"
                + f"config.environmentVariables[{json.dumps(FLAG)}] = {json.dumps(VALUES[mode])};\n"
                + f"console.info({json.dumps(proof_message)});\n").encode()
    served = original + appendix
    return served, {"mode": mode, "environment_variables": settings, "proof_message": proof_message,
                    "original_sha256": digest(original), "served_sha256": digest(served),
                    "appendix_sha256": digest(appendix), "original_bytes": len(original),
                    "served_bytes": len(served), "original_prefix_preserved": served.startswith(original),
                    "served_package_modified": True, "primary_comparison": False, "request_count": 0}


def verify_depth_observation(snapshot: dict[str, Any], mode: str) -> None:
    if mode not in VALUES or snapshot.get("initialized") is not True:
        raise ValueError("The native depth-pass observer was not initialized")
    keys = ("renderPasses", "depthPasses", "colorOnlyPasses", "unknownPasses")
    if any(type(snapshot.get(key)) is not int or snapshot[key] < 0 for key in keys):
        raise ValueError("Depth evidence requires explicit nonnegative integer counts")
    if (snapshot["renderPasses"] <= 0 or snapshot["colorOnlyPasses"] <= 0
            or snapshot["unknownPasses"] != 0
            or snapshot["renderPasses"] != snapshot["depthPasses"] + snapshot["colorOnlyPasses"]):
        raise ValueError("Actual render-pass descriptors are missing or ambiguous")
    if (snapshot["depthPasses"] > 0) != (mode == "depth-enabled"):
        raise ValueError("Actual depth attachments do not match the configured arm")


@contextlib.contextmanager
def serve_configured(site: Path, evidence: Path, mode: str) -> Iterator[tuple[str, dict[str, Any]]]:
    from smoke import SiteHandler
    original = (site / "uno-config.js").read_bytes()
    served, proof = configure(original, mode)
    evidence.mkdir(parents=True, exist_ok=True)
    (evidence / "uno-config.original.js").write_bytes(original)
    (evidence / "uno-config.served.js").write_bytes(served)

    class Handler(SiteHandler):
        def configuration(self, body: bool) -> None:
            proof["request_count"] += int(body)
            self.send_response(200)
            self.send_header("Content-Type", "text/javascript; charset=utf-8")
            self.send_header("Content-Length", str(len(served)))
            self.end_headers()
            if body:
                self.wfile.write(served)

        def do_GET(self) -> None:
            if urlsplit(self.path).path == "/uno-config.js":
                self.configuration(True)
            else:
                super().do_GET()

        def do_HEAD(self) -> None:
            if urlsplit(self.path).path == "/uno-config.js":
                self.configuration(False)
            else:
                super().do_HEAD()

    server = http.server.ThreadingHTTPServer(("127.0.0.1", 0), functools.partial(Handler, directory=str(site)))
    server.daemon_threads = True
    thread = threading.Thread(target=server.serve_forever, daemon=True)
    thread.start()
    try:
        yield f"http://127.0.0.1:{server.server_port}/", proof
    finally:
        server.shutdown()
        server.server_close()
        thread.join(timeout=5)
        (evidence / "configuration-proof.json").write_text(json.dumps(proof, indent=2) + "\n")


class ObservedContext:
    def __init__(self, context: Any, observations: list[dict[str, Any]]) -> None:
        self.context, self.observations, self.page = context, observations, None

    def __getattr__(self, name: str) -> Any:
        return getattr(self.context, name)

    def new_page(self) -> Any:
        self.page = self.context.new_page()
        return self.page

    def close(self) -> None:
        try:
            self.observations.append(self.page.evaluate("() => globalThis.__unoDrawDepthProbe.snapshot()"))
        except Exception as error:
            self.observations.append({"failure": f"{type(error).__name__}: {error}"})
        finally:
            self.context.close()


class ObservedBrowser:
    def __init__(self, browser: Any) -> None:
        self.browser, self.observations = browser, []

    def new_context(self, **kwargs: Any) -> ObservedContext:
        context = self.browser.new_context(**kwargs)
        context.add_init_script(path=str(Path(__file__).with_name("depth_observer.js")))
        return ObservedContext(context, self.observations)


def verify_run(run: dict[str, Any], source: str, bridge: str) -> None:
    from bridge_experiment_contracts import verify_legacy_composition_environment
    from metrics import summarize
    from quality import verify_bridge
    mode = run.get("depth_mode")
    if (run.get("passed") is not True or run.get("source_sha") != source
            or run.get("canvas_bridge") != bridge or run.get("workload") != "selection"
            or run.get("backend") != "webgpu" or run.get("diagnostic_pass") is not False
            or run.get("primary_comparison") is not False):
        raise ValueError("The configuration probe changed source, renderer or workload")
    # Reuse its general environment, actual provider, full warmup and error gates.
    verify_legacy_composition_environment([run], diagnostics=False)
    verify_bridge(run["console"], bridge)
    metrics = run["metrics"]
    if any(type(metrics.get(key)) is not int or metrics[key] != value for key, value in {
            "operations": 121, "input_events": 121, "inputs_without_later_graphics": 0,
            "inputs_without_completed_render_callback": 0}.items()):
        raise ValueError("The original 121 selection inputs and zero-tail requirements must hold")
    if any(type(metrics.get(key)) is not int or metrics[key] <= 0
           for key in ("completed_render_callbacks", "webgpu_canvas_submissions")):
        raise ValueError("Selection did not complete observed graphics work")
    if metrics != summarize(run["raw_browser_observations"], run["cdp_cpu_before"], run["cdp_cpu_after"], 121):
        raise ValueError("Recorded metrics do not match the raw observations")
    quality = run.get("quality", {})
    changed = quality.get("changed_document_pixels")
    restored = quality.get("restored", {})
    rulers = restored.get("ruler_changed_pixel_fraction", {})
    fractions = [restored.get("changed_pixel_fraction"), *rulers.values()]
    if (quality.get("minimum_changed_pixels") != 50 or type(changed) is not int or changed < 50
            or set(rulers) != {"horizontal", "vertical"}
            or any(type(value) not in (int, float) or not math.isfinite(value) or not 0 <= value <= 0.001
                   for value in fractions)):
        raise ValueError("The original selected/restored document and ruler requirements must hold")
    configuration = run.get("configuration_override", {})
    if (mode not in VALUES or configuration.get("mode") != mode or configuration.get("request_count", 0) < 1
            or configuration.get("environment_variables") != {FLAG: VALUES[mode]}
            or configuration.get("original_prefix_preserved") is not True
            or configuration.get("served_package_modified") is not True):
        raise ValueError("The fresh imported configuration is not proved")
    messages = [entry.get("text", "") for entry in run["console"] if entry.get("text", "").startswith(PREFIX)]
    if messages != [PREFIX + json.dumps({FLAG: VALUES[mode]}, sort_keys=True)]:
        raise ValueError("The actual fresh application did not import exactly one expected flag setting")
    observations = run.get("depth_observations", [])
    if len(observations) != 1:
        raise ValueError("Each trial requires exactly one fresh observed application context")
    verify_depth_observation(observations[0], mode)


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--harness", type=Path, required=True)
    parser.add_argument("--harness-sha", required=True)
    parser.add_argument("--package", type=Path, required=True)
    parser.add_argument("--receipt", type=Path, required=True)
    parser.add_argument("--expected-source", required=True)
    parser.add_argument("--output", type=Path, required=True)
    parser.add_argument("--bridge", choices=("single-canvas",), required=True)
    args = parser.parse_args()
    try:
        verify_source_pins(args.expected_source, args.harness_sha, args.bridge)
    except ValueError as error:
        parser.error(str(error))
    package_dir, output = args.package.resolve(), args.output.resolve()
    if output == package_dir or output in package_dir.parents or package_dir in output.parents:
        parser.error("Keep diagnostic output outside the immutable package")
    if output.exists() and any(output.iterdir()):
        parser.error("Keep each attempt in a new directory")
    output.mkdir(parents=True, exist_ok=True)
    adapter = adapter_identity(Path(__file__).resolve().parents[2])
    harness = output / "harness-snapshot"
    sources = snapshot_harness(args.harness.resolve(), args.harness_sha, harness)
    # No mutable worktree source is imported, and no bytecode file may shadow or
    # modify the newly extracted snapshot used by the browser scripts later.
    sys.dont_write_bytecode = True
    sys.path.insert(0, str(harness / "tests/UnoDraw.BrowserPerformance"))
    from candidate_package import verify_package
    from performance import launch_arguments, run_trial
    from quality import raw_comparison
    from transport_experiment_contracts import verify_native_pairs
    from playwright.sync_api import sync_playwright
    receipt_bytes = args.receipt.read_bytes()
    receipt = json.loads(receipt_bytes)
    verify_package_pin(receipt)
    package = verify_package(package_dir, receipt)
    if package["source_sha"] != args.expected_source:
        parser.error("The original package is from a different source")
    args.steps, args.idle_ms = 120, 2000
    arguments = launch_arguments("webgpu")
    report: dict[str, Any] = {"schema": 1, "report_kind": "supplemental_webgpu_depth_configuration_probe",
        "primary_comparison": False, "application_promotion_qualified": False, "passed": False,
        "scope": "Symmetrically instrumented configuration experiment on SwiftShader; not unchanged-package primary timing or hardware evidence.",
        "application_source_sha": PINNED_SOURCE, "package_run_id": PINNED_PACKAGE_RUN,
        "diagnostic_adapter": adapter, "current_source_equivalence_claimed": False,
        "current_default_measured": False, "physical_gpu_performance_qualified": False,
        "started_unix": time.time(), "package": package, "package_receipt": receipt,
        "receipt_sha256": digest(receipt_bytes), "harness_source_files": sources,
        "harness_source_sha": args.harness_sha, "harness_snapshot": "harness-snapshot",
        "adapter_sha256": digest(Path(__file__).read_bytes()),
        "observer_sha256": digest(Path(__file__).with_name("depth_observer.js").read_bytes()),
        "bridge": args.bridge, "workload": "selection", "steps": 120, "actual_inputs": 121,
        "viewport": VIEWPORT, "dpr": 1, "runner": platform.platform(), "plan": trial_plan(),
        "depth_observation_scope": "Native beginRenderPass calls that returned in each complete fresh application context, including startup/warmup/restoration; not asynchronous GPU-validation proof, a timed-window count or a GPU-duration measure.",
        "runs": [], "native_pairs": [], "metric_comparisons": []}

    def save() -> None:
        report["duration_seconds"] = time.time() - report["started_unix"]
        (output / "report.json").write_text(json.dumps(report, indent=2) + "\n")

    save()
    try:
        with sync_playwright() as playwright:
            for trial, mode in trial_plan():
                relative = Path(f"trial-{trial}") / mode
                directory = output / relative
                browser = None
                run: dict[str, Any] = {"passed": False}
                try:
                    verify_package(package_dir, receipt)
                    with serve_configured(package_dir / "wwwroot", directory / "configuration", mode) as (url, proof):
                        browser = playwright.chromium.launch(headless=True, args=arguments)
                        observed = ObservedBrowser(browser)
                        run = run_trial(observed, url, package, "webgpu", "selection", trial, args, directory,
                                        diagnostics=False, canvas_bridge=args.bridge)
                        run.update({"depth_mode": mode, "primary_comparison": False,
                                    "configuration_override": proof, "depth_observations": observed.observations,
                                    "browser_version": browser.version, "browser_launch_arguments": arguments})
                    verify_package(package_dir, receipt)
                    verify_run(run, args.expected_source, args.bridge)
                except Exception as error:
                    run.update({"passed": False, "failure": f"{type(error).__name__}: {error}"})
                finally:
                    if browser is not None:
                        with contextlib.suppress(Exception):
                            browser.close()
                run.update({"trial": trial, "depth_mode": mode, "artifact_directory": str(relative)})
                directory.mkdir(parents=True, exist_ok=True)
                (directory / "report.json").write_text(json.dumps(run, indent=2) + "\n")
                report["runs"].append(run)
                save()
                print(f"{mode} trial {trial}: " + ("passed" if run["passed"] else run["failure"]), flush=True)
        if [(run["trial"], run["depth_mode"]) for run in report["runs"]] != trial_plan():
            raise ValueError("All six trials must retain their alternating order")
        for run in report["runs"]:
            verify_run(run, args.expected_source, args.bridge)
        if (len({run["browser_version"] for run in report["runs"]}) != 1
                or len({tuple(run["browser_launch_arguments"]) for run in report["runs"]}) != 1):
            raise ValueError("The two depth arms used different browser configurations")
        for trial in range(1, 4):
            for state in STATES:
                row: dict[str, Any] = {"trial": trial, "state": state, "passed": False}
                try:
                    row.update(raw_comparison(output / f"trial-{trial}" / MODES[0] / f"{state}.png",
                                              output / f"trial-{trial}" / MODES[1] / f"{state}.png", VIEWPORT, 1))
                    row["passed"] = True
                except Exception as error:
                    row["failure"] = f"{type(error).__name__}: {error}"
                report["native_pairs"].append(row)
        verify_native_pairs(report["native_pairs"])
        verify_package(package_dir, receipt)
        for metric in ("elapsed_ms", "thread_cpu_ms", "completed_render_callbacks", "webgpu_canvas_submissions"):
            values = {mode: [run["metrics"].get(metric) for run in report["runs"] if run["depth_mode"] == mode]
                      for mode in MODES}
            if metric == "thread_cpu_ms" and any(value is None for samples in values.values() for value in samples):
                continue
            if any(type(value) not in (int, float) or not math.isfinite(value) or value < 0
                   for samples in values.values() for value in samples):
                raise ValueError("Configuration metrics must be finite measured values")
            medians = {mode: statistics.median(samples) for mode, samples in values.items()}
            report["metric_comparisons"].append({"metric": metric, "samples": values, "medians": medians,
                "disabled_over_enabled": medians[MODES[1]] / medians[MODES[0]] if medians[MODES[0]] else None})
        report["passed"] = True
    except Exception as error:
        report["failure"] = f"{type(error).__name__}: {error}"
    finally:
        try:
            if source_inventory(harness) != sources:
                raise ValueError("The committed harness snapshot changed during the probe")
            report["final_harness_snapshot_unchanged"] = True
        except Exception as error:
            report["passed"] = False
            report["final_harness_snapshot_unchanged"] = False
            report["harness_verification_failure"] = f"{type(error).__name__}: {error}"
        try:
            verify_package(package_dir, receipt)
            report["final_archive_and_served_inventory_verified"] = True
        except Exception as error:
            report["passed"] = False
            report["final_archive_and_served_inventory_verified"] = False
            report["package_verification_failure"] = f"{type(error).__name__}: {error}"
        save()
    return 0 if report["passed"] else 1


if __name__ == "__main__":
    raise SystemExit(main())
