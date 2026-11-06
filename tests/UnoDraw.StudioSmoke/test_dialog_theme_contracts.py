"""Native dialogs keep their interaction template but share the studio action hierarchy."""
from pathlib import Path
import unittest
import xml.etree.ElementTree as ET

ROOT = Path(__file__).resolve().parents[2] / 'src/Svg.Editor.Skia.Uno'
XAML = '{http://schemas.microsoft.com/winfx/2006/xaml/presentation}'


class StudioDialogThemeContracts(unittest.TestCase):
    def test_dialog_buttons_do_not_fall_back_to_unstyled_platform_actions(self):
        root = ET.parse(ROOT / 'Themes/StudioThemeResources.xaml').getroot()
        styles = [style for style in root.findall(XAML + 'Style') if style.get('TargetType') == 'ContentDialog']
        self.assertEqual(1, len(styles))
        setters = {setter.get('Property'): setter.get('Value') for setter in styles[0].findall(XAML + 'Setter')}
        self.assertEqual('{StaticResource StudioPrimaryButtonStyle}', setters['PrimaryButtonStyle'])
        self.assertEqual('{StaticResource StudioButtonStyle}', setters['SecondaryButtonStyle'])
        self.assertEqual('{StaticResource StudioButtonStyle}', setters['CloseButtonStyle'])
        self.assertNotIn('Template', setters, 'Keep native dialog lifecycle, keyboard and button deferrals.')


if __name__ == '__main__':
    unittest.main()
