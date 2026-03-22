namespace Svg.Editor.Core;

public sealed class AutoLayoutSettings
{
    public bool IsEnabled { get; set; }

    public AutoLayoutFlow Flow { get; set; } = AutoLayoutFlow.Vertical;

    public AutoLayoutSizeMode WidthMode { get; set; } = AutoLayoutSizeMode.Fixed;

    public AutoLayoutSizeMode HeightMode { get; set; } = AutoLayoutSizeMode.Fixed;

    public AutoLayoutAlignment HorizontalAlignment { get; set; } = AutoLayoutAlignment.Start;

    public AutoLayoutAlignment VerticalAlignment { get; set; } = AutoLayoutAlignment.Start;

    public float Gap { get; set; } = 24f;

    public float PaddingHorizontal { get; set; } = 24f;

    public float PaddingVertical { get; set; } = 24f;

    public bool ClipContent { get; set; }
}
