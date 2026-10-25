namespace Svg.Editor.Skia.Uno;

/// <summary>An editing gesture failed; the document controller has rolled back its command.</summary>
public sealed class SvgEditorInteractionFailedEventArgs(Exception exception) : EventArgs
{
    public Exception Exception { get; } = exception;
}
