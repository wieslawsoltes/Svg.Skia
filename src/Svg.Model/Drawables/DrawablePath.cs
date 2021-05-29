using System.Collections.Generic;
using Svg.Model.Primitives;

namespace Svg.Model.Drawables
{
    public abstract class DrawablePath : DrawableBase, IMarkerHost
    {
        public SKPath? Path { get; set; }
        public List<DrawableBase>? MarkerDrawables { get; set; }

        protected DrawablePath(IAssetLoader assetLoader)
            : base(assetLoader)
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

        public override void PostProcess(SKRect? viewport)
        {
            base.PostProcess(viewport);

            if (MarkerDrawables is { })
            {
                foreach (var drawable in MarkerDrawables)
                {
                    drawable.PostProcess(viewport);
                }
            }
        }
    }
}
