using System.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Svg.Editor.Skia;
using Windows.Storage;
using Windows.Storage.Pickers;

namespace Svg.Editor.Skia.Uno;

public sealed partial class SvgEditorWorkspace
{
    private const string NamedWorkspaceIdentityKey = "UnoDraw.Studio.ActiveWorkspace.v1";
    private static readonly SemaphoreSlim NamedWorkspaceStorageGate = new(1, 1);
    private SvgWorkspaceLibrary _namedWorkspaces = new();
    private Task? _namedWorkspacesLoad;
    private string? _namedWorkspacesDiskSource;
    private string? _activeNamedWorkspaceId;
    private bool _namedWorkspacesLoaded;
    private sealed record WorkspaceChoice(string Label, SvgWorkspacePreset? Preset = null, string? Id = null)
    { public override string ToString() => Label; }
    private static string NamedWorkspacePath => Path.Combine(Path.GetDirectoryName(RecoveryDirectory)!, "Workspaces.xml");

    private void BuildNamedWorkspaceCommands()
    {
        _commands.Add(new EditorCommand("workspace-save", "Save workspace as…", "Window", string.Empty, SaveNamedWorkspaceAsync));
        _commands.Add(new EditorCommand("workspace-manage", "Manage saved workspaces…", "Window", string.Empty, ManageNamedWorkspacesAsync));
        _commands.Add(new EditorCommand("workspace-restore-saved", "Restore saved workspace", "Window", string.Empty, () =>
        {
            if (_activeNamedWorkspaceId is not { } id || _namedWorkspaces.Find(id) is not { } entry)
                throw new InvalidOperationException("Choose a saved workspace first.");
            ApplyNamedWorkspace(entry);
            return Task.CompletedTask;
        }));
        _commands.Add(new EditorCommand("workspace-import", "Import workspace library…", "Window", string.Empty, ImportNamedWorkspacesAsync));
        _commands.Add(new EditorCommand("workspace-export", "Export workspace library…", "Window", string.Empty, ExportNamedWorkspacesAsync));
        Loaded += async (_, _) =>
        {
            try { await EnsureNamedWorkspacesAsync(); }
            catch (Exception exception) { if (!_disposed) StatusText.Text = "Saved workspaces could not be loaded: " + exception.Message; }
        };
        var menu = new MenuFlyout();
        foreach (var command in _commands.Where(command => command.Id is "workspace-save" or "workspace-manage" or "workspace-restore-saved" or "workspace-import" or "workspace-export"))
        {
            var item = new MenuFlyoutItem { Text = command.Label };
            item.Click += async (_, _) => await RunCommandAsync(command.Id);
            menu.Items.Add(item);
        }
        WorkspacePresetPicker.ContextFlyout = menu;
        ToolTipService.SetToolTip(WorkspacePresetPicker, "Switch workspaces. Right-click for save, manage and portable library commands.");
    }
    private async Task EnsureNamedWorkspacesAsync()
    {
        if (_namedWorkspacesLoaded) return;
        var loading = _namedWorkspacesLoad ??= LoadNamedWorkspacesAsync();
        try { await loading; }
        catch { if (ReferenceEquals(_namedWorkspacesLoad, loading)) _namedWorkspacesLoad = null; throw; }
    }
    private async Task LoadNamedWorkspacesAsync()
    {
        await NamedWorkspaceStorageGate.WaitAsync();
        try
        {
            await EnsureRecoveryStorageAsync();
            var xml = await ReadWorkspaceLibraryFileAsync(NamedWorkspacePath);
            var library = xml is null ? new SvgWorkspaceLibrary() : SvgWorkspaceLibrary.Parse(xml);
            if (_disposed) return;
            if (!_namedWorkspacesLoaded)
            {
                try { _activeNamedWorkspaceId = ApplicationData.Current.LocalSettings.Values[NamedWorkspaceIdentityKey] as string; }
                catch (Exception) { _activeNamedWorkspaceId = null; }
            }
            _namedWorkspaces = library; _namedWorkspacesDiskSource = xml; _namedWorkspacesLoaded = true;
            SynchronizeNamedWorkspacePicker();
        }
        finally { NamedWorkspaceStorageGate.Release(); }
    }
    private static async Task<string?> ReadWorkspaceLibraryFileAsync(string path)
    {
        if (!File.Exists(path)) return null;
        using var stream = File.OpenRead(path);
        return await ReadBoundedWorkspaceXmlAsync(stream);
    }
    private static async Task<string> ReadBoundedWorkspaceXmlAsync(Stream stream)
    {
        using var reader = new StreamReader(stream, Encoding.UTF8, true, 4096, leaveOpen: true);
        var output = new StringBuilder(); var buffer = new char[4096]; int count;
        while ((count = await reader.ReadAsync(buffer, 0, buffer.Length)) != 0)
        {
            if (output.Length + count > SvgWorkspaceLibrary.MaximumCharacters)
                throw new InvalidOperationException("The workspace library exceeds the supported size.");
            output.Append(buffer, 0, count);
        }
        return output.ToString();
    }
    private async Task PersistNamedWorkspacesAsync(SvgWorkspaceLibrary library, SvgWorkspaceLibrary expected)
    {
        var xml = library.Serialize();
        await NamedWorkspaceStorageGate.WaitAsync();
        string? temporary = null;
        try
        {
            if (_disposed) throw new ObjectDisposedException(nameof(SvgEditorWorkspace));
            if (!ReferenceEquals(expected, _namedWorkspaces))
                throw new InvalidOperationException("The library changed while this edit was open. Cancel and reopen its manager before retrying.");
            var current = await ReadWorkspaceLibraryFileAsync(NamedWorkspacePath);
            if (!string.Equals(current, _namedWorkspacesDiskSource, StringComparison.Ordinal))
                throw new InvalidOperationException("The workspace library changed in another window or process. Cancel, reopen Manage saved workspaces, and retry; the newer file has not been overwritten.");
            Directory.CreateDirectory(Path.GetDirectoryName(NamedWorkspacePath)!);
            temporary = NamedWorkspacePath + "." + Guid.NewGuid().ToString("N") + ".tmp";
            await File.WriteAllTextAsync(temporary, xml, new UTF8Encoding(false));
            if (_disposed) throw new ObjectDisposedException(nameof(SvgEditorWorkspace));
            // This gate serializes windows in this process. The file check is optimistic
            // conflict detection, not a cross-process transactional filesystem lock.
            if (!string.Equals(await ReadWorkspaceLibraryFileAsync(NamedWorkspacePath), current, StringComparison.Ordinal))
                throw new InvalidOperationException("The workspace library changed during saving. Reopen its manager before retrying.");
            File.Move(temporary, NamedWorkspacePath, overwrite: true); temporary = null;
            _namedWorkspaces = library; _namedWorkspacesDiskSource = xml;
            if (_activeNamedWorkspaceId is { } id && library.Find(id) is null) _activeNamedWorkspaceId = null;
            SynchronizeNamedWorkspacePicker();
        }
        finally
        {
            try
            {
                if (temporary is not null) File.Delete(temporary);
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException) { }
            finally { NamedWorkspaceStorageGate.Release(); }
        }
    }
    private async Task ReloadNamedWorkspacesAsync()
    {
        await EnsureNamedWorkspacesAsync();
        await LoadNamedWorkspacesAsync();
    }

