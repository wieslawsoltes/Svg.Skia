namespace Svg.Picture
{
    public class SaveLayerCanvasCommand : CanvasCommand
    {
        public int Count { get; set; }
        public Paint? Paint { get; set; }

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
