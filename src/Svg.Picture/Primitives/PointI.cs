namespace Svg.Picture
{
    public readonly struct PointI
    {
        public int X { get; }
        public int Y { get; }

        public static readonly PointI Empty;

        public bool IsEmpty => X == default && Y == default;

        public PointI(int x, int y)
        {
            X = x;
            Y = y;
        }
    }
}
