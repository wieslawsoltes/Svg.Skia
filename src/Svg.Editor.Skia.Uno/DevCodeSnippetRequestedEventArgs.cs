namespace Svg.Editor.Skia.Uno;

public sealed class DevCodeSnippetRequestedEventArgs : EventArgs
{
    public DevCodeSnippetRequestedEventArgs(string snippetId)
    {
        SnippetId = snippetId;
    }

    public string SnippetId { get; }
}
