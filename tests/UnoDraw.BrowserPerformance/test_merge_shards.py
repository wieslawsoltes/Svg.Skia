from __future__ import annotations

import copy
import json
import subprocess
import sys
import tempfile
import unittest
from pathlib import Path

from merge_shards import merge_reports
from metrics import comparisons, summarize
from quality import FALLBACK_PREFIX, PERSISTENT_INITIALIZED
from workload_plan import BACKENDS, WORKLOADS, diagnostic_plan, primary_plan


def packages(reference: bool = True) -> list[dict]:
    return [{"role": role, "source_sha": letter * 40, "fixture_sha256": "f" * 64,
             "configuration": "Release", "execution_mode": "InterpreterAndAOT",
             "dotnet_sdk": "10.0.403", "dotnet_workload_version": "10.0.403",
             "runtime_generation": "uno6" if role == "reference" else "uno7",
             "index_sha256": letter * 64, "supports_render_diagnostics": role == "candidate",
             "path": "/runner/packages/" + role}
            for role, letter in (("baseline", "a"), ("candidate", "b"), ("reference", "c"))
            if reference or role != "reference"]


def run(task: dict, manifests: list[dict], ordinal: int, *, diagnostic: bool = False) -> dict:
    offset = 1000 * ordinal + 100 * task["trial"]
    raw = {"started_ms": offset, "ended_ms": offset + 100,
           "raf_ms": [offset, offset + 16, offset + 32],
           "graphics_active_intervals_ms": [offset, offset + 32],
           "application_raf_callback_ms": [1, 2], "completed_render_callback_ms": [1, 2],
           "completed_render_callback_end_ms": [offset + 10, offset + 42],
           "input_to_completed_render_callback_ms": [2, 3], "input_to_next_graphics_ms": [2, 3],
           "webgpu_canvas_submit_ms": [], "webgl_flush_ms": [], "input_events": 120,
           "inputs_without_later_graphics": 0, "inputs_without_completed_render_callback": 0,
           "calls": {}, "supplied_upload_buffer_bytes": 0, "upload_calls_without_buffer_size": 0}
    cost = {"baseline": 0.1, "candidate": 0.02, "reference": 0.05}[task["role"]] * task["trial"]
    result = {**task, "passed": True, "diagnostic_pass": diagnostic,
              "source_sha": next(package["source_sha"] for package in manifests if package["role"] == task["role"]),
              "browser_version": "143.0.7499.4", "browser_launch_arguments": [task["backend"]],
              "raw_browser_observations": raw,
              "metrics": summarize(raw, {"TaskDuration": 0}, {"TaskDuration": cost},
                                   0 if task["workload"] == "idle" else 120),
              "artifact_directory": f"{task['role']}/{task['backend']}/{task['workload']}/trial-{task['trial']}",
              "console": [{"text": PERSISTENT_INITIALIZED}]}
    if diagnostic:
        result["managed_render_diagnostics"] = {"after": {"enabled": True, "phases": []}}
    return result


