using Svg.Editor.Skia.Uno.Models;

namespace Svg.Editor.Skia.Uno;

public sealed class CommentTextRequestedEventArgs : EventArgs
{
    public CommentTextRequestedEventArgs(EditorCommentThread? thread, string text)
    {
        Thread = thread;
        Text = text;
    }

    public EditorCommentThread? Thread { get; }

    public string Text { get; }
}
