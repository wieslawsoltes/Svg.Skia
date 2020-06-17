using AM = Avalonia.Media;

namespace Svg.Picture.Avalonia
{
    public sealed class GeometryClipDrawCommand : DrawCommand
    {
        public readonly AM.Geometry? Clip;

        public GeometryClipDrawCommand(AM.Geometry? clip)
        {
            Clip = clip;
        }
    }
}
