namespace Svg.Model
{
    public struct Point3
    {
        public float X;
        public float Y;
        public float Z;

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
