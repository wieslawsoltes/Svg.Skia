"""Shared-control source contracts; native interaction checks remain authoritative."""
from pathlib import Path
import unittest
import xml.etree.ElementTree as ET

ROOT = Path(__file__).resolve().parents[2] / 'src/Svg.Editor.Skia.Uno'
NS = '{http://schemas.microsoft.com/winfx/2006/xaml/presentation}'
X = '{http://schemas.microsoft.com/winfx/2006/xaml}'
LOCAL = '{using:Svg.Editor.Skia.Uno}'


def read(name):
    return ET.parse(ROOT / 'Themes' / name).getroot()


class SharedControlRefinements(unittest.TestCase):
    def test_native_state_aliases_only_use_semantic_palette_resources(self):
        palette = {node.get(X + 'Key') for node in read('StudioPaletteResources.xaml')}
        aliases = read('StudioNativeStateResources.xaml').findall(NS + 'StaticResource')
        self.assertGreater(len(aliases), 80)
        for alias in aliases:
            self.assertIn(alias.get('ResourceKey'), palette, alias.get(X + 'Key'))

    def test_every_toggle_selection_and_interaction_state_has_all_brushes(self):
        aliases = {node.get(X + 'Key'): node.get('ResourceKey')
                   for node in read('StudioNativeStateResources.xaml').findall(NS + 'StaticResource')}
        for selection in ('', 'Checked', 'Indeterminate'):
            for interaction in ('', 'PointerOver', 'Pressed', 'Disabled'):
                for part in ('Background', 'Foreground', 'BorderBrush'):
                    self.assertIn('ToggleButton' + part + selection + interaction, aliases)
        for selection in ('Checked', 'Indeterminate'):
            self.assertNotEqual(aliases['ToggleButtonForeground' + selection], aliases['ToggleButtonBackground' + selection])

    def test_checkbox_selected_and_mixed_states_keep_visible_glyphs(self):
        aliases = {node.get(X + 'Key'): node.get('ResourceKey')
                   for node in read('StudioNativeStateResources.xaml').findall(NS + 'StaticResource')}
        for selection in ('Checked', 'Indeterminate'):
            for interaction in ('', 'PointerOver', 'Pressed', 'Disabled'):
                fill = aliases['CheckBoxCheckBackgroundFill' + selection + interaction]
                glyph = aliases['CheckBoxCheckGlyphForeground' + selection + interaction]
                self.assertNotEqual(fill, glyph)
                self.assertIn('CheckBoxCheckBackgroundStroke' + selection + interaction, aliases)

    def test_native_inputs_and_toggles_inherit_platform_templates(self):
        root = read('StudioThemeResources.xaml')
        for target in ('TextBox', 'ComboBox', 'ToggleButton', 'CheckBox', 'RadioButton'):
            styles = [s for s in root.findall(NS + 'Style') if s.get('TargetType') == target]
            self.assertTrue(any(s.get('BasedOn') == '{StaticResource Default' + target + 'Style}' for s in styles), target)
            self.assertFalse(any(n.get('Property') == 'Template' for s in styles for n in s.findall(NS + 'Setter')), target)

    def test_segmented_choices_keep_native_selection_and_focus_contracts(self):
        root = read('StudioSelectionResources.xaml')
        styles = root.findall(NS + 'Style')
        self.assertEqual(1, len(styles))
        self.assertEqual('RadioButton', styles[0].get('TargetType'))
        self.assertTrue(any(s.get('Property') == 'UseSystemFocusVisuals' and s.get('Value') == 'True' for s in styles[0]))
        groups = {g.get(X + 'Name'): {s.get(X + 'Name') for s in g} for g in root.iter(NS + 'VisualStateGroup')}
        self.assertEqual({'Normal', 'PointerOver', 'Pressed', 'Disabled'}, groups['CommonStates'])
        self.assertEqual({'Checked', 'Unchecked', 'Indeterminate'}, groups['CheckStates'])

    def test_panel_navigation_does_not_scroll_out_of_its_own_viewport(self):
        template = next(t for t in read('Generic.xaml').iter(NS + 'ControlTemplate') if t.get('TargetType') == 'local:StudioDockGroup')
        named = {n.get(X + 'Name'): n for n in template.iter() if n.get(X + 'Name')}
        viewport = named['PART_TabScroll']
        self.assertEqual('Hidden', viewport.get('HorizontalScrollBarVisibility'))
        self.assertEqual('Enabled', viewport.get('HorizontalScrollMode'))
        for part in ('PART_PreviousTabs', 'PART_NextTabs', 'PART_PanelPicker'):
            self.assertIn(part, named)
            self.assertNotIn(named[part], list(viewport.iter()))
            self.assertTrue(named[part].get('AutomationProperties.Name'))
            self.assertEqual('{StaticResource StudioQuietButtonStyle}', named[part].get('Style'))

    def test_resource_dictionary_composition_has_no_cycles(self):
        files = {p.stem: ET.parse(p).getroot() for p in (ROOT / 'Themes').glob('*.xaml')}
        def visit(name, ancestry):
            self.assertNotIn(name, ancestry, 'Recursive dictionary composition: ' + ' -> '.join(ancestry + [name]))
            for node in files[name].iter():
                if node.tag.startswith(LOCAL) and node.tag[len(LOCAL):] in files:
                    visit(node.tag[len(LOCAL):], ancestry + [name])
        for name in files:
            visit(name, [])

    def test_both_nonmodal_paint_editors_guard_segment_target_changes(self):
        for name in ('StudioColorEditor.cs', 'StudioGradientEditor.cs'):
            source = (ROOT / 'Controls' / name).read_text()
            self.assertIn('StudioChoiceStrip _target', source)
            self.assertIn('_target.SelectionChanging +=', source)
            self.assertIn('args.Cancel = true;', source)
            self.assertIn('StudioActionRole.Primary, this', source)

    def test_segment_navigation_leaves_modified_shortcuts_to_the_editor(self):
        source = (ROOT / 'Controls/StudioChoiceStrip.cs').read_text()
        body = source[source.index('private void ChoiceKeyDown'):]
        self.assertIn('IsDown(VirtualKey.Control)', body)
        self.assertIn('IsDown(VirtualKey.Menu)', body)
        self.assertIn('IsDown(VirtualKey.LeftWindows)', body)
        self.assertLess(body.index('IsDown(VirtualKey.Control)'), body.index('RequestSelection('))


if __name__ == '__main__':
    unittest.main()
