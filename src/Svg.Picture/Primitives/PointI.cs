namespace Svg.Picture
{
    public struct PointI
    {
        public int X { get; set; }
        public int Y { get; set; }

        public static readonly PointI Empty;

        public bool IsEmpty => X == default && Y == default;

        public PointI(int x, int y)
        {
            X = x;
            Y = y;
        }
    }
}
