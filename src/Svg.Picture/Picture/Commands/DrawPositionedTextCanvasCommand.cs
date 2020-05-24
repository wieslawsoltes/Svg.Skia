namespace Svg.Picture
{
    public class DrawPositionedTextCanvasCommand : CanvasCommand
    {
        public string Text { get; set; }
        public Point[]? Points { get; set; }
        public Paint? Paint { get; set; }

        public DrawPositionedTextCanvasCommand(string text, Point[] points, Paint paint)
        {
            Text = text;
            Points = points;
            Paint = paint;
        }
    }
}
