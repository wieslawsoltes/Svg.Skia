namespace Svg.Model
{
    public struct SizeI
    {
        public int Width;
        public int Height;

        public static readonly SizeI Empty;

        public bool IsEmpty => Width == default && Height == default;

        public SizeI(int width, int height)
        {
            Width = width;
            Height = height;
        }
    }
}
