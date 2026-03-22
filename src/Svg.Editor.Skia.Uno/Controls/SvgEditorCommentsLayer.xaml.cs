using System.Collections.Specialized;
using System.ComponentModel;
using Microsoft.UI;
using Microsoft.UI.Input;
using Microsoft.UI.Xaml.Media;
using Svg.Editor.Skia.Uno.Models;
using Windows.Foundation;

namespace Svg.Editor.Skia.Uno.Controls;

public sealed partial class SvgEditorCommentsLayer : UserControl
{
    public static readonly DependencyProperty ItemsSourceProperty =
        DependencyProperty.Register(
            nameof(ItemsSource),
            typeof(IEnumerable<EditorCommentThread>),
            typeof(SvgEditorCommentsLayer),
            new PropertyMetadata(null, OnItemsSourcePropertyChanged));

    public static readonly DependencyProperty SelectedThreadProperty =
        DependencyProperty.Register(
            nameof(SelectedThread),
            typeof(EditorCommentThread),
            typeof(SvgEditorCommentsLayer),
            new PropertyMetadata(null, OnVisualStatePropertyChanged));

    public static readonly DependencyProperty IsCommentModeActiveProperty =
        DependencyProperty.Register(
            nameof(IsCommentModeActive),
            typeof(bool),
            typeof(SvgEditorCommentsLayer),
            new PropertyMetadata(false, OnVisualStatePropertyChanged));

    public static readonly DependencyProperty IsComposerVisibleProperty =
        DependencyProperty.Register(
            nameof(IsComposerVisible),
            typeof(bool),
            typeof(SvgEditorCommentsLayer),
            new PropertyMetadata(false, OnVisualStatePropertyChanged));

    public static readonly DependencyProperty ComposerTextProperty =
        DependencyProperty.Register(
            nameof(ComposerText),
            typeof(string),
            typeof(SvgEditorCommentsLayer),
            new PropertyMetadata(string.Empty));

    public static readonly DependencyProperty ComposerPositionProperty =
        DependencyProperty.Register(
            nameof(ComposerPosition),
            typeof(Point),
            typeof(SvgEditorCommentsLayer),
            new PropertyMetadata(default(Point), OnVisualStatePropertyChanged));

    private readonly List<EditorCommentThread> _trackedThreads = [];

    public SvgEditorCommentsLayer()
    {
        InitializeComponent();
        Loaded += (_, _) => RefreshVisualState();
        SizeChanged += (_, _) => RefreshVisualState();
    }

    public IEnumerable<EditorCommentThread>? ItemsSource
    {
        get => (IEnumerable<EditorCommentThread>?)GetValue(ItemsSourceProperty);
        set => SetValue(ItemsSourceProperty, value);
    }

    public EditorCommentThread? SelectedThread
    {
        get => (EditorCommentThread?)GetValue(SelectedThreadProperty);
        set => SetValue(SelectedThreadProperty, value);
    }

    public bool IsCommentModeActive
    {
        get => (bool)GetValue(IsCommentModeActiveProperty);
        set => SetValue(IsCommentModeActiveProperty, value);
    }

    public bool IsComposerVisible
    {
        get => (bool)GetValue(IsComposerVisibleProperty);
        set => SetValue(IsComposerVisibleProperty, value);
    }

    public string ComposerText
    {
        get => (string)GetValue(ComposerTextProperty);
        set => SetValue(ComposerTextProperty, value);
    }

    public Point ComposerPosition
    {
        get => (Point)GetValue(ComposerPositionProperty);
        set => SetValue(ComposerPositionProperty, value);
    }

    public event EventHandler<CommentThreadRequestedEventArgs>? ThreadRequested;

    public event EventHandler<CommentTextRequestedEventArgs>? ComposerSubmitted;

    public event EventHandler? ComposerCanceled;

