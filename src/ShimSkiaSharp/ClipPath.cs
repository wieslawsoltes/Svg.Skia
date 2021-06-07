using System.Collections.Generic;
#if USE_SKIASHARP
using SkiaSharp;
namespace SkiaSharp
#else
namespace ShimSkiaSharp
#endif
{
    public class ClipPath
    {
        public IList<PathClip>? Clips { get; set; }

        public SKMatrix? Transform { get; set; }

        public ClipPath? Clip { get; set; }

        public bool IsEmpty => Clips is null || Clips.Count == 0;

        public ClipPath()
        {
            Clips = new List<PathClip>();
        }
    }
}
