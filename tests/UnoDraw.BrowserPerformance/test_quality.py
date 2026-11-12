from __future__ import annotations

import copy
import tempfile
import unittest
from itertools import product
from pathlib import Path

from PIL import Image, ImageDraw

from quality import (LEGACY_PREFERENCE, PERSISTENT_INITIALIZED, assert_native_size, compare_bridge_states,
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
