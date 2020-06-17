
namespace Svg.Picture
{
    public sealed class DrawTextOnPathCanvasCommand : CanvasCommand
    {
        public string Text { get; }
        public Path? Path { get; }
        public float HOffset { get; }
        public float VOffset { get; }
        public Paint? Paint { get; }

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
