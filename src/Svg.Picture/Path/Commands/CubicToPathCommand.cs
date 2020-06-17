
namespace Svg.Picture
{
    public sealed class CubicToPathCommand : PathCommand
    {
        public float X0 { get; }
        public float Y0 { get; }
        public float X1 { get; }
        public float Y1 { get; }
        public float X2 { get; }
        public float Y2 { get; }

        public CubicToPathCommand(float x0, float y0, float x1, float y1, float x2, float y2)
        {
            X0 = x0;
            Y0 = y0;
            X1 = x1;
            Y1 = y1;
            X2 = x2;
            Y2 = y2;
        }
    }
}
