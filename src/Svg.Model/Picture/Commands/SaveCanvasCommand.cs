namespace Svg.Model
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
