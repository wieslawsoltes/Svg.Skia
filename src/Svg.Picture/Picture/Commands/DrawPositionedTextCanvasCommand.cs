namespace Svg.Picture
{
    public class DrawPositionedTextCanvasCommand : CanvasCommand
    {
        public string Text;
        public Point[]? Points;
        public Paint? Paint;

        public DrawPositionedTextCanvasCommand(string text, Point[] points, Paint paint)
        {
            Text = text;
            Points = points;
            Paint = paint;
        }
    }
}
