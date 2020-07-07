using System.Collections.Generic;
#if !USE_PICTURE
using SkiaSharp;
using CropRect = SkiaSharp.SKImageFilter.CropRect;
#endif
#if USE_PICTURE
using SKCanvas = Svg.Picture.Canvas;
using SKPath = Svg.Picture.Path;
#endif

namespace Svg.Skia
{
    internal abstract class DrawablePath : DrawableBase
    {
        public SKPath? Path;

        public List<DrawableBase>? MarkerDrawables;

        protected DrawablePath()
            : base()
        {
        }

        public override void OnDraw(SKCanvas canvas, Attributes ignoreAttributes, DrawableBase? until)
        {
            if (until != null && this == until)
            {
                return;
            }

            if (Fill != null && Path != null)
            {
                canvas.DrawPath(Path, Fill);
            }

            if (Stroke != null && Path != null)
            {
                canvas.DrawPath(Path, Stroke);
            }

            if (MarkerDrawables != null)
            {
                foreach (var drawable in MarkerDrawables)
                {
                    drawable.Draw(canvas, ignoreAttributes, until);
                }
            }
        }

        public override void PostProcess()
        {
            base.PostProcess();

            if (MarkerDrawables != null)
            {
                foreach (var drawable in MarkerDrawables)
                {
                    drawable.PostProcess();
                }
            }
        }
    }
}
