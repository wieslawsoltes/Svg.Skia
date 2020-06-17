namespace Svg.Picture
{
    public sealed class DrawTextCanvasCommand : CanvasCommand
    {
        public string Text { get; set; }
        public float X { get; set; }
        public float Y { get; set; }
        public Paint? Paint { get; set; }

        public DrawTextCanvasCommand(string text, float x, float y, Paint paint)
        {
            Text = text;
            X = x;
            Y = y;
            Paint = paint;
        }
    }
}
