using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Linq;
using Microsoft.UI.Xaml.Controls.Primitives;
using Svg.Editor.Skia.Uno.Models;
using Windows.ApplicationModel.DataTransfer;

namespace Svg.Editor.Skia.Uno.Controls;

public sealed partial class SvgEditorAssetsBrowser : UserControl
{
    private enum AssetsLibraryFilter
    {
        All,
        Connected,
        Updates
    }

    private readonly ObservableCollection<EditorLibraryItem> _visibleLibraries = [];
    private readonly ObservableCollection<EditorComponentItem> _visibleAssets = [];
    private readonly ObservableCollection<EditorAssetSectionItem> _sections = [];
    private AssetsLibraryFilter _filter = AssetsLibraryFilter.All;
    private EditorLibraryItem? _selectedLibrary;
    private string? _selectedSection;
    private long _lastAssetDragTicks;

    public static readonly DependencyProperty LibrariesProperty =
        DependencyProperty.Register(
            nameof(Libraries),
            typeof(IEnumerable<EditorLibraryItem>),
            typeof(SvgEditorAssetsBrowser),
            new PropertyMetadata(null, OnInputCollectionChanged));

    public static readonly DependencyProperty ComponentsProperty =
        DependencyProperty.Register(
            nameof(Components),
            typeof(IEnumerable<EditorComponentItem>),
            typeof(SvgEditorAssetsBrowser),
            new PropertyMetadata(null, OnInputCollectionChanged));

    public static readonly DependencyProperty DocumentTitleProperty =
        DependencyProperty.Register(
            nameof(DocumentTitle),
            typeof(string),
            typeof(SvgEditorAssetsBrowser),
            new PropertyMetadata("This file", OnInputCollectionChanged));

    public SvgEditorAssetsBrowser()
    {
        InitializeComponent();
        LibraryCardsList.ItemsSource = _visibleLibraries;
        AssetsGridView.ItemsSource = _visibleAssets;
        SectionTabsList.ItemsSource = _sections;
        RefreshView();
    }

    public IEnumerable<EditorLibraryItem>? Libraries
    {
        get => (IEnumerable<EditorLibraryItem>?)GetValue(LibrariesProperty);
        set => SetValue(LibrariesProperty, value);
    }

    public IEnumerable<EditorComponentItem>? Components
    {
        get => (IEnumerable<EditorComponentItem>?)GetValue(ComponentsProperty);
        set => SetValue(ComponentsProperty, value);
    }

    public string DocumentTitle
    {
        get => (string)GetValue(DocumentTitleProperty);
        set => SetValue(DocumentTitleProperty, value);
    }

    public event RoutedEventHandler? ManageLibrariesRequested;

    public event EventHandler<ComponentRequestedEventArgs>? ComponentRequested;

    private static void OnInputCollectionChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var control = (SvgEditorAssetsBrowser)d;
        if (e.OldValue is INotifyCollectionChanged oldCollection)
        {
            oldCollection.CollectionChanged -= control.OnInputItemsChanged;
        }

        if (e.NewValue is INotifyCollectionChanged newCollection)
        {
            newCollection.CollectionChanged += control.OnInputItemsChanged;
        }

