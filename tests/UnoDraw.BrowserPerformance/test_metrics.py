from __future__ import annotations

import copy
import math
import unittest

from metrics import comparisons, distribution, intervals, summarize, validate_packages, verify_hover_isolation


def observation() -> dict:
    return {
        "started_ms": 0, "ended_ms": 100, "raf_ms": [0, 16, 32, 48, 64, 80, 96],
        "graphics_active_intervals_ms": [], "application_raf_callback_ms": [],
        "input_to_next_graphics_ms": [], "webgpu_canvas_submit_ms": [], "webgl_flush_ms": [],
        "completed_render_callback_ms": [], "completed_render_callback_end_ms": [],
        "input_to_completed_render_callback_ms": [], "inputs_without_completed_render_callback": 0,
        "input_events": 0, "inputs_without_later_graphics": 0, "calls": {},
        "supplied_upload_buffer_bytes": 0, "upload_calls_without_buffer_size": 0,
    }


def package(role: str) -> dict:
    return {"role": role, "fixture_sha256": "same-fixture", "configuration": "Release",
            "execution_mode": "InterpreterAndAOT", "dotnet_sdk": "10.0.403",
            "dotnet_workload_version": "10.0.403"}


class MeasurementTests(unittest.TestCase):
    def test_idle_raf_callbacks_are_not_rendered_frames(self) -> None:
        value = summarize(observation(), {}, {}, 0)
        self.assertEqual(value["raf_interval_ms"]["count"], 6)
        self.assertEqual(value["graphics_active_animation_intervals"], 0)
        self.assertEqual(value["webgpu_canvas_submissions"], 0)
        self.assertEqual(value["completed_render_callbacks"], 0)
        self.assertIsNone(value["physical_presented_frames"])
        self.assertIsNone(value["operations_per_second"])
        self.assertIsNone(value["managed_allocated_bytes"])
        self.assertIsNone(value["cpu_task_ms_per_operation"])

    def test_task_seconds_convert_to_milliseconds_and_heap_is_a_signed_delta(self) -> None:
        value = summarize(observation(), {"TaskDuration": 2, "JSHeapUsedSize": 900},
                          {"TaskDuration": 2.03, "JSHeapUsedSize": 300}, 10)
        self.assertAlmostEqual(value["cpu_ms"]["TaskDuration"], 30)
        self.assertAlmostEqual(value["cpu_task_ms_per_operation"], 3)
        self.assertEqual(value["js_heap_used_delta_bytes"], -600)

    def test_task_wall_duration_and_thread_cpu_use_independent_counters(self) -> None:
        value = summarize(observation(), {"TaskDuration": 2, "ThreadTime": 1},
                          {"TaskDuration": 2.03, "ThreadTime": 1.005}, 10)
        self.assertAlmostEqual(value["cpu_ms"]["TaskDuration"], 30)
        self.assertAlmostEqual(value["cpu_task_ms_per_operation"], 3)
        self.assertAlmostEqual(value["thread_cpu_ms"], 5)
        self.assertAlmostEqual(value["thread_cpu_ms_per_operation"], 0.5)

    def test_missing_thread_counter_is_unavailable_without_substituting_task_time(self) -> None:
        for before_thread, after_thread in ((None, 2), (1, None), (None, None)):
            before, after = {"TaskDuration": 2}, {"TaskDuration": 2.03}
            if before_thread is not None:
                before["ThreadTime"] = before_thread
            if after_thread is not None:
                after["ThreadTime"] = after_thread
            with self.subTest(before=before_thread, after=after_thread):
                value = summarize(observation(), before, after, 10)
                self.assertAlmostEqual(value["cpu_task_ms_per_operation"], 3)
                self.assertIsNone(value["thread_cpu_ms"])
                self.assertIsNone(value["thread_cpu_ms_per_operation"])

    def test_zero_thread_cpu_and_idle_cpu_are_distinct_from_missing(self) -> None:
        value = summarize(observation(), {"ThreadTime": 1}, {"ThreadTime": 1}, 10)
        self.assertEqual(value["thread_cpu_ms"], 0)
        self.assertEqual(value["thread_cpu_ms_per_operation"], 0)
        idle = summarize(observation(), {"ThreadTime": 1}, {"ThreadTime": 1.002}, 0)
        self.assertAlmostEqual(idle["thread_cpu_ms"], 2)
        self.assertIsNone(idle["thread_cpu_ms_per_operation"])

    def test_thread_cpu_rejects_invalid_counter_deltas(self) -> None:
        for before, after in ((2, 1), (math.nan, 1), (1, math.nan), (1, math.inf),
                              (-math.inf, 1), (math.inf, math.inf)):
            with self.subTest(before=before, after=after), self.assertRaisesRegex(ValueError, "ThreadTime"):
                summarize(observation(), {"ThreadTime": before}, {"ThreadTime": after}, 10)

    def test_percentiles_keep_empty_data_distinct_from_zero(self) -> None:
        self.assertEqual(distribution([]), {"count": 0, "p50": None, "p95": None, "p99": None, "max": None})
        self.assertEqual(distribution([0])["p95"], 0)
        self.assertEqual(distribution([0, 10, 20])["p95"], 19)

    def test_bad_time_data_is_rejected(self) -> None:
        for values in ([1, math.nan], [math.inf], [-1]):
            with self.subTest(values=values), self.assertRaises(ValueError):
                distribution(values)
        with self.assertRaises(ValueError):
            intervals([3, 2])
        sample = observation()
        sample["ended_ms"] = 0
        with self.assertRaises(ValueError):
            summarize(sample, {}, {}, 1)

    def test_backend_and_workload_are_not_pooled(self) -> None:
        runs = []
        for role, costs in (("baseline", [1, 2, 100]), ("candidate", [1, 1, 4])):
            for cost in costs:
                value = summarize(observation(), {"TaskDuration": 0}, {"TaskDuration": cost / 1000}, 1)
                runs.append({"role": role, "backend": "webgpu", "workload": "pan", "metrics": value})
        # A fast reference on a different backend cannot stand in for WebGPU.
        runs.append({"role": "reference", "backend": "skia-preferred", "workload": "pan",
                     "metrics": summarize(observation(), {}, {}, 0)})
        rows = comparisons(runs)
        cpu = next(row for row in rows if row["metric"] == "cpu_task_ms_per_operation")
        self.assertEqual(cpu["baseline_median"], 2)
        self.assertEqual(cpu["candidate_median"], 1)
        self.assertEqual(cpu["candidate_over_baseline"], 0.5)
        self.assertFalse(any(row["baseline_role"] == "reference" for row in rows))

    def test_required_baseline_cannot_be_replaced_by_reference(self) -> None:
        with self.assertRaisesRegex(ValueError, "Both baseline"):
            validate_packages([package("reference"), package("candidate")])

    def test_thread_cpu_comparisons_preserve_trial_values_and_use_trial_medians(self) -> None:
        runs = []
        for role, costs in (("baseline", [20, 40, 200]), ("candidate", [10, 20, 100])):
            for cost in costs:
                value = summarize(observation(), {"ThreadTime": 0}, {"ThreadTime": cost / 1000}, 10)
                runs.append({"role": role, "backend": "webgpu", "workload": "pan", "metrics": value})
        rows = {row["metric"]: row for row in comparisons(runs)}
        self.assertEqual(rows["thread_cpu_ms"]["baseline_trials"], [20, 40, 200])
        self.assertEqual(rows["thread_cpu_ms"]["candidate_trials"], [10, 20, 100])
        self.assertEqual(rows["thread_cpu_ms"]["baseline_median"], 40)
        self.assertEqual(rows["thread_cpu_ms"]["candidate_median"], 20)
        self.assertEqual(rows["thread_cpu_ms"]["candidate_over_baseline"], 0.5)
        self.assertEqual(rows["thread_cpu_ms_per_operation"]["baseline_median"], 4)
        self.assertEqual(rows["thread_cpu_ms_per_operation"]["candidate_median"], 2)
        for missing_role in ("baseline", "candidate"):
            incomplete = copy.deepcopy(runs)
            target = next(run for run in incomplete if run["role"] == missing_role)
            target["metrics"]["thread_cpu_ms"] = None
            target["metrics"]["thread_cpu_ms_per_operation"] = None
            with self.subTest(missing_role=missing_role):
                self.assertFalse(any(row["metric"].startswith("thread_cpu") for row in comparisons(incomplete)))

    def test_legacy_reports_remain_comparable_without_synthetic_cpu_data(self) -> None:
        runs = [{"role": role, "backend": "webgpu", "workload": "pan",
                 "metrics": summarize(observation(), {"TaskDuration": 0}, {"TaskDuration": cost}, 10)}
                for role, cost in (("baseline", 0.04), ("candidate", 0.02))]
        with_nulls = comparisons(runs)
        for run in runs:
            del run["metrics"]["thread_cpu_ms"]
            del run["metrics"]["thread_cpu_ms_per_operation"]
        self.assertEqual(comparisons(runs), with_nulls)
        self.assertFalse(any(row["metric"].startswith("thread_cpu") for row in with_nulls))
        task = next(row for row in with_nulls if row["metric"] == "cpu_task_ms_per_operation")
        self.assertEqual(task["candidate_over_baseline"], 0.5)

    def test_hover_isolation_rejects_redrawn_artwork_and_missing_overlay(self) -> None:
        def snapshot(document: int, interaction: int) -> dict:
            return {"phases": [{"phase": "Document", "count": document},
                               {"phase": "Interaction", "count": interaction}]}
        verify_hover_isolation(snapshot(0, 120))
        for document, interaction in ((1, 120), (0, 0), (1, 0)):
            with self.subTest(document=document, interaction=interaction), self.assertRaises(AssertionError):
                verify_hover_isolation(snapshot(document, interaction))

    def test_comparison_rejects_changed_fixture_toolchain_and_interpreter_build(self) -> None:
        packages = [package("baseline"), package("candidate"), package("reference")]
        validate_packages(packages)
        for key, value in (("fixture_sha256", "different"), ("dotnet_sdk", "10.0.200"),
                           ("dotnet_workload_version", "10.0.404"), ("dotnet_workload_version", None),
                           ("execution_mode", "Interpreter"), ("configuration", "Debug")):
            changed = copy.deepcopy(packages)
            changed[1][key] = value
            with self.subTest(key=key), self.assertRaises(ValueError):
                validate_packages(changed)

    def test_legacy_packages_without_workload_provenance_are_rejected(self) -> None:
        packages = [package("baseline"), package("candidate")]
        for value in packages:
            del value["dotnet_workload_version"]
        with self.assertRaisesRegex(ValueError, "record their exact"):
            validate_packages(packages)


if __name__ == "__main__":
    unittest.main()
