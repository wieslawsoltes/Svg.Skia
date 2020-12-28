
using Svg.Model.Painting;

namespace Svg.Model.Primitives.CanvasCommands
{
    public sealed class SaveLayerCanvasCommand : CanvasCommand
    {
        public int Count { get; }
        public Paint? Paint { get; }

        public SaveLayerCanvasCommand(int count)
        {
            Count = count;
        }

        public SaveLayerCanvasCommand(int count, Paint paint)
        {
            Count = count;
            Paint = paint;
        }
    }
}
