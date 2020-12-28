
namespace Svg.Model.Picture.Commands
{
    public sealed class SaveLayerCanvasCommand : CanvasCommand
    {
        public int Count { get; }
        public Paint.Paint? Paint { get; }

        public SaveLayerCanvasCommand(int count)
        {
            Count = count;
        }

        public SaveLayerCanvasCommand(int count, Paint.Paint paint)
        {
            Count = count;
            Paint = paint;
        }
    }
}
