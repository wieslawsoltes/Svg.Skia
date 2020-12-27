namespace Svg.Model.Drawables
{
    public sealed class PolygonDrawable : DrawablePath
    {
        private PolygonDrawable()
            : base()
        {
        }

        public static PolygonDrawable Create(SvgPolygon svgPolygon, Rect skOwnerBounds, DrawableBase? parent, Attributes ignoreAttributes = Attributes.None)
        {
            var drawable = new PolygonDrawable
            {
                Element = svgPolygon,
                Parent = parent,
                IgnoreAttributes = ignoreAttributes
            };

            drawable.IsDrawable = drawable.CanDraw(svgPolygon, drawable.IgnoreAttributes) && drawable.HasFeatures(svgPolygon, drawable.IgnoreAttributes);

            if (!drawable.IsDrawable)
            {
                return drawable;
            }

            drawable.Path = svgPolygon.Points?.ToPath(svgPolygon.FillRule, true, skOwnerBounds, drawable.Disposable);
            if (drawable.Path is null || drawable.Path.IsEmpty)
            {
                drawable.IsDrawable = false;
                return drawable;
            }

            drawable.IsAntialias = SvgExtensions.IsAntialias(svgPolygon);

            drawable.TransformedBounds = drawable.Path.Bounds;

            drawable.Transform = SvgExtensions.ToMatrix(svgPolygon.Transforms);

            bool canDrawFill = true;
            bool canDrawStroke = true;

            if (SvgExtensions.IsValidFill(svgPolygon))
            {
                drawable.Fill = SvgExtensions.GetFillPaint(svgPolygon, drawable.TransformedBounds, ignoreAttributes, drawable.Disposable);
                if (drawable.Fill is null)
                {
                    canDrawFill = false;
                }
            }

            if (SvgExtensions.IsValidStroke(svgPolygon, drawable.TransformedBounds))
            {
                drawable.Stroke = SvgExtensions.GetStrokePaint(svgPolygon, drawable.TransformedBounds, ignoreAttributes, drawable.Disposable);
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

            SvgExtensions.CreateMarkers(svgPolygon, drawable.Path, skOwnerBounds, ref drawable.MarkerDrawables, drawable.Disposable);

            // TODO: Transform _skBounds using _skMatrix.
            drawable.TransformedBounds = drawable.Transform.MapRect(drawable.TransformedBounds);

            return drawable;
        }
    }
}
