namespace Svg.Model
{
    public struct SizeI
    {
        public int Width;
        public int Height;

        public static readonly SizeI Empty;

        public SizeI(int width, int height)
        {
            Width = width;
            Height = height;
        }
    }
}
