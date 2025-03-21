using AM = Avalonia.Media;

namespace Avalonia.Svg.Commands;

public sealed class GeometryClipDrawCommand : DrawCommand
{
    public AM.Geometry? Clip { get; }

    public GeometryClipDrawCommand(AM.Geometry? clip)
    {
        Clip = clip;
    }
}