using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Svg.Editor.Skia;

namespace Svg.Editor.Skia.Uno;

public sealed partial class SvgEditorWorkspace
{
    private readonly List<string> _recentStudioCommands = new();

    private void InitializeStudioCommandCenter()
    {
        var index = _commands.FindIndex(command => command.Id == "commands");
        if (index < 0) throw new InvalidOperationException("Register the workspace command catalog before its command center.");
        _commands[index] = new EditorCommand("commands", "Search commands…", "View", "Ctrl+K", ShowStudioCommandCenterAsync);
    }

    private async Task ShowStudioCommandCenterAsync()
    {
        var search = new StudioCommandSearch
        {
            Width = DialogContentWidth(560),
            Height = Math.Clamp((XamlRoot?.Size.Height ?? ActualHeight) - 280, 200, 460)
        };
        // Searching for command search itself would reopen the same dialog, not
        // perform useful work. All other commands retain their unique host IDs.
        search.SetCatalog(_commands.Where(command => command.Id != "commands").Select(command =>
            new SvgCommandCatalogEntry(command.Id, command.Label, command.Group, command.Shortcut)), _recentStudioCommands);
        var dialog = MakeDialog("Commands", search, "Run", "Close");
        dialog.Resources.MergedDictionaries.Add(new StudioThemeResources());
        dialog.IsPrimaryButtonEnabled = search.SelectedCommand is not null;
        string? selectedId = null;
        search.SelectedCommandChanged += (_, _) => dialog.IsPrimaryButtonEnabled = search.SelectedCommand is not null;
        search.CommandInvoked += (_, entry) =>
        {
            if (selectedId is not null) return;
            selectedId = entry.Id;
            dialog.Hide();
        };
        dialog.Opened += (_, _) => search.FocusSearch();
        var result = await ShowDialogAsync(dialog);
        selectedId ??= result == ContentDialogResult.Primary ? search.SelectedCommand?.Id : null;
        if (selectedId is null) return;
        // The dialog is fully closed before a chosen command can open another
        // dialog or reparent panel content. Recent choices are in-memory UI state.
        _recentStudioCommands.Remove(selectedId);
        _recentStudioCommands.Insert(0, selectedId);
        if (_recentStudioCommands.Count > 16) _recentStudioCommands.RemoveAt(16);
        await RunCommandAsync(selectedId);
    }
}
