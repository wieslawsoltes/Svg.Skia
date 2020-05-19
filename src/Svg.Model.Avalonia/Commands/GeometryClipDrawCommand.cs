using AM = Avalonia.Media;

namespace Svg.Model.Avalonia
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
