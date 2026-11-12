from __future__ import annotations

import copy
import hashlib
import json
import tempfile
import unittest
from pathlib import Path

from bridge_experiment_contracts import compare_metrics, load_candidate, trial_plan, verify_runs
from quality import PERSISTENT_INITIALIZED, WEBGL_INITIALIZED, WEBGL_PREFERENCE


SHA = "a" * 40


def runs(*, diagnostics: bool = False) -> list[dict]:
    plan = [(1, "persistent"), (1, "webgl")] if diagnostics else trial_plan(3)
    return [{"passed": True, "source_sha": SHA, "backend": "webgpu", "workload": "pan",
             "trial": trial, "canvas_bridge": bridge, "diagnostic_pass": diagnostics,
             "console": [{"text": PERSISTENT_INITIALIZED}] + (
                 [{"text": WEBGL_PREFERENCE}, {"text": WEBGL_INITIALIZED}] if bridge == "webgl" else []),
             "metrics": {"operations": 120, "input_events": 120, "inputs_without_later_graphics": 0,
                         "inputs_without_completed_render_callback": 0,
                         "graphics_api_calls": {"webgpu.copyExternalImageToTexture": 120 if bridge == "webgl" else 0},
                         "cpu_task_ms_per_operation": trial * (20 if bridge == "persistent" else 5),
                         "elapsed_ms": trial * (3000 if bridge == "persistent" else 2000),
                         "operations_per_second": 120000 / (trial * (3000 if bridge == "persistent" else 2000))}}
            for trial, bridge in plan]


class BridgeExperimentTests(unittest.TestCase):
    def test_order_rotates_without_splitting_a_pair_or_losing_trials(self) -> None:
        self.assertEqual(trial_plan(3), [(1, "persistent"), (1, "webgl"),
                                        (2, "webgl"), (2, "persistent"),
                                        (3, "persistent"), (3, "webgl")])
        for repeats in (0, 1, 21):
            with self.subTest(repeats=repeats), self.assertRaises(ValueError):
                trial_plan(repeats)

    def test_missing_duplicate_failed_or_misattributed_samples_cannot_pass(self) -> None:
        original = runs()
        verify_runs(original, 3, SHA, "pan")
        for defect in ("missing", "duplicate", "failed", "source", "mode", "backend", "workload",
                       "operations", "input_tail", "no_copy", "fallback"):
            values = copy.deepcopy(original)
            if defect == "missing":
                values.pop()
            elif defect == "duplicate":
                values[-1] = copy.deepcopy(values[0])
            elif defect in {"failed", "source", "mode", "backend", "workload"}:
                key, value = {"failed": ("passed", False), "source": ("source_sha", "b" * 40),
                              "mode": ("diagnostic_pass", True), "backend": ("backend", "skia-preferred"),
                              "workload": ("workload", "zoom")}[defect]
                values[0][key] = value
            elif defect == "operations":
                values[0]["metrics"]["operations"] = 119
            elif defect == "input_tail":
                values[0]["metrics"]["inputs_without_completed_render_callback"] = 1
            elif defect == "no_copy":
                values[1]["metrics"]["graphics_api_calls"].clear()
            else:
                values[1]["console"].append({"text": "UnoDraw WebGL canvas fallback: context lost."})
            with self.subTest(defect=defect), self.assertRaises((ValueError, AssertionError)):
                verify_runs(values, 3, SHA, "pan")

    def test_diagnostic_passes_cannot_satisfy_primary_coverage(self) -> None:
        diagnostic = runs(diagnostics=True)
        verify_runs(diagnostic, 3, SHA, "pan", diagnostics=True)
        with self.assertRaises(ValueError):
            verify_runs(diagnostic, 3, SHA, "pan")
        with self.assertRaises(ValueError):
            verify_runs(runs(), 3, SHA, "pan", diagnostics=True)

    def test_metrics_use_trial_medians_and_retain_the_individual_values(self) -> None:
        comparisons = compare_metrics(runs())
        cpu = comparisons[0]
        self.assertEqual(cpu["trial_values"], {"persistent": [20, 40, 60], "webgl": [5, 10, 15]})
        self.assertEqual(cpu["medians"], {"persistent": 40, "webgl": 10})
        self.assertEqual(cpu["webgl_over_persistent"], 0.25)
        for value in (None, float("nan"), float("inf"), -1):
            invalid = runs()
            invalid[0]["metrics"]["cpu_task_ms_per_operation"] = value
            with self.subTest(value=value), self.assertRaises(ValueError):
                compare_metrics(invalid)

    def test_interpreter_mismatched_source_or_changed_index_are_rejected(self) -> None:
        with tempfile.TemporaryDirectory() as temporary:
            directory = Path(temporary)
            index = directory / "wwwroot/index.html"
            index.parent.mkdir()
            index.write_bytes(b"<!doctype html><title>bridge fixture</title>")
            manifest = {"schema": 2, "role": "candidate", "source_sha": SHA,
                        "configuration": "Release", "execution_mode": "InterpreterAndAOT",
                        "runtime_generation": "uno7", "dotnet_sdk": "10.0.403",
                        "dotnet_workload_version": "10.0.403", "fixture_sha256": "f" * 64,
                        "index_sha256": hashlib.sha256(index.read_bytes()).hexdigest(),
                        "supports_render_diagnostics": True}
            path = directory / "benchmark-package.json"
            path.write_text(json.dumps(manifest))
            self.assertEqual(load_candidate(directory)["source_sha"], SHA)
            for key, value in (("execution_mode", "Interpreter"), ("role", "baseline"),
                               ("source_sha", "HEAD"), ("index_sha256", "0" * 64),
                               ("dotnet_workload_version", ""), ("supports_render_diagnostics", False)):
                path.write_text(json.dumps({**manifest, key: value}))
                with self.subTest(key=key), self.assertRaises(ValueError):
                    load_candidate(directory)


if __name__ == "__main__":
    unittest.main()
