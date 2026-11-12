"""Editor dialogs retain native lifecycle parts and share the action hierarchy."""
from pathlib import Path
import unittest
import xml.etree.ElementTree as ET

ROOT = Path(__file__).resolve().parents[2] / 'src/Svg.Editor.Skia.Uno'
XAML = '{http://schemas.microsoft.com/winfx/2006/xaml/presentation}'


class StudioDialogThemeContracts(unittest.TestCase):
    def test_dialog_buttons_do_not_fall_back_to_unstyled_platform_actions(self):
        root = ET.parse(ROOT / 'Themes/EditorTheme.xaml').getroot()
        styles = [style for style in root.findall(XAML + 'Style') if style.get('TargetType') == 'ContentDialog']
        self.assertEqual(1, len(styles))
        setters = {setter.get('Property'): setter.get('Value') for setter in styles[0].findall(XAML + 'Setter')}
        self.assertEqual('{StaticResource StudioPrimaryButtonStyle}', setters['PrimaryButtonStyle'])
        self.assertEqual('{StaticResource StudioButtonStyle}', setters['SecondaryButtonStyle'])
        self.assertEqual('{StaticResource StudioButtonStyle}', setters['CloseButtonStyle'])
        self.assertEqual('{StaticResource EditorContentDialogTemplate}', setters['Template'])

    def test_dialog_template_preserves_native_lifecycle_and_button_states(self):
        root = ET.parse(ROOT / 'Themes/EditorDialogTemplates.xaml').getroot()
        x = '{http://schemas.microsoft.com/winfx/2006/xaml}'
        required = {'Container', 'LayoutRoot', 'BackgroundElement', 'ScaleTransform', 'DialogSpace',
                    'ContentPanel', 'ContentScrollViewer', 'Content', 'Title', 'CommandSpace',
                    'PrimaryButton', 'SecondaryButton', 'CloseButton', 'Button1Host', 'Button2Host'}
        self.assertFalse(required - {node.get(x + 'Name') for node in root.iter()})
        states = {node.get(x + 'Name') for node in root.iter(XAML + 'VisualState')}
        self.assertTrue({'AllVisible', 'NoneVisible', 'PrimaryVisible', 'SecondaryVisible', 'CloseVisible',
                         'PrimaryAndSecondaryVisible', 'PrimaryAndCloseVisible', 'SecondaryAndCloseVisible',
                         'DialogShowing', 'DialogHidden', 'DialogShowingWithoutSmokeLayer',
                         'FullDialogSizing', 'DefaultDialogSizing'} <= states)
        for node in root.iter(XAML + 'Button'):
            name = node.get(x + 'Name')
            if name in {'PrimaryButton', 'SecondaryButton', 'CloseButton'}:
                self.assertEqual('{TemplateBinding ' + name + 'Style}', node.get('Style'))


if __name__ == '__main__':
    unittest.main()
