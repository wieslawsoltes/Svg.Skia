
namespace Svg.Model.Picture.Commands
{
    public sealed class SaveLayerCanvasCommand : CanvasCommand
    {
        public int Count { get; }
        public Painting.Paint? Paint { get; }

        public SaveLayerCanvasCommand(int count)
        {
            Count = count;
        }

        public SaveLayerCanvasCommand(int count, Painting.Paint paint)
        {
            Count = count;
            Paint = paint;
        }
    }
}
