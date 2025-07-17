using Avalonia.Input;
using Avalonia;
using Avalonia.Svg.Skia;
using Svg;
using Svg.Skia;

namespace AvalonDraw.Services;

public class ToolService
{
    public enum Tool
    {
        Select,
        PathSelect,
        PolygonSelect,
        PolylineSelect,
        Line,
        Rect,
        Circle,
        Ellipse,
        Polygon,
        Polyline
    }

    public Tool CurrentTool { get; private set; } = Tool.Select;

    public void SetTool(Tool tool)
    {
        CurrentTool = tool;
    }
}
