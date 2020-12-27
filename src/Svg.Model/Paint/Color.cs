
namespace Svg.Model
{
    public readonly struct Color
    {
        public byte Red { get; }
        public byte Green { get; }
        public byte Blue { get; }
        public byte Alpha { get; }

        public static readonly Color Empty = default;

        public Color(byte red, byte green, byte blue, byte alpha)
        {
            Red = red;
            Green = green;
            Blue = blue;
            Alpha = alpha;
        }

        public static implicit operator ColorF(Color color)
        {
            return new ColorF(
                color.Red * (1 / 255.0f),
                color.Green * (1 / 255.0f),
                color.Blue * (1 / 255.0f),
                color.Alpha * (1 / 255.0f));
        }
    }
}
