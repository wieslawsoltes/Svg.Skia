using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using Microsoft.UI.Xaml.Media;
using Svg.Editor.Skia.Uno.Models;

namespace Svg.Editor.Skia.Uno.Controls;

public sealed partial class FigmaLibrariesManager : UserControl
{
    private enum LibrariesSection
    {
        ThisFile,
        Updates,
        Teams,
        UiKits
    }

    private readonly ObservableCollection<EditorLibraryItem> _enabledLibraries = [];
    private readonly ObservableCollection<EditorLibraryItem> _updateLibraries = [];
    private readonly ObservableCollection<EditorLibraryItem> _browseLibraries = [];
    private LibrariesSection _section = LibrariesSection.ThisFile;

    public static readonly DependencyProperty LibrariesProperty =
        DependencyProperty.Register(
            nameof(Libraries),
            typeof(IEnumerable<EditorLibraryItem>),
            typeof(FigmaLibrariesManager),
            new PropertyMetadata(null, OnLibrariesChanged));

    public static readonly DependencyProperty DocumentTitleProperty =
        DependencyProperty.Register(
            nameof(DocumentTitle),
            typeof(string),
            typeof(FigmaLibrariesManager),
            new PropertyMetadata("This file", OnStatePropertyChanged));

    public static readonly DependencyProperty MissingLibraryCountProperty =
        DependencyProperty.Register(
            nameof(MissingLibraryCount),
            typeof(int),
            typeof(FigmaLibrariesManager),
            new PropertyMetadata(0, OnStatePropertyChanged));

    public FigmaLibrariesManager()
    {
        InitializeComponent();
        EnabledLibrariesList.ItemsSource = _enabledLibraries;
        UpdatesLibrariesList.ItemsSource = _updateLibraries;
        BrowseLibrariesList.ItemsSource = _browseLibraries;
        UpdateSectionChrome();
        RefreshView();
    }

    public IEnumerable<EditorLibraryItem>? Libraries
    {
        get => (IEnumerable<EditorLibraryItem>?)GetValue(LibrariesProperty);
        set => SetValue(LibrariesProperty, value);
    }

    public string DocumentTitle
    {
        get => (string)GetValue(DocumentTitleProperty);
        set => SetValue(DocumentTitleProperty, value);
    }

    public int MissingLibraryCount
    {
        get => (int)GetValue(MissingLibraryCountProperty);
        set => SetValue(MissingLibraryCountProperty, value);
    }

    public event EventHandler? CloseRequested;

    public event EventHandler<LibraryCommandRequestedEventArgs>? CommandRequested;

    public void RefreshView()
    {
        UpdatePanels();
        UpdateThisFileSection();
        UpdateUpdatesSection();
        UpdateBrowseSection();
    }

    private static void OnLibrariesChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var control = (FigmaLibrariesManager)d;
        if (e.OldValue is INotifyCollectionChanged oldCollection)
        {
            oldCollection.CollectionChanged -= control.OnLibrariesCollectionChanged;
        }

        if (e.NewValue is INotifyCollectionChanged newCollection)
        {
            newCollection.CollectionChanged += control.OnLibrariesCollectionChanged;
        }

