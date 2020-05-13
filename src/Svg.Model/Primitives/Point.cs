namespace Svg.Model
{
    public struct Point
    {
        public float X;
        public float Y;

        public static readonly Point Empty;

        public Point(float x, float y)
        {
            X = x;
            Y = y;
        }
    }
}
