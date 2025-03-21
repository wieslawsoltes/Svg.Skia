using AM = Avalonia.Media;

namespace Avalonia.Svg.Commands;

public sealed class GeometryClipDrawCommand(AM.Geometry? clip) : DrawCommand
{
    public AM.Geometry? Clip { get; } = clip;
}