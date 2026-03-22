using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using Svg.Editor.Skia.Uno.Models;

namespace Svg.Editor.Skia.Uno.Controls;

public sealed partial class SvgEditorCommentsPanel : UserControl
{
    public static readonly DependencyProperty ItemsSourceProperty =
        DependencyProperty.Register(
            nameof(ItemsSource),
            typeof(IEnumerable<EditorCommentThread>),
            typeof(SvgEditorCommentsPanel),
            new PropertyMetadata(null, OnItemsSourcePropertyChanged));

    public static readonly DependencyProperty SelectedThreadProperty =
        DependencyProperty.Register(
            nameof(SelectedThread),
            typeof(EditorCommentThread),
            typeof(SvgEditorCommentsPanel),
            new PropertyMetadata(null, OnSelectedThreadPropertyChanged));

    public static readonly DependencyProperty SummaryTextProperty =
        DependencyProperty.Register(
            nameof(SummaryText),
            typeof(string),
            typeof(SvgEditorCommentsPanel),
            new PropertyMetadata(string.Empty));

    private readonly ObservableCollection<EditorCommentThread> _visibleThreads = [];
    private readonly List<EditorCommentThread> _trackedThreads = [];
    private bool _showResolved = true;
    private string _searchText = string.Empty;
    public SvgEditorCommentsPanel()
    {
        InitializeComponent();
        UpdateResolvedToggleText();
        RefreshVisibleThreads();
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

    public string SummaryText
    {
        get => (string)GetValue(SummaryTextProperty);
        set => SetValue(SummaryTextProperty, value);
    }

    public string ResolvedToggleText { get; private set; } = "All";

    public ObservableCollection<EditorCommentThread> VisibleThreads => _visibleThreads;

    public event EventHandler<CommentThreadRequestedEventArgs>? ThreadRequested;

    public event EventHandler<CommentTextRequestedEventArgs>? ReplyRequested;

    public event EventHandler<CommentThreadRequestedEventArgs>? ResolveRequested;

    public event EventHandler<CommentThreadRequestedEventArgs>? DeleteRequested;

    private static void OnItemsSourcePropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var control = (SvgEditorCommentsPanel)d;
        control.Unsubscribe(e.OldValue as IEnumerable<EditorCommentThread>);
        control.Subscribe(e.NewValue as IEnumerable<EditorCommentThread>);
        control.RefreshVisibleThreads();
    }

    private static void OnSelectedThreadPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        ((SvgEditorCommentsPanel)d).RefreshVisibleThreads();
    }

    private void Subscribe(IEnumerable<EditorCommentThread>? threads)
    {
        if (threads is INotifyCollectionChanged collectionChanged)
        {
            collectionChanged.CollectionChanged += OnItemsCollectionChanged;
        }

        if (threads is null)
        {
            return;
        }

        foreach (var thread in threads)
        {
            _trackedThreads.Add(thread);
            thread.PropertyChanged += OnThreadPropertyChanged;
            thread.Messages.CollectionChanged += OnThreadMessagesChanged;
        }
    }

    private void Unsubscribe(IEnumerable<EditorCommentThread>? threads)
    {
        if (threads is INotifyCollectionChanged collectionChanged)
        {
            collectionChanged.CollectionChanged -= OnItemsCollectionChanged;
        }

        foreach (var thread in _trackedThreads)
        {
            thread.PropertyChanged -= OnThreadPropertyChanged;
            thread.Messages.CollectionChanged -= OnThreadMessagesChanged;
        }

        _trackedThreads.Clear();
    }

    private void OnItemsCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        Unsubscribe(sender as IEnumerable<EditorCommentThread>);
        Subscribe(sender as IEnumerable<EditorCommentThread>);
        RefreshVisibleThreads();
    }

    private void OnThreadPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        RefreshVisibleThreads();
    }

    private void OnThreadMessagesChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        RefreshVisibleThreads();
    }

    private void OnSearchTextChanged(object sender, TextChangedEventArgs e)
    {
        _searchText = SearchField.Text;
        RefreshVisibleThreads();
    }

    private void OnResolvedToggleClick(object sender, RoutedEventArgs e)
    {
        _showResolved = !_showResolved;
        UpdateResolvedToggleText();
        RefreshVisibleThreads();
    }

    private void OnThreadButtonClick(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { DataContext: EditorCommentThread thread })
        {
            ThreadRequested?.Invoke(this, new CommentThreadRequestedEventArgs(thread));
        }
    }

    private void OnReplyButtonClick(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement { DataContext: EditorCommentThread thread })
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(thread.DraftReplyText))
        {
            return;
        }

        ReplyRequested?.Invoke(this, new CommentTextRequestedEventArgs(thread, thread.DraftReplyText.Trim()));
    }

    private void OnResolveButtonClick(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { DataContext: EditorCommentThread thread })
        {
            ResolveRequested?.Invoke(this, new CommentThreadRequestedEventArgs(thread));
        }
    }

    private void OnDeleteButtonClick(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { DataContext: EditorCommentThread thread })
        {
            DeleteRequested?.Invoke(this, new CommentThreadRequestedEventArgs(thread));
        }
    }

    private void RefreshVisibleThreads()
    {
        _visibleThreads.Clear();

        if (ItemsSource is not null)
        {
            foreach (var thread in ItemsSource
                         .Where(thread => (_showResolved || !thread.IsResolved) && thread.Matches(_searchText))
                         .OrderBy(thread => thread.IsResolved)
                         .ThenByDescending(thread => thread.Id))
            {
                _visibleThreads.Add(thread);
            }
        }

        EmptyStateTextBlock.Visibility = _visibleThreads.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    private void UpdateResolvedToggleText()
    {
        ResolvedToggleText = _showResolved ? "All" : "Open only";
        if (ResolvedToggleTextBlock is not null)
        {
            ResolvedToggleTextBlock.Text = ResolvedToggleText;
        }
    }
}
