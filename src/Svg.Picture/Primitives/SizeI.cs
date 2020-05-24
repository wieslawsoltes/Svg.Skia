namespace Svg.Picture
{
    public struct SizeI
    {
        public int Width { get; set; }
        public int Height { get; set; }

        public static readonly SizeI Empty;

        public bool IsEmpty => Width == default && Height == default;

        public SizeI(int width, int height)
        {
            Width = width;
            Height = height;
        }
    }
}
