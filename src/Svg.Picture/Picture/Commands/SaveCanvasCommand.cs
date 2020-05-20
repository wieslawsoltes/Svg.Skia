namespace Svg.Picture
{
    public class SaveCanvasCommand : CanvasCommand
    {
        public int Count;

        public SaveCanvasCommand(int count)
        {
            Count = count;
        }
    }
}
