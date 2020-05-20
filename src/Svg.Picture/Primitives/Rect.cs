
using System;

namespace Svg.Picture
{
    public struct Rect
    {
        public float Left;
        public float Top;
        public float Right;
        public float Bottom;

        public static readonly Rect Empty;

        public bool IsEmpty => Left == default && Top == default && Right == default && Bottom == default;

        public float Width => Right - Left;

        public float Height => Bottom - Top;

        public Size Size => new Size(Width, Height);

        public Point Location => new Point(Left, Top);

        public Rect(float left, float top, float right, float bottom)
        {
            Left = left;
            Right = right;
            Top = top;
            Bottom = bottom;
        }

        public static Rect Create(float x, float y, float width, float height)
        {
            return new Rect()
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
            return new Rect(
                Math.Min(a.Left, b.Left),
                Math.Min(a.Top, b.Top),
                Math.Max(a.Right, b.Right),
                Math.Max(a.Bottom, b.Bottom));
        }
    }
}
