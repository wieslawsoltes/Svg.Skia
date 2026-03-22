namespace Svg.Editor.Skia.Uno.Models;

public sealed class EditorLayoutGuideStyleItem
{
    public EditorLayoutGuideStyleItem(
        string styleId,
        string name,
        string? description,
        double gridSize,
        bool isGridVisible,
        bool isSnapEnabled)
    {
        StyleId = styleId;
        Name = string.IsNullOrWhiteSpace(name) ? "Grid" : name.Trim();
        Description = description?.Trim() ?? string.Empty;
        GridSize = Math.Max(1.0, gridSize);
        IsGridVisible = isGridVisible;
        IsSnapEnabled = isSnapEnabled;
    }

    public string StyleId { get; }

    public string Name { get; }

    public string Description { get; }

    public double GridSize { get; }

    public bool IsGridVisible { get; }

    public bool IsSnapEnabled { get; }

    public string Summary => $"{GridSize:0.##} px grid";

    public EditorLayoutGuideStyleItem Clone()
    {
        return new EditorLayoutGuideStyleItem(
            StyleId,
            Name,
            Description,
            GridSize,
            IsGridVisible,
            IsSnapEnabled);
    }
}