        control.RefreshView();
    }

    private static void OnStatePropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        ((FigmaLibrariesManager)d).RefreshView();
    }

    private void OnLibrariesCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        RefreshView();
    }

    private void OnSearchTextChanged(object sender, TextChangedEventArgs e)
    {
        RefreshView();
    }

    private void OnThisFileClick(object sender, RoutedEventArgs e)
    {
        _section = LibrariesSection.ThisFile;
        UpdateSectionChrome();
        RefreshView();
    }

    private void OnUpdatesClick(object sender, RoutedEventArgs e)
    {
        _section = LibrariesSection.Updates;
        UpdateSectionChrome();
        RefreshView();
    }

    private void OnTeamsClick(object sender, RoutedEventArgs e)
    {
        _section = LibrariesSection.Teams;
        UpdateSectionChrome();
        RefreshView();
    }

    private void OnUiKitsClick(object sender, RoutedEventArgs e)
    {
        _section = LibrariesSection.UiKits;
        UpdateSectionChrome();
        RefreshView();
    }

    private void OnCloseClick(object sender, RoutedEventArgs e)
    {
        CloseRequested?.Invoke(this, EventArgs.Empty);
    }

    private void OnPublishCurrentFileClick(object sender, RoutedEventArgs e)
    {
        CommandRequested?.Invoke(this, new LibraryCommandRequestedEventArgs(EditorLibraryCommand.PublishCurrentFile));
        RefreshView();
    }

    private void OnLibraryToggleClick(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.DataContext is not EditorLibraryItem library)
        {
            return;
        }

        CommandRequested?.Invoke(this, new LibraryCommandRequestedEventArgs(EditorLibraryCommand.ToggleLibrary, library));
        RefreshView();
    }

    private void OnLibraryUpdateClick(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.DataContext is not EditorLibraryItem library)
        {
            return;
        }

        CommandRequested?.Invoke(this, new LibraryCommandRequestedEventArgs(EditorLibraryCommand.UpdateLibrary, library));
        RefreshView();
    }

    private void OnMissingLibrariesClick(object sender, RoutedEventArgs e)
    {
        CommandRequested?.Invoke(this, new LibraryCommandRequestedEventArgs(EditorLibraryCommand.ViewMissingLibraries));
    }

    private void UpdatePanels()
    {
        ThisFilePanel.Visibility = _section == LibrariesSection.ThisFile ? Visibility.Visible : Visibility.Collapsed;
        UpdatesPanel.Visibility = _section == LibrariesSection.Updates ? Visibility.Visible : Visibility.Collapsed;
        BrowsePanel.Visibility = _section is LibrariesSection.Teams or LibrariesSection.UiKits ? Visibility.Visible : Visibility.Collapsed;
    }

    private void UpdateThisFileSection()
    {
        var currentFile = Libraries?.FirstOrDefault(static item => item.IsCurrentFile);
        if (currentFile is null)
        {
            PublishTitleText.Text = $"Publish {DocumentTitle} as a library";
            PublishSubtitleText.Text = "Share local components, colors, and editor assets as a reusable team library.";
            PublishButton.Content = "Publish this file";
        }
        else if (currentFile.IsPublished)
        {
            PublishTitleText.Text = $"{currentFile.Name} is published";
            PublishSubtitleText.Text = currentFile.StatusLabel;
            PublishButton.Content = "Republish";
        }
        else
        {
            PublishTitleText.Text = $"Publish {currentFile.Name} as a library";
            PublishSubtitleText.Text = currentFile.Description;
            PublishButton.Content = "Publish this file";
        }

        _enabledLibraries.Clear();
        foreach (var item in GetFilteredLibraries().Where(static item => !item.IsCurrentFile && item.IsEnabled))
        {
            _enabledLibraries.Add(item);
        }

        EnabledLibrariesEmptyState.Visibility = _enabledLibraries.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        MissingLibrariesButton.Visibility = MissingLibraryCount > 0 ? Visibility.Visible : Visibility.Collapsed;
        MissingLibrariesButton.Content = MissingLibraryCount == 1
            ? "View 1 missing library"
            : $"View {MissingLibraryCount} missing libraries";
    }

    private void UpdateUpdatesSection()
    {
        _updateLibraries.Clear();
        foreach (var item in GetFilteredLibraries().Where(static item => !item.IsCurrentFile && item.HasUpdate))
        {
            _updateLibraries.Add(item);
        }

        UpdatesEmptyState.Visibility = _updateLibraries.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        UpdatesBadge.Visibility = _updateLibraries.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
        UpdatesBadgeText.Text = _updateLibraries.Count.ToString();
    }

    private void UpdateBrowseSection()
    {
        _browseLibraries.Clear();
        var category = _section == LibrariesSection.Teams
            ? EditorLibraryCategory.Team
            : EditorLibraryCategory.UiKit;

        foreach (var item in GetFilteredLibraries().Where(item => !item.IsCurrentFile && item.Category == category))
        {
            _browseLibraries.Add(item);
        }

        BrowseTitleText.Text = _section == LibrariesSection.Teams ? "Team libraries" : "UI kits";
        BrowseSubtitleText.Text = _section == LibrariesSection.Teams
            ? "Browse shared libraries published by your file, team, and local sample catalog."
            : "Enable design kits and component collections that can be inserted as SVG symbol instances.";
        BrowseEmptyState.Visibility = _browseLibraries.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    private IEnumerable<EditorLibraryItem> GetFilteredLibraries()
    {
        var search = SearchTextBox.Text?.Trim();
        var items = Libraries ?? Enumerable.Empty<EditorLibraryItem>();
        if (string.IsNullOrWhiteSpace(search))
        {
            return items;
        }

        return items.Where(item =>
            item.SearchText.Contains(search, StringComparison.OrdinalIgnoreCase)
            || item.Name.Contains(search, StringComparison.OrdinalIgnoreCase)
            || item.Publisher.Contains(search, StringComparison.OrdinalIgnoreCase));
    }

    private void UpdateSectionChrome()
    {
        SetSectionState(ThisFileButtonBorder, _section == LibrariesSection.ThisFile);
        SetSectionState(UpdatesButtonBorder, _section == LibrariesSection.Updates);
        SetSectionState(TeamsButtonBorder, _section == LibrariesSection.Teams);
        SetSectionState(UiKitsButtonBorder, _section == LibrariesSection.UiKits);
    }

    private static void SetSectionState(Border border, bool isActive)
    {
        border.Background = (Brush)Application.Current.Resources[isActive ? "SurfaceAltBrush" : "PickerSurfaceBrush"];
    }
}
