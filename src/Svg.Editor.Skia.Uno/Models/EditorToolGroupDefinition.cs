using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using Microsoft.UI.Xaml;

namespace Svg.Editor.Skia.Uno.Models;

public sealed class EditorToolGroupDefinition : INotifyPropertyChanged
{
    private EditorToolDefinition _currentItem;

    public EditorToolGroupDefinition(
        string id,
        string label,
        EditorToolTrayMode visibleModes,
        IEnumerable<EditorToolDefinition> items)
    {
        Id = id;
        Label = label;
        VisibleModes = visibleModes;
        Items = new ObservableCollection<EditorToolDefinition>(items);
        _currentItem = Items.FirstOrDefault() ?? throw new ArgumentException("Tool group must contain at least one item.", nameof(items));

        Items.CollectionChanged += OnItemsCollectionChanged;
        foreach (var item in Items)
        {
            item.PropertyChanged += OnItemPropertyChanged;
        }

        SyncSelection();
    }

    public string Id { get; }

    public string Label { get; }

    public EditorToolTrayMode VisibleModes { get; }

    public ObservableCollection<EditorToolDefinition> Items { get; }

    public EditorToolDefinition CurrentItem
    {
        get => _currentItem;
        private set
        {
            if (ReferenceEquals(_currentItem, value))
            {
                return;
            }

            _currentItem = value;
            RaiseDisplayPropertiesChanged();
        }
    }

    public bool HasFlyout => Items.Count > 1;

    public Visibility FlyoutVisibility => HasFlyout ? Visibility.Visible : Visibility.Collapsed;

    public bool IsSelected => Items.Any(static item => item.IsSelected);

    public FigmaIconKind CurrentIconKind => CurrentItem.IconKind;

    public bool CurrentHasIcon => CurrentItem.HasIcon;

    public string CurrentGlyph => CurrentItem.Glyph;

    public string CurrentLabel => CurrentItem.Label;

    public string CurrentShortcut => CurrentItem.Shortcut;

    public string CurrentToolTip => string.IsNullOrWhiteSpace(CurrentItem.Shortcut)
        ? CurrentItem.Label
        : $"{CurrentItem.Label}  {CurrentItem.Shortcut}";

    public Visibility CurrentIconVisibility => CurrentItem.IconVisibility;

    public Visibility CurrentGlyphVisibility => CurrentItem.GlyphVisibility;

    public event PropertyChangedEventHandler? PropertyChanged;

    public void SyncSelection()
    {
        var selected = Items.FirstOrDefault(static item => item.IsSelected);
        if (selected is not null)
        {
            CurrentItem = selected;
        }

        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsSelected)));
    }

    private void OnItemsCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.OldItems is not null)
        {
            foreach (var item in e.OldItems.OfType<EditorToolDefinition>())
            {
                item.PropertyChanged -= OnItemPropertyChanged;
            }
        }

        if (e.NewItems is not null)
        {
            foreach (var item in e.NewItems.OfType<EditorToolDefinition>())
            {
                item.PropertyChanged += OnItemPropertyChanged;
            }
        }

        if (!Items.Contains(CurrentItem))
        {
            CurrentItem = Items.FirstOrDefault() ?? CurrentItem;
        }

        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(HasFlyout)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(FlyoutVisibility)));
        SyncSelection();
    }

    private void OnItemPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(EditorToolDefinition.IsSelected))
        {
            SyncSelection();
        }
    }

    private void RaiseDisplayPropertiesChanged()
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(CurrentItem)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(CurrentIconKind)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(CurrentHasIcon)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(CurrentGlyph)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(CurrentLabel)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(CurrentShortcut)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(CurrentToolTip)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(CurrentIconVisibility)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(CurrentGlyphVisibility)));
    }
}
