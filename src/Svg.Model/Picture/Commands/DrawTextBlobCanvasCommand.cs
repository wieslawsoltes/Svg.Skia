
namespace Svg.Model
{
    public sealed class DrawTextBlobCanvasCommand : CanvasCommand
    {
        public TextBlob? TextBlob { get; }
        public float X { get; }
        public float Y { get; }
        public Paint? Paint { get; }

        public DrawTextBlobCanvasCommand(TextBlob textBlob, float x, float y, Paint paint)
        {
            TextBlob = textBlob;
            X = x;
            Y = y;
            Paint = paint;
        }
    }
}
