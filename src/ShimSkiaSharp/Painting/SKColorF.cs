using System;

namespace ShimSkiaSharp.Painting
{
    public readonly struct SKColorF
    {
        public float Red { get; }
        public float Green { get; }
        public float Blue { get; }
        public float Alpha { get; }

        public static readonly SKColorF Empty = default;

        public SKColorF(float red, float green, float blue, float alpha)
        {
            Red = red;
            Green = green;
            Blue = blue;
            Alpha = alpha;
        }

        public static implicit operator SKColor(SKColorF color)
        {
            return new(
                (byte)(color.Red * 255.0f),
                (byte)(color.Green * 255.0f),
                (byte)(color.Blue * 255.0f),
                (byte)(color.Alpha * 255.0f));
        }

        public override string ToString() 
            => FormattableString.Invariant($"{Red}, {Green}, {Blue}, {Alpha}");
    }
}
