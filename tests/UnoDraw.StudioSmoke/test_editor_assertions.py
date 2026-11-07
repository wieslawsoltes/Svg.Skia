import copy
import pathlib
import unittest
import uuid
import xml.etree.ElementTree as ET

from assert_editor import assert_editor_change
from assert_svg import SVG


class EditorSavedDocumentAssertions(unittest.TestCase):
    def setUp(self):
        self.baseline = ET.parse(pathlib.Path(__file__).with_name('StudioFixture.svg')).getroot()
        self.root = copy.deepcopy(self.baseline)

    def test_drawing_requires_a_real_new_object_and_unchanged_originals(self):
        with self.assertRaises(AssertionError):
            assert_editor_change(self.root, self.baseline, 'rectangle')
        ET.SubElement(self.root, SVG + 'rect', id='new-rect', width='25', height='40')
        assert_editor_change(self.root, self.baseline, 'rectangle')
        self.root[0].set('fill', 'red')
        with self.assertRaises(AssertionError):
            assert_editor_change(self.root, self.baseline, 'rectangle')

    def test_wrong_tool_and_nonfinite_geometry_fail(self):
        element = ET.SubElement(self.root, SVG + 'ellipse', id='new-shape', rx='20', ry='30')
        with self.assertRaises(AssertionError):
            assert_editor_change(self.root, self.baseline, 'rectangle')
        assert_editor_change(self.root, self.baseline, 'ellipse')
        element.set('rx', 'NaN')
        with self.assertRaises(AssertionError):
            assert_editor_change(self.root, self.baseline, 'ellipse')

    def test_guide_requires_nonprinting_finite_normalized_metadata(self):
        with self.assertRaises(AssertionError):
            assert_editor_change(self.root, self.baseline, 'ruler-guide')
        identifier = uuid.uuid4().hex
        self.root.set('data-unodraw-guides', '1;' + identifier + ',0,20,1,0')
        assert_editor_change(self.root, self.baseline, 'ruler-guide')
        self.root.set('data-unodraw-guides', '1;' + identifier + ',0,nan,1,0')
        with self.assertRaises(AssertionError):
            assert_editor_change(self.root, self.baseline, 'ruler-guide')

    def test_style_requires_real_metadata_and_exact_source_retention(self):
        ET.SubElement(self.root, SVG + 'rect', id='new-rect', width='25', height='40')
        with self.assertRaises(AssertionError):
            assert_editor_change(self.root, self.baseline, 'style')
        self.root.set('data-style-Style_1', 'fill:#fff;stroke:#000;opacity:1')
        assert_editor_change(self.root, self.baseline, 'style')
        self.root.set('width', '50')
        with self.assertRaises(AssertionError):
            assert_editor_change(self.root, self.baseline, 'style')

    def test_artboard_requires_real_regions_dimensions_and_unchanged_artwork(self):
        for name, x in (('Artboard 1', 0), ('Native board', 1050)):
            view = ET.SubElement(self.root, SVG + 'view', id='board-' + str(x), viewBox=f'{x} 0 1000 640')
            view.set('data-unodraw-artboard', 'true')
            view.set('data-unodraw-name', name)
        self.root.set('width', '2050')
        self.root.set('viewBox', '0 0 2050 640')
        assert_editor_change(self.root, self.baseline, 'artboard')
        self.root[0].set('width', '2050')
        with self.assertRaises(AssertionError):
            assert_editor_change(self.root, self.baseline, 'artboard')


if __name__ == '__main__':
    unittest.main()
