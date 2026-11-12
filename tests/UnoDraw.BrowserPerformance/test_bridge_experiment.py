from __future__ import annotations

import copy
import hashlib
import json
import tempfile
import unittest
from pathlib import Path

from bridge_experiment_contracts import (bridge_pair, compare_metrics, load_candidate,
                                         trial_plan, verify_experiment_workload, verify_runs)
from quality import (COMPOSITE_INITIALIZED, COMPOSITE_PREFERENCE, COMPOSITE_REUSED,
                     GRAPHITE_COVERAGE_INITIALIZED,
                     GRAPHITE_INITIALIZED, GRAPHITE_PREFERENCE,
                     PERSISTENT_INITIALIZED, WEBGL_INITIALIZED, WEBGL_PREFERENCE)


SHA = "a" * 40


def runs(*, diagnostics: bool = False, candidate_bridge: str = "webgl",
         workload: str | None = None, graphite_coverage: str | None = None) -> list[dict]:
    workload = workload or ("brush-hover" if candidate_bridge == "composite" else "pan")
    operations = 121 if workload == "selection" else 120
    plan = [(1, bridge) for bridge in bridge_pair(candidate_bridge)] if diagnostics else trial_plan(3, candidate_bridge)
    return [{"passed": True, "source_sha": SHA, "backend": "webgpu",
             "workload": workload,
             "trial": trial, "canvas_bridge": bridge, "diagnostic_pass": diagnostics,
             "graphite_coverage": graphite_coverage if bridge == "graphite" else None,
             "console": [{"text": PERSISTENT_INITIALIZED}] + (
                 [{"text": WEBGL_PREFERENCE}, {"text": WEBGL_INITIALIZED}] if bridge == "webgl" else
                 [{"text": GRAPHITE_PREFERENCE}, {"text": GRAPHITE_INITIALIZED}] if bridge == "graphite" else
                 [{"text": COMPOSITE_PREFERENCE}, {"text": COMPOSITE_INITIALIZED}, {"text": COMPOSITE_REUSED}]
                 if bridge == "composite" else []) + (
                 [{"text": GRAPHITE_COVERAGE_INITIALIZED[graphite_coverage]}]
                 if bridge == "graphite" and graphite_coverage is not None else []),
             "managed_render_diagnostics": {"after": {"phases": [
                 {"phase": "Document", "count": 0}, {"phase": "Interaction", "count": operations}]}},
             "quality": {"changed_document_pixels": 50, "minimum_changed_pixels": 50,
                         "restored": {"changed_pixel_fraction": 0,
                                      "ruler_changed_pixel_fraction": {"horizontal": 0, "vertical": 0}}},
             "graphite_queue_drain": {"available": True, "result": {
                 "completed": True, "queueCount": 1, "duration_ms": 0, "errors": []}}
                 if bridge == "graphite" else {"available": False},
             "metrics": {"operations": operations, "input_events": operations, "inputs_without_later_graphics": 0,
                         "inputs_without_completed_render_callback": 0,
                         "completed_render_callbacks": 240, "webgpu_canvas_submissions": 240,
                         "graphics_api_calls": {"webgpu.copyExternalImageToTexture": 120} if bridge == "webgl" else {},
                         "graphite_document_submissions": 120 if bridge == "graphite" else None,
                         "cpu_task_ms_per_operation": trial * (20 if bridge == "persistent" else 5),
                         "elapsed_ms": trial * (3000 if bridge == "persistent" else 2000),
                         "operations_per_second": 120000 / (trial * (3000 if bridge == "persistent" else 2000))}}
            for trial, bridge in plan]


