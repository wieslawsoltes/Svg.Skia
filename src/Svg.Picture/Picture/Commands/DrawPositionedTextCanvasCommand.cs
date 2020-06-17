namespace Svg.Picture
{
    public sealed class DrawPositionedTextCanvasCommand : CanvasCommand
    {
        public string Text { get; }
        public Point[]? Points { get; }
        public Paint? Paint { get; }

        public DrawPositionedTextCanvasCommand(string text, Point[] points, Paint paint)
        {
            Text = text;
            Points = points;
            Paint = paint;
        }
    }
}
