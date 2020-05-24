namespace Svg.Picture
{
    public class RestoreCanvasCommand : CanvasCommand
    {
        public int Count { get; set; }

        public RestoreCanvasCommand(int count)
        {
            Count = count;
        }
    }
}
