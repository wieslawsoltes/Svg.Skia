using System.Collections.Generic;

namespace Svg.Model
{
    public abstract class DrawablePath : DrawableBase
    {
        public Path? Path;

        public List<DrawableBase>? MarkerDrawables;

        protected DrawablePath()
            : base()
        {
        }

        public override void OnDraw(Canvas canvas, Attributes ignoreAttributes, DrawableBase? until)
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
