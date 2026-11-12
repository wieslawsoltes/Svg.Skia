from __future__ import annotations

import copy
import unittest

from quality import (COMPOSITE_INITIALIZED, COMPOSITE_PREFERENCE, COMPOSITE_REUSED,
                     LEGACY_PREFERENCE, PERSISTENT_INITIALIZED)
from transport_experiment_contracts import (BRIDGES, REPORT_KIND, STATES, VIEWPORT, compare_metrics,
                                            trial_plan, verify_native_pairs, verify_report_identity, verify_runs)


SHA = "a" * 40


def runs(diagnostics: bool = False) -> list[dict]:
    plan = [(1, bridge) for bridge in BRIDGES] if diagnostics else trial_plan()
    result = []
    for trial, bridge in plan:
        value = {
            "passed": True, "role": "candidate", "source_sha": SHA, "backend": "webgpu",
            "workload": "selection", "trial": trial, "canvas_bridge": bridge,
            "diagnostic_pass": diagnostics, "graphite_coverage": None,
            "page_errors": [], "resource_errors": [],
            "environment": {"viewport": VIEWPORT, "device_pixel_ratio": 1, "cross_origin_isolated": False},
            "renderer": {"negotiations": [{"provider": "WebGpuGraphicsProvider", "context": "WebGpu"}],
                         "application_contexts": [{"kind": "webgpu", "connected": True,
                                                   "target": True, "presentations": 5}]},
            "common_warmup": {"offered_pointer_moves": 120, "trusted_pointer_moves": 120,
                              "completed_graphics_callbacks": 125, "included_in_primary_metrics": False},
            "metrics": {"operations": 121, "input_events": 121, "inputs_without_later_graphics": 0,
                        "inputs_without_completed_render_callback": 0,
                        "completed_render_callbacks": 124, "webgpu_canvas_submissions": 124,
                        "graphics_api_calls": {"webgpu.canvasSubmit": 124, "webgpu.writeTexture": 120},
                        "graphite_document_submissions": None,
                        "elapsed_ms": trial * (2000 if bridge == "legacy" else 1000),
                        "operations_per_second": 121000 / (trial * (2000 if bridge == "legacy" else 1000)),
                        "cpu_task_ms_per_operation": trial * (10 if bridge == "legacy" else 5)},
            "quality": {"changed_document_pixels": 1000, "minimum_changed_pixels": 50,
                        "restored": {"changed_pixel_fraction": 0,
                                     "ruler_changed_pixel_fraction": {"horizontal": 0, "vertical": 0}}},
            "console": [{"text": LEGACY_PREFERENCE}] if bridge == "legacy" else [
                {"text": PERSISTENT_INITIALIZED}, {"text": COMPOSITE_PREFERENCE},
                {"text": COMPOSITE_INITIALIZED}, {"text": COMPOSITE_REUSED}],
            "browser_version": "143.0.7499.4", "browser_launch_arguments": ["--use-angle=swiftshader"],
        }
        if diagnostics:
            value["managed_render_diagnostics"] = {"after": {"phases": [
                {"phase": "Document", "count": 0}, {"phase": "Interaction", "count": 120}]}}
        result.append(value)
    return result


def pairs() -> list[dict]:
    return [{"trial": trial, "state": state, "passed": True, "physical_size": [1440, 960],
             "device_pixel_ratio": 1, "resized_for_comparison": False,
             "luminance_threshold": 12, "maximum_changed_fraction": 0.001,
             "changed_pixel_fractions": {"whole_frame": 0, "document": 0.0005,
                                         "horizontal_ruler": 0, "vertical_ruler": 0}}
            for trial in (1, 2, 3) for state in STATES]


