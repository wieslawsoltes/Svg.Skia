
namespace Svg.Model.Picture.Commands
{
    public sealed class DrawTextOnPathCanvasCommand : CanvasCommand
    {
        public string Text { get; }
        public Path.Path? Path { get; }
        public float HOffset { get; }
        public float VOffset { get; }
        public Paint.Paint? Paint { get; }

        public DrawTextOnPathCanvasCommand(string text, Path.Path path, float hOffset, float vOffset, Paint.Paint paint)
        {
            Text = text;
            Path = path;
            HOffset = hOffset;
            VOffset = vOffset;
            Paint = paint;
        }
    }
}
