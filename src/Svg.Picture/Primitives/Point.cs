namespace Svg.Picture
{
    public struct Point
    {
        public float X;
        public float Y;

        public static readonly Point Empty;

        public bool IsEmpty => X == default && Y == default;

        public Point(float x, float y)
        {
            X = x;
            Y = y;
        }
    }
}
