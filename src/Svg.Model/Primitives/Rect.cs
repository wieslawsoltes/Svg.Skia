
namespace Svg.Model
{
    public struct Rect
    {
        public float Left;
        public float Top;
        public float Right;
        public float Bottom;

        public static readonly Rect Empty;

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
    }
}
