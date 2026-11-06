"""Source contracts for the shared UnoDraw theme; runtime coverage is separate."""
from pathlib import Path
import re
import unittest
import xml.etree.ElementTree as ET

ROOT = Path(__file__).resolve().parents[2]
LIBRARY = ROOT / 'src/Svg.Editor.Skia.Uno'
XAML = '{http://schemas.microsoft.com/winfx/2006/xaml/presentation}'
X = '{http://schemas.microsoft.com/winfx/2006/xaml}'


def load(name):
    return ET.parse(LIBRARY / name).getroot()


def luminance(hexadecimal):
    rgb = [int(hexadecimal[i:i + 2], 16) / 255 for i in (1, 3, 5)]
    linear = [channel / 12.92 if channel <= 0.04045 else ((channel + 0.055) / 1.055) ** 2.4 for channel in rgb]
    return sum(a * b for a, b in zip(linear, (0.2126, 0.7152, 0.0722)))


def ratio(first, second):
    light, dark = sorted((luminance(first), luminance(second)), reverse=True)
    return (light + 0.05) / (dark + 0.05)


class StudioThemeContracts(unittest.TestCase):
    def test_palette_variants_have_identical_semantic_contracts(self):
        root = load('Themes/StudioPaletteResources.xaml')
        variants = {node.get(X + 'Key'): node for node in root.find(XAML + 'ResourceDictionary.ThemeDictionaries')}
        self.assertEqual({'Light', 'Dark', 'HighContrast'}, set(variants))
        expected = {node.get(X + 'Key') for node in variants['Light']}
        self.assertGreaterEqual(len(expected), 16)
        for name, variant in variants.items():
            self.assertEqual(expected, {node.get(X + 'Key') for node in variant}, name)
            if name == 'HighContrast':
                self.assertTrue(all(node.tag == XAML + 'StaticResource' and node.get('ResourceKey', '').startswith('SystemColor') for node in variant))
            else:
                self.assertTrue(all(re.fullmatch(r'#[0-9A-Fa-f]{6}', (node.text or '').strip()) for node in variant))

    def test_normal_muted_selected_and_primary_text_meet_contrast_budget(self):
        root = load('Themes/StudioPaletteResources.xaml')
        pairs = [('StudioForegroundColor', 'StudioSurfaceColor'), ('StudioForegroundColor', 'StudioInputColor'),
                 ('StudioMutedColor', 'StudioSurfaceColor'), ('StudioMutedColor', 'StudioInputColor'),
                 ('StudioSelectionForegroundColor', 'StudioSelectionColor'), ('StudioOnAccentColor', 'StudioAccentColor'),
                 ('StudioDangerColor', 'StudioSurfaceColor')]
        for variant in root.find(XAML + 'ResourceDictionary.ThemeDictionaries'):
            if variant.get(X + 'Key') == 'HighContrast':
                continue
            colors = {node.get(X + 'Key'): node.text.strip() for node in variant}
            for foreground, background in pairs:
                with self.subTest(theme=variant.get(X + 'Key'), pair=(foreground, background)):
                    self.assertGreaterEqual(ratio(colors[foreground], colors[background]), 4.5)

    def test_workspace_and_custom_templates_do_not_fork_the_palette(self):
        for name in ('SvgEditorWorkspace.xaml', 'Themes/Generic.xaml', 'Themes/StudioThemeResources.xaml'):
            root = load(name)
            self.assertFalse(list(root.iter(XAML + 'ResourceDictionary.ThemeDictionaries')), name)
            self.assertFalse(any(re.match(r'#[0-9a-fA-F]{6,8}$', value) for node in root.iter() for value in node.attrib.values()), name)
            self.assertIn('StudioThemeResources', (LIBRARY / name).read_text())

    def test_native_metric_resource_types_match_template_contracts(self):
        root = load('Themes/StudioThemeResources.xaml')
        resources = {node.get(X + 'Key'): node for node in root if node.get(X + 'Key')}
        expected = {'ContentDialogButtonSpacing': XAML + 'GridLength',
                    'TextControlBorderThemeThickness': XAML + 'Thickness',
                    'TextControlBorderThemeThicknessFocused': XAML + 'Thickness',
                    'ColorPickerVerticalOrientationMinWidth': X + 'Double',
                    'NumberBoxMinWidth': X + 'Double'}
        for key, tag in expected.items():
            self.assertEqual(tag, resources[key].tag, key)
        self.assertLessEqual(float(resources['ColorPickerVerticalOrientationMinWidth'].text), 236)

    def test_native_text_and_selection_controls_keep_their_platform_templates(self):
        root = load('Themes/StudioThemeResources.xaml')
        for target in ('TextBox', 'NumberBox', 'ComboBox', 'CheckBox', 'RadioButton', 'ToggleSwitch', 'ContentDialog'):
            styles = [node for node in root.findall(XAML + 'Style') if node.get('TargetType') == target]
            self.assertTrue(styles, target)
            self.assertFalse(any(setter.get('Property') == 'Template' for style in styles for setter in style.findall(XAML + 'Setter')), target)

    def test_all_custom_default_style_keys_have_templates(self):
        custom = set()
        for path in (LIBRARY / 'Controls').glob('*.cs'):
            custom.update(re.findall(r'DefaultStyleKey\s*=\s*typeof\((Studio\w+)\)', path.read_text()))
        root = load('Themes/Generic.xaml')
        templates = {node.get('TargetType', '').split(':')[-1] for node in root.iter(XAML + 'ControlTemplate')}
        self.assertTrue(custom)
        self.assertFalse(custom - templates, 'Missing custom templates: ' + str(custom - templates))
        names = {node.get(X + 'Name') for node in root.iter()}
        for part in ('PART_Number', 'PART_Header', 'PART_Content', 'PART_Tabs', 'PART_Grip', 'PART_Menu', 'PART_Collapse'):
            self.assertIn(part, names)

    def test_button_roles_share_one_template_and_keep_focus_visuals(self):
        root = load('Themes/StudioThemeResources.xaml')
        styles = {node.get(X + 'Key'): node for node in root.findall(XAML + 'Style') if node.get(X + 'Key')}
        base = styles['StudioButtonStyle']
        self.assertTrue(any(node.get('Property') == 'UseSystemFocusVisuals' and node.get('Value') == 'True' for node in base))
        for role in ('StudioPrimaryButtonStyle', 'StudioQuietButtonStyle', 'StudioDangerButtonStyle', 'CompactButton'):
            self.assertEqual('{StaticResource StudioButtonStyle}', styles[role].get('BasedOn'))
        for style in styles.values():
            self.assertFalse(any(node.get('Property') == 'Height' for node in style), 'Use minimum heights so scaled text can grow.')

    def test_application_and_embedded_workspace_both_use_shared_resources(self):
        app = (ROOT / 'samples/UnoDraw/App.xaml').read_text()
        workspace = (LIBRARY / 'SvgEditorWorkspace.xaml').read_text()
        self.assertIn('StudioThemeResources', app)
        self.assertIn('StudioThemeResources', workspace)
        self.assertNotIn('SystemAccentColor', app)


if __name__ == '__main__':
    unittest.main()
