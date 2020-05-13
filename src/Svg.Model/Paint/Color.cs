namespace Svg.Model
{
    public struct Color
    {
        public byte Red;
        public byte Green;
        public byte Blue;
        public byte Alpha;

        public static readonly Color Empty;

        public Color(byte red, byte green, byte blue, byte alpha)
        {
            Red = red;
            Green = green;
            Blue = blue;
            Alpha = alpha;
        }
    }
}
