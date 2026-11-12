using System.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Svg.Editor.Core;
using Windows.Storage;
using Windows.Storage.Pickers;
using Windows.System;

namespace Svg.Editor.Skia.Uno;

public sealed partial class SvgEditorWorkspace
{
    private const string EditorShortcutStorageKey = "UnoDraw.EditorTheme.Shortcuts.v1";
    private SvgEditorShortcutProfile _editorShortcuts = new();
    private string? _editorShortcutStorageSource;

    private sealed record EditorShortcutRow(EditorCommand Command, string Gesture, bool Customized)
    {
        public override string ToString()
            => $"{Command.Group} · {Command.Label}    {(Gesture.Length == 0 ? "Unassigned" : Gesture)}{(Customized ? " *" : string.Empty)}";
    }

    private void LoadEditorShortcutProfile()
    {
        try
        {
            _editorShortcutStorageSource = ApplicationData.Current.LocalSettings.Values[EditorShortcutStorageKey] as string;
            if (_editorShortcutStorageSource is { Length: > 0 } xml)
                _editorShortcuts = SvgEditorShortcutProfile.Parse(xml);
        }
        catch (Exception exception)
        {
            // A malformed imported or interrupted old profile must not prevent the
            // workspace from opening. Keep its stored text until the user resets it.
            _editorShortcuts = new SvgEditorShortcutProfile();
            void NotifyOnce(object sender, RoutedEventArgs args)
            {
                Loaded -= NotifyOnce;
                StatusText.Text = "Default keyboard shortcuts are active. Saved shortcuts could not be loaded: " + exception.Message;
            }
            Loaded += NotifyOnce;
        }
    }

