from __future__ import annotations

import copy
import json
import re
import tempfile
import unittest
from pathlib import Path

from bridge_experiment import save_report
from bridge_experiment_contracts import (LEGACY_COMPOSITION_REPORT_KIND, bridge_pair, compare_metrics,
                                         report_kind, trial_plan, verify_experiment_workload,
                                         verify_report_identity, verify_runs)
from quality import (COMPOSITE_INITIALIZED, COMPOSITE_PREFERENCE, COMPOSITE_REUSED,
                     GRAPHITE_COVERAGE_INITIALIZED, GRAPHITE_INITIALIZED, GRAPHITE_PREFERENCE,
                     LEGACY_COMPOSITE_FALLBACK_PREFIX, LEGACY_COMPOSITE_INITIALIZED,
                     LEGACY_COMPOSITE_PREFERENCE, LEGACY_COMPOSITE_REUSED, LEGACY_PREFERENCE,
                     PERSISTENT_INITIALIZED, STATE_NAMES, WEBGL_INITIALIZED, WEBGL_PREFERENCE,
                     compare_bridge_states, matrix_failures, verify_bridge)
from test_bridge_experiment import SHA, runs as existing_runs


MESSAGES = [LEGACY_COMPOSITE_PREFERENCE, LEGACY_COMPOSITE_INITIALIZED, LEGACY_COMPOSITE_REUSED]


def runs(*, workload: str = "selection", diagnostics: bool = False) -> list[dict]:
    values = existing_runs(candidate_bridge="composite", workload=workload, diagnostics=diagnostics)
    for run in values:
        run["canvas_bridge"] = "legacy" if run["canvas_bridge"] == "persistent" else "legacy-composite"
        messages = [LEGACY_PREFERENCE] if run["canvas_bridge"] == "legacy" else MESSAGES
        run["console"] = [{"text": text} for text in messages]
        run.update({"role": "candidate", "page_errors": [], "resource_errors": [],
                    "environment": {"viewport": {"width": 1440, "height": 960},
                                    "device_pixel_ratio": 1, "cross_origin_isolated": False},
                    "renderer": {"negotiations": [{"provider": "WebGpuGraphicsProvider", "context": "WebGpu"}],
                                 "application_contexts": [{"kind": "webgpu", "connected": True,
                                                           "target": True, "presentations": 10}]},
                    "common_warmup": {"offered_pointer_moves": 120, "trusted_pointer_moves": 120,
                                      "completed_graphics_callbacks": 60, "included_in_primary_metrics": False},
                    "browser_version": "143.0.7499.4", "browser_launch_arguments": ["--enable-unsafe-webgpu"]})
        if not diagnostics:
            del run["managed_render_diagnostics"]
    return values


