using System;

namespace Svg.Model.Primitives
{
    public struct Rect
    {
        public float Left { get; set; }
        public float Top { get; set; }
        public float Right { get; set; }
        public float Bottom { get; set; }

        public static readonly Rect Empty = default;

        public readonly bool IsEmpty => Left == default && Top == default && Right == default && Bottom == default;

        public readonly float Width => Right - Left;

        public readonly float Height => Bottom - Top;

        public readonly Size Size => new(Width, Height);

        public readonly Point Location => new(Left, Top);

        public Rect(float left, float top, float right, float bottom)
        {
            Left = left;
            Right = right;
            Top = top;
            Bottom = bottom;
        }

        public static Rect Create(float x, float y, float width, float height)
        {
            return new()
            {
                Left = x,
                Top = y,
                Right = x + width,
                Bottom = y + height
            };
        }

        public static Rect Create(Size size)
        {
            return Create(0, 0, size.Width, size.Height);
        }

        public static Rect Union(Rect a, Rect b)
        {
            return new(
                Math.Min(a.Left, b.Left),
                Math.Min(a.Top, b.Top),
                Math.Max(a.Right, b.Right),
                Math.Max(a.Bottom, b.Bottom));
        }

        public override string ToString()
        {
            return FormattableString.Invariant($"{Left}, {Top}, {Width}, {Height}");
        }
    }
}
