namespace Svg.Picture
{
    public class SaveLayerCanvasCommand : CanvasCommand
    {
        public int Count;
        public Paint? Paint;

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
