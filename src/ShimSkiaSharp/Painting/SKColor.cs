using System;

namespace ShimSkiaSharp.Painting
{
    public readonly struct SKColor
    {
        public byte Red { get; }
        
        public byte Green { get; }
        
        public byte Blue { get; }
        
        public byte Alpha { get; }

        public static readonly SKColor Empty = default;

        public SKColor(byte red, byte green, byte blue, byte alpha)
        {
            Red = red;
            Green = green;
            Blue = blue;
            Alpha = alpha;
        }

        public static implicit operator SKColorF(SKColor color)
        {
            return new(
                color.Red * (1 / 255.0f),
                color.Green * (1 / 255.0f),
                color.Blue * (1 / 255.0f),
                color.Alpha * (1 / 255.0f));
        }

        public override string ToString() 
            => FormattableString.Invariant($"{Red}, {Green}, {Blue}, {Alpha}");
    }
}
