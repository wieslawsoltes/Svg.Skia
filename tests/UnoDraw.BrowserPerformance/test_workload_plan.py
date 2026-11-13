from __future__ import annotations

import unittest

from workload_plan import (BACKENDS, WORKLOADS, diagnostic_plan, primary_plan,
                           require_run_coverage, run_key)


def packages() -> list[dict]:
    return [{"role": "baseline", "runtime_generation": "uno7"},
            {"role": "candidate", "runtime_generation": "uno7", "supports_render_diagnostics": True},
            {"role": "reference", "runtime_generation": "uno6"}]


class WorkloadPlanTests(unittest.TestCase):
    def test_twelve_shards_cover_the_original_ninety_plus_twelve_exactly_once(self) -> None:
        primary = primary_plan(packages(), 3)
        diagnostics = diagnostic_plan(packages())
        self.assertEqual(len(primary), 90)
        self.assertEqual(len(diagnostics), 12)
        sharded_primary, sharded_diagnostics = [], []
        for workload in WORKLOADS:
            for backend in BACKENDS:
                shard = primary_plan(packages(), 3, (workload,), (backend,))
                diagnostic = diagnostic_plan(packages(), (workload,), (backend,))
                self.assertEqual(len(shard), 6 if backend == "webgpu" else 9)
                self.assertEqual(len(diagnostic), 1)
                for role in ("baseline", "candidate"):
                    self.assertEqual({run["trial"] for run in shard if run["role"] == role}, {1, 2, 3})
                self.assertEqual(any(run["role"] == "reference" for run in shard), backend == "skia-preferred")
                sharded_primary.extend(dict(run, passed=True) for run in shard)
                sharded_diagnostics.extend(dict(run, passed=True) for run in diagnostic)
        require_run_coverage(sharded_primary, primary, "primary")
        require_run_coverage(sharded_diagnostics, diagnostics, "diagnostic")

    def test_filtering_preserves_the_original_relative_order(self) -> None:
        full = primary_plan(packages(), 3)
        for workload in WORKLOADS:
            for backend in BACKENDS:
                expected = [run for run in full if run["workload"] == workload and run["backend"] == backend]
                self.assertEqual(primary_plan(packages(), 3, (workload,), (backend,)), expected)

    def test_rotation_keeps_every_revision_on_the_same_shard(self) -> None:
        plan = primary_plan(packages(), 3, ("pan",), ("skia-preferred",))
        self.assertEqual([run["role"] for run in plan],
                         ["baseline", "candidate", "reference", "candidate", "reference", "baseline",
                          "reference", "baseline", "candidate"])
        self.assertEqual(len(set(map(run_key, plan))), 9)

    def test_coverage_rejects_missing_duplicate_and_failed_trials(self) -> None:
        plan = primary_plan(packages(), 3, ("pan",), ("webgpu",))
        complete = [dict(run, passed=True) for run in plan]
        require_run_coverage(complete, plan, "primary")
        for value in (complete[:-1], complete + complete[:1],
                      [dict(run, passed=index != 0) for index, run in enumerate(plan)]):
            with self.subTest(value=value), self.assertRaises(ValueError):
                require_run_coverage(value, plan, "primary")

    def test_unknown_or_duplicate_selections_cannot_create_silent_missing_work(self) -> None:
        for workloads, backends in (((), BACKENDS), (("pan", "pan"), BACKENDS),
                                   (("unsupported",), BACKENDS), (WORKLOADS, ()),
                                   (WORKLOADS, ("webgpu", "webgpu")), (WORKLOADS, ("software",))):
            with self.subTest(workloads=workloads, backends=backends), self.assertRaises(ValueError):
                primary_plan(packages(), 3, workloads, backends)
        unknown = packages()
        unknown[-1]["runtime_generation"] = "unknown"
        with self.assertRaisesRegex(ValueError, "Unknown runtime"):
            primary_plan(unknown, 3)

    def test_a_candidate_without_exports_has_no_invented_diagnostic_runs(self) -> None:
        values = packages()
        values[1]["supports_render_diagnostics"] = False
        self.assertEqual(diagnostic_plan(values), [])


if __name__ == "__main__":
    unittest.main()
