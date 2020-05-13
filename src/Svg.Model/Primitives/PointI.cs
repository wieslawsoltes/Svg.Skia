namespace Svg.Model
{
    public struct PointI
    {
        public int X;
        public int Y;

        public static readonly PointI Empty;

        public PointI(int x, int y)
        {
            X = x;
            Y = y;
        }
    }
}
