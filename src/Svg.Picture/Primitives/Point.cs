namespace Svg.Picture
{
    public struct Point
    {
        public float X { get; set; }
        public float Y { get; set; }

        public static readonly Point Empty;

        public bool IsEmpty => X == default && Y == default;

        public Point(float x, float y)
        {
            X = x;
            Y = y;
        }
    }
}
