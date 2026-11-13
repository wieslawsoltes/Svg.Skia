from __future__ import annotations

import tempfile
import unittest
from pathlib import Path

from PIL import Image, ImageDraw

from selection_readiness import DESKTOP_SIZE, SELECTION_LABEL_REGION, require_cleared_selection


class SelectionReadinessTests(unittest.TestCase):
    def setUp(self) -> None:
        self.temporary = tempfile.TemporaryDirectory()
        self.addCleanup(self.temporary.cleanup)
        self.directory = Path(self.temporary.name)
        self.reference = self.directory / "before.png"
        self.current = self.directory / "after.png"
        self.image = Image.new("RGB", DESKTOP_SIZE, (44, 44, 44))
        ImageDraw.Draw(self.image).text((8, 45), "No selection", fill=(185, 185, 185))
        self.image.save(self.reference)

    def test_changed_document_does_not_stand_in_for_cleared_selection(self) -> None:
        selected = self.image.copy()
        draw = ImageDraw.Draw(selected)
        draw.rectangle((100, 160, 1050, 880), fill=(120, 80, 220))
        draw.rectangle(SELECTION_LABEL_REGION, fill=(44, 44, 44))
        draw.text((8, 45), "5 objects", fill=(185, 185, 185))
        selected.save(self.current)
        with self.assertRaisesRegex(AssertionError, "selection-clear"):
            require_cleared_selection(self.reference, self.current)
        draw.rectangle(SELECTION_LABEL_REGION, fill=(44, 44, 44))
        draw.text((8, 45), "No selection", fill=(185, 185, 185))
        selected.save(self.current)
        proof = require_cleared_selection(self.reference, self.current)
        self.assertEqual(proof["changed_pixel_fraction"], 0)
        self.assertFalse(proof["resized_for_comparison"])

    def test_blank_reference_or_missing_current_label_cannot_pass(self) -> None:
        Image.new("RGB", DESKTOP_SIZE, (44, 44, 44)).save(self.current)
        with self.assertRaisesRegex(AssertionError, "selection-clear"):
            require_cleared_selection(self.reference, self.current)
        with self.assertRaisesRegex(AssertionError, "visible toolbar label"):
            require_cleared_selection(self.current, self.current)

    def test_original_luminance_and_fraction_thresholds_are_retained(self) -> None:
        for delta in (12, 13):
            altered = self.image.copy()
            for x in range(5, 10):
                altered.putpixel((x, 41), (44 + delta,) * 3)
            altered.save(self.current)
            if delta == 12:
                proof = require_cleared_selection(self.reference, self.current)
                self.assertEqual(proof["luminance_threshold"], 12)
                self.assertEqual(proof["maximum_changed_fraction"], 0.001)
            else:
                with self.assertRaisesRegex(AssertionError, "selection-clear"):
                    require_cleared_selection(self.reference, self.current)

    def test_native_size_is_required_without_resampling(self) -> None:
        Image.new("RGB", (1800, 1200), (44, 44, 44)).save(self.current)
        with self.assertRaisesRegex(AssertionError, "native"):
            require_cleared_selection(self.reference, self.current)


if __name__ == "__main__":
    unittest.main()
