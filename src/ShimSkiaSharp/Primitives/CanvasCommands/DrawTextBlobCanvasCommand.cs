
using ShimSkiaSharp.Painting;

namespace ShimSkiaSharp.Primitives.CanvasCommands
{
    public sealed class DrawTextBlobCanvasCommand : CanvasCommand
    {
        public SKTextBlob? TextBlob { get; }
        public float X { get; }
        public float Y { get; }
        public SKPaint? Paint { get; }

        public DrawTextBlobCanvasCommand(SKTextBlob textBlob, float x, float y, SKPaint paint)
        {
            TextBlob = textBlob;
            X = x;
            Y = y;
            Paint = paint;
        }
    }
}
