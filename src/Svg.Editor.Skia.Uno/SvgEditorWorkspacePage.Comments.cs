using System.Collections.ObjectModel;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Input;
using Svg;
using Svg.Editor.Skia.Uno.Models;
using Svg.Editor.Svg;
using Windows.Foundation;
using Windows.System;
using Shim = ShimSkiaSharp;

namespace Svg.Editor.Skia.Uno;

public partial class SvgEditorWorkspacePage
{
    private const double CommentHitRadiusPixels = 22.0;
    private const string CurrentCommentAuthor = "Wieslaw Soltes";

    private readonly ObservableCollection<EditorCommentThread> _commentThreads = [];
    private EditorCommentThread? _selectedCommentThread;
    private string _commentsSummary = "No comments on this page";
    private bool _isCommentsInspector;
    private bool _isCommentDraftVisible;
    private string _commentDraftText = string.Empty;
    private Point _commentDraftViewPosition;
    private Shim.SKPoint _commentDraftPicturePoint;
    private string _commentDraftTargetLabel = string.Empty;
    private string? _commentDraftTargetElementId;
    private int _generatedCommentId;

    public ObservableCollection<EditorCommentThread> CommentThreads => _commentThreads;

    public EditorCommentThread? SelectedCommentThread => _selectedCommentThread;

    public string CommentsSummary
    {
        get => _commentsSummary;
        private set => SetField(ref _commentsSummary, value);
    }

    public bool IsCommentsInspectorActive => _isCommentsInspector;

    public Visibility CommentsInspectorVisibility => _isCommentsInspector ? Visibility.Visible : Visibility.Collapsed;

    public Visibility InspectorTabsVisibility => _isCommentsInspector ? Visibility.Collapsed : Visibility.Visible;

    public Visibility InspectorSelectionVisibility => _isCommentsInspector ? Visibility.Collapsed : Visibility.Visible;

    public bool IsCommentDraftVisible => _isCommentDraftVisible;

    public string CommentDraftText => _commentDraftText;

    public Point CommentDraftViewPosition => _commentDraftViewPosition;

    protected void OnCommentThreadRequested(object sender, CommentThreadRequestedEventArgs e)
    {
        CancelCommentDraft();
        SelectCommentThread(e.Thread, focusTarget: true);
    }

    protected void OnCommentReplyRequested(object sender, CommentTextRequestedEventArgs e)
    {
        if (e.Thread is null)
        {
            return;
        }

        AddReplyToThread(e.Thread, e.Text);
    }

    protected void OnCommentResolveRequested(object sender, CommentThreadRequestedEventArgs e)
    {
        ToggleCommentResolved(e.Thread);
    }

    protected void OnCommentDeleteRequested(object sender, CommentThreadRequestedEventArgs e)
    {
        DeleteCommentThread(e.Thread);
    }

    protected void OnCommentComposerSubmitted(object sender, CommentTextRequestedEventArgs e)
    {
        SubmitCommentDraft(e.Text);
    }

    protected void OnCommentComposerCanceled(object? sender, EventArgs e)
    {
        CancelCommentDraft();
    }

    private void ResetCommentState()
    {
        _commentThreads.Clear();
        _selectedCommentThread = null;
        _generatedCommentId = 0;
        _isCommentsInspector = false;
        _isCommentDraftVisible = false;
        _commentDraftText = string.Empty;
        _commentDraftTargetLabel = string.Empty;
        _commentDraftTargetElementId = null;
        CommentsSummary = "No comments on this page";
    }

    private void SyncCommentsForPage(EditorPageState state)
    {
        _commentThreads.Clear();
        foreach (var thread in state.CommentThreads)
        {
            _commentThreads.Add(thread);
        }

        _selectedCommentThread = null;
        foreach (var thread in _commentThreads)
        {
            thread.IsSelected = false;
        }

        if (state.SelectedCommentThread is not null && _commentThreads.Contains(state.SelectedCommentThread))
        {
            state.SelectedCommentThread.IsSelected = true;
            _selectedCommentThread = state.SelectedCommentThread;
        }

        CancelCommentDraft();
        RefreshCommentPositions();
        RefreshCommentsSummary();
        RaisePropertyChanged(nameof(CommentThreads));
        RaisePropertyChanged(nameof(SelectedCommentThread));
    }

    private void PersistCommentState()
    {
        if (_activePage is null)
        {
            return;
        }

        _activePage.SelectedCommentThread = _selectedCommentThread;
    }

