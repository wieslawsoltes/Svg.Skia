from __future__ import annotations

import copy
import subprocess
import sys
import tempfile
import unittest
from itertools import product
from pathlib import Path
from unittest.mock import MagicMock

from PIL import Image, ImageDraw

from quality import (COMPOSITE_INITIALIZED, COMPOSITE_PREFERENCE, COMPOSITE_REUSED,
                     GRAPHITE_COVERAGE_INITIALIZED,
                     GRAPHITE_INITIALIZED, GRAPHITE_PREFERENCE,
                     LEGACY_PREFERENCE, PERSISTENT_INITIALIZED, WEBGL_INITIALIZED, WEBGL_PREFERENCE,
                     assert_native_size, bridge_query, compare_bridge_states,
                     is_welcome_green, matrix_failures, raw_comparison, verify_bridge)


class WelcomeReadinessTests(unittest.TestCase):
    # Welcome artwork colors, followed by their documented viewport-tinted colors.
    PALETTE = ((111, 157, 145), (66, 125, 115), (167, 195, 187),
               (115, 161, 156), (75, 132, 129), (165, 195, 193))

    def test_artwork_and_viewport_tinted_palette_are_recognized(self) -> None:
        for pixel in self.PALETTE:
            with self.subTest(pixel=pixel):
                self.assertTrue(is_welcome_green(pixel))

    def test_rounding_tolerance_includes_two_levels_in_each_channel(self) -> None:
        for color in self.PALETTE:
            for offsets in product(range(-2, 3), repeat=3):
                pixel = tuple(channel + offset for channel, offset in zip(color, offsets))
                with self.subTest(color=color, offsets=offsets):
                    self.assertTrue(is_welcome_green(pixel))

    def test_three_level_error_in_any_channel_is_rejected(self) -> None:
        for color in self.PALETTE:
            for channel in range(3):
                for offset in (-3, 3):
                    pixel = tuple(value + (offset if index == channel else 0)
                                  for index, value in enumerate(color))
                    with self.subTest(color=color, channel=channel, offset=offset):
                        self.assertFalse(is_welcome_green(pixel))

    def test_blank_chrome_and_unrelated_greens_cannot_prove_readiness(self) -> None:
        rejected = ((0, 0, 0), (255, 255, 255), (41, 41, 41), (128, 128, 128),
                    (190, 190, 190), (148, 191, 242), (131, 176, 255), (54, 106, 204),
                    (0, 255, 0), (0, 128, 0), (64, 200, 80), (70, 160, 90),
                    (170, 210, 100), (140, 180, 175), (111, 195, 193))
        for pixel in rejected:
            with self.subTest(pixel=pixel):
                self.assertFalse(is_welcome_green(pixel))


