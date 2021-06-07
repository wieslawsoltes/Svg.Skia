using System.Collections.Generic;

namespace ShimSkiaSharp.Primitives
{
    public record SKPicture(SKRect CullRect, IList<CanvasCommand>? Commands);
}
