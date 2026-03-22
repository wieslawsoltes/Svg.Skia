using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Linq;
using Microsoft.UI.Xaml.Media;
using Svg.Editor.Skia.Uno.Models;

namespace Svg.Editor.Skia.Uno.Controls;

public sealed partial class FigmaActionsPalette : UserControl
{
    private readonly ObservableCollection<EditorActionPaletteItem> _suggestions = [];
    private readonly ObservableCollection<EditorActionPaletteItem> _settings = [];
    private readonly ObservableCollection<EditorActionPaletteItem> _results = [];
    private EditorActionPaletteTab _selectedTab = EditorActionPaletteTab.All;

    public static readonly DependencyProperty ItemsProperty =
        DependencyProperty.Register(
            nameof(Items),
            typeof(IEnumerable<EditorActionPaletteItem>),
            typeof(FigmaActionsPalette),
            new PropertyMetadata(null, OnItemsChanged));

    public FigmaActionsPalette()
    {
        InitializeComponent();
        SuggestionsList.ItemsSource = _suggestions;
        SettingsList.ItemsSource = _settings;
        ResultsList.ItemsSource = _results;
        UpdateTabChrome();
        RefreshView();
    }

    public IEnumerable<EditorActionPaletteItem>? Items
    {
        get => (IEnumerable<EditorActionPaletteItem>?)GetValue(ItemsProperty);
        set => SetValue(ItemsProperty, value);
    }

    public event EventHandler<ActionPaletteItemRequestedEventArgs>? ActionRequested;

    public event EventHandler? CloseRequested;

    public void FocusSearchBox()
    {
        SearchTextBox.FocusTextBox();
    }

    private static void OnItemsChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var control = (FigmaActionsPalette)d;
        if (e.OldValue is INotifyCollectionChanged oldCollection)
        {
            oldCollection.CollectionChanged -= control.OnItemsCollectionChanged;
        }

        if (e.NewValue is INotifyCollectionChanged newCollection)
        {
            newCollection.CollectionChanged += control.OnItemsCollectionChanged;
        }

        control.RefreshView();
    }

    private void OnItemsCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        RefreshView();
    }

    private void OnSearchTextChanged(object sender, TextChangedEventArgs e)
    {
        RefreshView();
    }

    private void OnSearchTextBoxKeyDown(object sender, KeyRoutedEventArgs e)
    {
        switch (e.Key)
        {
            case Windows.System.VirtualKey.Enter:
                {
                    var item = GetDefaultItem();
                    if (item is null)
                    {
                        return;
                    }

                    ActionRequested?.Invoke(this, new ActionPaletteItemRequestedEventArgs(item));
                    e.Handled = true;
                    break;
                }
            case Windows.System.VirtualKey.Escape:
                CloseRequested?.Invoke(this, EventArgs.Empty);
                e.Handled = true;
                break;
        }
    }

    private void OnTabClick(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement { Tag: string rawTag }
            || !Enum.TryParse<EditorActionPaletteTab>(rawTag, ignoreCase: true, out var tab))
        {
            return;
        }

        _selectedTab = tab;
        UpdateTabChrome();
        RefreshView();
        FocusSearchBox();
    }

    private void OnCloseClick(object sender, RoutedEventArgs e)
    {
        CloseRequested?.Invoke(this, EventArgs.Empty);
    }

    private void OnActionItemClick(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.DataContext is not EditorActionPaletteItem item)
        {
            return;
        }

        ActionRequested?.Invoke(this, new ActionPaletteItemRequestedEventArgs(item));
    }

    private void RefreshView()
    {
        var search = SearchTextBox.Text?.Trim();
        var allItems = (Items ?? []).ToList();

        if (_selectedTab == EditorActionPaletteTab.All && string.IsNullOrWhiteSpace(search))
        {
            SetItems(_suggestions, allItems
                .Where(static item => item.IsSuggested)
                .OrderBy(static item => item.SortOrder));

            SetItems(_settings, allItems
                .Where(static item => item.IsCommonSetting)
                .OrderBy(static item => item.SortOrder));

            _results.Clear();
            ResultsPanel.Visibility = Visibility.Collapsed;
        }
        else
        {
            _suggestions.Clear();
            _settings.Clear();

            SetItems(_results, allItems
                .Where(item => item.BelongsToTab(_selectedTab))
                .Where(item => item.Matches(search))
                .OrderBy(static item => item.SortOrder)
                .ThenBy(static item => item.Title, StringComparer.OrdinalIgnoreCase));

            ResultsHeaderText.Text = _selectedTab switch
            {
                EditorActionPaletteTab.Assets => "Assets",
                EditorActionPaletteTab.Libraries => "Libraries",
                _ => string.IsNullOrWhiteSpace(search) ? "Quick actions" : "Results"
            };

            ResultsPanel.Visibility = _results.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
        }

        SuggestionsPanel.Visibility = _suggestions.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
        SettingsPanel.Visibility = _settings.Count > 0 ? Visibility.Visible : Visibility.Collapsed;

        var hasContent = _suggestions.Count > 0 || _settings.Count > 0 || _results.Count > 0;
        EmptyStateBorder.Visibility = hasContent ? Visibility.Collapsed : Visibility.Visible;
        EmptyStateText.Text = BuildEmptyStateText(search);
    }

    private EditorActionPaletteItem? GetDefaultItem()
    {
        return _suggestions.FirstOrDefault(static item => item.IsEnabled)
            ?? _settings.FirstOrDefault(static item => item.IsEnabled)
            ?? _results.FirstOrDefault(static item => item.IsEnabled);
    }

    private string BuildEmptyStateText(string? search)
    {
        return _selectedTab switch
        {
            EditorActionPaletteTab.Assets when string.IsNullOrWhiteSpace(search) => "No component assets are available in the current file or connected libraries.",
            EditorActionPaletteTab.Assets => $"No assets match “{search}”.",
            EditorActionPaletteTab.Libraries when string.IsNullOrWhiteSpace(search) => "No library actions are available yet.",
            EditorActionPaletteTab.Libraries => $"No libraries match “{search}”.",
            _ when string.IsNullOrWhiteSpace(search) => "No actions are available for the current editor state.",
            _ => $"No actions match “{search}”."
        };
    }

    private void UpdateTabChrome()
    {
        SetTabState(AllTabBorder, _selectedTab == EditorActionPaletteTab.All);
        SetTabState(AssetsTabBorder, _selectedTab == EditorActionPaletteTab.Assets);
        SetTabState(LibrariesTabBorder, _selectedTab == EditorActionPaletteTab.Libraries);
    }

    private static void SetItems(ObservableCollection<EditorActionPaletteItem> target, IEnumerable<EditorActionPaletteItem> source)
    {
        target.Clear();
        foreach (var item in source)
        {
            target.Add(item);
        }
    }

    private static void SetTabState(Border border, bool isActive)
    {
        border.Background = (Brush)Application.Current.Resources[isActive ? "SurfaceAltBrush" : "PickerSurfaceBrush"];
    }
}
