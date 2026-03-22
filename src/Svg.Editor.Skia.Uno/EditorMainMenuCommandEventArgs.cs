namespace Svg.Editor.Skia.Uno;

public sealed class EditorMainMenuCommandEventArgs : EventArgs
{
    public EditorMainMenuCommandEventArgs(EditorMainMenuCommand command)
    {
        Command = command;
    }

    public EditorMainMenuCommand Command { get; }
}
