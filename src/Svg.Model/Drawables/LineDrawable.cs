namespace Svg.Model.Drawables
{
    public sealed class LineDrawable : DrawablePath
    {
        private LineDrawable()
            : base()
        {
        }

        public static LineDrawable Create(SvgLine svgLine, Rect skOwnerBounds, DrawableBase? parent, Attributes ignoreAttributes = Attributes.None)
        {
            var drawable = new LineDrawable
            {
                Element = svgLine,
                Parent = parent,
                IgnoreAttributes = ignoreAttributes
            };

            drawable.IsDrawable = drawable.CanDraw(svgLine, drawable.IgnoreAttributes) && drawable.HasFeatures(svgLine, drawable.IgnoreAttributes);

            if (!drawable.IsDrawable)
            {
                return drawable;
            }

            drawable.Path = svgLine.ToPath(svgLine.FillRule, skOwnerBounds, drawable.Disposable);
            if (drawable.Path is null || drawable.Path.IsEmpty)
            {
                drawable.IsDrawable = false;
                return drawable;
            }

            drawable.IsAntialias = SvgModelExtensions.IsAntialias(svgLine);

            drawable.TransformedBounds = drawable.Path.Bounds;

            drawable.Transform = SvgModelExtensions.ToMatrix(svgLine.Transforms);

            bool canDrawFill = true;
            bool canDrawStroke = true;

            if (SvgModelExtensions.IsValidFill(svgLine))
            {
                drawable.Fill = SvgModelExtensions.GetFillPaint(svgLine, drawable.TransformedBounds, ignoreAttributes, drawable.Disposable);
                if (drawable.Fill is null)
                {
                    canDrawFill = false;
                }
            }

            if (SvgModelExtensions.IsValidStroke(svgLine, drawable.TransformedBounds))
            {
                drawable.Stroke = SvgModelExtensions.GetStrokePaint(svgLine, drawable.TransformedBounds, ignoreAttributes, drawable.Disposable);
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

            SvgModelExtensions.CreateMarkers(svgLine, drawable.Path, skOwnerBounds, ref drawable.MarkerDrawables, drawable.Disposable);

            // TODO: Transform _skBounds using _skMatrix.
            drawable.TransformedBounds = drawable.Transform.MapRect(drawable.TransformedBounds);

            return drawable;
        }
    }
}
