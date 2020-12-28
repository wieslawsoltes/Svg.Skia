namespace Svg.Model.Drawables
{
    public sealed class RectangleDrawable : DrawablePath
    {
        private RectangleDrawable()
            : base()
        {
        }

        public static RectangleDrawable Create(SvgRectangle svgRectangle, Rect skOwnerBounds, DrawableBase? parent, Attributes ignoreAttributes = Attributes.None)
        {
            var drawable = new RectangleDrawable
            {
                Element = svgRectangle,
                Parent = parent,
                IgnoreAttributes = ignoreAttributes
            };

            drawable.IsDrawable = drawable.CanDraw(svgRectangle, drawable.IgnoreAttributes) && drawable.HasFeatures(svgRectangle, drawable.IgnoreAttributes);

            if (!drawable.IsDrawable)
            {
                return drawable;
            }

            drawable.Path = svgRectangle.ToPath(svgRectangle.FillRule, skOwnerBounds, drawable.Disposable);
            if (drawable.Path is null || drawable.Path.IsEmpty)
            {
                drawable.IsDrawable = false;
                return drawable;
            }

            drawable.IsAntialias = SvgModelExtensions.IsAntialias(svgRectangle);

            drawable.TransformedBounds = drawable.Path.Bounds;

            drawable.Transform = SvgModelExtensions.ToMatrix(svgRectangle.Transforms);

            bool canDrawFill = true;
            bool canDrawStroke = true;

            if (SvgModelExtensions.IsValidFill(svgRectangle))
            {
                drawable.Fill = SvgModelExtensions.GetFillPaint(svgRectangle, drawable.TransformedBounds, ignoreAttributes, drawable.Disposable);
                if (drawable.Fill is null)
                {
                    canDrawFill = false;
                }
            }

            if (SvgModelExtensions.IsValidStroke(svgRectangle, drawable.TransformedBounds))
            {
                drawable.Stroke = SvgModelExtensions.GetStrokePaint(svgRectangle, drawable.TransformedBounds, ignoreAttributes, drawable.Disposable);
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

            // TODO: Transform _skBounds using _skMatrix.
            drawable.TransformedBounds = drawable.Transform.MapRect(drawable.TransformedBounds);

            return drawable;
        }
    }
}
