"""Configuration and evidence boundaries for a separately instrumented run."""
from __future__ import annotations

import hashlib
import json
import math
import re
from typing import Any


ENVIRONMENT = {"UNO_LOG_FRAME_PHASES": "1", "UNO_WEBGPU_STATS": "1", "UNO_WEBGPU_STATS_EVERY": "1"}
BOOTSTRAP_SOURCE = "0452dfce12ed05ed4e5a4fb920b52f59973225fd"
CONFIG_PROOF_PREFIX = "UnoDraw diagnostic-only runtime configuration: "
CONFIG_APPENDIX = ("\n// Diagnostic HTTP response override; original package bytes remain unchanged on disk.\n"
                   + "\n".join(f"config.environmentVariables[{json.dumps(key)}] = {json.dumps(value)};"
                               for key, value in ENVIRONMENT.items())
                   + "\nconsole.info(" + json.dumps(CONFIG_PROOF_PREFIX + json.dumps(ENVIRONMENT, sort_keys=True)) + ");\n").encode()
MODES = (("baseline-original", "baseline", None), ("candidate-legacy", "candidate", "legacy"),
         ("candidate-persistent", "candidate", "persistent"), ("candidate-composite", "candidate", "composite"))


def config_response(original: bytes) -> tuple[bytes, dict[str, Any]]:
    text = original.decode("utf-8-sig")
    if (len(re.findall(r"(?m)^let config = \{\};\s*$", text)) != 1
            or len(re.findall(r"(?m)^export \{ config \};\s*$", text)) != 1
            or not text.rstrip().endswith("export { config };")
            or text.count("config.environmentVariables = config.environmentVariables || {};") != 1
            or any(key in text for key in ENVIRONMENT)
            or CONFIG_PROOF_PREFIX in text):
        raise ValueError("Expected the unmodified generated Bootstrap 10.1.1 configuration with no diagnostic flags")
    served = original + CONFIG_APPENDIX
    proof = {"relative_url": "uno-config.js", "original_sha256": hashlib.sha256(original).hexdigest(),
             "served_sha256": hashlib.sha256(served).hexdigest(), "original_bytes": len(original),
             "served_bytes": len(served), "appendix_sha256": hashlib.sha256(CONFIG_APPENDIX).hexdigest(),
             "environment_variables": ENVIRONMENT, "bootstrap_source": BOOTSTRAP_SOURCE,
             "original_prefix_preserved": served[:len(original)] == original,
             "served_package_modified": True, "binary_runtime_font_assets_modified": False,
             "primary_performance_qualification": False}
    return served, proof


def verify_observation(run: dict[str, Any]) -> None:
    if run.get("diagnostic_pass") is not True or run.get("primary_comparison") is not False:
        raise ValueError("Runtime configuration diagnostics cannot qualify primary performance")
    metrics = run.get("metrics", {})
    for key, value in {"operations": 121, "input_events": 121, "inputs_without_later_graphics": 0,
                       "inputs_without_completed_render_callback": 0}.items():
        if metrics.get(key) != value:
            raise ValueError("Runtime diagnostics require all 121 trusted selection inputs without an unobserved tail")
    if metrics.get("completed_render_callbacks", 0) <= 0 or metrics.get("webgpu_canvas_submissions", 0) <= 0:
        raise ValueError("The diagnostic did not complete real WebGPU canvas work")
    messages = [entry.get("text", "") for entry in run.get("console", [])]
    expected = CONFIG_PROOF_PREFIX + json.dumps(ENVIRONMENT, sort_keys=True)
    proofs = [message for message in messages if message.startswith(CONFIG_PROOF_PREFIX)]
    if proofs != [expected]:
        raise ValueError("The actual imported configuration did not prove the exact diagnostic override")
    logs = run.get("runtime_phase_logs", [])
    for prefix in ("[frame-phases]", "[webgpu-frame]", "[webgpu-stats]"):
        if not any(line.startswith(prefix) for line in logs):
            raise ValueError(f"No measured-window {prefix} output proves runtime diagnostics were enabled")
    if len([line for line in logs if line.startswith("[webgpu-frame]")]) < 60:
        raise ValueError("The per-frame cadence was not observed during the full selection workload")
    configuration = run.get("configuration_override", {})
    if (configuration.get("request_count", 0) <= 0 or configuration.get("served_package_modified") is not True
            or configuration.get("original_prefix_preserved") is not True
            or configuration.get("environment_variables") != ENVIRONMENT
            or configuration.get("on_disk_inventory_unchanged") is not True):
        raise ValueError("The served configuration or unchanged on-disk package was not proved")
    states = run.get("canonical_states", [])
    if [state.get("name") for state in states] != ["selected", "cleared"]:
        raise ValueError("Canonical selected and cleared states must both be observed")
    for state in states:
        comparison = state.get("native_comparison", {})
        fractions = comparison.get("changed_pixel_fractions", {})
        if (state.get("included_in_measured_window") is not False
                or not state.get("last_frame", "").startswith("[webgpu-frame]")
                or not state.get("last_stats", "").startswith("[webgpu-stats]")
                or comparison.get("physical_size") != [1440, 960]
                or comparison.get("device_pixel_ratio") != 1
                or comparison.get("luminance_threshold") != 12
                or comparison.get("maximum_changed_fraction") != 0.001
                or comparison.get("resized_for_comparison") is not False
                or set(fractions) != {"whole_frame", "document", "horizontal_ruler", "vertical_ruler"}
                or any(type(value) not in (int, float) or not math.isfinite(value) or not 0 <= value <= 0.001
                       for value in fractions.values())):
            raise ValueError("A canonical state lacks its unchanged native gate or actual command evidence")


def summarize_logs(logs: list[str]) -> dict[str, Any]:
    """Keep raw rows; only unambiguous per-pass numeric fields are summarized."""
    from statistics import median
    fields = ("ops", "emitted", "scissorChanges", "cmds", "replays", "opsBuild", "walk", "encode",
              "renderInto", "finishSubmit", "prepass", "clipCulled")
    samples = {key: [] for key in fields}
    for line in logs:
        if not line.startswith(("[webgpu-frame]", "[webgpu-stats]")):
            continue
        for key in fields:
            found = re.search(r"(?:^|[ (])" + key + r"=(\d+(?:\.\d+)?)(?:ms)?(?=[ /)]|$)", line)
            if found:
                samples[key].append(float(found.group(1)))
    return {"cadence": "UNO_WEBGPU_STATS_EVERY=1; each native stats interval contains one completed frame",
            "frame_phase_cadence": "frame-phases remains a 60-recording average; gap is not GPU time",
            "fields": {key: {"count": len(values), "min": min(values), "median": median(values), "max": max(values)}
                       for key, values in samples.items() if values}}