    private void SynchronizeNamedWorkspacePicker()
    {
        if (!_studioInitialized) return;
        _syncingStudioPreset = true;
        try
        {
            var xml = _studioLayout.Serialize();
            var active = _activeNamedWorkspaceId is { } id ? _namedWorkspaces.Find(id) : null;
            var choices = Enum.GetValues<SvgWorkspacePreset>().Select(preset => new WorkspaceChoice(preset.ToString(), preset)).ToList();
            choices.AddRange(_namedWorkspaces.Entries.Select(entry => new WorkspaceChoice(
                entry.Name + (entry.Id == active?.Id && entry.LayoutXml != xml ? " *" : string.Empty), Id: entry.Id)));
            WorkspaceChoice? selected = active is null ? null : choices.First(choice => choice.Id == active.Id);
            if (selected is null)
                foreach (var choice in choices.Where(choice => choice.Preset is not null))
                {
                    var candidate = new SvgWorkspaceLayout(); candidate.Reset(choice.Preset!.Value);
                    if (Enum.GetValues<SvgPanelDock>().All(dock => _studioLayout.Panels.Where(panel => panel.Dock == dock).Select(panel => panel.Id)
                        .SequenceEqual(candidate.Panels.Where(panel => panel.Dock == dock).Select(panel => panel.Id))))
                    { selected = choice; break; }
                }
            if (_namedWorkspacesLoaded) _activeNamedWorkspaceId = active?.Id;
            var current = WorkspacePresetPicker.ItemsSource as IReadOnlyList<WorkspaceChoice>;
            if (current is null || !current.SequenceEqual(choices))
            {
                WorkspacePresetPicker.ItemsSource = choices;
                current = choices;
            }
            WorkspacePresetPicker.SelectedItem = current.FirstOrDefault(choice => choice == selected);
            WorkspacePresetPicker.PlaceholderText = "Custom workspace";
            if (_namedWorkspacesLoaded)
            {
                try { ApplicationData.Current.LocalSettings.Values[NamedWorkspaceIdentityKey] = _activeNamedWorkspaceId ?? string.Empty; }
                catch (Exception exception) { StatusText.Text = "Workspace identity could not be saved: " + exception.Message; }
            }
        }
        finally { _syncingStudioPreset = false; }
    }
    private void SelectNamedWorkspaceChoice()
    {
        if (!_studioInitialized || _syncingStudioPreset || WorkspacePresetPicker.SelectedItem is not WorkspaceChoice choice) return;
        try
        {
            CommitInspectorEdits();
            if (choice.Preset is { } preset) { _activeNamedWorkspaceId = null; ResetStudioLayout(preset); }
            else if (choice.Id is { } id && _namedWorkspaces.Find(id) is { } entry) ApplyNamedWorkspace(entry);
        }
        catch (Exception exception) { SynchronizeNamedWorkspacePicker(); StatusText.Text = exception.Message; }
    }
    private void ApplyNamedWorkspace(SvgNamedWorkspace entry)
    {
        CommitInspectorEdits(); _activeNamedWorkspaceId = entry.Id; _zenMode = false;
        RestoreWorkspaceLayout(entry.LayoutXml);
        StatusText.Text = "Workspace restored: " + entry.Name;
    }

