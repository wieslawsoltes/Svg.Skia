using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using Microsoft.UI.Xaml;

namespace Svg.Editor.Skia.Uno.Models;

public sealed class EditorCommentThread : INotifyPropertyChanged
{
    private double _viewX;
    private double _viewY;
    private bool _isResolved;
    private bool _isSelected;
    private string _draftReplyText = string.Empty;

    public EditorCommentThread(
        int id,
        string pageTitle,
        double pictureX,
        double pictureY,
        string targetLabel,
        string? targetElementId,
        EditorCommentMessage openingMessage)
    {
        Id = id;
        PageTitle = pageTitle;
        PictureX = pictureX;
        PictureY = pictureY;
        TargetLabel = targetLabel;
        TargetElementId = targetElementId;
        Messages = [openingMessage];
        Messages.CollectionChanged += OnMessagesCollectionChanged;
    }

    public int Id { get; }

    public string PageTitle { get; }

    public double PictureX { get; }

    public double PictureY { get; }

    public string TargetLabel { get; }

    public string? TargetElementId { get; }

    public ObservableCollection<EditorCommentMessage> Messages { get; }

    public string ThreadLabel => $"#{Id} · {PageTitle}";

    public string MetaText => Messages.Count == 0
        ? TargetLabel
        : $"{Messages[0].AuthorName} {Messages[0].TimestampText}";

    public string PreviewText => Messages.Count == 0 ? string.Empty : Messages[^1].Text;

    public string MarkerInitials => Messages.Count == 0 ? "C" : Messages[0].AuthorInitials;

    public string ReplyCountText => Messages.Count > 1 ? $"+{Messages.Count - 1}" : string.Empty;

    public Visibility ReplyCountVisibility => Messages.Count > 1 ? Visibility.Visible : Visibility.Collapsed;

    public Visibility ResolvedVisibility => _isResolved ? Visibility.Visible : Visibility.Collapsed;

    public Visibility ExpandedVisibility => _isSelected ? Visibility.Visible : Visibility.Collapsed;

    public string ResolveButtonText => _isResolved ? "Reopen" : "Resolve";

    public double ViewX
    {
        get => _viewX;
        set
        {
            if (Math.Abs(_viewX - value) < 0.01)
            {
                return;
            }

            _viewX = value;
            RaisePropertyChanged(nameof(ViewX));
        }
    }

    public double ViewY
    {
        get => _viewY;
        set
        {
            if (Math.Abs(_viewY - value) < 0.01)
            {
                return;
            }

            _viewY = value;
            RaisePropertyChanged(nameof(ViewY));
        }
    }

    public bool IsResolved
    {
        get => _isResolved;
        set
        {
            if (_isResolved == value)
            {
                return;
            }

            _isResolved = value;
            RaisePropertyChanged(nameof(IsResolved));
            RaisePropertyChanged(nameof(ResolvedVisibility));
            RaisePropertyChanged(nameof(ResolveButtonText));
        }
    }

    public bool IsSelected
    {
        get => _isSelected;
        set
        {
            if (_isSelected == value)
            {
                return;
            }

            _isSelected = value;
            RaisePropertyChanged(nameof(IsSelected));
            RaisePropertyChanged(nameof(ExpandedVisibility));
        }
    }

    public string DraftReplyText
    {
        get => _draftReplyText;
        set
        {
            if (_draftReplyText == value)
            {
                return;
            }

            _draftReplyText = value;
            RaisePropertyChanged(nameof(DraftReplyText));
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public void AddReply(EditorCommentMessage message)
    {
        Messages.Add(message);
    }

    public bool Matches(string search)
    {
        if (string.IsNullOrWhiteSpace(search))
        {
            return true;
        }

        var value = search.Trim();
        return ThreadLabel.Contains(value, StringComparison.OrdinalIgnoreCase)
            || TargetLabel.Contains(value, StringComparison.OrdinalIgnoreCase)
            || Messages.Any(message =>
                message.AuthorName.Contains(value, StringComparison.OrdinalIgnoreCase)
                || message.Text.Contains(value, StringComparison.OrdinalIgnoreCase));
    }

    private void OnMessagesCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        RaisePropertyChanged(nameof(MetaText));
        RaisePropertyChanged(nameof(PreviewText));
        RaisePropertyChanged(nameof(MarkerInitials));
        RaisePropertyChanged(nameof(ReplyCountText));
        RaisePropertyChanged(nameof(ReplyCountVisibility));
    }

    private void RaisePropertyChanged(string propertyName)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
