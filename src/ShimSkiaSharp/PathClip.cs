#if USE_SKIASHARP
using SkiaSharp;
namespace SkiaSharp;
#else
namespace ShimSkiaSharp;
#endif

public class PathClip
{
    public SKPath? Path { get; set; }

    public SKMatrix? Transform { get; set; }

    public ClipPath? Clip { get; set; }
}
