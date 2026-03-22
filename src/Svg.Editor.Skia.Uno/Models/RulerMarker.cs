namespace Svg.Editor.Skia.Uno.Models;

public sealed class RulerMarker
{
    public RulerMarker(double start, double end, double? center = null, string? label = null)
    {
        Start = start;
        End = end;
        Center = center;
        Label = label;
    }

    public double Start { get; }

    public double End { get; }

    public double? Center { get; }

    public string? Label { get; }
}
