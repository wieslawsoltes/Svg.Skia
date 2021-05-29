
namespace Svg.Model.Primitives.CanvasCommands
{
    public sealed class SetMatrixCanvasCommand : CanvasCommand
    {
        public SKMatrix Matrix { get; }

        public SetMatrixCanvasCommand(SKMatrix matrix)
        {
            Matrix = matrix;
        }
    }
}
