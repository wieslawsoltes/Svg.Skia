
using Svg.Model.Painting;

namespace Svg.Model.Primitives.CanvasCommands
{
    public sealed class DrawTextOnPathCanvasCommand : CanvasCommand
    {
        public string Text { get; }
        public SKPath? Path { get; }
        public float HOffset { get; }
        public float VOffset { get; }
        public SKPaint? Paint { get; }

        public DrawTextOnPathCanvasCommand(string text, SKPath path, float hOffset, float vOffset, SKPaint paint)
        {
            Text = text;
            Path = path;
            HOffset = hOffset;
            VOffset = vOffset;
            Paint = paint;
        }
    }
}
