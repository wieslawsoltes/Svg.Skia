
using Svg.Model.Painting;

namespace Svg.Model.Primitives.CanvasCommands
{
    public sealed class DrawTextCanvasCommand : CanvasCommand
    {
        public string Text { get; }
        public float X { get; }
        public float Y { get; }
        public SKPaint? Paint { get; }

        public DrawTextCanvasCommand(string text, float x, float y, SKPaint paint)
        {
            Text = text;
            X = x;
            Y = y;
            Paint = paint;
        }
    }
}
