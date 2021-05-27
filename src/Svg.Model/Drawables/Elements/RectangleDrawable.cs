using Svg.Model.Primitives;

namespace Svg.Model.Drawables.Elements
{
    public sealed class RectangleDrawable : DrawablePath
    {
        private RectangleDrawable(IAssetLoader assetLoader)
            : base(assetLoader)
        {
        }

        public static RectangleDrawable Create(SvgRectangle svgRectangle, Rect skOwnerBounds, DrawableBase? parent, IAssetLoader assetLoader, Attributes ignoreAttributes = Attributes.None)
        {
            var drawable = new RectangleDrawable(assetLoader)
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

            drawable.Path = svgRectangle.ToPath(svgRectangle.FillRule, skOwnerBounds);
            if (drawable.Path is null || drawable.Path.IsEmpty)
            {
                drawable.IsDrawable = false;
                return drawable;
            }

            drawable.IsAntialias = SvgModelExtensions.IsAntialias(svgRectangle);

            var skBounds = drawable.Path.Bounds;

            drawable.TransformedBounds = skBounds;

            drawable.Transform = SvgModelExtensions.ToMatrix(svgRectangle.Transforms);

            // TODO: Transform _skBounds using _skMatrix.
            drawable.TransformedBounds = drawable.Transform.MapRect(drawable.TransformedBounds);

            var canDrawFill = true;
            var canDrawStroke = true;

            if (SvgModelExtensions.IsValidFill(svgRectangle))
            {
                drawable.Fill = SvgModelExtensions.GetFillPaint(svgRectangle, skBounds, assetLoader, ignoreAttributes);
                if (drawable.Fill is null)
                {
                    canDrawFill = false;
                }
            }

            if (SvgModelExtensions.IsValidStroke(svgRectangle, skBounds))
            {
                drawable.Stroke = SvgModelExtensions.GetStrokePaint(svgRectangle, skBounds, assetLoader, ignoreAttributes);
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

            return drawable;
        }
    }
}