        control.RefreshView();
    }

    private void OnInputItemsChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        RefreshView();
    }

    private void OnSearchTextChanged(object sender, TextChangedEventArgs e)
    {
        RefreshView();
    }

    private void OnManageLibrariesClick(object sender, RoutedEventArgs e)
    {
        ManageLibrariesRequested?.Invoke(this, e);
    }

    private void OnBackClick(object sender, RoutedEventArgs e)
    {
        _selectedLibrary = null;
        _selectedSection = null;
        RefreshView();
    }

    private void OnLibraryCardClick(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.DataContext is not EditorLibraryItem library)
        {
            return;
        }

        _selectedLibrary = library;
        _selectedSection = null;
        RefreshView();
    }

    private void OnAssetClick(object sender, RoutedEventArgs e)
    {
        if (Environment.TickCount64 - _lastAssetDragTicks < 250)
        {
            return;
        }

        if ((sender as FrameworkElement)?.DataContext is EditorComponentItem component)
        {
            ComponentRequested?.Invoke(this, new ComponentRequestedEventArgs(component));
        }
    }

    private void OnAssetDragStarting(UIElement sender, DragStartingEventArgs args)
    {
        if (sender is not FrameworkElement { DataContext: EditorComponentItem component })
        {
            args.Cancel = true;
            return;
        }

        _lastAssetDragTicks = Environment.TickCount64;
        args.AllowedOperations = DataPackageOperation.Copy;
        args.Data.Properties.Add(EditorDragDropData.KindKey, EditorDragDropData.ComponentKind);
        args.Data.Properties.Add(EditorDragDropData.ComponentAssetKey, component.AssetKey);
        args.Data.Properties.Add(EditorDragDropData.ComponentName, component.Name);
        args.Data.SetText($"{EditorDragDropData.ComponentTextPrefix}{component.AssetKey}");
    }

    private void OnSectionClick(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.DataContext is not EditorAssetSectionItem section)
        {
            return;
        }

        _selectedSection = string.Equals(section.Name, "All", StringComparison.Ordinal) ? null : section.Name;
        RefreshView();
    }

    private void OnFilterClick(object sender, RoutedEventArgs e)
    {
        var flyout = new MenuFlyout();

        if (_selectedLibrary is null)
        {
            flyout.Items.Add(CreateFilterItem("All libraries", AssetsLibraryFilter.All));
            flyout.Items.Add(CreateFilterItem("Connected libraries", AssetsLibraryFilter.Connected));
            flyout.Items.Add(CreateFilterItem("Updates", AssetsLibraryFilter.Updates));
        }
        else
        {
            flyout.Items.Add(CreateSectionItem("All assets", null));
            foreach (var section in BuildSections(GetLibraryAssets(_selectedLibrary)))
            {
                flyout.Items.Add(CreateSectionItem(section.Name, section.Name));
            }
        }

        flyout.ShowAt((FrameworkElement)sender, new FlyoutShowOptions { Placement = FlyoutPlacementMode.BottomEdgeAlignedRight });
    }

    public void RefreshView()
    {
        var libraries = (Libraries ?? Enumerable.Empty<EditorLibraryItem>()).ToList();
        if (_selectedLibrary is not null)
        {
            _selectedLibrary = libraries.FirstOrDefault(item => string.Equals(item.Id, _selectedLibrary.Id, StringComparison.Ordinal));
        }

        OverviewPanel.Visibility = _selectedLibrary is null ? Visibility.Visible : Visibility.Collapsed;
        OverviewHeader.Visibility = OverviewPanel.Visibility;
        DetailPanel.Visibility = _selectedLibrary is null ? Visibility.Collapsed : Visibility.Visible;
        DetailHeader.Visibility = DetailPanel.Visibility;
        SearchTextBox.PlaceholderText = _selectedLibrary is null ? "Search all libraries" : "Search in this library";

        if (_selectedLibrary is null)
        {
            UpdateLibrariesOverview(libraries);
        }
        else
        {
            UpdateLibraryDetail(_selectedLibrary);
        }
    }

    private void UpdateLibrariesOverview(List<EditorLibraryItem> libraries)
    {
        OverviewSubtitleText.Text = BuildOverviewSubtitle(libraries);
        _visibleLibraries.Clear();

        foreach (var library in ApplyLibraryFilter(libraries))
        {
            _visibleLibraries.Add(library);
        }
    }

    private void UpdateLibraryDetail(EditorLibraryItem library)
    {
        var assets = GetLibraryAssets(library);
        var filteredAssets = ApplySearchFilter(assets).ToList();
        var sections = BuildSections(filteredAssets);

        if (_selectedSection is not null && sections.All(section => !string.Equals(section.Name, _selectedSection, StringComparison.Ordinal)))
        {
            _selectedSection = null;
        }

        _sections.Clear();
        if (sections.Count > 1)
        {
            var allSection = new EditorAssetSectionItem("All", filteredAssets.Count) { IsSelected = string.IsNullOrWhiteSpace(_selectedSection) };
            _sections.Add(allSection);
        }

        foreach (var section in sections)
        {
            section.IsSelected = string.Equals(section.Name, _selectedSection, StringComparison.Ordinal)
                || (string.IsNullOrWhiteSpace(_selectedSection) && _sections.Count == 0 && ReferenceEquals(section, sections[0]));
            _sections.Add(section);
        }

        var visibleAssets = string.IsNullOrWhiteSpace(_selectedSection)
            ? filteredAssets
            : filteredAssets.Where(item => string.Equals(item.SectionName, _selectedSection, StringComparison.Ordinal)).ToList();

        _visibleAssets.Clear();
        foreach (var asset in visibleAssets)
        {
            _visibleAssets.Add(asset);
        }

        var activeSection = string.IsNullOrWhiteSpace(_selectedSection) ? sections.FirstOrDefault()?.Name : _selectedSection;
        BreadcrumbText.Text = activeSection is null
            ? library.Name
            : $"{library.Name} / {activeSection}";
        DetailSubtitleText.Text = library.IsEnabled || library.IsCurrentFile
            ? library.AssetsCountLabel
            : "Browse assets first. Selecting one will add the library to this file and prepare it for placement.";

        AssetsEmptyStateText.Visibility = visibleAssets.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        AssetsEmptyStateText.Text = string.IsNullOrWhiteSpace(SearchTextBox.Text)
            ? "No reusable assets are available in this section yet."
            : "No assets match the current search.";
    }

    private IEnumerable<EditorLibraryItem> ApplyLibraryFilter(List<EditorLibraryItem> libraries)
    {
        IEnumerable<EditorLibraryItem> items = libraries.Where(ShouldShowLibraryCard);
        var search = SearchTextBox.Text?.Trim();
        if (!string.IsNullOrWhiteSpace(search))
        {
            items = items.Where(item => item.SearchText.Contains(search, StringComparison.OrdinalIgnoreCase));
        }

        items = _filter switch
        {
            AssetsLibraryFilter.Connected => items.Where(static item => item.IsCurrentFile || item.IsEnabled || item.IsMissing),
            AssetsLibraryFilter.Updates => items.Where(static item => item.HasUpdate),
            _ => items
        };

        return items;
    }

    private bool ShouldShowLibraryCard(EditorLibraryItem library)
    {
        if (!library.IsCurrentFile)
        {
            return true;
        }

        return GetLibraryAssets(library).Count > 0 || library.IsPublished;
    }

    private List<EditorComponentItem> GetLibraryAssets(EditorLibraryItem library)
    {
        var libraryId = library.IsCurrentFile ? library.Id : library.Id;
        return (Components ?? Enumerable.Empty<EditorComponentItem>())
            .Where(item => string.Equals(item.LibraryId, libraryId, StringComparison.Ordinal))
            .OrderBy(item => item.SectionName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(item => item.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private IEnumerable<EditorComponentItem> ApplySearchFilter(IEnumerable<EditorComponentItem> assets)
    {
        var search = SearchTextBox.Text?.Trim();
        if (string.IsNullOrWhiteSpace(search))
        {
            return assets;
        }

        return assets.Where(item => item.SearchText.Contains(search, StringComparison.OrdinalIgnoreCase));
    }

    private static List<EditorAssetSectionItem> BuildSections(IEnumerable<EditorComponentItem> assets)
    {
        return assets
            .GroupBy(item => string.IsNullOrWhiteSpace(item.SectionName) ? "Components" : item.SectionName)
            .Select(group => new EditorAssetSectionItem(group.Key, group.Count()))
            .OrderBy(section => section.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private string BuildOverviewSubtitle(List<EditorLibraryItem> libraries)
    {
        var visibleCount = ApplyLibraryFilter(libraries).Count();
        return visibleCount == 1 ? "1 library ready to browse" : $"{visibleCount} libraries ready to browse";
    }

    private MenuFlyoutItem CreateFilterItem(string label, AssetsLibraryFilter filter)
    {
        var item = new MenuFlyoutItem
        {
            Text = label
        };
        item.Click += (_, _) =>
        {
            _filter = filter;
            RefreshView();
        };
        return item;
    }

    private MenuFlyoutItem CreateSectionItem(string label, string? sectionName)
    {
        var item = new MenuFlyoutItem
        {
            Text = label
        };
        item.Click += (_, _) =>
        {
            _selectedSection = sectionName;
            RefreshView();
        };
        return item;
    }
}
