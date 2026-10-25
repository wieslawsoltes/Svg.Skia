"""Presentation contracts supplement real native command dispatch and core ranking tests."""
from pathlib import Path
import unittest
import xml.etree.ElementTree as ET

ROOT = Path(__file__).resolve().parents[2] / 'src/Svg.Editor.Skia.Uno'
XAML = '{http://schemas.microsoft.com/winfx/2006/xaml/presentation}'
X = '{http://schemas.microsoft.com/winfx/2006/xaml}'


class CommandCenterContracts(unittest.TestCase):
    def test_palette_uses_shared_resources_without_private_colors(self):
        text = (ROOT / 'Controls/StudioCommandSearch.xaml').read_text()
        tree = ET.fromstring(text)
        self.assertIn('StudioThemeResources', text)
        self.assertNotIn('<Color ', text)
        self.assertNotIn('<SolidColorBrush ', text)
        self.assertFalse(list(tree.iter(XAML + 'ControlTemplate')), 'Keep native text and list interaction templates.')

    def test_query_is_bounded_and_results_retain_native_list_selection(self):
        tree = ET.parse(ROOT / 'Controls/StudioCommandSearch.xaml').getroot()
        query = next(node for node in tree.iter(XAML + 'TextBox') if node.get(X + 'Name') == 'Query')
        self.assertEqual('256', query.get('MaxLength'))
        self.assertEqual('StudioCommandSearchBox', query.get('AutomationProperties.AutomationId'))
        results = next(tree.iter(XAML + 'ListView'))
        self.assertEqual('Single', results.get('SelectionMode'))
        self.assertEqual('True', results.get('IsItemClickEnabled'))
        self.assertEqual('Disabled', results.get('ScrollViewer.HorizontalScrollBarVisibility'))

    def test_document_accelerators_do_not_override_text_or_canvas_only_shortcuts(self):
        source = (ROOT / 'SvgEditorWorkspace.AppAccelerators.cs').read_text()
        for key in ('K', 'N', 'O', 'S', 'E', 'P'):
            self.assertIn('Add(VirtualKey.' + key + ',', source)
        for key in ('A', 'C', 'X', 'V', 'Z', 'Y', 'Delete', 'Space', 'Tab'):
            self.assertNotIn('Add(VirtualKey.' + key + ',', source)
        self.assertIn('_dialogVisible', source)
        self.assertIn('StudioContainsFocus(focused, this)', source)
        self.assertLess(source.index('args.Handled = true'), source.index('await RunCommandAsync'))

    def test_palette_execution_happens_after_modal_completion(self):
        source = (ROOT / 'SvgEditorWorkspace.CommandCenter.cs').read_text()
        self.assertIn('search.CommandInvoked', source)
        self.assertLess(source.index('await ShowDialogAsync(dialog)'), source.index('await RunCommandAsync(selectedId)'))
        self.assertIn('_recentStudioCommands.Count > 16', source)
        self.assertNotIn('Controller.Execute', source)


if __name__ == '__main__':
    unittest.main()
