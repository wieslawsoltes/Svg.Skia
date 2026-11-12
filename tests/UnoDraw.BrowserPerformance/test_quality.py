from __future__ import annotations

import tempfile
import unittest
from pathlib import Path

from PIL import Image, ImageDraw

from quality import LEGACY_PREFERENCE, PERSISTENT_INITIALIZED, assert_native_size, raw_comparison, verify_bridge


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
