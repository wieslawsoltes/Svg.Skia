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
                 ('StudioHoverForegroundColor', 'StudioHoverColor'),
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
        for name in ('SvgEditorWorkspace.xaml', 'Themes/Generic.xaml', 'Themes/EditorTheme.xaml'):
            root = load(name)
            self.assertFalse(list(root.iter(XAML + 'ResourceDictionary.ThemeDictionaries')), name)
            self.assertFalse(any(re.match(r'#[0-9a-fA-F]{6,8}$', value) for node in root.iter() for value in node.attrib.values()), name)
            self.assertIn('EditorTheme', (LIBRARY / name).read_text())

    def test_native_metric_resource_types_match_template_contracts(self):
        root = load('Themes/EditorTheme.xaml')
        resources = {node.get(X + 'Key'): node for node in root if node.get(X + 'Key')}
        expected = {'ContentDialogButtonSpacing': XAML + 'GridLength',
                    'TextControlBorderThemeThickness': XAML + 'Thickness',
                    'TextControlBorderThemeThicknessFocused': XAML + 'Thickness',
                    'ColorPickerVerticalOrientationMinWidth': X + 'Double',
                    'NumberBoxMinWidth': X + 'Double'}
        for key, tag in expected.items():
            self.assertEqual(tag, resources[key].tag, key)
        self.assertLessEqual(float(resources['ColorPickerVerticalOrientationMinWidth'].text), 236)

    def test_editor_inputs_keep_native_template_parts(self):
        root = load('Themes/EditorInputTemplates.xaml')
        templates = {node.get(X + 'Key'): node for node in root.findall(XAML + 'ControlTemplate')}
        required = {
            'TextBox': {'ContentElement', 'HeaderContentPresenter', 'PlaceholderTextContentPresenter', 'DeleteButton'},
            'NumberBox': {'InputBox', 'UpSpinButton', 'DownSpinButton', 'UpDownPopup', 'PopupContentRoot', 'PopupUpSpinButton', 'PopupDownSpinButton'},
            'ComboBox': {'ContentPresenter', 'PlaceholderTextBlock', 'EditableText', 'DropDownOverlay', 'Popup', 'PopupBorder'},
            'ToggleSwitch': {'SwitchKnob', 'SwitchKnobBounds', 'SwitchThumb', 'HeaderContentPresenter'},
        }
        for target, parts in required.items():
            template = templates['Editor' + target + 'Template']
            self.assertEqual(target, template.get('TargetType'))
            self.assertFalse(parts - {node.get(X + 'Name') for node in template.iter()}, target)
        text = templates['EditorTextBoxTemplate']
        content = next(node for node in text.iter() if node.get(X + 'Name') == 'ContentElement')
        self.assertEqual(XAML + 'ScrollViewer', content.tag, 'Native text composition owns ContentElement.')
        self.assertEqual('False', content.get('IsTabStop'))
        self.assertEqual('{TemplateBinding Padding}', content.get('Padding'))
        number = templates['EditorNumberBoxTemplate']
        input_box = next(node for node in number.iter() if node.get(X + 'Name') == 'InputBox')
        self.assertEqual(XAML + 'TextBox', input_box.tag)
        for property_name in ('InputScope', 'SelectionFlyout', 'SelectionHighlightColor', 'TextReadingOrder'):
            self.assertEqual('{TemplateBinding ' + property_name + '}', input_box.get(property_name))

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

    def test_editor_templates_keep_native_navigation_and_range_parts(self):
        templates = {}
        for name in ('EditorNavigationTemplates', 'EditorRangeTemplates', 'EditorMenuTemplates'):
            templates.update({node.get(X + 'Key'): node for node in load('Themes/' + name + '.xaml').findall(XAML + 'ControlTemplate')})
        contracts = {
            'EditorMenuBarItemTemplate': {'ContentButton': 'Button'},
            'EditorMenuFlyoutPresenterTemplate': {'MenuFlyoutPresenterScrollViewer': 'ScrollViewer'},
            'EditorMenuFlyoutItemTemplate': {'KeyboardAcceleratorTextBlock': 'TextBlock'},
            'EditorListViewTemplate': {'ScrollViewer': 'ScrollViewer'},
            'EditorListViewItemTemplate': {'MultiSelectCheckBox': 'CheckBox'},
            'EditorTreeViewTemplate': {'ListControl': 'TreeViewList'},
            'EditorTreeViewItemTemplate': {'MultiSelectCheckBox': 'CheckBox', 'ExpandCollapseChevron': 'Grid'},
            'EditorTabViewTemplate': {'TabListView': 'TabViewListView', 'TabContentPresenter': 'ContentPresenter', 'AddButton': 'Button', 'TabColumn': 'ColumnDefinition'},
            'EditorTabViewItemTemplate': {'CloseButton': 'Button', 'ContentPresenter': 'ContentPresenter', 'TabSeparator': 'Border'},
            'EditorTabScrollViewerTemplate': {'ScrollContentPresenter': 'ScrollContentPresenter', 'ScrollDecreaseButton': 'RepeatButton', 'ScrollIncreaseButton': 'RepeatButton'},
            'EditorExpanderTemplate': {'ExpanderHeader': 'ToggleButton', 'ExpanderContent': 'Border', 'ExpanderContentClip': 'Border'},
            'EditorScrollViewerTemplate': {'ScrollContentPresenter': 'ScrollContentPresenter', 'VerticalScrollBar': 'ScrollBar', 'HorizontalScrollBar': 'ScrollBar'},
            'EditorScrollBarTemplate': {'VerticalThumb': 'Thumb', 'HorizontalThumb': 'Thumb', 'VerticalLargeDecrease': 'RepeatButton', 'HorizontalLargeIncrease': 'RepeatButton'},
            'EditorSliderTemplate': {'HorizontalThumb': 'Thumb', 'VerticalThumb': 'Thumb', 'HorizontalDecreaseRect': 'Rectangle', 'VerticalDecreaseRect': 'Rectangle', 'SliderContainer': 'Grid'},
            'EditorProgressRingTemplate': {'LayoutRoot': 'Grid', 'LottiePlayer': 'AnimatedVisualPlayer'},
            'EditorProgressBarTemplate': {'LayoutRoot': 'Grid', 'DeterminateProgressBarIndicator': 'Rectangle', 'IndeterminateProgressBarIndicator': 'Rectangle', 'IndeterminateProgressBarIndicator2': 'Rectangle'},
        }
        for key, required in contracts.items():
            parts = {node.get(X + 'Name'): node.tag.split('}')[-1] for node in templates[key].iter()}
            for name, expected_type in required.items():
                with self.subTest(template=key, part=name):
                    self.assertEqual(expected_type, parts.get(name), 'Native OnApplyTemplate requires this named part and type.')

    def test_color_templates_keep_native_color_conversion_and_accessibility_parts(self):
        root = load('Themes/EditorColorTemplates.xaml')
        templates = {node.get(X + 'Key'): node for node in root.findall(XAML + 'ControlTemplate')}
        picker = templates['EditorColorPickerTemplate']
        parts = {node.get(X + 'Name'): node.tag.split('}')[-1] for node in picker.iter()}
        expected = {'ColorSpectrum': 'ColorSpectrum', 'ThirdDimensionSlider': 'ColorPickerSlider', 'AlphaSlider': 'ColorPickerSlider',
                    'ColorRepresentationComboBox': 'ComboBox', 'ColorPreviewRectangleGrid': 'Grid',
                    'ColorPreviewRectangle': 'Rectangle', 'PreviousColorRectangle': 'Rectangle',
                    'AlphaSliderBackgroundRectangle': 'Rectangle', 'ThirdDimensionSliderGradientBrush': 'LinearGradientBrush',
                    'AlphaSliderGradientBrush': 'LinearGradientBrush', 'AlphaSliderCheckeredBackgroundImageBrush': 'ImageBrush',
                    'ColorPreviewRectangleCheckeredBackgroundImageBrush': 'ImageBrush', 'CheckerColorBrush': 'SolidColorBrush'}
        expected.update({channel + 'TextBox': 'TextBox' for channel in ('Red', 'Green', 'Blue', 'Hue', 'Saturation', 'Value', 'Alpha', 'Hex')})
        expected.update({channel + 'Label': 'TextBlock' for channel in ('Red', 'Green', 'Blue', 'Hue', 'Saturation', 'Value', 'Alpha')})
        for name, expected_type in expected.items():
            self.assertEqual(expected_type, parts.get(name), name)
        spectrum = {node.get(X + 'Name'): node.tag.split('}')[-1] for node in templates['EditorColorSpectrumTemplate'].iter()}
        for name, expected_type in {'SizingGrid': 'Grid', 'InputTarget': 'Canvas', 'SelectionEllipsePanel': 'Grid', 'SpectrumRectangle': 'Rectangle',
                                    'SpectrumOverlayRectangle': 'Rectangle', 'SpectrumEllipse': 'Ellipse', 'SpectrumOverlayEllipse': 'Ellipse', 'ColorNameToolTip': 'ToolTip'}.items():
            self.assertEqual(expected_type, spectrum.get(name), name)
        states = {node.get(X + 'Name') for node in picker.iter(XAML + 'VisualState')}
        self.assertTrue({'RgbSelected', 'HsvSelected', 'Vertical', 'Horizontal', 'AlphaEnabled', 'AlphaDisabled',
                         'TextEntryGridVisible', 'TextEntryGridCollapsed', 'PreviousColorVisibleVertical', 'PreviousColorVisibleHorizontal'} <= states)

    def test_theme_modules_resolve_without_a_resource_cycle(self):
        modules = {path.stem: ET.parse(path).getroot() for path in (LIBRARY / 'Themes').glob('*.xaml')}
        visited = set()

        def visit(name, ancestors):
            self.assertNotIn(name, ancestors, 'Merged dictionaries must not recursively instantiate EditorTheme.')
            if name in visited:
                return
            merged = modules[name].find(XAML + 'ResourceDictionary.MergedDictionaries')
            for child in merged if merged is not None else ():
                if child.tag.startswith('{using:Svg.Editor.Skia.Uno}'):
                    dependency = child.tag.split('}')[-1]
                    self.assertIn(dependency, modules, 'Every merged module must ship with the theme.')
                    visit(dependency, ancestors | {name})
            visited.add(name)

        visit('StudioThemeResources', set())
        self.assertIn('EditorTheme', visited)
        self.assertTrue({'EditorInputTemplates', 'EditorMenuTemplates', 'EditorNavigationTemplates',
                         'EditorDialogTemplates', 'EditorRangeTemplates', 'EditorColorTemplates'} <= visited)

    def test_button_roles_share_one_template_and_keep_focus_visuals(self):
        root = load('Themes/EditorTheme.xaml')
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
        self.assertIn('EditorTheme', app)
        self.assertIn('EditorTheme', workspace)
        self.assertNotIn('SystemAccentColor', app)


if __name__ == '__main__':
    unittest.main()
