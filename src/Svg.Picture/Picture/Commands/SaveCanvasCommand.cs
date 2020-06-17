
namespace Svg.Picture
{
    public sealed class SaveCanvasCommand : CanvasCommand
    {
        public int Count { get; }

        public SaveCanvasCommand(int count)
        {
            Count = count;
        }
    }
}
