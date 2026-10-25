using Microsoft.UI.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Svg.Editor.Skia;
using Windows.System;
using Windows.UI.Core;

namespace Svg.Editor.Skia.Uno;

/// <summary>Read-only view over command metadata. The host alone executes a chosen ID.</summary>
public sealed class StudioCommandSearchItem
{
    public StudioCommandSearchItem(SvgCommandCatalogEntry entry) => Entry = entry;
    public SvgCommandCatalogEntry Entry { get; }
    public string Label => Entry.Label;
    public string Category => Entry.Category;
    public string Shortcut => Entry.Shortcut;
    public Visibility ShortcutVisibility => Shortcut.Length == 0 ? Visibility.Collapsed : Visibility.Visible;
    public override string ToString() => Category + " · " + Label + (Shortcut.Length == 0 ? "" : " · " + Shortcut);
}

/// <summary>
/// Reusable command palette. Uses native text editing and list virtualization;
/// filter changes never execute commands and a no-result search cannot reuse an
/// earlier selection. The result limit keeps rebind/layout work bounded.
/// </summary>
public sealed partial class StudioCommandSearch : UserControl
{
    private SvgCommandSearchIndex _index = new(Array.Empty<SvgCommandCatalogEntry>());
    private string[] _recentIds = Array.Empty<string>();
    public event EventHandler<SvgCommandCatalogEntry>? CommandInvoked;
    public event EventHandler? SelectedCommandChanged;
    public SvgCommandCatalogEntry? SelectedCommand => (Results.SelectedItem as StudioCommandSearchItem)?.Entry;

    public StudioCommandSearch()
    {
        InitializeComponent();
        RefreshResults();
    }

    public void SetCatalog(IEnumerable<SvgCommandCatalogEntry> commands, IEnumerable<string>? recentIds = null)
    {
        // Validate before replacing a usable catalog.
        var index = new SvgCommandSearchIndex(commands);
        var recent = recentIds?.Take(32).ToArray() ?? Array.Empty<string>();
        _index = index; _recentIds = recent; RefreshResults();
    }

    public void FocusSearch()
    {
        if (IsLoaded && IsEnabled) Query.Focus(FocusState.Programmatic);
    }

    private void Query_TextChanged(object sender, TextChangedEventArgs args) => RefreshResults();
    private void RefreshResults()
    {
        if (Results is null) return;
        var matches = _index.Search(Query.Text, 100, _recentIds).Select(entry => new StudioCommandSearchItem(entry)).ToArray();
        Results.ItemsSource = matches;
        Results.SelectedIndex = matches.Length == 0 ? -1 : 0;
        EmptyState.Visibility = matches.Length == 0 ? Visibility.Visible : Visibility.Collapsed;
        ClearQuery.IsEnabled = Query.Text.Length != 0;
        ResultCount.Text = matches.Length == 0 ? "Try fewer or different words."
            : matches.Length == 100 ? "100 commands shown · refine your search"
            : matches.Length == 1 ? "1 command" : matches.Length + " commands";
        if (string.IsNullOrWhiteSpace(Query.Text) && _recentIds.Length > 0) ResultCount.Text += " · recent choices first";
        if (Results.SelectedItem is { } selected) Results.ScrollIntoView(selected);
        SelectedCommandChanged?.Invoke(this, EventArgs.Empty);
    }

    private static bool Modified() => new[] { VirtualKey.Control, VirtualKey.Menu, VirtualKey.LeftWindows, VirtualKey.RightWindows }
        .Any(key => (InputKeyboardSource.GetKeyStateForCurrentThread(key) & CoreVirtualKeyStates.Down) != 0);

    private void Query_KeyDown(object sender, KeyRoutedEventArgs args)
    {
        if (args.Handled || Modified()) return;
        if (args.Key == VirtualKey.Enter)
        {
            if (SelectedCommand is { } entry) CommandInvoked?.Invoke(this, entry);
            args.Handled = true; // No match must not invoke a dialog's previous/default result.
            return;
        }
        if (args.Key is not (VirtualKey.Down or VirtualKey.Up or VirtualKey.PageDown or VirtualKey.PageUp)) return;
        if (Results.Items.Count > 0)
        {
            var step = args.Key is VirtualKey.PageDown or VirtualKey.PageUp ? 8 : 1;
            if (args.Key is VirtualKey.Up or VirtualKey.PageUp) step = -step;
            Results.SelectedIndex = Math.Clamp(Results.SelectedIndex + step, 0, Results.Items.Count - 1);
            Results.ScrollIntoView(Results.SelectedItem);
        }
        args.Handled = true;
    }
    private void Results_KeyDown(object sender, KeyRoutedEventArgs args)
    {
        if (!args.Handled && !Modified() && args.Key == VirtualKey.Enter && SelectedCommand is { } selected)
        { args.Handled = true; CommandInvoked?.Invoke(this, selected); }
    }
    private void Results_ItemClick(object sender, ItemClickEventArgs args)
    {
        if (args.ClickedItem is StudioCommandSearchItem item) CommandInvoked?.Invoke(this, item.Entry);
    }
    private void Results_SelectionChanged(object sender, SelectionChangedEventArgs args)
        => SelectedCommandChanged?.Invoke(this, EventArgs.Empty);
    private void ClearQuery_Click(object sender, RoutedEventArgs args)
    {
        Query.Text = string.Empty;
        FocusSearch(); // Clear button disables itself: restore focus to the input explicitly.
    }
}
