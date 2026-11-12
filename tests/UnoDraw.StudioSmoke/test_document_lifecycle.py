import copy
import unittest
import xml.etree.ElementTree as ET

from assert_document_lifecycle import assert_blank, assert_rectangle, assert_retained
from assert_svg import SVG


class DocumentLifecycleAssertions(unittest.TestCase):
    def setUp(self):
        self.document = ET.Element(SVG + 'svg', width='1200', height='800', viewBox='0, 0, 1200, 800')

    def test_replacement_must_be_empty_and_have_default_dimensions(self):
        assert_blank(self.document)
        for attribute, value in [('width', '1000'), ('height', '640'), ('viewBox', '0 0 1000 640')]:
            candidate = copy.deepcopy(self.document)
            candidate.set(attribute, value)
            with self.assertRaises(AssertionError):
                assert_blank(candidate)
        ET.SubElement(self.document, SVG + 'rect', id='stale', width='20', height='30')
        with self.assertRaises(AssertionError):
            assert_blank(self.document)

    def test_live_drawing_rejects_empty_stale_or_invalid_geometry(self):
        with self.assertRaises(AssertionError):
            assert_rectangle(self.document)
        rectangle = ET.SubElement(self.document, SVG + 'rect', id='rect1', width='20', height='30')
        assert_rectangle(self.document)
        for value in ('0', '-10', 'nan', 'inf'):
            rectangle.set('width', value)
            with self.assertRaises(AssertionError):
                assert_rectangle(self.document)
        rectangle.set('width', '20')
        ET.SubElement(self.document, SVG + 'rect', id='stale', width='10', height='10')
        with self.assertRaises(AssertionError):
            assert_rectangle(self.document)

    def test_retained_document_requires_same_identity_geometry_and_source(self):
        ET.SubElement(self.document, SVG + 'rect', id='rect1', width='20', height='30', fill='red')
        expected = copy.deepcopy(self.document)
        assert_retained(self.document, expected)
        for attribute, value in [('id', 'other'), ('width', '21'), ('fill', 'blue')]:
            candidate = copy.deepcopy(self.document)
            candidate[0].set(attribute, value)
            with self.assertRaises(AssertionError):
                assert_retained(candidate, expected)


if __name__ == '__main__':
    unittest.main()