class BridgeExperimentTests(unittest.TestCase):
    def test_explicit_graphite_coverage_cannot_mix_variants_or_change_the_control(self) -> None:
        for diagnostics in (False, True):
            for coverage in GRAPHITE_COVERAGE_INITIALIZED:
                original = runs(diagnostics=diagnostics, candidate_bridge="graphite", graphite_coverage=coverage)
                verify_runs(original, 3, SHA, "pan", diagnostics=diagnostics,
                            candidate_bridge="graphite", graphite_coverage=coverage)
                for defect in ("missing_label", "different_label", "missing_log", "different_log", "control_label",
                               "control_log", "mixed_contexts"):
                    values = copy.deepcopy(original)
                    target = next(value for value in values if value["canvas_bridge"] == "graphite")
                    other = "raster" if coverage == "default" else "default"
                    if defect == "missing_label":
                        del target["graphite_coverage"]
                    elif defect == "different_label":
                        target["graphite_coverage"] = other
                    elif defect in {"missing_log", "different_log"}:
                        target["console"] = [entry for entry in target["console"]
                                             if entry["text"] != GRAPHITE_COVERAGE_INITIALIZED[coverage]]
                        if defect == "different_log":
                            target["console"].append({"text": GRAPHITE_COVERAGE_INITIALIZED[other]})
                    elif defect == "mixed_contexts":
                        target["console"].append({"text": GRAPHITE_COVERAGE_INITIALIZED[other]})
                    else:
                        target = next(value for value in values if value["canvas_bridge"] == "persistent")
                        if defect == "control_label":
                            target["graphite_coverage"] = coverage
                        else:
                            target["console"].append({"text": GRAPHITE_COVERAGE_INITIALIZED[coverage]})
                    with self.subTest(diagnostics=diagnostics, coverage=coverage, defect=defect), self.assertRaises((ValueError, AssertionError)):
                        verify_runs(values, 3, SHA, "pan", diagnostics=diagnostics,
                                    candidate_bridge="graphite", graphite_coverage=coverage)
        with self.assertRaises(ValueError):
            verify_runs(runs(), 3, SHA, "pan", graphite_coverage="raster")

    def test_graphite_requires_timed_document_submissions_and_cannot_use_external_image_copies(self) -> None:
        for diagnostics in (False, True):
            original = runs(diagnostics=diagnostics, candidate_bridge="graphite")
            verify_runs(original, 3, SHA, "pan", diagnostics=diagnostics, candidate_bridge="graphite")
            for defect in ("missing", "zero", "negative", "fractional", "boolean", "copy", "fallback", "wrong_mode",
                           "missing_drain", "unavailable_drain", "failed_drain"):
                values = copy.deepcopy(original)
                target = next(value for value in values if value["canvas_bridge"] == "graphite")
                if defect in {"missing", "zero", "negative", "fractional", "boolean"}:
                    target["metrics"]["graphite_document_submissions"] = {
                        "missing": None, "zero": 0, "negative": -1, "fractional": 1.5, "boolean": True}[defect]
                elif defect == "copy":
                    target["metrics"]["graphics_api_calls"]["webgpu.copyExternalImageToTexture"] = 1
                elif defect == "fallback":
                    target["console"].append({"text": "UnoDraw Graphite canvas fallback: submit failed."})
                elif defect == "missing_drain":
                    del target["graphite_queue_drain"]
                elif defect == "unavailable_drain":
                    target["graphite_queue_drain"] = {"available": False}
                elif defect == "failed_drain":
                    target["graphite_queue_drain"]["result"]["completed"] = False
                else:
                    target["console"].append({"text": WEBGL_INITIALIZED})
                with self.subTest(diagnostics=diagnostics, defect=defect), self.assertRaises((ValueError, AssertionError)):
                    verify_runs(values, 3, SHA, "pan", diagnostics=diagnostics, candidate_bridge="graphite")
        row = compare_metrics(runs(candidate_bridge="graphite"), "graphite")[0]
        self.assertEqual(row["candidate_bridge"], "graphite")
        self.assertNotIn("webgl_over_persistent", row)

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
                       "operations", "input_tail", "no_copy", "no_callbacks", "no_submits", "fallback"):
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
            elif defect in {"no_callbacks", "no_submits"}:
                values[0]["metrics"]["completed_render_callbacks" if defect == "no_callbacks"
                                      else "webgpu_canvas_submissions"] = 0
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

    def test_composite_hover_has_its_own_rotating_pairs_and_named_ratios(self) -> None:
        values = runs(candidate_bridge="composite")
        self.assertEqual(trial_plan(3, "composite"), [(1, "persistent"), (1, "composite"),
                                                     (2, "composite"), (2, "persistent"),
                                                     (3, "persistent"), (3, "composite")])
        verify_runs(values, 3, SHA, "brush-hover", candidate_bridge="composite")
        row = compare_metrics(values, "composite")[0]
        self.assertEqual(row["medians"], {"persistent": 40, "composite": 10})
        self.assertEqual(row["candidate_over_persistent"], 0.25)
        self.assertEqual(row["candidate_bridge"], "composite")
        self.assertNotIn("webgl_over_persistent", row)
        with self.assertRaises(ValueError):
            compare_metrics(values)
        with self.assertRaises(ValueError):
            verify_runs(values, 3, SHA, "pan")

    def test_thread_cpu_comparisons_are_independent_of_task_wall_time(self) -> None:
        values = runs()
        for run in values:
            run["metrics"]["thread_cpu_ms"] = run["trial"] * (2000 if run["canvas_bridge"] == "persistent" else 100)
            run["metrics"]["thread_cpu_ms_per_operation"] = run["metrics"]["thread_cpu_ms"] / 120
        rows = {row["metric"]: row for row in compare_metrics(values)}
        self.assertEqual(rows["thread_cpu_ms"]["trial_values"],
                         {"persistent": [2000, 4000, 6000], "webgl": [100, 200, 300]})
        self.assertEqual(rows["thread_cpu_ms"]["candidate_over_persistent"], 0.05)
        self.assertAlmostEqual(rows["thread_cpu_ms_per_operation"]["candidate_over_persistent"], 0.05)
        self.assertEqual(rows["cpu_task_ms_per_operation"]["candidate_over_persistent"], 0.25)
        for invalid in (float("nan"), float("inf"), -1):
            changed = copy.deepcopy(values)
            changed[0]["metrics"]["thread_cpu_ms"] = invalid
            with self.subTest(invalid=invalid), self.assertRaisesRegex(ValueError, "finite measured"):
                compare_metrics(changed)

    def test_optional_thread_counters_require_every_paired_trial_and_preserve_old_reports(self) -> None:
        legacy = compare_metrics(runs())
        self.assertEqual({row["metric"] for row in legacy},
                         {"cpu_task_ms_per_operation", "elapsed_ms", "operations_per_second"})
        for missing_index in (0, 1):
            values = runs()
            for run in values:
                run["metrics"].update({"thread_cpu_ms": 120, "thread_cpu_ms_per_operation": 1})
            values[missing_index]["metrics"].update({"thread_cpu_ms": None, "thread_cpu_ms_per_operation": None})
            with self.subTest(missing_index=missing_index):
                self.assertEqual(compare_metrics(values), legacy)

    def test_composite_requires_actual_reuse_and_both_modes_retain_document_during_hover(self) -> None:
        original = runs(diagnostics=True, candidate_bridge="composite")
        verify_runs(original, 3, SHA, "brush-hover", diagnostics=True, candidate_bridge="composite")
        for defect in ("reuse", "persistent_document", "composite_document", "interaction", "fallback",
                       "inputs", "graphics_tail", "callback_tail"):
            values = copy.deepcopy(original)
            if defect == "reuse":
                values[1]["console"] = [entry for entry in values[1]["console"] if entry["text"] != COMPOSITE_REUSED]
            elif defect in {"persistent_document", "composite_document", "interaction"}:
                run_index = 0 if defect == "persistent_document" else 1
                phase_index = 1 if defect == "interaction" else 0
                values[run_index]["managed_render_diagnostics"]["after"]["phases"][phase_index]["count"] = (
                    0 if defect == "interaction" else 1)
            elif defect == "fallback":
                values[1]["console"].append({"text": "UnoDraw canvas bridge composite fallback: bitmap unavailable."})
            else:
                key = {"inputs": "input_events", "graphics_tail": "inputs_without_later_graphics",
                       "callback_tail": "inputs_without_completed_render_callback"}[defect]
                values[1]["metrics"][key] = 119 if defect == "inputs" else 1
            with self.subTest(defect=defect), self.assertRaises((ValueError, AssertionError)):
                verify_runs(values, 3, SHA, "brush-hover", diagnostics=True, candidate_bridge="composite")

    def test_composite_selection_preserves_odd_input_count_visual_readiness_and_source(self) -> None:
        for diagnostics in (False, True):
            original = runs(diagnostics=diagnostics, candidate_bridge="composite", workload="selection")
            verify_runs(original, 3, SHA, "selection", diagnostics=diagnostics, candidate_bridge="composite")
            for bridge in bridge_pair("composite"):
                for defect in ("source", "failed", "workload", "operations", "inputs", "graphics_tail", "callback_tail",
                               "no_visual_proof", "no_selected_pixels", "wrong_minimum", "no_restore", "stale_restore",
                               "unsettled_ruler"):
                    values = copy.deepcopy(original)
                    target = next(value for value in values if value["canvas_bridge"] == bridge)
                    if defect in {"source", "failed", "workload"}:
                        key, value = {"source": ("source_sha", "b" * 40), "failed": ("passed", False),
                                      "workload": ("workload", "brush-hover")}[defect]
                        target[key] = value
                    elif defect in {"operations", "inputs", "graphics_tail", "callback_tail"}:
                        key = {"operations": "operations", "inputs": "input_events",
                               "graphics_tail": "inputs_without_later_graphics",
                               "callback_tail": "inputs_without_completed_render_callback"}[defect]
                        target["metrics"][key] = 120 if defect in {"operations", "inputs"} else 1
                    elif defect == "no_visual_proof":
                        target.pop("quality")
                    elif defect == "no_selected_pixels":
                        target["quality"]["changed_document_pixels"] = 49
                    elif defect == "wrong_minimum":
                        target["quality"]["minimum_changed_pixels"] = 0
                    elif defect == "no_restore":
                        target["quality"].pop("restored")
                    elif defect == "stale_restore":
                        target["quality"]["restored"]["changed_pixel_fraction"] = 0.0011
                    else:
                        target["quality"]["restored"]["ruler_changed_pixel_fraction"]["horizontal"] = 0.0011
                    with self.subTest(diagnostics=diagnostics, bridge=bridge, defect=defect), self.assertRaises(ValueError):
                        verify_runs(values, 3, SHA, "selection", diagnostics=diagnostics, candidate_bridge="composite")

    def test_composite_selection_requires_retained_document_in_both_diagnostic_modes(self) -> None:
        original = runs(diagnostics=True, candidate_bridge="composite", workload="selection")
        for bridge in bridge_pair("composite"):
            for phase, count in (("Document", 1), ("Interaction", 0)):
                values = copy.deepcopy(original)
                target = next(value for value in values if value["canvas_bridge"] == bridge)
                next(value for value in target["managed_render_diagnostics"]["after"]["phases"]
                     if value["phase"] == phase)["count"] = count
                with self.subTest(bridge=bridge, phase=phase), self.assertRaises(AssertionError):
                    verify_runs(values, 3, SHA, "selection", diagnostics=True, candidate_bridge="composite")

    def test_workloads_must_exercise_the_selected_optimization(self) -> None:
        for bridge, workload in (("webgl", "pan"), ("webgl", "zoom"), ("composite", "brush-hover"), ("composite", "selection"),
                                 ("graphite", "pan"), ("graphite", "zoom")):
            verify_experiment_workload(workload, bridge)
        for bridge, workload in (("webgl", "brush-hover"), ("composite", "pan"), ("composite", "zoom"),
                                 ("webgl", "selection"), ("graphite", "selection"), ("graphite", "brush-hover"),
                                 ("persistent", "brush-hover"), ("unknown", "pan")):
            with self.subTest(bridge=bridge, workload=workload), self.assertRaises(ValueError):
                verify_experiment_workload(workload, bridge)

    def test_saved_summary_names_the_actual_pair_and_survives_partial_failure(self) -> None:
        from bridge_experiment import save_report

        for candidate_bridge, workload in (("webgl", "pan"), ("composite", "brush-hover"),
                                           ("composite", "selection"), ("graphite", "pan")):
            with self.subTest(candidate_bridge=candidate_bridge, workload=workload), tempfile.TemporaryDirectory() as temporary:
                diagnostic = runs(diagnostics=True, candidate_bridge=candidate_bridge, workload=workload)
                for run in diagnostic:
                    for phase in run["managed_render_diagnostics"]["after"]["phases"]:
                        phase.update({"totalMilliseconds": 2.5, "allocatedBytes": 512})
                    run["quality"] = {"final_capture_validation_elapsed_ms": 150.25,
                                      "restore_validation_elapsed_ms": 250.5}
                    run["graphite_queue_drain"] = ({"available": True, "result": {
                        "completed": True, "queueCount": 1, "duration_ms": 10.125, "errors": []}}
                        if run["canvas_bridge"] == "graphite" else {"available": False})
                report = {"started_unix": 0, "candidate_bridge": candidate_bridge, "candidate_source_sha": SHA,
                          "workload": workload,
                          "repeats": 3, "passed": True, "bridge_diagnostics": diagnostic,
                          "bridge_runs": runs(candidate_bridge=candidate_bridge, workload=workload),
                          "metric_comparisons": compare_metrics(
                              runs(candidate_bridge=candidate_bridge, workload=workload), candidate_bridge)}
                output = Path(temporary)
                save_report(report, output)
                text = (output / "summary.md").read_text()
                self.assertIn(f"canvasBridge=persistent/{candidate_bridge}", text)
                self.assertIn("Task wall time / operation (ms; historical key)", text)
                self.assertIn("ThreadTime separately measures", text)
                count = 121 if workload == "selection" else 120
                self.assertIn(f"| {candidate_bridge} | Interaction | {count} | 2.500 | 512 |", text)
                if workload == "selection":
                    self.assertIn("121 real alternating selection commands", text)
                if candidate_bridge == "graphite":
                    self.assertIn("| 1 | False | 120 | 240 | 0 |", text)
                    self.assertIn("| graphite | 1 | True | 150.250 | 250.500 | 10.125 |", text)
                self.assertIn("| persistent | 1 | True | 150.250 | 250.500 | n/a |", text)
                report.update({"passed": False, "failure": "Observed pixel gate failure",
                               "bridge_diagnostics": [], "metric_comparisons": []})
                save_report(report, output)
                self.assertIn("Observed pixel gate failure", (output / "summary.md").read_text())
                self.assertFalse(json.loads((output / "report.json").read_text())["passed"])

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
