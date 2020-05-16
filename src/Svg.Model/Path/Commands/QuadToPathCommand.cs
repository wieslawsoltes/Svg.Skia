namespace Svg.Model
{
    public class QuadToPathCommand : PathCommand
    {
        public float X0;
        public float Y0;
        public float X1;
        public float Y1;

        public QuadToPathCommand(float x0, float y0, float x1, float y1)
        {
            X0 = x0;
            Y0 = y0;
            X1 = x1;
            Y1 = y1;
        }
    }
}
