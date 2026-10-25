import unittest
import xml.etree.ElementTree as ET
from assert_gradient_target import assert_target, local_properties
from assert_svg import SVG, PAINT_TARGETS


def fixture(target=None):
    root = ET.Element(SVG + 'svg')
    if target:
        defs = ET.SubElement(root, SVG + 'defs')
        gradient = ET.SubElement(defs, SVG + 'linearGradient', {'id': 'authored'})
        ET.SubElement(gradient, SVG + 'stop', {'offset': '0%', 'stop-color': '#f2b99e'})
        ET.SubElement(gradient, SVG + 'stop', {'offset': '100%', 'stop-color': '#7061dc'})
    for identifier in PAINT_TARGETS:
        attributes = {'id': identifier, 'x': '4', 'width': '30', 'height': '20', 'fill': 'red', 'stroke': 'black'}
        if target:
            attributes[target] = 'url(#authored)'
        ET.SubElement(root, SVG + 'rect', attributes)
    return root


class GradientTargetAssertions(unittest.TestCase):
    def test_accepts_exact_fill_or_stroke_target(self):
        for target in ('fill', 'stroke'):
            with self.subTest(target=target):
                assert_target(fixture(target), fixture(), target)

    def test_rejects_wrong_paint_target(self):
        with self.assertRaises(AssertionError):
            assert_target(fixture('stroke'), fixture(), 'fill')

    def test_rejects_draft_reset_and_nonfinite_offsets(self):
        actual = fixture('fill')
        stops = list(actual.iter(SVG + 'stop'))
        stops[0].set('stop-color', '#7061dc')
        with self.assertRaises(AssertionError):
            assert_target(actual, fixture(), 'fill')
        stops[0].set('stop-color', '#f2b99e')
        stops[0].set('offset', 'NaN')
        with self.assertRaises(AssertionError):
            assert_target(actual, fixture(), 'fill')

    def test_rejects_unrelated_geometry_or_opacity_changes(self):
        for attribute in ('x', 'opacity', 'stroke'):
            actual = fixture('fill')
            list(actual.iter(SVG + 'rect'))[0].set(attribute, '0.7')
            with self.subTest(attribute=attribute), self.assertRaises(AssertionError):
                assert_target(actual, fixture(), 'fill')

    def test_inline_paint_takes_precedence_and_important_is_preserved(self):
        actual = fixture('fill')
        first = list(actual.iter(SVG + 'rect'))[0]
        first.set('fill', 'red')
        first.set('style', 'fill:url(#authored)!important;fill:blue')
        assert_target(actual, fixture(), 'fill')
        first.set('style', 'fill:blue!important;fill:url(#authored)')
        with self.assertRaises(AssertionError):
            assert_target(actual, fixture(), 'fill')
        first.set('style', 'fill:var(--paint)')
        with self.assertRaises(AssertionError):
            local_properties(first)


if __name__ == '__main__':
    unittest.main()
