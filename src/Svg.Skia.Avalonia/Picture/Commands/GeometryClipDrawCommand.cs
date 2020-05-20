using AM = Avalonia.Media;

namespace Svg.Picture.Avalonia
{
    internal class GeometryClipDrawCommand : DrawCommand
    {
        public readonly AM.Geometry? Clip;

        public GeometryClipDrawCommand(AM.Geometry? clip)
        {
            Clip = clip;
        }
    }
}
