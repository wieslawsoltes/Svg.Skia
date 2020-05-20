namespace Svg.Picture
{
    public class RestoreCanvasCommand : CanvasCommand
    {
        public int Count;

        public RestoreCanvasCommand(int count)
        {
            Count = count;
        }
    }
}
