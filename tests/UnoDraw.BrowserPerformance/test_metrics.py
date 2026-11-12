from __future__ import annotations

import copy
import math
import unittest

from metrics import comparisons, distribution, intervals, summarize, validate_packages


def observation() -> dict:
    return {
        "started_ms": 0, "ended_ms": 100, "raf_ms": [0, 16, 32, 48, 64, 80, 96],
        "graphics_active_intervals_ms": [], "application_raf_callback_ms": [],
        "input_to_next_graphics_ms": [], "webgpu_canvas_submit_ms": [], "webgl_flush_ms": [],
        "input_events": 0, "inputs_without_later_graphics": 0, "calls": {},
        "known_upload_bytes": 0, "uploads_with_unknown_size": 0,
    }


def package(role: str) -> dict:
    return {"role": role, "fixture_sha256": "same-fixture", "configuration": "Release",
            "execution_mode": "InterpreterAndAOT", "dotnet_sdk": "10.0.100"}


class MeasurementTests(unittest.TestCase):
    def test_idle_raf_callbacks_are_not_rendered_frames(self) -> None:
        value = summarize(observation(), {}, {}, 0)
        self.assertEqual(value["raf_interval_ms"]["count"], 6)
        self.assertEqual(value["graphics_active_animation_intervals"], 0)
        self.assertEqual(value["webgpu_canvas_submissions"], 0)
        self.assertIsNone(value["physical_presented_frames"])
        self.assertIsNone(value["operations_per_second"])
        self.assertIsNone(value["managed_allocated_bytes"])
        self.assertIsNone(value["cpu_task_ms_per_operation"])

    def test_cpu_seconds_convert_to_milliseconds_and_heap_is_a_signed_delta(self) -> None:
        value = summarize(observation(), {"TaskDuration": 2, "JSHeapUsedSize": 900},
                          {"TaskDuration": 2.03, "JSHeapUsedSize": 300}, 10)
        self.assertAlmostEqual(value["cpu_ms"]["TaskDuration"], 30)
        self.assertAlmostEqual(value["cpu_task_ms_per_operation"], 3)
        self.assertEqual(value["js_heap_used_delta_bytes"], -600)

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

    def test_comparison_rejects_changed_fixture_sdk_and_interpreter_build(self) -> None:
        packages = [package("baseline"), package("candidate"), package("reference")]
        validate_packages(packages)
        for key, value in (("fixture_sha256", "different"), ("dotnet_sdk", "10.0.200"),
                           ("execution_mode", "Interpreter"), ("configuration", "Debug")):
            changed = copy.deepcopy(packages)
            changed[1][key] = value
            with self.subTest(key=key), self.assertRaises(ValueError):
                validate_packages(changed)


if __name__ == "__main__":
    unittest.main()
