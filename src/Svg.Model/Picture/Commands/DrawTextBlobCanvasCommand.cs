
using Svg.Model.Painting;

namespace Svg.Model.Picture.Commands
{
    public sealed class DrawTextBlobCanvasCommand : CanvasCommand
    {
        public TextBlob? TextBlob { get; }
        public float X { get; }
        public float Y { get; }
        public Painting.Paint? Paint { get; }

        public DrawTextBlobCanvasCommand(TextBlob textBlob, float x, float y, Painting.Paint paint)
        {
            TextBlob = textBlob;
            X = x;
            Y = y;
            Paint = paint;
        }
    }
}
