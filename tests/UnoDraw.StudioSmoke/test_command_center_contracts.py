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

    def test_labels_and_both_dispatch_paths_use_the_active_profile(self):
        accelerators = (ROOT / 'SvgEditorWorkspace.AppAccelerators.cs').read_text()
        routing = (ROOT / 'SvgEditorWorkspace.CommandRouting.cs').read_text()
        self.assertIn('_editorShortcuts.GetShortcutText(', accelerators)
        self.assertIn('command.Shortcut = EditorShortcutText(command.Id)', accelerators)
        self.assertIn('_editorShortcuts.Bindings', accelerators)
        self.assertIn('_editorShortcuts.Resolve(', accelerators)
        self.assertIn('_editorShortcuts.Resolve(', routing)
        # Key meanings and modifier precedence are tested by the executable core
        # tests. These checks catch a UI adapter bypassing that tested profile.
        self.assertNotIn('args.Key switch', routing)
        self.assertNotIn('Add(VirtualKey.', accelerators)
        self.assertIn('TryGetEditorModifiers(', routing)
        self.assertIn('TryGetEditorModifiers(', accelerators)

    def test_accelerators_preserve_native_focus_scopes_and_modal_input(self):
        accelerators = (ROOT / 'SvgEditorWorkspace.AppAccelerators.cs').read_text()
        routing = (ROOT / 'SvgEditorWorkspace.CommandRouting.cs').read_text()
        self.assertIn('shortcut.Scope is SvgEditorShortcutScope.Canvas or SvgEditorShortcutScope.CanvasOrDocumentTree', accelerators)
        self.assertIn('EditorShortcutContext(focused)', accelerators)
        self.assertIn('StudioContainsFocus(focused, this)', routing)
        self.assertIn('StudioIsTextInput(focused)', routing)
        self.assertIn('SvgEditorShortcutContext.TextInput', routing)
        self.assertIn('SvgEditorShortcutContext.OtherControl', routing)
        self.assertIn('_dialogVisible', accelerators)
        self.assertIn('_dialogVisible', routing)
        self.assertIn('HasEditorCommand(shortcut.CommandId)', accelerators)
        self.assertLess(accelerators.index('args.Handled = true'), accelerators.index('await RunCommandAsync'))
        self.assertLess(routing.index('command.CanExecute?.Invoke()'), routing.index('CommitInspectorEdits();'))

    def test_key_release_and_window_lifetime_cannot_leave_navigation_latched(self):
        accelerators = (ROOT / 'SvgEditorWorkspace.AppAccelerators.cs').read_text()
        routing = (ROOT / 'SvgEditorWorkspace.CommandRouting.cs').read_text()
        tree = ET.parse(ROOT / 'SvgEditorWorkspace.xaml').getroot()
        # Observe handled releases once, even if focus moved to a native control.
        self.assertNotIn('KeyUp', tree.attrib)
        self.assertIn('AddHandler(KeyUpEvent, new KeyEventHandler(Workspace_KeyUp), true)', accelerators)
        self.assertIn('RemoveHandler(KeyUpEvent, new KeyEventHandler(Workspace_KeyUp))', accelerators)
        self.assertIn('Canvas.KeyboardShortcutsManagedExternally = true', accelerators)
        self.assertIn('LostFocus +=', accelerators)
        self.assertIn('Unloaded +=', accelerators)
        self.assertIn('Activated -= EditorWindow_Activated', accelerators)
        self.assertIn('CoreWindowActivationState.Deactivated', accelerators)
        self.assertIn('_heldEditorShortcutKeys.Clear()', accelerators)
        self.assertIn('Canvas.PanMode = _spacePreviousPanMode', routing)
        self.assertIn('Canvas.ZoomMode = _spacePreviousZoomMode', routing)
        self.assertIn('Canvas.ZoomOutMode = _spacePreviousZoomOutMode', routing)

    def test_profile_updates_rebuild_accelerators_and_notify_visible_shortcuts(self):
        accelerators = (ROOT / 'SvgEditorWorkspace.AppAccelerators.cs').read_text()
        settings = (ROOT / 'SvgEditorWorkspace.ShortcutSettings.cs').read_text()
        self.assertIn('KeyboardAccelerators.Remove(accelerator)', accelerators)
        self.assertIn('RebuildEditorAccelerators()', accelerators)
        self.assertIn('EditorShortcutsChanged?.Invoke(', accelerators)
        self.assertIn('RefreshEditorShellShortcuts()', accelerators)
        self.assertIn('var draft = _editorShortcuts.Clone()', settings)
        self.assertIn('ApplyEditorShortcutMetadata()', settings)
        self.assertIn('dialog.PrimaryButtonClick +=', settings)

    def test_canvas_focus_has_native_keyboard_context_menu_access(self):
        routing = (ROOT / 'SvgEditorWorkspace.CommandRouting.cs').read_text()
        self.assertIn('context == SvgEditorShortcutContext.Canvas && Canvas.ContextFlyout', routing)
        self.assertIn('args.Key == VirtualKey.Application && modifiers == SvgEditorShortcutModifiers.None', routing)
        self.assertIn('args.Key == VirtualKey.F10 && modifiers == SvgEditorShortcutModifiers.Shift', routing)
        self.assertIn('contextMenu.ShowAt(Canvas, new FlyoutShowOptions', routing)
        self.assertIn('Canvas.Viewport.WorldToView', routing)

    def test_palette_execution_happens_after_modal_completion(self):
        source = (ROOT / 'SvgEditorWorkspace.CommandCenter.cs').read_text()
        self.assertIn('search.CommandInvoked', source)
        self.assertLess(source.index('await ShowDialogAsync(dialog)'), source.index('await RunCommandAsync(selectedId)'))
        self.assertIn('_recentStudioCommands.Count > 16', source)
        self.assertNotIn('Controller.Execute', source)


if __name__ == '__main__':
    unittest.main()
