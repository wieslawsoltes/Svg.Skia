"""Prove that interface chrome cannot satisfy the document-rendering gate."""
import tempfile
import unittest
from pathlib import Path

from PIL import Image, ImageDraw

from smoke import artwork_pixels, assert_artwork


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


if __name__ == "__main__":
    unittest.main()
