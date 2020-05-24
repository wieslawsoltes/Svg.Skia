namespace Svg.Picture
{
    public struct Point3
    {
        public float X { get; set; }
        public float Y { get; set; }
        public float Z { get; set; }

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