    private void RefreshCommentPositions()
    {
        foreach (var thread in _commentThreads)
        {
            if (EditorSvg.TryGetViewPoint(new Shim.SKPoint((float)thread.PictureX, (float)thread.PictureY), out var viewPoint))
            {
                thread.ViewX = viewPoint.X;
                thread.ViewY = viewPoint.Y;
            }
            else
            {
                thread.ViewX = double.NaN;
                thread.ViewY = double.NaN;
            }
        }
    }

    private void RefreshCommentsSummary()
    {
        var total = _commentThreads.Count;
        if (total == 0)
        {
            CommentsSummary = "No comments on this page";
            return;
        }

        var resolved = _commentThreads.Count(thread => thread.IsResolved);
        var open = total - resolved;
        CommentsSummary = resolved == 0
            ? $"{open} open thread{(open == 1 ? string.Empty : "s")}"
            : $"{open} open · {resolved} resolved";
    }

    private void SetCommentsInspectorActive(bool isActive)
    {
        if (_isCommentsInspector == isActive)
        {
            return;
        }

        _isCommentsInspector = isActive;
        if (isActive)
        {
            _isPrototypeInspector = false;
            _isDevInspector = false;
        }

        RaisePropertyChanged(nameof(IsDesignInspectorActive));
        RaisePropertyChanged(nameof(IsPrototypeInspectorActive));
        RaisePropertyChanged(nameof(IsDevInspectorActive));
        RaisePropertyChanged(nameof(IsCommentsInspectorActive));
        RaisePropertyChanged(nameof(DesignInspectorVisibility));
        RaisePropertyChanged(nameof(PrototypeInspectorVisibility));
        RaisePropertyChanged(nameof(DevInspectorVisibility));
        RaisePropertyChanged(nameof(CommentsInspectorVisibility));
        RaisePropertyChanged(nameof(InspectorTabsVisibility));
        RaisePropertyChanged(nameof(InspectorSelectionVisibility));
    }

    private bool HandleCommentToolPointerPressed(PointerRoutedEventArgs e, Point viewPoint, Shim.SKPoint picturePoint)
    {
        if (_toolService.CurrentTool != ToolService.Tool.Comment)
        {
            return false;
        }

        if (TryHitCommentThread(viewPoint, out var thread))
        {
            CancelCommentDraft();
            SelectCommentThread(thread, focusTarget: true);
        }
        else
        {
            BeginCommentDraft(viewPoint, picturePoint, GetVisualHit(viewPoint));
        }

        e.Handled = true;
        return true;
    }

    private bool HandleCommentShortcutKey(KeyRoutedEventArgs e)
    {
        if (_isCommentDraftVisible && e.Key == VirtualKey.Escape)
        {
            CancelCommentDraft();
            e.Handled = true;
            return true;
        }

        if (HasKeyboardModifierPressed())
        {
            return false;
        }

        if (e.Key != VirtualKey.C)
        {
            return false;
        }

        SetTool(ToolService.Tool.Comment);
        e.Handled = true;
        return true;
    }

    private void BeginCommentDraft(Point viewPoint, Shim.SKPoint picturePoint, SvgVisualElement? target)
    {
        SetCommentsInspectorActive(true);
        _commentDraftPicturePoint = picturePoint;
        _commentDraftViewPosition = viewPoint;
        _commentDraftTargetElementId = target?.ID;
        _commentDraftTargetLabel = GetCommentTargetLabel(target);
        _commentDraftText = string.Empty;
        _isCommentDraftVisible = true;
        RaisePropertyChanged(nameof(CommentDraftViewPosition));
        RaisePropertyChanged(nameof(CommentDraftText));
        RaisePropertyChanged(nameof(IsCommentDraftVisible));
        CanvasStatus = $"Commenting on {_commentDraftTargetLabel}.";
    }

    private void CancelCommentDraft()
    {
        if (!_isCommentDraftVisible && string.IsNullOrEmpty(_commentDraftText))
        {
            return;
        }

        _isCommentDraftVisible = false;
        _commentDraftText = string.Empty;
        _commentDraftTargetLabel = string.Empty;
        _commentDraftTargetElementId = null;
        RaisePropertyChanged(nameof(CommentDraftText));
        RaisePropertyChanged(nameof(IsCommentDraftVisible));
    }

