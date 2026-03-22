using Svg.Editor.Skia.Uno.Models;

namespace Svg.Editor.Skia.Uno;

public sealed class CommentThreadRequestedEventArgs : EventArgs
{
    public CommentThreadRequestedEventArgs(EditorCommentThread thread)
    {
        Thread = thread;
    }

    public EditorCommentThread Thread { get; }
}
