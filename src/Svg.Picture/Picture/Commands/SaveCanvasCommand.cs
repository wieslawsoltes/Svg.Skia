namespace Svg.Picture
{
    public class SaveCanvasCommand : CanvasCommand
    {
        public int Count { get; set; }

        public SaveCanvasCommand(int count)
        {
            Count = count;
        }
    }
}