class LegacyCompositionTests(unittest.TestCase):
    def test_workflow_declares_unique_artifacts_for_every_experiment_and_keeps_legacy_names(self) -> None:
        workflow = (Path(__file__).resolve().parents[2] / ".github/workflows/unodraw-performance.yml").read_text()
        job = workflow.split("\n  document-raster-experiment:\n", 1)[1].split("\n  cross-build-quality-interpreter:\n", 1)[0]
        # The workflow deliberately uses literal flow mappings here, so artifact
        # uniqueness is checked without introducing a second YAML dependency.
        matrix = job.split("      matrix:\n", 1)[1].split("    steps:\n", 1)[0]
        entries = [dict(tuple(part.strip() for part in item.split(":", 1)) for item in row.split(","))
                   for row in re.findall(r"^\s+- \{([^{}\n]+)\}\s*$", matrix, re.MULTILINE)]
        expected_pairs = {(bridge, workload) for bridge in ("composite", "legacy-composite")
                          for workload in ("brush-hover", "selection")}
        self.assertEqual(len(entries), len(expected_pairs))
        self.assertEqual({(entry["bridge"], entry["workload"]) for entry in entries}, expected_pairs)
        self.assertEqual(len({entry["artifact"] for entry in entries}), len(entries))
        self.assertIn("name: ${{ matrix.artifact }}", job)
        for entry in entries:
            expected = (f"unodraw-document-raster-experiment-{entry['workload']}" if entry["bridge"] == "composite"
                        else f"unodraw-{entry['variant']}-document-raster-experiment-{entry['workload']}")
            self.assertEqual(entry["artifact"], expected)
        self.assertEqual({entry["workload"] for entry in entries if entry["bridge"] == "legacy-composite"},
                         {"brush-hover", "selection"})

    def test_completed_original_host_copy_and_actual_cache_reuse_are_separate_requirements(self) -> None:
        initial = [{"text": text} for text in MESSAGES[:2]]
        verify_bridge(initial, "legacy-composite", require_reuse=False)
        with self.assertRaisesRegex(AssertionError, "reused cached"):
            verify_bridge(initial, "legacy-composite")
        complete = [{"text": text} for text in MESSAGES]
        verify_bridge(complete, "legacy-composite")
        for missing in MESSAGES[:2]:
            with self.subTest(missing=missing), self.assertRaises(AssertionError):
                verify_bridge([entry for entry in complete if entry["text"] != missing],
                              "legacy-composite", require_reuse=False)

    def test_competing_uploaders_and_every_mode_abandonment_are_rejected_in_both_directions(self) -> None:
        other_modes = {"legacy": [LEGACY_PREFERENCE], "persistent": [PERSISTENT_INITIALIZED],
                       "composite": [COMPOSITE_PREFERENCE, COMPOSITE_INITIALIZED,
                                     COMPOSITE_REUSED, PERSISTENT_INITIALIZED],
                       "graphite": [GRAPHITE_PREFERENCE, GRAPHITE_INITIALIZED, PERSISTENT_INITIALIZED],
                       "webgl": [WEBGL_PREFERENCE, WEBGL_INITIALIZED, PERSISTENT_INITIALIZED]}
        for bridge, messages in other_modes.items():
            for extra in messages:
                with self.subTest(bridge=bridge, extra=extra), self.assertRaises(AssertionError):
                    verify_bridge([{"text": text} for text in MESSAGES + [extra]], "legacy-composite")
            for extra in MESSAGES + [LEGACY_COMPOSITE_FALLBACK_PREFIX + " target unavailable."]:
                with self.subTest(bridge=bridge, extra=extra), self.assertRaises(AssertionError):
                    verify_bridge([{"text": text} for text in messages + [extra]], bridge)
        for extra in (LEGACY_COMPOSITE_FALLBACK_PREFIX + " copy failed.",
                      "UnoDraw canvas bridge fallback: target unavailable.",
                      "UnoDraw canvas bridge composite fallback: no cache.",
                      "UnoDraw WebGL canvas fallback: context lost.",
                      "UnoDraw Graphite canvas fallback: submit failed.",
                      *GRAPHITE_COVERAGE_INITIALIZED.values()):
            with self.subTest(extra=extra), self.assertRaises(AssertionError):
                verify_bridge([{"text": text} for text in MESSAGES + [extra]], "legacy-composite")

    def test_welcome_still_requires_all_six_cases_and_thirty_native_pairs(self) -> None:
        cases = [{"bridge": bridge, "dpr": dpr, "passed": True,
                  "states": [{"name": name} for name in STATE_NAMES]}
                 for bridge in bridge_pair("legacy-composite") for dpr in (1, 1.25, 2)]
        pairs = []
        for dpr in (1, 1.25, 2):
            pair = {case["bridge"]: case for case in cases if case["dpr"] == dpr}
            pairs += compare_bridge_states(pair, dpr, lambda before, after: {}, candidate_bridge="legacy-composite")
        self.assertEqual((len(cases), len(pairs)), (6, 30))
        self.assertEqual(matrix_failures(cases, pairs, (1, 1.25, 2), candidate_bridge="legacy-composite"), [])
        self.assertTrue(matrix_failures(cases, pairs[:-1], (1, 1.25, 2), candidate_bridge="legacy-composite"))
        self.assertTrue(matrix_failures(cases, pairs, (1, 1.25, 2), candidate_bridge="composite"))

    def test_report_identity_and_ratios_cannot_be_confused_with_the_persistent_experiment(self) -> None:
        self.assertEqual(bridge_pair("legacy-composite"), ("legacy", "legacy-composite"))
        self.assertEqual(trial_plan(3, "legacy-composite"), [(1, "legacy"), (1, "legacy-composite"),
                                                          (2, "legacy-composite"), (2, "legacy"),
                                                          (3, "legacy"), (3, "legacy-composite")])
        identity = {"candidate_bridge": "legacy-composite", "report_kind": LEGACY_COMPOSITION_REPORT_KIND,
                    "primary_comparison": False, "bridges": ["legacy", "legacy-composite"],
                    "repeats": 3, "steps": 120, "workload": "selection", "harness_source_sha": SHA,
                    "viewport": {"width": 1440, "height": 960}, "device_scale_factor": 1}
        verify_report_identity(identity)
        self.assertNotEqual(report_kind("legacy-composite"), report_kind("composite"))
        for key, value in (("report_kind", "same_candidate_bridge_experiment"), ("primary_comparison", True),
                           ("bridges", ["persistent", "legacy-composite"]), ("harness_source_sha", "HEAD"),
                           ("repeats", 2), ("steps", 60), ("device_scale_factor", 2), ("workload", "pan")):
            with self.subTest(key=key), self.assertRaises(ValueError):
                verify_report_identity({**identity, key: value})
        for repeats in (2, 4):
            with self.assertRaises(ValueError):
                trial_plan(repeats, "legacy-composite")
        for workload in ("pan", "zoom"):
            with self.assertRaises(ValueError):
                verify_experiment_workload(workload, "legacy-composite")
        rows = compare_metrics(runs(), "legacy-composite")
        self.assertEqual(rows[0]["medians"], {"legacy": 40, "legacy-composite": 10})
        self.assertEqual(rows[0]["candidate_over_legacy"], 0.25)
        self.assertNotIn("candidate_over_persistent", rows[0])
        for values in (runs()[:-1], list(reversed(runs())), runs(diagnostics=True)):
            with self.assertRaises(ValueError):
                compare_metrics(values, "legacy-composite")

    def test_hover_and_selection_keep_the_full_common_warmup_and_exact_input_windows(self) -> None:
        for workload in ("brush-hover", "selection"):
            for diagnostics in (False, True):
                original = runs(workload=workload, diagnostics=diagnostics)
                verify_runs(original, 3, SHA, workload, diagnostics=diagnostics, candidate_bridge="legacy-composite")
                for defect in ("order", "missing", "source", "role", "errors", "dpr", "provider", "canvas",
                               "warmup", "trusted_warmup", "warmup_callbacks", "warmup_in_timing", "input",
                               "tail", "callback_tail", "callbacks", "submissions", "external_copy",
                               "graphite", "browser", "launch", "reuse"):
                    changed = copy.deepcopy(original)
                    target = changed[0]
                    if defect == "order":
                        changed.reverse()
                    elif defect == "missing":
                        changed.pop()
                    elif defect in {"source", "role", "errors", "browser", "launch"}:
                        key, value = {"source": ("source_sha", "b" * 40), "role": ("role", "baseline"),
                                      "errors": ("page_errors", ["failure"]), "browser": ("browser_version", "144"),
                                      "launch": ("browser_launch_arguments", ["--different"])}[defect]
                        target[key] = value
                    elif defect == "dpr":
                        target["environment"]["device_pixel_ratio"] = 2
                    elif defect == "provider":
                        target["renderer"]["negotiations"].clear()
                    elif defect == "canvas":
                        target["renderer"]["application_contexts"][0]["connected"] = False
                    elif defect in {"warmup", "trusted_warmup", "warmup_callbacks", "warmup_in_timing"}:
                        key, value = {"warmup": ("offered_pointer_moves", 60), "trusted_warmup": ("trusted_pointer_moves", 119),
                                      "warmup_callbacks": ("completed_graphics_callbacks", 59),
                                      "warmup_in_timing": ("included_in_primary_metrics", True)}[defect]
                        target["common_warmup"][key] = value
                    elif defect == "external_copy":
                        target["metrics"]["graphics_api_calls"]["webgpu.copyExternalImageToTexture"] = 1
                    elif defect == "graphite":
                        target["metrics"]["graphite_document_submissions"] = 1
                    elif defect == "reuse":
                        target = next(run for run in changed if run["canvas_bridge"] == "legacy-composite")
                        target["console"] = [entry for entry in target["console"] if entry["text"] != LEGACY_COMPOSITE_REUSED]
                    else:
                        key, value = {"input": ("input_events", 119), "tail": ("inputs_without_later_graphics", 1),
                                      "callback_tail": ("inputs_without_completed_render_callback", 1),
                                      "callbacks": ("completed_render_callbacks", 0),
                                      "submissions": ("webgpu_canvas_submissions", 0)}[defect]
                        target["metrics"][key] = value
                    with self.subTest(workload=workload, diagnostics=diagnostics, defect=defect), self.assertRaises((ValueError, AssertionError)):
                        verify_runs(changed, 3, SHA, workload, diagnostics=diagnostics, candidate_bridge="legacy-composite")

    def test_diagnostics_are_separate_and_both_modes_retain_the_document(self) -> None:
        for workload in ("brush-hover", "selection"):
            diagnostic = runs(workload=workload, diagnostics=True)
            for bridge in bridge_pair("legacy-composite"):
                for phase, count in (("Document", 1), ("Interaction", 0)):
                    changed = copy.deepcopy(diagnostic)
                    target = next(run for run in changed if run["canvas_bridge"] == bridge)
                    next(row for row in target["managed_render_diagnostics"]["after"]["phases"]
                         if row["phase"] == phase)["count"] = count
                    with self.subTest(workload=workload, bridge=bridge, phase=phase), self.assertRaises(AssertionError):
                        verify_runs(changed, 3, SHA, workload, diagnostics=True, candidate_bridge="legacy-composite")
            for key in ("managed_render_diagnostics", "runtime_phase_logs"):
                primary = runs(workload=workload)
                primary[0][key] = {}
                with self.assertRaises(ValueError):
                    verify_runs(primary, 3, SHA, workload, candidate_bridge="legacy-composite")

    def test_selection_requires_real_selected_pixels_and_the_original_restore_gate(self) -> None:
        for bridge in bridge_pair("legacy-composite"):
            for defect in ("changed", "minimum", "document", "ruler", "nan"):
                changed = runs()
                target = next(run for run in changed if run["canvas_bridge"] == bridge)["quality"]
                if defect == "changed":
                    target["changed_document_pixels"] = 49
                elif defect == "minimum":
                    target["minimum_changed_pixels"] = 0
                elif defect == "document":
                    target["restored"]["changed_pixel_fraction"] = 0.0011
                else:
                    target["restored"]["ruler_changed_pixel_fraction"]["horizontal"] = float("nan") if defect == "nan" else 0.0011
                with self.subTest(bridge=bridge, defect=defect), self.assertRaises(ValueError):
                    verify_runs(changed, 3, SHA, "selection", candidate_bridge="legacy-composite")

    def test_summary_names_the_original_uploader_control_and_excludes_baseline_recovery(self) -> None:
        for workload in ("brush-hover", "selection"):
            report = {"started_unix": 0, "candidate_bridge": "legacy-composite", "candidate_source_sha": SHA,
                      "harness_source_sha": "b" * 40, "workload": workload, "repeats": 3, "passed": True,
                      "bridge_runs": runs(workload=workload), "bridge_diagnostics": [],
                      "metric_comparisons": compare_metrics(runs(workload=workload), "legacy-composite")}
            with tempfile.TemporaryDirectory() as temporary:
                output = Path(temporary)
                save_report(report, output)
                text = (output / "summary.md").read_text()
                self.assertIn("canvasBridge=legacy/legacy-composite", text)
                self.assertIn("| Metric | legacy median | legacy-composite median | Candidate / legacy |", text)
                self.assertIn("does not establish recovery against the older baseline", text)
                self.assertNotIn("Candidate / persistent", text)
                if workload == "selection":
                    self.assertIn("121 real alternating selection commands", text)
                report.update({"passed": False, "failure": "No completed host copy", "metric_comparisons": []})
                save_report(report, output)
                self.assertFalse(json.loads((output / "report.json").read_text())["passed"])
                self.assertIn("No completed host copy", (output / "summary.md").read_text())


if __name__ == "__main__":
    unittest.main()