class QualityMatrixTests(unittest.TestCase):
    def test_opt_in_bridge_requires_its_own_complete_matrix(self) -> None:
        dprs, states = (1, 1.25, 2), ("desktop", "restored")
        for candidate_bridge in ("webgl", "composite", "legacy-composite", "graphite"):
            cases = [{"bridge": bridge, "dpr": dpr, "passed": True,
                      "states": [{"name": name} for name in states]}
                     for bridge in ("legacy", candidate_bridge) for dpr in dprs]
            rows = [{"device_pixel_ratio": dpr, "state": name, "passed": True}
                    for dpr in dprs for name in states]
            self.assertEqual(matrix_failures(cases, rows, dprs, states, candidate_bridge=candidate_bridge), [])
            self.assertTrue(matrix_failures(cases, rows, dprs, states))
            paired = {case["bridge"]: case for case in cases if case["dpr"] == 1.25}
            comparisons = compare_bridge_states(paired, 1.25, lambda first, second: {}, states,
                                                 candidate_bridge=candidate_bridge)
            self.assertTrue(all(row["passed"] for row in comparisons))
            paired[candidate_bridge]["states"].pop()
            comparisons = compare_bridge_states(paired, 1.25, lambda first, second: {}, states,
                                                 candidate_bridge=candidate_bridge)
            self.assertEqual(comparisons[-1]["missing_bridges"], [candidate_bridge])
        with self.assertRaises(ValueError):
            matrix_failures(cases, rows, dprs, states, candidate_bridge="legacy")

    def test_failed_state_does_not_hide_later_comparisons_or_missing_states(self) -> None:
        names = ("first", "different", "later", "missing")
        cases = {bridge: {"states": [{"name": name} for name in names[:-1]]}
                 for bridge in ("legacy", "persistent")}
        visited = []

        def compare(before, after):
            visited.append(before["name"])
            if before["name"] == "different":
                raise AssertionError("Original pixel threshold exceeded")
            return {"pixels": "equal"}

        rows = compare_bridge_states(cases, 1.25, compare, names)
        self.assertEqual(visited, list(names[:-1]))
        self.assertEqual([row["passed"] for row in rows], [True, False, True, False])
        self.assertIn("threshold exceeded", rows[1]["failure"])
        self.assertEqual(rows[-1]["missing_bridges"], ["legacy", "persistent"])

    def test_only_a_complete_successful_six_case_matrix_can_pass(self) -> None:
        dprs, states = (1, 1.25, 2), ("desktop", "restored")
        cases = [{"bridge": bridge, "dpr": dpr, "passed": True,
                  "states": [{"name": name} for name in states]}
                 for bridge in ("legacy", "persistent") for dpr in dprs]
        rows = [{"device_pixel_ratio": dpr, "state": name, "passed": True}
                for dpr in dprs for name in states]
        self.assertEqual(matrix_failures(cases, rows, dprs, states), [])
        for defect in ("failed_case", "missing_case", "duplicate_case", "missing_state",
                       "failed_comparison", "missing_comparison"):
            changed_cases, changed_rows = copy.deepcopy(cases), copy.deepcopy(rows)
            if defect == "failed_case":
                changed_cases[0]["passed"] = False
            elif defect == "missing_case":
                changed_cases.pop()
            elif defect == "duplicate_case":
                changed_cases[-1] = copy.deepcopy(changed_cases[0])
            elif defect == "missing_state":
                changed_cases[0]["states"].pop()
            elif defect == "failed_comparison":
                changed_rows[0]["passed"] = False
            else:
                changed_rows.pop()
            with self.subTest(defect=defect):
                self.assertTrue(matrix_failures(changed_cases, changed_rows, dprs, states))


