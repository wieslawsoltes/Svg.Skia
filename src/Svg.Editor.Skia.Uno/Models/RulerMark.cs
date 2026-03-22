namespace Svg.Editor.Skia.Uno.Models;

public sealed class RulerMark
{
    public RulerMark(string label, double position, double tickSize, bool isAccent = false)
    {
        Label = label;
        Position = position;
        TickSize = tickSize;
        IsAccent = isAccent;
    }

    public string Label { get; }

    public double Position { get; }

    public double TickSize { get; }

    public bool IsAccent { get; }
}
