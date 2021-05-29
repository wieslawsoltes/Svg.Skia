using Svg.Model.Primitives;

namespace Svg.Model.Drawables.Elements
{
    public sealed class CircleDrawable : DrawablePath
    {
        private CircleDrawable(IAssetLoader assetLoader)
            : base(assetLoader)
        {
        }

        public static CircleDrawable Create(SvgCircle svgCircle, SKRect skOwnerBounds, DrawableBase? parent, IAssetLoader assetLoader, Attributes ignoreAttributes = Attributes.None)
        {
            var drawable = new CircleDrawable(assetLoader)
            {
                Element = svgCircle,
                Parent = parent,
                IgnoreAttributes = ignoreAttributes
            };

            drawable.IsDrawable = drawable.CanDraw(svgCircle, drawable.IgnoreAttributes) && drawable.HasFeatures(svgCircle, drawable.IgnoreAttributes);

            if (!drawable.IsDrawable)
            {
                return drawable;
            }

            drawable.Path = svgCircle.ToPath(svgCircle.FillRule, skOwnerBounds);
            if (drawable.Path is null || drawable.Path.IsEmpty)
            {
                drawable.IsDrawable = false;
                return drawable;
            }

            drawable.IsAntialias = SvgModelExtensions.IsAntialias(svgCircle);

            drawable.GeometryBounds = drawable.Path.Bounds;
            
            drawable.TransformedBounds = drawable.GeometryBounds;

            drawable.Transform = SvgModelExtensions.ToMatrix(svgCircle.Transforms);

            // TODO: Transform _skBounds using _skMatrix.
            drawable.TransformedBounds = drawable.Transform.MapRect(drawable.TransformedBounds);

            var canDrawFill = true;
            var canDrawStroke = true;

            if (SvgModelExtensions.IsValidFill(svgCircle))
            {
                drawable.Fill = SvgModelExtensions.GetFillPaint(svgCircle, drawable.GeometryBounds, assetLoader, ignoreAttributes);
                if (drawable.Fill is null)
                {
                    canDrawFill = false;
                }
            }

            if (SvgModelExtensions.IsValidStroke(svgCircle, drawable.GeometryBounds))
            {
                drawable.Stroke = SvgModelExtensions.GetStrokePaint(svgCircle, drawable.GeometryBounds, assetLoader, ignoreAttributes);
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
