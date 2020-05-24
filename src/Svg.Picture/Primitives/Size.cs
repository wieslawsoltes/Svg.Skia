namespace Svg.Picture
{
    public struct Size
    {
        public float Width { get; set; }
        public float Height { get; set; }

        public static readonly Size Empty;

        public bool IsEmpty => Width == default && Height == default;

        public Size(float width, float height)
        {
            Width = width;
            Height = height;
        }
    }
}
