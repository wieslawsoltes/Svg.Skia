using System;
using System.Collections.Generic;
#if USE_SKIASHARP
using SkiaSharp;
#else
using ShimSkiaSharp;
#endif

namespace Svg.Model.Drawables
{
    public abstract class DrawablePath : DrawableBase, IMarkerHost
    {
        public SKPath? Path { get; set; }
        public List<DrawableBase>? MarkerDrawables { get; set; }

        protected DrawablePath(IAssetLoader assetLoader, HashSet<Uri>? references)
            : base(assetLoader, references)
        {
        }

        void IMarkerHost.AddMarker(DrawableBase drawable)
        {
            MarkerDrawables ??= new List<DrawableBase>();
            MarkerDrawables.Add(drawable);
        }

        public override void OnDraw(SKCanvas canvas, DrawAttributes ignoreAttributes, DrawableBase? until)
        {
            if (until is { } && this == until)
            {
                return;
            }

            if (Fill is { } && Path is { })
            {
                canvas.DrawPath(Path, Fill);
            }

            if (Stroke is { } && Path is { })
            {
                canvas.DrawPath(Path, Stroke);
            }

            if (MarkerDrawables is { })
            {
                foreach (var drawable in MarkerDrawables)
                {
                    drawable.Draw(canvas, ignoreAttributes, until, true);
                }
            }
        }

        public override void PostProcess(SKRect? viewport, SKMatrix totalMatrix)
        {
            base.PostProcess(viewport, totalMatrix);

            if (MarkerDrawables is { })
            {
                foreach (var drawable in MarkerDrawables)
                {
                    drawable.PostProcess(viewport, TotalTransform);
                }
            }
        }
    }
}
