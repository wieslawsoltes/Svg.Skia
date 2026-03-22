using Svg.Editor.Core;

namespace Svg.Editor.Skia.Uno.Models;

public sealed class EditorFramePresetItem
{
    public const string CustomId = "custom";

    public required string Id { get; init; }

    public required string Name { get; init; }

    public required string Category { get; init; }

    public double Width { get; init; }

    public double Height { get; init; }

    public FrameContainerKind DefaultKind { get; init; } = FrameContainerKind.Frame;

    public bool IsCustom => string.Equals(Id, CustomId, StringComparison.Ordinal);

    public string SizeLabel => IsCustom ? "Keep current size" : $"{Width:0} × {Height:0}";

    public string DisplayLabel => IsCustom ? Name : $"{Name} • {SizeLabel}";

    public string SecondaryLabel => IsCustom ? Category : $"{Category} • {SizeLabel}";
}