def fixture(backend: str, workload: str, ordinal: int, *, reference: bool = True, repeats: int = 3) -> dict:
    manifests = packages(reference)
    tasks = primary_plan(manifests, repeats, (workload,), (backend,))
    diagnostics = diagnostic_plan(manifests, (workload,), (backend,))
    runs = [run(task, manifests, ordinal) for task in tasks]
    pixels = {"changed_pixel_fraction": 0, "color_family_pixels": {"indigo": 500, "green": 500, "peach": 500},
              "ruler_changed_pixel_fraction": {"horizontal": 0, "vertical": 0},
              "chrome_font_changed_pixel_fraction": {"menu_labels": 0, "toolbar_icons": 0, "panel_icons": 0}}
    return {"schema": 1, "passed": True, "packages": manifests,
            "shard": {"backend": backend, "workload": workload},
            "selected_workloads": [workload], "selected_backends": [backend],
            "viewport": {"width": 1440, "height": 960}, "device_scale_factor": 1,
            "repeats": repeats, "steps": 120, "idle_ms": 2000,
            "warmup_policy": {"minimum_real_pan_inputs": 120, "included_in_primary_metrics": False},
            "graphics_description": "SwiftShader", "metric_limits": {"raf": "not rendered FPS"},
            "python": "3.12.16", "platform": f"runner-{ordinal}",
            "started_unix": 1_000_000 + ordinal, "duration_seconds": 60 + ordinal,
            "expected_primary_runs": len(tasks), "expected_diagnostic_runs": len(diagnostics),
            "runs": runs, "diagnostic_runs": [run(task, manifests, ordinal, diagnostic=True) for task in diagnostics],
            "comparisons": comparisons(runs),
            "cross_build_quality": [{"backend": backend, "workload": workload, "trial": trial,
                                     **{state: copy.deepcopy(pixels) for state in ("before", "after", "restored")}}
                                    for trial in range(1, repeats + 1)]}


def proof() -> dict:
    return {"package_run_id": "123", "package_source_sha": "b" * 40, "harness_head_sha": "d" * 40,
            "allowed_difference_prefixes": ["tests/", "docs/", ".github/"],
            "changed_paths": ["tests/UnoDraw.BrowserPerformance/merge_shards.py"], "production_changes": [],
            "production_source_equivalent": True, "execution_mode": "InterpreterAndAOT"}


