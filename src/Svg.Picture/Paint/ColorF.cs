namespace Svg.Picture
{
    public readonly struct ColorF
    {
        public float Red { get; }
        public float Green { get; }
        public float Blue { get; }
        public float Alpha { get; }

        public static readonly ColorF Empty = default;

        public ColorF(float red, float green, float blue, float alpha)
        {
            Red = red;
            Green = green;
            Blue = blue;
            Alpha = alpha;
        }
    }
}