    private void SubmitCommentDraft(string text)
    {
        if (_activePage is null || string.IsNullOrWhiteSpace(text))
        {
            return;
        }

        var nextId = GetNextCommentId();
        var openingMessage = new EditorCommentMessage(nextId, CurrentCommentAuthor, text.Trim(), DateTimeOffset.Now, isMine: true);
        var thread = new EditorCommentThread(
            nextId,
            _activePage.Page.Title,
            _commentDraftPicturePoint.X,
            _commentDraftPicturePoint.Y,
            string.IsNullOrWhiteSpace(_commentDraftTargetLabel) ? _activePage.Page.Title : _commentDraftTargetLabel,
            _commentDraftTargetElementId,
            openingMessage);

        _commentThreads.Add(thread);
        _activePage.CommentThreads.Add(thread);
        CancelCommentDraft();
        SelectCommentThread(thread, focusTarget: true);
        RefreshCommentsSummary();
        CanvasStatus = $"Comment #{thread.Id} added to {thread.TargetLabel}.";
    }

    private void AddReplyToThread(EditorCommentThread thread, string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return;
        }

        thread.AddReply(new EditorCommentMessage(thread.Messages.Count + 1, CurrentCommentAuthor, text.Trim(), DateTimeOffset.Now, isMine: true));
        thread.DraftReplyText = string.Empty;
        SelectCommentThread(thread, focusTarget: false);
        CanvasStatus = $"Replied to comment #{thread.Id}.";
    }

    private void ToggleCommentResolved(EditorCommentThread thread)
    {
        thread.IsResolved = !thread.IsResolved;
        RefreshCommentsSummary();
        CanvasStatus = thread.IsResolved
            ? $"Resolved comment #{thread.Id}."
            : $"Reopened comment #{thread.Id}.";
    }

    private void DeleteCommentThread(EditorCommentThread thread)
    {
        _commentThreads.Remove(thread);
        _activePage?.CommentThreads.Remove(thread);
        if (ReferenceEquals(_selectedCommentThread, thread))
        {
            _selectedCommentThread = null;
            RaisePropertyChanged(nameof(SelectedCommentThread));
        }

        RefreshCommentsSummary();
        CanvasStatus = $"Deleted comment #{thread.Id}.";
    }

    private void SelectCommentThread(EditorCommentThread? thread, bool focusTarget)
    {
        foreach (var item in _commentThreads)
        {
            item.IsSelected = ReferenceEquals(item, thread);
        }

        _selectedCommentThread = thread;
        if (_activePage is not null)
        {
            _activePage.SelectedCommentThread = thread;
        }

        RaisePropertyChanged(nameof(SelectedCommentThread));
        SetCommentsInspectorActive(thread is not null || _toolService.CurrentTool == ToolService.Tool.Comment);
        RefreshCommentPositions();
        if (thread is not null && focusTarget)
        {
            var target = FindCommentTarget(thread);
            if (target is not null)
            {
                ApplySelection([target], target);
            }
            else
            {
                RefreshComputedState();
            }
        }
        else
        {
            RefreshComputedState();
        }

        if (thread is not null)
        {
            CanvasStatus = $"{thread.ThreadLabel} · {thread.TargetLabel}";
        }
    }

    private SvgVisualElement? FindCommentTarget(EditorCommentThread thread)
    {
        if (_document is null || string.IsNullOrWhiteSpace(thread.TargetElementId))
        {
            return null;
        }

        return _document
            .Descendants()
            .OfType<SvgVisualElement>()
            .FirstOrDefault(element => string.Equals(element.ID, thread.TargetElementId, StringComparison.Ordinal));
    }

    private bool TryHitCommentThread(Point viewPoint, out EditorCommentThread? thread)
    {
        thread = null;
        var bestDistance = double.MaxValue;

        foreach (var item in _commentThreads)
        {
            if (double.IsNaN(item.ViewX) || double.IsNaN(item.ViewY))
            {
                continue;
            }

            var dx = item.ViewX - viewPoint.X;
            var dy = item.ViewY - viewPoint.Y;
            var distance = Math.Sqrt((dx * dx) + (dy * dy));
            if (distance <= CommentHitRadiusPixels && distance < bestDistance)
            {
                bestDistance = distance;
                thread = item;
            }
        }

        return thread is not null;
    }

    private int GetNextCommentId()
    {
        _generatedCommentId = Math.Max(
            _generatedCommentId + 1,
            _pageStates.SelectMany(state => state.CommentThreads).Select(thread => thread.Id).DefaultIfEmpty(0).Max() + 1);
        return _generatedCommentId;
    }

    private string GetCommentTargetLabel(SvgVisualElement? target)
    {
        if (target is null)
        {
            return _activePage?.Page.Title ?? PageTitle;
        }

        return string.IsNullOrWhiteSpace(target.ID)
            ? GetElementTypeLabel(target)
            : target.ID!;
    }
}