class BridgeEvidenceTests(unittest.TestCase):
    def test_explicit_graphite_coverage_requires_exclusive_actual_context_evidence(self) -> None:
        base = [{"text": text} for text in (GRAPHITE_PREFERENCE, GRAPHITE_INITIALIZED, PERSISTENT_INITIALIZED)]
        # Unspecified coverage remains compatible with artifacts from before the
        # selector existed, but cannot turn an explicit request into an assumption.
        verify_bridge(base, "graphite")
        for coverage, message in GRAPHITE_COVERAGE_INITIALIZED.items():
            evidence = base + [{"text": message}, {"text": message}]
            verify_bridge(evidence, "graphite", graphite_coverage=coverage)
            verify_bridge(evidence, "graphite")
            other = "raster" if coverage == "default" else "default"
            for changed in (base, base + [{"text": GRAPHITE_COVERAGE_INITIALIZED[other]}],
                            evidence + [{"text": GRAPHITE_COVERAGE_INITIALIZED[other]}],
                            evidence + [{"text": "UnoDraw Graphite coverage initialized: unknown (InternalMultisampleCount=4)."}]):
                with self.subTest(coverage=coverage, messages=changed), self.assertRaises(AssertionError):
                    verify_bridge(changed, "graphite", graphite_coverage=coverage)
        both = base + [{"text": message} for message in GRAPHITE_COVERAGE_INITIALIZED.values()]
        with self.assertRaises(AssertionError):
            verify_bridge(both, "graphite")

    def test_graphite_coverage_cannot_contaminate_another_bridge_or_insert_query_parameters(self) -> None:
        other_modes = {"persistent": [PERSISTENT_INITIALIZED], "legacy": [LEGACY_PREFERENCE],
                       "webgl": [WEBGL_PREFERENCE, WEBGL_INITIALIZED],
                       "composite": [COMPOSITE_PREFERENCE, COMPOSITE_INITIALIZED,
                                     COMPOSITE_REUSED, PERSISTENT_INITIALIZED]}
        for bridge, messages in other_modes.items():
            for coverage, initialized in GRAPHITE_COVERAGE_INITIALIZED.items():
                with self.subTest(bridge=bridge, coverage=coverage), self.assertRaises(ValueError):
                    bridge_query(bridge, graphite_coverage=coverage)
                with self.assertRaises(ValueError):
                    verify_bridge([{"text": text} for text in messages], bridge, graphite_coverage=coverage)
                with self.assertRaises(AssertionError):
                    verify_bridge([{"text": text} for text in messages + [initialized]], bridge)
        for coverage in ("", "unknown", "raster&graphiteCoverage=default"):
            with self.subTest(coverage=coverage), self.assertRaises(ValueError):
                bridge_query("graphite", graphite_coverage=coverage)
        for coverage in GRAPHITE_COVERAGE_INITIALIZED:
            with self.subTest(coverage=coverage), self.assertRaisesRegex(ValueError, "retired"):
                bridge_query("graphite", graphite_coverage=coverage)

    def test_graphite_requires_validated_initialization_and_rejects_fallback_or_mode_contamination(self) -> None:
        messages = [GRAPHITE_PREFERENCE, GRAPHITE_INITIALIZED, PERSISTENT_INITIALIZED]
        evidence = [{"text": message} for message in messages]
        verify_bridge(evidence, "graphite")
        for missing in (GRAPHITE_PREFERENCE, GRAPHITE_INITIALIZED):
            with self.subTest(missing=missing), self.assertRaises(AssertionError):
                verify_bridge([entry for entry in evidence if entry["text"] != missing], "graphite")
        for extra in (LEGACY_PREFERENCE, WEBGL_PREFERENCE, WEBGL_INITIALIZED,
                      COMPOSITE_PREFERENCE, COMPOSITE_INITIALIZED, COMPOSITE_REUSED,
                      "UnoDraw Graphite canvas fallback: queue rejected recording.",
                      "UnoDraw WebGL canvas fallback: context unavailable.",
                      "UnoDraw canvas bridge composite fallback: no cache.",
                      "UnoDraw canvas bridge fallback: device lost."):
            with self.subTest(extra=extra), self.assertRaises(AssertionError):
                verify_bridge(evidence + [{"text": extra}], "graphite")
        other_modes = {"persistent": [PERSISTENT_INITIALIZED], "legacy": [LEGACY_PREFERENCE],
                       "webgl": [WEBGL_PREFERENCE, WEBGL_INITIALIZED],
                       "composite": [COMPOSITE_PREFERENCE, COMPOSITE_INITIALIZED,
                                     COMPOSITE_REUSED, PERSISTENT_INITIALIZED]}
        for bridge, original in other_modes.items():
            for extra in (GRAPHITE_PREFERENCE, GRAPHITE_INITIALIZED,
                          "UnoDraw Graphite canvas fallback: late validation error."):
                with self.subTest(bridge=bridge, extra=extra), self.assertRaises(AssertionError):
                    verify_bridge([{"text": text} for text in original + [extra]], bridge)

    def test_webgl_requires_actual_success_and_rejects_every_late_fallback(self) -> None:
        evidence = [{"text": WEBGL_PREFERENCE}, {"text": WEBGL_INITIALIZED},
                    {"text": PERSISTENT_INITIALIZED}]
        verify_bridge(evidence, "webgl")
        for change in (evidence[:1], evidence[1:], evidence + [{"text": LEGACY_PREFERENCE}],
                       evidence + [{"text": "UnoDraw WebGL canvas fallback: context lost."}],
                       evidence + [{"text": "UnoDraw canvas bridge fallback: device lost."}]):
            with self.subTest(evidence=change), self.assertRaises(AssertionError):
                verify_bridge(change, "webgl")
        for bridge in ("persistent", "legacy"):
            with self.subTest(bridge=bridge), self.assertRaises(AssertionError):
                verify_bridge(evidence + [{"text": LEGACY_PREFERENCE}], bridge)

    def test_bridge_queries_select_each_path_explicitly(self) -> None:
        for bridge in ("legacy", "persistent", "composite", "legacy-composite"):
            self.assertEqual(bridge_query(bridge), "canvasBridge=" + bridge)
        for bridge in ("unknown", "webgl", "graphite"):
            with self.subTest(bridge=bridge), self.assertRaises(ValueError):
                bridge_query(bridge)

    def test_live_clis_reject_retired_document_bridges_before_opening_a_package(self) -> None:
        directory = Path(__file__).resolve().parent
        drivers = [(directory / "bridge_quality.py", []),
                   (directory / "bridge_experiment.py", ["--workload", "selection"]),
                   (directory.parent / "UnoDraw.BrowserRenderQuality/svg_bridge_quality.py", [])]
        for driver, extra in drivers:
            for retired in ("webgl", "graphite"):
                completed = subprocess.run([sys.executable, str(driver), "--candidate-bridge", retired,
                                            "--package", "/nonexistent/retired-package", "--output", "/nonexistent/retired-output",
                                            *extra], capture_output=True, text=True, check=False, timeout=10)
                with self.subTest(driver=driver.name, bridge=retired):
                    self.assertEqual(completed.returncode, 2, completed.stderr)
                    self.assertIn(f"invalid choice: '{retired}'", completed.stderr)
            completed = subprocess.run([sys.executable, str(driver), "--graphite-coverage", "raster",
                                        "--package", "/nonexistent/retired-package", "--output", "/nonexistent/retired-output",
                                        *extra], capture_output=True, text=True, check=False, timeout=10)
            with self.subTest(driver=driver.name, retired_coverage=True):
                self.assertEqual(completed.returncode, 2, completed.stderr)
                self.assertIn("unrecognized arguments: --graphite-coverage raster", completed.stderr)

    def test_retired_startup_cannot_satisfy_native_or_default_bridge_quality(self) -> None:
        for retired, label, preference, initialized in (
                ("webgl", "WebGL", WEBGL_PREFERENCE, WEBGL_INITIALIZED),
                ("graphite", "Graphite", GRAPHITE_PREFERENCE, GRAPHITE_INITIALIZED)):
            historical = [{"text": text} for text in (preference, initialized, PERSISTENT_INITIALIZED)]
            verify_bridge(historical, retired)
            startup = [{"text": f"UnoDraw canvas bridge fallback: the {label} document renderer was retired; using persistent RGBA upload."},
                       {"text": "UnoDraw canvas bridge preference: persistent RGBA upload."},
                       {"text": PERSISTENT_INITIALIZED}]
            for requested in (retired, "persistent"):
                with self.subTest(retired=retired, requested=requested), self.assertRaises(AssertionError):
                    verify_bridge(startup, requested)
            # Even borrowed historical initialization cannot hide explicit retirement.
            with self.subTest(retired=retired, borrowed_initialization=True), self.assertRaises(AssertionError):
                verify_bridge(historical + startup, retired)

    def test_direct_browser_helpers_reject_retirement_before_creating_a_context(self) -> None:
        from bridge_quality import run_case
        from performance import run_trial

        with tempfile.TemporaryDirectory() as temporary:
            output = Path(temporary) / "not-created"
            for retired in ("webgl", "graphite"):
                browser = MagicMock()
                with self.subTest(bridge=retired, driver="quality"), self.assertRaisesRegex(ValueError, "retired"):
                    run_case(browser, "http://unused", retired, 1, output)
                with self.subTest(bridge=retired, driver="timing"), self.assertRaisesRegex(ValueError, "retired"):
                    run_trial(browser, "http://unused", {}, "webgpu", "pan", 1, None, output, canvas_bridge=retired)
                browser.new_context.assert_not_called()
                self.assertFalse(output.exists())

    def test_composite_requires_upload_and_real_reuse_not_a_preference_or_ordinary_upload(self) -> None:
        initial = [{"text": text} for text in (COMPOSITE_PREFERENCE, COMPOSITE_INITIALIZED, PERSISTENT_INITIALIZED)]
        verify_bridge(initial, "composite", require_reuse=False)
        with self.assertRaisesRegex(AssertionError, "cached document"):
            verify_bridge(initial, "composite")
        evidence = initial + [{"text": COMPOSITE_REUSED}]
        verify_bridge(evidence, "composite")
        for missing in (COMPOSITE_PREFERENCE, COMPOSITE_INITIALIZED, PERSISTENT_INITIALIZED):
            with self.subTest(missing=missing), self.assertRaises(AssertionError):
                verify_bridge([entry for entry in evidence if entry["text"] != missing], "composite",
                              require_reuse=False)

    def test_composite_rejects_late_fallback_and_cannot_satisfy_another_mode(self) -> None:
        evidence = [{"text": text} for text in (COMPOSITE_PREFERENCE, COMPOSITE_INITIALIZED,
                                                  PERSISTENT_INITIALIZED, COMPOSITE_REUSED)]
        for extra in (LEGACY_PREFERENCE, WEBGL_PREFERENCE, WEBGL_INITIALIZED,
                      "UnoDraw canvas bridge fallback: texture unavailable.",
                      "UnoDraw canvas bridge composite fallback: bitmap unavailable.",
                      "UnoDraw WebGL canvas fallback: context lost."):
            with self.subTest(extra=extra), self.assertRaises(AssertionError):
                verify_bridge(evidence + [{"text": extra}], "composite", require_reuse=False)
        for bridge, extra in (("persistent", []), ("legacy", [LEGACY_PREFERENCE]),
                              ("webgl", [WEBGL_PREFERENCE, WEBGL_INITIALIZED])):
            with self.subTest(bridge=bridge), self.assertRaises(AssertionError):
                verify_bridge(evidence + [{"text": message} for message in extra], bridge)

    def test_available_provider_does_not_prove_bridge_initialization(self) -> None:
        with self.assertRaisesRegex(AssertionError, "successful RGBA"):
            verify_bridge([{"text": "WebGpuGraphicsProvider"}], "persistent")

    def test_late_fallback_cannot_hide_behind_initial_success(self) -> None:
        with self.assertRaisesRegex(AssertionError, "fell back"):
            verify_bridge([{"text": PERSISTENT_INITIALIZED},
                           {"text": "UnoDraw canvas bridge fallback: device unavailable"}], "persistent")

    def test_bridge_modes_must_be_exclusive(self) -> None:
        verify_bridge([{"text": PERSISTENT_INITIALIZED}], "persistent")
        verify_bridge([{"text": LEGACY_PREFERENCE}], "legacy")
        for mode in ("persistent", "legacy"):
            with self.subTest(mode=mode), self.assertRaises(AssertionError):
                verify_bridge([{"text": PERSISTENT_INITIALIZED}, {"text": LEGACY_PREFERENCE}], mode)


