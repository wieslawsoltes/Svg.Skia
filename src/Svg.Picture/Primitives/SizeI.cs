namespace Svg.Picture
{
    public readonly struct SizeI
    {
        public int Width { get; }
        public int Height { get; }

        public static readonly SizeI Empty;

        public bool IsEmpty => Width == default && Height == default;

        public SizeI(int width, int height)
        {
            Width = width;
            Height = height;
        }
    }
}