class TransportExperimentTests(unittest.TestCase):
    def test_independent_identity_cannot_become_a_primary_or_other_bridge_report(self) -> None:
        original = {"report_kind": REPORT_KIND, "primary_comparison": False, "workload": "selection",
                    "bridges": list(BRIDGES), "repeats": 3, "steps": 120,
                    "viewport": VIEWPORT, "device_scale_factor": 1}
        verify_report_identity(original)
        for key, value in (("report_kind", "same_candidate_bridge_experiment"), ("primary_comparison", True),
                           ("bridges", ["persistent", "composite"]), ("workload", "pan"),
                           ("repeats", 2), ("steps", 60), ("device_scale_factor", 0.5)):
            changed = {**original, key: value}
            with self.subTest(key=key), self.assertRaises(ValueError):
                verify_report_identity(changed)

    def test_rotating_order_requires_every_pair_and_keeps_diagnostics_separate(self) -> None:
        self.assertEqual(trial_plan(), [(1, "legacy"), (1, "composite"), (2, "composite"),
                                       (2, "legacy"), (3, "legacy"), (3, "composite")])
        verify_runs(runs(), SHA)
        verify_runs(runs(True), SHA, diagnostics=True)
        for changed in (runs()[:-1], runs() + [runs()[0]], list(reversed(runs())), runs(True)):
            with self.assertRaises(ValueError):
                verify_runs(changed, SHA)
        with self.assertRaises(ValueError):
            verify_runs(runs(), SHA, diagnostics=True)

    def test_actual_mode_requires_webgpu_and_exclusive_legacy_or_composite_evidence(self) -> None:
        for defect in ("legacy_log", "persistent_leak", "composite_reuse", "fallback", "provider",
                       "no_canvas", "no_graphics", "external_copy", "graphite_submit"):
            changed = runs()
            if defect == "legacy_log":
                changed[0]["console"] = []
            elif defect == "persistent_leak":
                changed[0]["console"].append({"text": PERSISTENT_INITIALIZED})
            elif defect == "composite_reuse":
                changed[1]["console"] = [row for row in changed[1]["console"] if row["text"] != COMPOSITE_REUSED]
            elif defect == "fallback":
                changed[1]["console"].append({"text": "UnoDraw canvas bridge composite fallback: lost target."})
            elif defect == "provider":
                changed[0]["renderer"]["negotiations"][0]["provider"] = "OpenGLGraphicsProvider"
            elif defect == "no_canvas":
                changed[0]["renderer"]["application_contexts"] = []
            elif defect == "no_graphics":
                changed[0]["renderer"]["application_contexts"][0]["presentations"] = 0
            elif defect == "external_copy":
                changed[0]["metrics"]["graphics_api_calls"]["webgpu.copyExternalImageToTexture"] = 1
            else:
                changed[0]["metrics"]["graphite_document_submissions"] = 1
            with self.subTest(defect=defect), self.assertRaises((ValueError, AssertionError)):
                verify_runs(changed, SHA)

    def test_all_inputs_and_full_observed_warmup_are_required_in_both_pass_types(self) -> None:
        for diagnostics in (False, True):
            for section, key, value in (
                    ("metrics", "operations", 120), ("metrics", "input_events", 120),
                    ("metrics", "inputs_without_later_graphics", 1),
                    ("metrics", "inputs_without_completed_render_callback", 1),
                    ("metrics", "completed_render_callbacks", 0), ("metrics", "webgpu_canvas_submissions", 0),
                    ("common_warmup", "offered_pointer_moves", 119),
                    ("common_warmup", "trusted_pointer_moves", 119),
                    ("common_warmup", "completed_graphics_callbacks", 59),
                    ("common_warmup", "included_in_primary_metrics", True)):
                changed = runs(diagnostics)
                changed[0][section][key] = value
                with self.subTest(diagnostics=diagnostics, key=key), self.assertRaises(ValueError):
                    verify_runs(changed, SHA, diagnostics=diagnostics)

    def test_wrong_source_environment_browser_and_runtime_errors_are_rejected(self) -> None:
        for defect in ("source", "viewport", "dpr", "isolated", "browser", "arguments", "page", "resource"):
            changed = runs()
            if defect == "source":
                changed[0]["source_sha"] = "b" * 40
            elif defect in {"viewport", "dpr", "isolated"}:
                key, value = {"viewport": ("viewport", {"width": 720, "height": 480}),
                              "dpr": ("device_pixel_ratio", 0.5), "isolated": ("cross_origin_isolated", True)}[defect]
                changed[0]["environment"][key] = value
            elif defect == "browser":
                changed[0]["browser_version"] = "other"
            elif defect == "arguments":
                changed[0]["browser_launch_arguments"] = ["--other-backend"]
            else:
                changed[0][defect + "_errors"] = ["failed"]
            with self.subTest(defect=defect), self.assertRaises(ValueError):
                verify_runs(changed, SHA)

    def test_selection_must_be_visible_and_restore_original_rulers_and_document(self) -> None:
        for defect in ("missing", "empty", "threshold", "restore", "ruler", "nan"):
            changed = runs()
            quality = changed[0]["quality"]
            if defect == "missing":
                del quality["changed_document_pixels"]
            elif defect == "empty":
                quality["changed_document_pixels"] = 49
            elif defect == "threshold":
                quality["minimum_changed_pixels"] = 1
            elif defect == "restore":
                quality["restored"]["changed_pixel_fraction"] = 0.00101
            elif defect == "ruler":
                del quality["restored"]["ruler_changed_pixel_fraction"]["vertical"]
            else:
                quality["restored"]["changed_pixel_fraction"] = float("nan")
            with self.subTest(defect=defect), self.assertRaises(ValueError):
                verify_runs(changed, SHA)

    def test_diagnostics_prove_retained_document_without_contaminating_primary_timing(self) -> None:
        for index in (0, 1):
            changed = runs(True)
            changed[index]["managed_render_diagnostics"]["after"]["phases"][0]["count"] = 1
            with self.assertRaises(AssertionError):
                verify_runs(changed, SHA, diagnostics=True)
        primary = runs()
        primary[0]["managed_render_diagnostics"] = runs(True)[0]["managed_render_diagnostics"]
        with self.assertRaises(ValueError):
            verify_runs(primary, SHA)
        with self.assertRaises(ValueError):
            compare_metrics(runs(True))

    def test_native_comparison_rejects_missing_states_relaxed_tolerances_and_scaled_pixels(self) -> None:
        original = pairs()
        verify_native_pairs(original)
        for defect in ("missing", "duplicate", "failed", "scaled", "size", "luma", "fraction", "region", "difference"):
            changed = copy.deepcopy(original)
            if defect == "missing":
                changed.pop()
            elif defect == "duplicate":
                changed[-1] = changed[0]
            elif defect == "region":
                del changed[0]["changed_pixel_fractions"]["vertical_ruler"]
            elif defect == "difference":
                changed[0]["changed_pixel_fractions"]["document"] = 0.00101
            else:
                key, value = {"failed": ("passed", False), "scaled": ("resized_for_comparison", True),
                              "size": ("physical_size", [720, 480]), "luma": ("luminance_threshold", 20),
                              "fraction": ("maximum_changed_fraction", 0.01)}[defect]
                changed[0][key] = value
            with self.subTest(defect=defect), self.assertRaises(ValueError):
                verify_native_pairs(changed)

    def test_medians_keep_each_trial_and_reject_nonfinite_or_incomplete_measurements(self) -> None:
        rows = {row["metric"]: row for row in compare_metrics(runs())}
        self.assertEqual(rows["elapsed_ms"]["trial_values"], {"legacy": [2000, 4000, 6000],
                                                            "composite": [1000, 2000, 3000]})
        self.assertEqual(rows["elapsed_ms"]["medians"], {"legacy": 4000, "composite": 2000})
        self.assertEqual(rows["elapsed_ms"]["composite_over_legacy"], 0.5)
        self.assertNotIn("thread_cpu_ms", rows)
        for value in (float("nan"), float("inf"), -1, True, None):
            changed = runs()
            changed[0]["metrics"]["elapsed_ms"] = value
            with self.subTest(value=value), self.assertRaises(ValueError):
                compare_metrics(changed)
        with self.assertRaises(ValueError):
            compare_metrics(runs()[:-1])


if __name__ == "__main__":
    unittest.main()
