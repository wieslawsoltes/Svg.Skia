"""Prove that interface chrome cannot satisfy the document-rendering gate."""
import tempfile
import unittest
from pathlib import Path
from unittest.mock import Mock

from PIL import Image, ImageDraw

from smoke import artwork_pixels, artwork_transition, assert_artwork, wait_for_artwork_transition


class DocumentArtworkTests(unittest.TestCase):
    def check_image(self, image: Image.Image) -> dict[str, int]:
        with tempfile.TemporaryDirectory() as directory:
            path = Path(directory) / "workspace.png"
            image.save(path)
            return artwork_pixels(path)

    def test_paper_and_neutral_interface_do_not_prove_document_rendering(self) -> None:
        image = Image.new("RGB", (1440, 960), "#303030")
        ImageDraw.Draw(image).rectangle((140, 240, 1040, 818), fill="#F7F6F2")
        with self.assertRaisesRegex(AssertionError, "artwork did not render"):
            assert_artwork(self.check_image(image))

    def test_colored_chrome_outside_document_cannot_satisfy_the_gate(self) -> None:
        image = Image.new("RGB", (1440, 960), "#303030")
        draw = ImageDraw.Draw(image)
        draw.rectangle((1100, 100, 1430, 300), fill="#575AC8")
        draw.rectangle((1100, 350, 1430, 550), fill="#427D73")
        draw.rectangle((1100, 600, 1430, 800), fill="#F9AC84")
        with self.assertRaisesRegex(AssertionError, "artwork did not render"):
            assert_artwork(self.check_image(image))

    def test_each_document_color_family_must_be_present(self) -> None:
        image = Image.new("RGB", (1440, 960), "#F7F6F2")
        draw = ImageDraw.Draw(image)
        draw.rectangle((600, 300, 750, 600), fill="#575AC8")
        draw.rectangle((800, 300, 950, 600), fill="#427D73")
        with self.assertRaisesRegex(AssertionError, "peach"):
            assert_artwork(self.check_image(image))
        draw.ellipse((800, 650, 900, 750), fill="#F9AC84")
        assert_artwork(self.check_image(image))


class DocumentRepaintTests(unittest.TestCase):
    @staticmethod
    def scene(zoomed: bool = False) -> Image.Image:
        image = Image.new("RGB", (1440, 960), "#303030")
        draw = ImageDraw.Draw(image)
        draw.rectangle((140, 230, 1040, 830), fill="#F7F6F2")
        if zoomed:
            draw.rectangle((520, 260, 640, 650), fill="#575AC8")
            draw.rectangle((700, 390, 830, 740), fill="#427D73")
            draw.ellipse((880, 240, 1000, 360), fill="#F9AC84")
        else:
            draw.rectangle((600, 320, 680, 580), fill="#575AC8")
            draw.rectangle((720, 400, 800, 650), fill="#427D73")
            draw.ellipse((840, 320, 920, 400), fill="#F9AC84")
        return image

    def transition(self, before: Image.Image, after: Image.Image, restore: bool = False) -> dict:
        with tempfile.TemporaryDirectory() as directory:
            first, second = Path(directory) / "before.png", Path(directory) / "after.png"
            before.save(first)
            after.save(second)
            return artwork_transition(first, second, restore)

    def test_frozen_document_does_not_pass_repaint(self) -> None:
        image = self.scene()
        with self.assertRaisesRegex(AssertionError, "did not repaint"):
            self.transition(image, image)

    def test_blank_zoomed_document_does_not_pass_by_removing_initial_colors(self) -> None:
        with self.assertRaisesRegex(AssertionError, "artwork did not render"):
            self.transition(self.scene(), Image.new("RGB", (1440, 960), "#F7F6F2"))

    def test_chrome_and_selection_outlines_do_not_prove_repaint(self) -> None:
        initial = self.scene()
        overlay = initial.copy()
        draw = ImageDraw.Draw(overlay)
        draw.rectangle((1100, 100, 1430, 800), fill="#575AC8")
        # Selection-like outlines cross the filled interior of every family.
        draw.rectangle((605, 325, 675, 575), outline="#4CA6FF", width=3)
        draw.rectangle((725, 405, 795, 645), outline="#4CA6FF", width=3)
        draw.rectangle((845, 325, 915, 395), outline="#4CA6FF", width=3)
        with self.assertRaisesRegex(AssertionError, "did not repaint"):
            self.transition(initial, overlay)

    def test_every_artwork_family_must_repaint(self) -> None:
        partial = self.scene(True)
        draw = ImageDraw.Draw(partial)
        draw.rectangle((840, 200, 1020, 410), fill="#F7F6F2")
        draw.ellipse((840, 320, 920, 400), fill="#F9AC84")
        with self.assertRaisesRegex(AssertionError, "peach"):
            self.transition(self.scene(), partial)

    def test_zoom_and_refit_require_restored_document_pixels(self) -> None:
        fitted, zoomed = self.scene(), self.scene(True)
        self.transition(fitted, zoomed)
        restored = fitted.copy()
        ImageDraw.Draw(restored).rectangle((1100, 100, 1430, 800), fill="#575AC8")
        self.assertEqual(self.transition(fitted, restored, restore=True)["changed_pixel_fraction"], 0)
        with self.assertRaisesRegex(AssertionError, "did not restore"):
            self.transition(fitted, zoomed, restore=True)

    def test_camera_baseline_waits_for_canvas_and_rulers_after_chrome_changes_theme(self) -> None:
        fitted = self.scene()
        intermediate = fitted.copy()
        # Keep dark chrome and identical artwork while the canvas background
        # still contains the previous light frame from an asynchronous GL island.
        ImageDraw.Draw(intermediate).rectangle((90, 125, 1088, 932), fill="#BABABA")
        intermediate.paste(fitted.crop((140, 230, 1041, 831)), (140, 230))
        late_rulers = fitted.copy()
        ImageDraw.Draw(late_rulers).rectangle((92, 102, 1087, 124), fill="#DADADA")
        ImageDraw.Draw(late_rulers).rectangle((68, 125, 90, 931), fill="#DADADA")
        frames = iter((intermediate, late_rulers, fitted))
        page = Mock()
        page.screenshot.side_effect = lambda *, path: next(frames).save(path)
        with tempfile.TemporaryDirectory() as directory:
            initial, settled = Path(directory) / "initial.png", Path(directory) / "settled.png"
            fitted.save(initial)
            result = wait_for_artwork_transition(page, initial, settled, restore=True,
                                                require_settled_rulers=True)
        self.assertEqual(page.screenshot.call_count, 3)
        self.assertEqual(result["changed_pixel_fraction"], 0)
        self.assertEqual(result["ruler_changed_pixel_fraction"], {"horizontal": 0, "vertical": 0})


if __name__ == "__main__":
    unittest.main()
