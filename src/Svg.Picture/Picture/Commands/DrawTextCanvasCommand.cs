namespace Svg.Picture
{
    public class DrawTextCanvasCommand : CanvasCommand
    {
        public string Text;
        public float X;
        public float Y;
        public Paint? Paint;

        public DrawTextCanvasCommand(string text, float x, float y, Paint paint)
        {
            Text = text;
            X = x;
            Y = y;
            Paint = paint;
        }
    }
}
