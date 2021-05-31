using System.Collections.Generic;

namespace ShimSkiaSharp.Primitives
{
    public sealed class SKPicture
    {
        public SKRect CullRect { get; set; }
        public IList<CanvasCommand>? Commands { get; set; }
    }
}
