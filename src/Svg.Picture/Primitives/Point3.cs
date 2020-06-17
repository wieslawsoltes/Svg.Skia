namespace Svg.Picture
{
    public readonly struct Point3
    {
        public float X { get; }
        public float Y { get; }
        public float Z { get; }

        public static readonly Point3 Empty;

        public bool IsEmpty => X == default && Y == default && Z == default;

        public Point3(float x, float y, float z)
        {
            X = x;
            Y = y;
            Z = z;
        }
    }
}