class MergeShardTests(unittest.TestCase):
    def setUp(self) -> None:
        self.temporary = tempfile.TemporaryDirectory()
        self.addCleanup(self.temporary.cleanup)
        self.root = Path(self.temporary.name) / "shards"
        self.reports = []
        for backend in BACKENDS:
            for workload in WORKLOADS:
                path = self.root / f"artifact-{backend}-{workload}" / "report.json"
                value = fixture(backend, workload, len(self.reports))
                self.reports.append((path, value))
        self.save()

    def save(self) -> None:
        for path, value in self.reports:
            path.parent.mkdir(parents=True, exist_ok=True)
            path.write_text(json.dumps(value), encoding="utf-8")

    def test_complete_matrix_preserves_raw_timestamps_and_existing_within_runner_rows(self) -> None:
        # Trial-level and separate candidate-only reports are not root shards.
        (self.root / "report.json").write_text(json.dumps({"role": "candidate", "passed": True}))
        extra = self.root / "diagnostic" / "report.json"
        extra.parent.mkdir()
        extra.write_text(json.dumps({"schema": 1, "packages": packages(),
                                    "report_kind": "candidate_render_diagnostics", "diagnostic_run": {}}))
        for index, (_, value) in enumerate(self.reports):
            for package in value["packages"]:
                package["path"] = f"/another-runner-{index}/{package['role']}"
        self.save()
        merged, proofs = merge_reports(self.root, require_reference=True)
        ordered = [value for _, value in sorted(self.reports)]
        self.assertTrue(merged["passed"])
        self.assertEqual(90, len(merged["runs"]))
        self.assertEqual(12, len(merged["diagnostic_runs"]))
        self.assertEqual(36, len(merged["cross_build_quality"]))
        self.assertEqual([], list(proofs))
        for key in ("runs", "diagnostic_runs", "comparisons", "cross_build_quality"):
            self.assertEqual([entry for value in ordered for entry in value[key]], merged[key])
        self.assertEqual(12, len({entry["source_report"] for entry in merged["shards"]}))

    def test_explicit_reference_requirement_and_uniform_optional_reference(self) -> None:
        self.reports = [(path, fixture(value["shard"]["backend"], value["shard"]["workload"], index,
                                       reference=False)) for index, (path, value) in enumerate(self.reports)]
        self.save()
        result, _ = merge_reports(self.root)
        self.assertEqual(72, result["expected_primary_runs"])
        self.assertEqual(72, len(result["runs"]))
        with self.assertRaisesRegex(ValueError, "explicitly requires"):
            merge_reports(self.root, require_reference=True)
        path, value = self.reports[0]
        self.reports[0] = (path, fixture(value["shard"]["backend"], value["shard"]["workload"], 0))
        self.save()
        with self.assertRaisesRegex(ValueError, "inconsistent"):
            merge_reports(self.root)

    def test_manual_repetition_count_requires_the_exact_selected_plan(self) -> None:
        self.reports = [(path, fixture(value["shard"]["backend"], value["shard"]["workload"], index,
                                       repeats=2)) for index, (path, value) in enumerate(self.reports)]
        self.save()
        with self.assertRaisesRegex(ValueError, "fixed measurement settings"):
            merge_reports(self.root)
        merged, _ = merge_reports(self.root, expected_repeats=2)
        self.assertEqual(60, len(merged["runs"]))
        self.assertEqual(24, len(merged["cross_build_quality"]))
        self.assertEqual(12, len(merged["diagnostic_runs"]))
        for count in (1, 21):
            with self.subTest(count=count), self.assertRaises(ValueError):
                merge_reports(self.root, expected_repeats=count)

    def test_missing_duplicate_wrong_selection_and_failed_shards_are_rejected(self) -> None:
        path, original = self.reports[0]
        path.unlink()
        with self.assertRaisesRegex(ValueError, "12 distinct"):
            merge_reports(self.root)
        self.save()
        duplicate = self.root / "duplicate" / "report.json"
        duplicate.parent.mkdir()
        duplicate.write_text(path.read_text())
        with self.assertRaisesRegex(ValueError, "12 distinct"):
            merge_reports(self.root)
        duplicate.unlink()
        for key, bad in (("passed", False), ("schema", 2), ("selected_workloads", ["idle", "pan"]),
                         ("expected_primary_runs", 1), ("expected_diagnostic_runs", 0)):
            changed = copy.deepcopy(original)
            changed[key] = bad
            path.write_text(json.dumps(changed))
            with self.subTest(key=key), self.assertRaises(ValueError):
                merge_reports(self.root)
        self.save()

    def test_incomplete_failed_duplicate_and_misattributed_runs_are_rejected(self) -> None:
        path, original = self.reports[0]
        for group in ("runs", "diagnostic_runs"):
            for mutation in ("missing", "duplicate", "failed", "source", "pass"):
                changed = copy.deepcopy(original)
                runs = changed[group]
                if mutation == "missing":
                    runs.pop()
                elif mutation == "duplicate":
                    runs.append(copy.deepcopy(runs[0]))
                elif mutation == "failed":
                    runs[0]["passed"] = False
                elif mutation == "source":
                    runs[0]["source_sha"] = "e" * 40
                else:
                    runs[0]["diagnostic_pass"] = not runs[0]["diagnostic_pass"]
                path.write_text(json.dumps(changed))
                with self.subTest(group=group, mutation=mutation), self.assertRaises(ValueError):
                    merge_reports(self.root)

    def test_package_settings_and_browser_drift_are_rejected(self) -> None:
        path, original = self.reports[-1]
        mutations = [lambda value: value.update(steps=119),
                     lambda value: value["warmup_policy"].update(minimum_real_pan_inputs=60),
                     lambda value: value["packages"][1].update(source_sha="e" * 40),
                     lambda value: value["packages"][1].update(index_sha256="e" * 64),
                     lambda value: value["packages"][1].update(fixture_sha256="e" * 64),
                     lambda value: value["packages"][1].update(dotnet_workload_version="10.0.404"),
                     lambda value: value["packages"][1].update(execution_mode="Interpreter"),
                     lambda value: value["runs"][0].update(browser_version="different"),
                     lambda value: value["runs"][0].update(browser_launch_arguments=[])]
        for index, mutate in enumerate(mutations):
            changed = copy.deepcopy(original)
            mutate(changed)
            path.write_text(json.dumps(changed))
            with self.subTest(index=index), self.assertRaises(ValueError):
                merge_reports(self.root)

    def test_dropped_comparisons_and_failed_or_incomplete_pixel_gates_are_rejected(self) -> None:
        path, original = self.reports[0]
        mutations = [lambda value: value["comparisons"].pop(),
                     lambda value: value["comparisons"][0].update(candidate_over_baseline=42),
                     lambda value: value["cross_build_quality"].pop(),
                     lambda value: value["cross_build_quality"].append(copy.deepcopy(value["cross_build_quality"][0])),
                     lambda value: value["cross_build_quality"][0]["before"].update(changed_pixel_fraction=0.0011),
                     lambda value: value["cross_build_quality"][0]["after"]["ruler_changed_pixel_fraction"].pop("vertical"),
                     lambda value: value["cross_build_quality"][0]["restored"]["chrome_font_changed_pixel_fraction"].update(menu_labels=0.021)]
        for index, mutate in enumerate(mutations):
            changed = copy.deepcopy(original)
            mutate(changed)
            path.write_text(json.dumps(changed))
            with self.subTest(index=index), self.assertRaises(ValueError):
                merge_reports(self.root)

    def test_bridge_attribution_is_required_for_primary_and_diagnostic_passes(self) -> None:
        path, original = self.reports[0]
        for group in ("runs", "diagnostic_runs"):
            for messages in ([], [{"text": PERSISTENT_INITIALIZED}, {"text": FALLBACK_PREFIX + " failed"}]):
                changed = copy.deepcopy(original)
                candidate = next(run for run in changed[group] if run["role"] == "candidate")
                candidate["console"] = messages
                path.write_text(json.dumps(changed))
                with self.subTest(group=group, messages=messages), self.assertRaises(AssertionError):
                    merge_reports(self.root)

    def test_source_equivalence_is_copied_per_shard_and_inconsistent_or_missing_proofs_fail(self) -> None:
        for path, _ in self.reports:
            path.with_name("source-equivalence.json").write_text(json.dumps(proof()))
        merged, proofs = merge_reports(self.root)
        self.assertEqual(12, len(proofs))
        self.assertTrue(all(value == proof() for value in proofs.values()))
        self.assertTrue(all(entry["source_equivalence"] in proofs for entry in merged["shards"]))
        path = self.reports[0][0].with_name("source-equivalence.json")
        for key, bad in (("production_source_equivalent", False), ("harness_head_sha", "e" * 40),
                         ("package_source_sha", "e" * 40), ("changed_paths", ["src/actual.cs"]),
                         ("production_changes", ["src/actual.cs"])):
            changed = proof()
            changed[key] = bad
            path.write_text(json.dumps(changed))
            with self.subTest(key=key), self.assertRaises(ValueError):
                merge_reports(self.root)
        path.unlink()
        with self.assertRaisesRegex(ValueError, "source-equivalence"):
            merge_reports(self.root)

    def test_cli_failure_writes_explicit_failed_report_and_never_claims_completeness(self) -> None:
        self.reports[0][0].unlink()
        output = self.root.parent / "merged"
        result = subprocess.run([sys.executable, str(Path(__file__).with_name("merge_shards.py")),
                                 "--shards", str(self.root), "--output", str(output), "--require-reference"],
                                capture_output=True, text=True)
        self.assertEqual(1, result.returncode)
        self.assertIs(json.loads((output / "report.json").read_text())["passed"], False)
        self.assertIn("No complete performance result is claimed", (output / "summary.md").read_text())

    def test_nonfinite_observations_cannot_pass_through_json(self) -> None:
        path, value = self.reports[0]
        for bad in ("NaN", "Infinity", "1e999"):
            text = json.dumps(value).replace('"started_ms": 100', '"started_ms": ' + bad, 1)
            path.write_text(text)
            with self.subTest(value=bad), self.assertRaisesRegex(ValueError, "Nonfinite"):
                merge_reports(self.root)


if __name__ == "__main__":
    unittest.main()
