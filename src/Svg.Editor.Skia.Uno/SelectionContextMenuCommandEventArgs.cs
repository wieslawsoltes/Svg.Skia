namespace Svg.Editor.Skia.Uno;

public sealed class SelectionContextMenuCommandEventArgs : EventArgs
{
    public SelectionContextMenuCommandEventArgs(SelectionContextMenuCommand command, string? parameter = null)
    {
        Command = command;
        Parameter = parameter;
    }

    public SelectionContextMenuCommand Command { get; }

    public string? Parameter { get; }
}
