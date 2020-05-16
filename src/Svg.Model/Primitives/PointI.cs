namespace Svg.Model
{
    public struct PointI
    {
        public int X;
        public int Y;

        public static readonly PointI Empty;

        public bool IsEmpty => X == default && Y == default;

        public PointI(int x, int y)
        {
            X = x;
            Y = y;
        }
    }
}
