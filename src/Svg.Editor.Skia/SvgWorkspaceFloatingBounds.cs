using System;

namespace Svg.Editor.Skia;

/// <summary>
/// Display-only bounds for a retained floating panel. Responsive projection never
/// changes preferred placement; only an explicit user move/resize should persist it.
/// </summary>
public readonly struct SvgWorkspaceFloatingBounds
{
    private SvgWorkspaceFloatingBounds(double x, double y, double width, double height)
    { X = x; Y = y; Width = width; Height = height; }
    public double X { get; }
    public double Y { get; }
    public double Width { get; }
    public double Height { get; }

    public static SvgWorkspaceFloatingBounds Project(SvgWorkspacePanelPlacement panel, double viewportWidth, double viewportHeight)
    {
        if (panel is null) throw new ArgumentNullException(nameof(panel));
        if (!Finite(viewportWidth) || !Finite(viewportHeight) || viewportWidth < 0 || viewportHeight < 0)
            throw new ArgumentOutOfRangeException(nameof(viewportWidth));
        var width = Math.Min(panel.Width, viewportWidth);
        var height = Math.Min(panel.Height, viewportHeight);
        var x = Math.Max(0, Math.Min(panel.X, viewportWidth - width));
        var y = Math.Max(0, Math.Min(panel.Y, viewportHeight - height));
        return new SvgWorkspaceFloatingBounds(x, y, width, height);
    }

    private static bool Finite(double value) => !double.IsNaN(value) && !double.IsInfinity(value);
}
