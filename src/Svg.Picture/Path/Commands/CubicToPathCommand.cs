namespace Svg.Picture
{
    public class CubicToPathCommand : PathCommand
    {
        public float X0;
        public float Y0;
        public float X1;
        public float Y1;
        public float X2;
        public float Y2;

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
