"""Guard shared field-header configuration; native editing smoke checks the runtime layout."""
from pathlib import Path
import unittest
import xml.etree.ElementTree as ET

ROOT = Path(__file__).resolve().parents[2] / 'src/Svg.Editor.Skia.Uno'
XAML = '{http://schemas.microsoft.com/winfx/2006/xaml/presentation}'


class StudioFieldHeaderContracts(unittest.TestCase):
    def test_shared_native_styles_use_content_aware_header_templates(self):
        root = ET.parse(ROOT / 'Themes/StudioThemeResources.xaml').getroot()
        for target in ('TextBox', 'NumberBox', 'ComboBox'):
            styles = [style for style in root.findall(XAML + 'Style') if style.get('TargetType') == target]
            setters = [setter for style in styles for setter in style.findall(XAML + 'Setter')]
            self.assertFalse(any(setter.get('Property') == 'HeaderTemplate' for setter in setters), target)
            self.assertTrue(any(setter.get('Property') == 'local:StudioFieldHeader.Template' for setter in setters), target)

    def test_toolbar_precision_fields_keep_native_inputs_headerless(self):
        root = ET.parse(ROOT / 'Themes/Generic.xaml').getroot()
        fields = [node for node in root.iter(XAML + 'NumberBox') if node.get('{http://schemas.microsoft.com/winfx/2006/xaml}Name') == 'PART_Number']
        self.assertEqual(1, len(fields))
        self.assertNotIn('HeaderTemplate', fields[0].attrib)
        self.assertNotIn('Header', fields[0].attrib)
        self.assertEqual('{StaticResource StudioNumberBoxStyle}', fields[0].get('Style'))


if __name__ == '__main__':
    unittest.main()