class NativeResolutionTests(unittest.TestCase):
    def test_css_scaled_screenshot_is_rejected_at_high_dpi(self) -> None:
        with self.assertRaises(AssertionError):
            assert_native_size((1440, 960), {"width": 1440, "height": 960}, 2)
        assert_native_size((2880, 1920), {"width": 1440, "height": 960}, 2)
        assert_native_size((488, 1055), {"width": 390, "height": 844}, 1.25)

    def test_comparison_never_resizes_its_inputs(self) -> None:
        with tempfile.TemporaryDirectory() as directory:
            first, second = Path(directory) / "first.png", Path(directory) / "second.png"
            Image.new("RGB", (200, 160), "white").save(first)
            Image.new("RGB", (100, 80), "white").save(second)
            with self.assertRaises(AssertionError):
                raw_comparison(first, second, {"width": 100, "height": 80}, 2)
            Image.new("RGB", (200, 160), "white").save(second)
            value = raw_comparison(first, second, {"width": 100, "height": 80}, 2)
            self.assertIs(value["resized_for_comparison"], False)

    def test_resolution_loss_is_detected_even_when_output_size_is_preserved(self) -> None:
        with tempfile.TemporaryDirectory() as directory:
            first, second = Path(directory) / "first.png", Path(directory) / "second.png"
            sharp = Image.new("RGB", (200, 160), "white")
            drawing = ImageDraw.Draw(sharp)
            for x in range(0, 200, 2):
                drawing.line((x, 0, x, 159), fill="black")
            sharp.save(first)
            sharp.resize((100, 80), Image.Resampling.BILINEAR).resize((200, 160), Image.Resampling.BILINEAR).save(second)
            with self.assertRaisesRegex(AssertionError, "pixels differ"):
                raw_comparison(first, second, {"width": 100, "height": 80}, 2)

    def test_small_ruler_regression_is_not_diluted_by_whole_frame_area(self) -> None:
        with tempfile.TemporaryDirectory() as directory:
            first, second = Path(directory) / "first.png", Path(directory) / "second.png"
            image = Image.new("RGB", (2880, 1920), "white")
            image.save(first)
            ImageDraw.Draw(image).rectangle((200, 210, 210, 230), fill="black")
            image.save(second)
            with self.assertRaisesRegex(AssertionError, "horizontal_ruler"):
                raw_comparison(first, second, {"width": 1440, "height": 960}, 2)


if __name__ == "__main__":
    unittest.main()
