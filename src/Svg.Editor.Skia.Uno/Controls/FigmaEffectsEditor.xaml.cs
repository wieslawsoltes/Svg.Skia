using System.Collections.ObjectModel;
using System.Collections.Specialized;
using Svg.Editor.Skia.Uno.Models;

namespace Svg.Editor.Skia.Uno.Controls;

public sealed partial class FigmaEffectsEditor : UserControl
{
    public static readonly DependencyProperty ItemsSourceProperty =
        DependencyProperty.Register(
            nameof(ItemsSource),
            typeof(ObservableCollection<EditorEffectItem>),
            typeof(FigmaEffectsEditor),
            new PropertyMetadata(null, OnItemsSourceChanged));

    public static readonly DependencyProperty IsEditableProperty =
        DependencyProperty.Register(
            nameof(IsEditable),
            typeof(bool),
            typeof(FigmaEffectsEditor),
            new PropertyMetadata(false, OnDisplayPropertyChanged));

    public static readonly DependencyProperty ShowHeaderProperty =
        DependencyProperty.Register(
            nameof(ShowHeader),
            typeof(bool),
            typeof(FigmaEffectsEditor),
            new PropertyMetadata(true, OnDisplayPropertyChanged));

    public static readonly DependencyProperty SwatchesProperty =
        DependencyProperty.Register(
            nameof(Swatches),
            typeof(IEnumerable<ColorSwatchItem>),
            typeof(FigmaEffectsEditor),
            new PropertyMetadata(null));

    public FigmaEffectsEditor()
    {
        InitializeComponent();
        UpdateVisualState();
    }

    public ObservableCollection<EditorEffectItem>? ItemsSource
    {
        get => (ObservableCollection<EditorEffectItem>?)GetValue(ItemsSourceProperty);
        set => SetValue(ItemsSourceProperty, value);
    }

    public bool IsEditable
    {
        get => (bool)GetValue(IsEditableProperty);
        set => SetValue(IsEditableProperty, value);
    }

    public bool ShowHeader
    {
        get => (bool)GetValue(ShowHeaderProperty);
        set => SetValue(ShowHeaderProperty, value);
    }

    public IEnumerable<ColorSwatchItem>? Swatches
    {
        get => (IEnumerable<ColorSwatchItem>?)GetValue(SwatchesProperty);
        set => SetValue(SwatchesProperty, value);
    }

    private static void OnItemsSourceChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var editor = (FigmaEffectsEditor)d;
        if (e.OldValue is ObservableCollection<EditorEffectItem> oldCollection)
        {
            oldCollection.CollectionChanged -= editor.OnEffectsCollectionChanged;
        }

        if (e.NewValue is ObservableCollection<EditorEffectItem> newCollection)
        {
            newCollection.CollectionChanged += editor.OnEffectsCollectionChanged;
        }

        editor.UpdateVisualState();
    }

    private static void OnDisplayPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        ((FigmaEffectsEditor)d).UpdateVisualState();
    }

    private void OnEffectsCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        UpdateVisualState();
    }

    private void OnAddEffectMenuItemClick(object sender, RoutedEventArgs e)
    {
        if (!IsEditable || ItemsSource is null)
        {
            return;
        }

        if (sender is not FrameworkElement { Tag: string rawKind }
            || !Enum.TryParse<EditorEffectKind>(rawKind, true, out var kind))
        {
            return;
        }

        ItemsSource.Add(EditorEffectItem.CreateDefault(kind));
    }

    private void OnRemoveEffectClick(object sender, RoutedEventArgs e)
    {
        if (!IsEditable || ItemsSource is null || sender is not FrameworkElement { DataContext: EditorEffectItem item })
        {
            return;
        }

        ItemsSource.Remove(item);
    }

    private void OnMoveUpClick(object sender, RoutedEventArgs e)
    {
        MoveItem(sender, -1);
    }

    private void OnMoveDownClick(object sender, RoutedEventArgs e)
    {
        MoveItem(sender, 1);
    }

    private void MoveItem(object sender, int direction)
    {
        if (!IsEditable || ItemsSource is null || sender is not FrameworkElement { DataContext: EditorEffectItem item })
        {
            return;
        }

        var index = ItemsSource.IndexOf(item);
        if (index < 0)
        {
            return;
        }

        var nextIndex = index + direction;
        if (nextIndex < 0 || nextIndex >= ItemsSource.Count)
        {
            return;
        }

        ItemsSource.Move(index, nextIndex);
    }

    private void OnEffectKindComboBoxLoaded(object sender, RoutedEventArgs e)
    {
        if (sender is not ComboBox comboBox || comboBox.DataContext is not EditorEffectItem item)
        {
            return;
        }

        foreach (var entry in comboBox.Items.OfType<ComboBoxItem>())
        {
            if (entry.Tag is string rawTag
                && Enum.TryParse<EditorEffectKind>(rawTag, true, out var kind)
                && kind == item.Kind)
            {
                comboBox.SelectedItem = entry;
                break;
            }
        }
    }

    private void OnEffectKindSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (sender is not ComboBox { DataContext: EditorEffectItem item } comboBox
            || comboBox.SelectedItem is not ComboBoxItem { Tag: string rawTag }
            || !Enum.TryParse<EditorEffectKind>(rawTag, true, out var kind))
        {
            return;
        }

        item.Kind = kind;
    }

    private void UpdateVisualState()
    {
        var hasItems = ItemsSource is { Count: > 0 };

        HeaderGrid.Visibility = ShowHeader ? Visibility.Visible : Visibility.Collapsed;
        EffectsItemsControl.Visibility = hasItems ? Visibility.Visible : Visibility.Collapsed;
        HintTextBlock.Text = !IsEditable
            ? "Select a single layer to edit filters and generated SVG effect stacks."
            : hasItems
                ? "Effects are written back as SVG filters and re-rendered live on the canvas."
                : "Add a Figma-style effect to generate a live SVG filter stack.";
    }
}
