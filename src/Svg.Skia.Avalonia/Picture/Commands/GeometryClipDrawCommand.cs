using AM = Avalonia.Media;

namespace Svg.Skia.Avalonia
{
    public sealed class GeometryClipDrawCommand : DrawCommand
    {
        public AM.Geometry? Clip { get; }

        public GeometryClipDrawCommand(AM.Geometry? clip)
        {
            Clip = clip;
        }
    }
}
