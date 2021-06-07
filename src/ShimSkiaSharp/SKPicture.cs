using System.Collections.Generic;

namespace ShimSkiaSharp
{
    public record SKPicture(SKRect CullRect, IList<CanvasCommand>? Commands);
}
