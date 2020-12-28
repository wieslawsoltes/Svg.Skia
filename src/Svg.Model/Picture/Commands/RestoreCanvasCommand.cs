
namespace Svg.Model.Picture.Commands
{
    public sealed class RestoreCanvasCommand : CanvasCommand
    {
        public int Count { get; }

        public RestoreCanvasCommand(int count)
        {
            Count = count;
        }
    }
}
