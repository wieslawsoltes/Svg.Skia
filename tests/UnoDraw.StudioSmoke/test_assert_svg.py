import unittest
import xml.etree.ElementTree as ET
from assert_svg import assert_opacity, assert_paints, fixture_objects, presentation_value


def fixture(style=False, swapped=False):
    root = ET.Element('{http://www.w3.org/2000/svg}svg')
    fill, stroke = ('black', 'white') if swapped else ('white', 'black')
    for name in ('paper', 'card', 'arch', 'sun'):
        attributes = {'id': name, 'stroke-width': '1'}
        if style:
            attributes['style'] = f'fill:{fill};stroke:{stroke};'
        else:
            attributes.update(fill=fill, stroke=stroke)
        ET.SubElement(root, '{http://www.w3.org/2000/svg}rect', attributes)
    return root


class SavedSvgAssertionsTests(unittest.TestCase):
    def test_accepts_presentation_attributes(self):
        assert_paints(fixture())

    def test_accepts_serializer_inline_styles(self):
        assert_paints(fixture(style=True))

    def test_accepts_swapped_inline_styles(self):
        assert_paints(fixture(style=True, swapped=True), swapped=True)

    def test_style_overrides_correct_attribute_and_wrong_paint_fails(self):
        root = fixture()
        root[1].set('style', 'fill:red;')
        with self.assertRaises(AssertionError):
            assert_paints(root)

    def test_style_overrides_wrong_attribute_and_correct_paint_passes(self):
        root = fixture(style=True)
        root[1].set('fill', 'red')
        assert_paints(root)

    def test_last_declaration_wins_at_same_importance(self):
        node = ET.fromstring('<rect style="fill:red; FILL: white;"/>')
        self.assertEqual('white', presentation_value(node, 'fill'))

    def test_important_declaration_is_not_overwritten_by_normal(self):
        node = ET.fromstring('<rect style="fill:red !important;fill:white;"/>')
        self.assertEqual('red', presentation_value(node, 'fill'))

    def test_missing_paint_fails(self):
        root = fixture()
        del root[2].attrib['fill']
        with self.assertRaises(AssertionError):
            assert_paints(root)

    def test_noop_swap_fails(self):
        with self.assertRaises(AssertionError):
            assert_paints(fixture(style=True), swapped=True)

    def test_wrong_stroke_width_fails(self):
        root = fixture(style=True)
        root[3].set('style', root[3].get('style') + 'stroke-width:2;')
        with self.assertRaises(AssertionError):
            assert_paints(root)

    def test_missing_duplicate_or_reparented_targets_fail(self):
        for failure in ('missing', 'duplicate', 'reparented'):
            with self.subTest(failure=failure):
                root = fixture()
                if failure == 'missing':
                    root.remove(root[1])
                elif failure == 'duplicate':
                    root[1].set('id', 'paper')
                else:
                    node = root[1]; root.remove(node)
                    ET.SubElement(root, '{http://www.w3.org/2000/svg}g').append(node)
                with self.assertRaises(AssertionError):
                    fixture_objects(root)

    def test_opacity_attribute_and_style_are_both_checked(self):
        for style in (False, True):
            root = fixture()
            for node in root:
                node.set('style' if style else 'opacity', 'opacity:0.625;' if style else '0.625')
            assert_opacity(root)
            root[0].set('style', 'opacity:1;')
            with self.assertRaises(AssertionError):
                assert_opacity(root)

    def test_nonfinite_opacity_fails(self):
        for value in ('NaN', 'Infinity', '-Infinity'):
            root = fixture()
            for node in root:
                node.set('opacity', value)
            with self.assertRaises(AssertionError):
                assert_opacity(root)

    def test_unsupported_css_fails_closed(self):
        for style in ('fill:var(--paint)', 'fill:url(#p)', 'fill:red;bogus', 'fill:red!important!'):
            with self.subTest(style=style):
                root = fixture(); root[0].set('style', style)
                with self.assertRaises(AssertionError):
                    assert_paints(root)
        root = fixture()
        ET.SubElement(root, '{http://www.w3.org/2000/svg}style').text = 'rect{fill:red}'
        with self.assertRaises(AssertionError):
            assert_paints(root)


if __name__ == '__main__':
    unittest.main()
