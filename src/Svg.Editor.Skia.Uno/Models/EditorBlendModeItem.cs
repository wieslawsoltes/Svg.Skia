namespace Svg.Editor.Skia.Uno.Models;

public sealed class EditorBlendModeItem
{
    public EditorBlendModeItem(string id, string label)
    {
        Id = id;
        Label = label;
    }

    public string Id { get; }

    public string Label { get; }
}
