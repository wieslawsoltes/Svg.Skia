using Svg.Editor.Skia.Uno.Models;

namespace Svg.Editor.Skia.Uno;

public sealed class LibraryCommandRequestedEventArgs : EventArgs
{
    public LibraryCommandRequestedEventArgs(EditorLibraryCommand command, EditorLibraryItem? library = null)
    {
        Command = command;
        Library = library;
    }

    public EditorLibraryCommand Command { get; }

    public EditorLibraryItem? Library { get; }
}
