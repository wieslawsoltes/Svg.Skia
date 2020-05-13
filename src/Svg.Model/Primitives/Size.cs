namespace Svg.Model
{
    public struct Size
    {
        public float Width;
        public float Height;

        public static readonly Size Empty;

        public Size(float width, float height)
        {
            Width = width;
            Height = height;
        }
    }
}
