namespace ShimSkiaSharp.Primitives.CanvasCommands
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
