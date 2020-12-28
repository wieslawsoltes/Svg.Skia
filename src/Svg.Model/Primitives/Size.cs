namespace Svg.Model
{
    public readonly struct Size
    {
        public float Width { get; }
        public float Height { get; }

        public static readonly Size Empty;

        public readonly bool IsEmpty => Width == default && Height == default;

        public Size(float width, float height)
        {
            Width = width;
            Height = height;
        }
    }
}