    private async Task ShowEditorShortcutSettingsAsync()
    {
        var stored = ApplicationData.Current.LocalSettings.Values[EditorShortcutStorageKey] as string;
        if (!string.Equals(stored, _editorShortcutStorageSource, StringComparison.Ordinal))
        {
            // Refresh changes from another window before taking an editable draft.
            var current = string.IsNullOrEmpty(stored) ? new SvgEditorShortcutProfile() : SvgEditorShortcutProfile.Parse(stored);
            _editorShortcuts = current;
            _editorShortcutStorageSource = stored;
            ApplyEditorShortcutMetadata();
            RefreshEditorMenus();
        }
        var expectedStorage = stored;
        var draft = _editorShortcuts.Clone();
        var name = new TextBox { Header = "Profile name", Text = draft.Name, MaxLength = 64 };
        var search = new TextBox { PlaceholderText = "Find a command, tool, or shortcut…" };
        var list = new ListView { Height = 216, SelectionMode = ListViewSelectionMode.Single };
        var selected = new TextBlock { TextWrapping = TextWrapping.Wrap };
        var capture = new TextBox { Header = "Shortcut", IsReadOnly = true, PlaceholderText = "Click here and press the shortcut" };
        var feedback = StudioHint("Changes stay in this draft until you choose Apply. An asterisk marks a customized command.");
        var error = new TextBlock { TextWrapping = TextWrapping.Wrap, Foreground = (Brush)Resources["StudioDanger"] };
        AutomationProperties.SetName(search, "Search keyboard shortcuts");
        AutomationProperties.SetName(list, "Commands and assigned keyboard shortcuts");
        AutomationProperties.SetName(capture, "Record a keyboard shortcut");
        AutomationProperties.SetAutomationId(capture, "editor-shortcut-capture");
        AutomationProperties.SetAutomationId(list, "editor-shortcut-commands");
        AutomationProperties.SetLiveSetting(error, Microsoft.UI.Xaml.Automation.Peers.AutomationLiveSetting.Polite);

        var panel = new StackPanel { Width = DialogContentWidth(640), Spacing = 10 };
        panel.Children.Add(name);
        panel.Children.Add(search);
        panel.Children.Add(list);
        panel.Children.Add(selected);
        panel.Children.Add(capture);
        var editing = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        var assign = new Button { Content = "Assign", IsEnabled = false };
        var clear = new Button { Content = "Clear" };
        var reset = new Button { Content = "Reset command" };
        editing.Children.Add(assign);
        editing.Children.Add(clear);
        editing.Children.Add(reset);
        panel.Children.Add(editing);
        var profiles = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        var defaults = new Button { Content = "Reset all" };
        var import = new Button { Content = "Import…" };
        var export = new Button { Content = "Export…" };
        profiles.Children.Add(defaults);
        profiles.Children.Add(import);
        profiles.Children.Add(export);
        panel.Children.Add(profiles);
        panel.Children.Add(feedback);
        panel.Children.Add(error);
        panel.Children.Add(StudioHint("Tab moves focus; Esc closes this dialog. Text editing, menu navigation, and held Space remain protected. Profiles use Ctrl/Cmd automatically on each platform."));
        var editorHost = new ContentControl { Content = panel, HorizontalContentAlignment = HorizontalAlignment.Stretch, IsTabStop = false };
        var dialog = MakeDialog("Keyboard shortcuts", editorHost, "Apply", "Cancel");
        dialog.DefaultButton = ContentDialogButton.None;
        (SvgEditorShortcutKey Key, SvgEditorShortcutModifiers Modifiers)? recorded = null;
        var refreshing = false;
        var busy = false;

        void UpdateSelection()
        {
            var command = (list.SelectedItem as EditorShortcutRow)?.Command;
            selected.Text = command is null ? "Choose a command." : command.Group + " / " + command.Label;
            capture.Text = command is null ? string.Empty : draft.GetShortcutText(command.Id, _editorUsesCommandKey);
            capture.IsEnabled = clear.IsEnabled = reset.IsEnabled = command is not null;
            recorded = null;
            assign.IsEnabled = false;
            error.Text = string.Empty;
        }
        void Refresh(string? preferredId = null)
        {
            preferredId ??= (list.SelectedItem as EditorShortcutRow)?.Command.Id;
            var query = search.Text.Trim();
            var rows = _commands.OrderBy(command => command.Group, StringComparer.Ordinal)
                .ThenBy(command => command.Label, StringComparer.Ordinal)
                .Select(command => new EditorShortcutRow(command, draft.GetShortcutText(command.Id, _editorUsesCommandKey), draft.IsCustomized(command.Id)))
                .Where(row => query.Length == 0 || row.ToString().Contains(query, StringComparison.OrdinalIgnoreCase)).ToArray();
            refreshing = true;
            list.ItemsSource = rows;
            list.SelectedItem = rows.FirstOrDefault(row => row.Command.Id == preferredId) ?? rows.FirstOrDefault();
            refreshing = false;
            UpdateSelection();
        }
        void AssignRecorded()
        {
            if (list.SelectedItem is not EditorShortcutRow row || recorded is not { } gesture)
                return;
            var conflict = draft.FindShortcut(gesture.Key, gesture.Modifiers);
            if (conflict is not null && conflict.CommandId != row.Command.Id)
            {
                var owner = _commands.FirstOrDefault(command => command.Id == conflict.CommandId)?.Label ?? conflict.CommandId;
                error.Text = "Already assigned to “" + owner + "”. Clear that command first, then assign this shortcut.";
                return;
            }
            if (!draft.TryAssign(row.Command.Id, gesture.Key, gesture.Modifiers, out var problem))
            { error.Text = problem; return; }
            Refresh(row.Command.Id);
            feedback.Text = "Shortcut assigned in this draft. Choose Apply to save.";
        }
        list.SelectionChanged += (_, _) => { if (!refreshing) UpdateSelection(); };
        search.TextChanged += (_, _) => Refresh();
        capture.AddHandler(KeyDownEvent, new KeyEventHandler((_, args) =>
        {
            if (args.Key == VirtualKey.Escape || IsEditorModifierKey(args.Key))
                return;
            if (!TryGetEditorModifiers(out var modifiers, args.Key))
            {
                if (args.Key == VirtualKey.Tab)
                    return;
                args.Handled = true;
                error.Text = "Use the platform's Ctrl/Cmd modifier. AltGr and system chords are reserved.";
                return;
            }
            if (args.Key == VirtualKey.Tab && !SvgEditorShortcutCatalog.UsesPhysicalControl((SvgEditorShortcutKey)args.Key, modifiers))
                return;
            args.Handled = true;
            recorded = ((SvgEditorShortcutKey)args.Key, modifiers);
            capture.Text = SvgEditorShortcutCatalog.FormatGesture(recorded.Value.Key, modifiers, _editorUsesCommandKey);
            assign.IsEnabled = list.SelectedItem is EditorShortcutRow;
            error.Text = string.Empty;
        }), true);
        assign.Click += (_, _) => AssignRecorded();
        clear.Click += (_, _) =>
        {
            if (list.SelectedItem is not EditorShortcutRow row)
                return;
            try
            { draft.Clear(row.Command.Id); Refresh(row.Command.Id); feedback.Text = "Shortcut cleared in this draft."; }
            catch (Exception exception) { error.Text = exception.Message; }
        };
        reset.Click += (_, _) =>
        {
            if (list.SelectedItem is not EditorShortcutRow row)
                return;
            if (draft.TryResetCommand(row.Command.Id, out var problem))
            { Refresh(row.Command.Id); feedback.Text = "Default shortcut restored for this command."; }
            else
                error.Text = problem;
        };
        defaults.Click += (_, _) =>
        {
            draft.ResetDefaults();
            Refresh();
            feedback.Text = "Default gestures restored in this draft. Choose Apply to save.";
        };

        async Task ProfileFileAsync(bool importing)
        {
            if (busy)
                return;
            busy = true;
            editorHost.IsEnabled = false;
            dialog.IsPrimaryButtonEnabled = false;
            try
            {
                if (importing)
                {
                    var picker = new FileOpenPicker();
                    picker.FileTypeFilter.Add(".unoshortcuts");
                    picker.FileTypeFilter.Add(".xml");
                    InitializePicker(picker);
                    var file = await picker.PickSingleFileAsync();
                    if (file is null)
                        return;
                    using var input = await file.OpenStreamForReadAsync();
                    using var reader = new StreamReader(input, Encoding.UTF8, true, 4096);
                    var xml = new StringBuilder();
                    var buffer = new char[4096];
                    int count;
                    while ((count = await reader.ReadAsync(buffer)) > 0)
                    {
                        if (xml.Length + count > SvgEditorShortcutProfile.MaximumCharacters)
                            throw new InvalidOperationException("The shortcut profile exceeds the supported size.");
                        xml.Append(buffer, 0, count);
                    }
                    var candidate = SvgEditorShortcutProfile.Parse(xml.ToString());
                    draft = candidate;
                    name.Text = draft.Name;
                    Refresh();
                    feedback.Text = "Profile imported into this draft. Choose Apply to use it.";
                }
                else
                {
                    draft.Name = name.Text;
                    var picker = new FileSavePicker { SuggestedFileName = "UnoDraw-keyboard-shortcuts", DefaultFileExtension = ".unoshortcuts" };
                    picker.FileTypeChoices.Add("EditorTheme keyboard shortcuts", new List<string> { ".unoshortcuts" });
                    InitializePicker(picker);
                    if (await picker.PickSaveFileAsync() is { } file)
                    {
                        await WriteFileAsync(file, Encoding.UTF8.GetBytes(draft.Serialize()));
                        feedback.Text = "Shortcut profile exported.";
                    }
                }
            }
            catch (Exception exception) { error.Text = exception.Message; }
            finally { busy = false; editorHost.IsEnabled = true; dialog.IsPrimaryButtonEnabled = true; }
        }
        import.Click += async (_, _) => await ProfileFileAsync(true);
        export.Click += async (_, _) => await ProfileFileAsync(false);
        dialog.Closing += (_, args) => { if (busy) args.Cancel = true; };
        dialog.PrimaryButtonClick += (_, args) =>
        {
            if (args.Cancel)
                return;
            try
            {
                draft.Name = name.Text;
                var current = ApplicationData.Current.LocalSettings.Values[EditorShortcutStorageKey] as string;
                if (!string.Equals(current, expectedStorage, StringComparison.Ordinal))
                    throw new InvalidOperationException("Shortcut settings changed in another window. Cancel and reopen this dialog to use the latest profile.");
                var xml = draft.Serialize();
                ApplicationData.Current.LocalSettings.Values[EditorShortcutStorageKey] = xml;
                _editorShortcuts = draft.Clone();
                _editorShortcutStorageSource = xml;
                RestoreEditorNavigation();
                ApplyEditorShortcutMetadata();
                RefreshEditorMenus();
                StatusText.Text = "Keyboard shortcuts saved: " + draft.Name;
            }
            catch (Exception exception) { args.Cancel = true; error.Text = exception.Message; }
        };
        dialog.Opened += (_, _) => search.Focus(FocusState.Programmatic);
        Refresh();
        await ShowDialogAsync(dialog);
    }
}