    private async Task SaveNamedWorkspaceAsync()
    {
        await ReloadNamedWorkspacesAsync();
        var expected = _namedWorkspaces;
        var name = new TextBox { Header = "Workspace name", MaxLength = SvgWorkspaceLibrary.MaximumNameLength, PlaceholderText = "My drawing desk" };
        AutomationProperties.SetAutomationId(name, "StudioWorkspaceName");
        var error = StudioHint(string.Empty);
        var panel = new StackPanel { Spacing = 10, Width = DialogContentWidth(360) };
        panel.Children.Add(StudioHint("Save panel positions, active tabs, sizes, rulers and tool density. SVG artwork and undo history are not changed."));
        panel.Children.Add(name); panel.Children.Add(error);
        // IsEnabled belongs to Control, not Panel. A content host disables both
        // pointer and keyboard interaction for its descendants during the async save.
        var editorHost = new ContentControl { Content = panel, HorizontalContentAlignment = HorizontalAlignment.Stretch, IsTabStop = false };
        var dialog = MakeDialog("Save workspace", editorHost, "Save workspace");
        dialog.Opened += (_, _) => name.Focus(FocusState.Programmatic);
        var saving = false;
        dialog.Closing += (_, args) => { if (saving) args.Cancel = true; };
        dialog.PrimaryButtonClick += async (_, args) =>
        {
            if (saving) { args.Cancel = true; return; }
            var deferral = args.GetDeferral(); saving = true; dialog.IsPrimaryButtonEnabled = false; editorHost.IsEnabled = false;
            try
            {
                var candidate = expected.Add(name.Text, _studioLayout);
                await PersistNamedWorkspacesAsync(candidate, expected);
                _activeNamedWorkspaceId = candidate.Entries.Last().Id; SynchronizeNamedWorkspacePicker();
            }
            catch (Exception exception) { args.Cancel = true; error.Text = exception.Message; }
            finally { saving = false; editorHost.IsEnabled = true; dialog.IsPrimaryButtonEnabled = true; deferral.Complete(); }
        };
        await ShowDialogAsync(dialog);
    }
    private async Task ManageNamedWorkspacesAsync()
    {
        await ReloadNamedWorkspacesAsync();
        var expected = _namedWorkspaces; var draft = expected;
        var list = new ListView { ItemsSource = draft.Entries, SelectionMode = ListViewSelectionMode.Single, MaxHeight = 260 };
        var name = new TextBox { Header = "Name", MaxLength = SvgWorkspaceLibrary.MaximumNameLength };
        var restore = new CheckBox { Content = "Restore the selected snapshot after saving" };
        var removeConfirmation = new CheckBox { Content = "Confirm removal of the selected saved workspace" };
        var error = StudioHint(string.Empty);
        var panel = new StackPanel { Spacing = 10, Width = DialogContentWidth(440) };
        panel.Children.Add(StudioHint("Changes stay in this draft until Save library. Updating a snapshot is explicit; arranging panels never overwrites a saved workspace."));
        panel.Children.Add(list); panel.Children.Add(name);
        var buttons = new StudioWrapPanel { Spacing = 6 };
        panel.Children.Add(buttons); panel.Children.Add(removeConfirmation); panel.Children.Add(restore); panel.Children.Add(error);
        void Refresh(string? id)
        {
            list.ItemsSource = draft.Entries;
            list.SelectedItem = id is null ? draft.Entries.FirstOrDefault() : draft.Find(id);
        }
        list.SelectionChanged += (_, _) =>
        { name.Text = (list.SelectedItem as SvgNamedWorkspace)?.Name ?? string.Empty; removeConfirmation.IsChecked = false; };
        void Action(string label, Func<SvgWorkspaceLibrary, SvgNamedWorkspace?, SvgWorkspaceLibrary> change)
        {
            var button = new Button { Content = label, Style = (Style)Resources["CompactButton"] };
            button.Click += (_, _) =>
            {
                try
                {
                    var selected = list.SelectedItem as SvgNamedWorkspace; var count = draft.Entries.Count;
                    draft = change(draft, selected);
                    Refresh(draft.Entries.Count > count ? draft.Entries.Last().Id : selected?.Id);
                    error.Text = string.Empty;
                }
                catch (Exception exception) { error.Text = exception.Message; }
            };
            buttons.Children.Add(button);
        }
        Action("Save current as new", (library, _) => library.Add(name.Text, _studioLayout));
        Action("Update snapshot", (library, entry) => library.Update(entry?.Id ?? string.Empty, _studioLayout));
        Action("Rename", (library, entry) => library.Rename(entry?.Id ?? string.Empty, name.Text));
        Action("Remove", (library, entry) => removeConfirmation.IsChecked == true ? library.Remove(entry?.Id ?? string.Empty)
            : throw new InvalidOperationException("Confirm removal first. This does not delete any SVG document."));
        Refresh(_activeNamedWorkspaceId);
        var editorHost = new ContentControl { Content = panel, HorizontalContentAlignment = HorizontalAlignment.Stretch, IsTabStop = false };
        var dialog = MakeDialog("Saved workspaces", editorHost, "Save library");
        var saving = false;
        dialog.Closing += (_, args) => { if (saving) args.Cancel = true; };
        dialog.PrimaryButtonClick += async (_, args) =>
        {
            if (saving) { args.Cancel = true; return; }
            var deferral = args.GetDeferral(); saving = true; dialog.IsPrimaryButtonEnabled = false; editorHost.IsEnabled = false;
            try
            {
                var selected = list.SelectedItem as SvgNamedWorkspace;
                if (restore.IsChecked == true && selected is null) throw new InvalidOperationException("Choose a saved workspace to restore.");
                await PersistNamedWorkspacesAsync(draft, expected);
                if (restore.IsChecked == true) ApplyNamedWorkspace(selected!);
            }
            catch (Exception exception) { args.Cancel = true; error.Text = exception.Message; }
            finally { saving = false; editorHost.IsEnabled = true; dialog.IsPrimaryButtonEnabled = true; deferral.Complete(); }
        };
        await ShowDialogAsync(dialog);
    }
    private async Task ImportNamedWorkspacesAsync()
    {
        await ReloadNamedWorkspacesAsync(); var expected = _namedWorkspaces;
        var picker = new FileOpenPicker(); picker.FileTypeFilter.Add(".unoworkspaces"); picker.FileTypeFilter.Add(".xml"); InitializePicker(picker);
        var file = await picker.PickSingleFileAsync(); if (file is null || _disposed) return;
        using var input = await file.OpenStreamForReadAsync();
        var candidate = expected.Import(await ReadBoundedWorkspaceXmlAsync(input));
        var count = candidate.Entries.Count - expected.Entries.Count;
        var dialog = MakeDialog("Import workspaces", StudioHint($"Add {count} workspace snapshot(s)? Existing snapshots are preserved. Conflicting names receive a numeric suffixes."), "Import");
        if (await ShowDialogAsync(dialog) != ContentDialogResult.Primary) return;
        await PersistNamedWorkspacesAsync(candidate, expected);
        StatusText.Text = $"Imported {count} named workspaces.";
    }
    private async Task ExportNamedWorkspacesAsync()
    {
        await ReloadNamedWorkspacesAsync(); var xml = _namedWorkspaces.Serialize();
        var picker = new FileSavePicker { SuggestedFileName = "UnoDraw-workspaces", DefaultFileExtension = ".unoworkspaces" };
        picker.FileTypeChoices.Add("UnoDraw workspace library", new List<string> { ".unoworkspaces" }); InitializePicker(picker);
        if (await picker.PickSaveFileAsync() is { } file) await WriteFileAsync(file, Encoding.UTF8.GetBytes(xml));
    }
}