    private static void OnItemsSourcePropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var control = (SvgEditorCommentsLayer)d;
        control.UnsubscribeFromThreads(e.OldValue as IEnumerable<EditorCommentThread>);
        control.SubscribeToThreads(e.NewValue as IEnumerable<EditorCommentThread>);
        control.RefreshVisualState();
    }

    private static void OnVisualStatePropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        ((SvgEditorCommentsLayer)d).RefreshVisualState();
    }

    private void SubscribeToThreads(IEnumerable<EditorCommentThread>? threads)
    {
        _trackedThreads.Clear();

        if (threads is null)
        {
            return;
        }

        foreach (var thread in threads)
        {
            _trackedThreads.Add(thread);
            thread.PropertyChanged += OnThreadPropertyChanged;
            thread.Messages.CollectionChanged += OnThreadMessagesCollectionChanged;
        }

        if (threads is INotifyCollectionChanged collectionChanged)
        {
            collectionChanged.CollectionChanged += OnThreadsCollectionChanged;
        }
    }

    private void UnsubscribeFromThreads(IEnumerable<EditorCommentThread>? threads)
    {
        if (threads is INotifyCollectionChanged collectionChanged)
        {
            collectionChanged.CollectionChanged -= OnThreadsCollectionChanged;
        }

        foreach (var thread in _trackedThreads)
        {
            thread.PropertyChanged -= OnThreadPropertyChanged;
            thread.Messages.CollectionChanged -= OnThreadMessagesCollectionChanged;
        }

        _trackedThreads.Clear();
    }

    private void OnThreadsCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        UnsubscribeFromThreads(sender as IEnumerable<EditorCommentThread>);
        SubscribeToThreads(sender as IEnumerable<EditorCommentThread>);
        RefreshVisualState();
    }

    private void OnThreadMessagesCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        RefreshVisualState();
    }

    private void OnThreadPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        RefreshVisualState();
    }

    private void RefreshVisualState()
    {
        var showPins = ShouldShowPins();
        var showComposer = IsComposerVisible;
        Visibility = showPins || showComposer ? Visibility.Visible : Visibility.Collapsed;

        if (Visibility != Visibility.Visible)
        {
            PinsCanvas.Children.Clear();
            PinsCanvas.Visibility = Visibility.Collapsed;
            ComposerBorder.Visibility = Visibility.Collapsed;
            return;
        }

        RefreshPins(showPins);
        UpdateComposer(showComposer);
    }

    private bool ShouldShowPins()
    {
        if (ItemsSource is null || (!IsCommentModeActive && SelectedThread is null && !IsComposerVisible))
        {
            return false;
        }

        return ItemsSource.Any(static thread => !double.IsNaN(thread.ViewX) && !double.IsNaN(thread.ViewY));
    }

    private void RefreshPins(bool showPins)
    {
        PinsCanvas.Children.Clear();
        PinsCanvas.Visibility = showPins ? Visibility.Visible : Visibility.Collapsed;

        if (!showPins || ItemsSource is null)
        {
            return;
        }

        foreach (var thread in ItemsSource)
        {
            if (double.IsNaN(thread.ViewX) || double.IsNaN(thread.ViewY))
            {
                continue;
            }

            var pin = BuildPin(thread);
            var size = thread.IsSelected ? 42.0 : 34.0;
            Canvas.SetLeft(pin, thread.ViewX - (size / 2.0));
            Canvas.SetTop(pin, thread.ViewY - (size / 2.0));
            PinsCanvas.Children.Add(pin);
        }
    }

    private UIElement BuildPin(EditorCommentThread thread)
    {
        var fillColor = thread.IsResolved
            ? ColorHelper.FromArgb(255, 156, 163, 175)
            : thread.IsSelected
                ? ColorHelper.FromArgb(255, 13, 153, 255)
                : ColorHelper.FromArgb(255, 22, 163, 74);
        var strokeColor = thread.IsSelected
            ? ColorHelper.FromArgb(255, 191, 229, 255)
            : ColorHelper.FromArgb(255, 255, 255, 255);
        var size = thread.IsSelected ? 42.0 : 34.0;

        var root = new Grid
        {
            Width = size,
            Height = size
        };

        if (thread.IsSelected)
        {
            root.Children.Add(new Border
            {
                Width = size,
                Height = size,
                CornerRadius = new CornerRadius(size / 2.0),
                Background = new SolidColorBrush(ColorHelper.FromArgb(54, 13, 153, 255))
            });
        }

        root.Children.Add(new Border
        {
            Width = size - 4.0,
            Height = size - 4.0,
            CornerRadius = new CornerRadius((size - 4.0) / 2.0),
            Background = new SolidColorBrush(fillColor),
            BorderBrush = new SolidColorBrush(strokeColor),
            BorderThickness = new Thickness(thread.IsSelected ? 2 : 1.5)
        });

        root.Children.Add(new TextBlock
        {
            Text = thread.MarkerInitials,
            Foreground = new SolidColorBrush(Colors.White),
            FontSize = thread.IsSelected ? 14 : 12,
            FontWeight = new Windows.UI.Text.FontWeight { Weight = 700 },
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        });

        if (thread.Messages.Count > 1)
        {
            var badge = new Border
            {
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Bottom,
                Margin = new Thickness(0, 0, -4, -4),
                MinWidth = 18,
                Height = 18,
                CornerRadius = new CornerRadius(9),
                Background = new SolidColorBrush(ColorHelper.FromArgb(255, 17, 24, 39)),
                BorderBrush = new SolidColorBrush(Colors.White),
                BorderThickness = new Thickness(1)
            };
            badge.Child = new TextBlock
            {
                Text = thread.Messages.Count.ToString(),
                Foreground = new SolidColorBrush(Colors.White),
                FontSize = 10,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            root.Children.Add(badge);
        }

        var button = new Button
        {
            Tag = thread,
            Background = new SolidColorBrush(ColorHelper.FromArgb(0, 0, 0, 0)),
            BorderThickness = new Thickness(0),
            Padding = new Thickness(0),
            Content = root
        };
        button.Click += OnPinClick;
        Microsoft.UI.Xaml.Controls.ToolTipService.SetToolTip(button, $"{thread.ThreadLabel} · {thread.TargetLabel}");
        return button;
    }

    private void UpdateComposer(bool showComposer)
    {
        ComposerBorder.Visibility = showComposer ? Visibility.Visible : Visibility.Collapsed;
        if (!showComposer)
        {
            return;
        }

        var left = ComposerPosition.X + 26.0;
        var top = ComposerPosition.Y - 18.0;
        var maxLeft = Math.Max(12.0, ActualWidth - ComposerBorder.ActualWidth - 16.0);
        var maxTop = Math.Max(12.0, ActualHeight - ComposerBorder.ActualHeight - 16.0);
        left = Math.Clamp(left, 12.0, maxLeft);
        top = Math.Clamp(top, 12.0, maxTop);
        ComposerBorder.Margin = new Thickness(left, top, 0, 0);
        if (!string.IsNullOrWhiteSpace(ComposerText))
        {
            ComposerTextBox.SelectionStart = ComposerText.Length;
        }

        ComposerTextBox.Focus(FocusState.Programmatic);
    }

    private void OnPinClick(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { Tag: EditorCommentThread thread })
        {
            ThreadRequested?.Invoke(this, new CommentThreadRequestedEventArgs(thread));
        }
    }

    private void OnComposerTextChanged(object sender, TextChangedEventArgs e)
    {
        ComposerText = ComposerTextBox.Text;
    }

    private void OnComposerKeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key == Windows.System.VirtualKey.Escape)
        {
            ComposerCanceled?.Invoke(this, EventArgs.Empty);
            e.Handled = true;
            return;
        }

        if (e.Key == Windows.System.VirtualKey.Enter
            && (InputKeyboardSource.GetKeyStateForCurrentThread(Windows.System.VirtualKey.Shift) & Windows.UI.Core.CoreVirtualKeyStates.Down)
                != Windows.UI.Core.CoreVirtualKeyStates.Down)
        {
            SubmitComposer();
            e.Handled = true;
        }
    }

    private void OnCancelClick(object sender, RoutedEventArgs e)
    {
        ComposerCanceled?.Invoke(this, EventArgs.Empty);
    }

    private void OnSubmitClick(object sender, RoutedEventArgs e)
    {
        SubmitComposer();
    }

    private void SubmitComposer()
    {
        if (string.IsNullOrWhiteSpace(ComposerText))
        {
            return;
        }

        ComposerSubmitted?.Invoke(this, new CommentTextRequestedEventArgs(null, ComposerText.Trim()));
    }
}
