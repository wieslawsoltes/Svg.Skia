namespace Svg.Model.Drawables
{
    public sealed class PolylineDrawable : DrawablePath
    {
        private PolylineDrawable()
            : base()
        {
        }

        public static PolylineDrawable Create(SvgPolyline svgPolyline, Rect skOwnerBounds, DrawableBase? parent, Attributes ignoreAttributes = Attributes.None)
        {
            var drawable = new PolylineDrawable
            {
                Element = svgPolyline,
                Parent = parent,
                IgnoreAttributes = ignoreAttributes
            };

            drawable.IsDrawable = drawable.CanDraw(svgPolyline, drawable.IgnoreAttributes) && drawable.HasFeatures(svgPolyline, drawable.IgnoreAttributes);

            if (!drawable.IsDrawable)
            {
                return drawable;
            }

            drawable.Path = svgPolyline.Points?.ToPath(svgPolyline.FillRule, false, skOwnerBounds, drawable.Disposable);
            if (drawable.Path is null || drawable.Path.IsEmpty)
            {
                drawable.IsDrawable = false;
                return drawable;
            }

            drawable.IsAntialias = SvgExtensions.IsAntialias(svgPolyline);

            drawable.TransformedBounds = drawable.Path.Bounds;

            drawable.Transform = SvgExtensions.ToMatrix(svgPolyline.Transforms);

            bool canDrawFill = true;
            bool canDrawStroke = true;

            if (SvgExtensions.IsValidFill(svgPolyline))
            {
                drawable.Fill = SvgExtensions.GetFillPaint(svgPolyline, drawable.TransformedBounds, ignoreAttributes, drawable.Disposable);
                if (drawable.Fill is null)
                {
                    canDrawFill = false;
                }
            }

            if (SvgExtensions.IsValidStroke(svgPolyline, drawable.TransformedBounds))
            {
                drawable.Stroke = SvgExtensions.GetStrokePaint(svgPolyline, drawable.TransformedBounds, ignoreAttributes, drawable.Disposable);
                if (drawable.Stroke is null)
                {
                    canDrawStroke = false;
                }
            }

            if (canDrawFill && !canDrawStroke)
            {
                drawable.IsDrawable = false;
                return drawable;
            }

            SvgExtensions.CreateMarkers(svgPolyline, drawable.Path, skOwnerBounds, ref drawable.MarkerDrawables, drawable.Disposable);

            // TODO: Transform _skBounds using _skMatrix.
            drawable.TransformedBounds = drawable.Transform.MapRect(drawable.TransformedBounds);

            return drawable;
        }
    }
}
