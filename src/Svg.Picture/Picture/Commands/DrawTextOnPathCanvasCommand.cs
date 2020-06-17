namespace Svg.Picture
{
    public sealed class DrawTextOnPathCanvasCommand : CanvasCommand
    {
        public string Text { get; set; }
        public Path? Path { get; set; }
        public float HOffset { get; set; }
        public float VOffset { get; set; }
        public Paint? Paint { get; set; }

        public DrawTextOnPathCanvasCommand(string text, Path path, float hOffset, float vOffset, Paint paint)
        {
            Text = text;
            Path = path;
            HOffset = hOffset;
            VOffset = vOffset;
            Paint = paint;
        }
    }
}
