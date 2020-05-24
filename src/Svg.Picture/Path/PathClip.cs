using System;

namespace Svg.Picture
{
    public class PathClip : IDisposable
    {
        public Path? Path;
        public Matrix? Transform;
        public ClipPath? Clip;

        public void Dispose()
        {
        }
    }
}
