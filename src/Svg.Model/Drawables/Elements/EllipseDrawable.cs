using Svg.Model.Primitives;

namespace Svg.Model.Drawables.Elements
{
    public sealed class EllipseDrawable : DrawablePath
    {
        private EllipseDrawable(IAssetLoader assetLoader)
            : base(assetLoader)
        {
        }

        public static EllipseDrawable Create(SvgEllipse svgEllipse, SKRect skOwnerBounds, DrawableBase? parent, IAssetLoader assetLoader, DrawAttributes ignoreAttributes = DrawAttributes.None)
        {
            var drawable = new EllipseDrawable(assetLoader)
            {
                Element = svgEllipse,
                Parent = parent,
                IgnoreAttributes = ignoreAttributes
            };

            drawable.IsDrawable = drawable.CanDraw(svgEllipse, drawable.IgnoreAttributes) && drawable.HasFeatures(svgEllipse, drawable.IgnoreAttributes);

            if (!drawable.IsDrawable)
            {
                return drawable;
            }

            drawable.Path = svgEllipse.ToPath(svgEllipse.FillRule, skOwnerBounds);
            if (drawable.Path is null || drawable.Path.IsEmpty)
            {
                drawable.IsDrawable = false;
                return drawable;
            }

            drawable.IsAntialias = SvgModelExtensions.IsAntialias(svgEllipse);
            drawable.Transform = SvgModelExtensions.ToMatrix(svgEllipse.Transforms);

            drawable.GeometryBounds = drawable.Path.Bounds;

            drawable.TransformedBounds = drawable.GeometryBounds;

            // TODO: Transform _skBounds using _skMatrix.
            drawable.TransformedBounds = drawable.Transform.MapRect(drawable.TransformedBounds);

            var canDrawFill = true;
            var canDrawStroke = true;

            if (SvgModelExtensions.IsValidFill(svgEllipse))
            {
                drawable.Fill = SvgModelExtensions.GetFillPaint(svgEllipse, drawable.GeometryBounds, assetLoader, ignoreAttributes);
                if (drawable.Fill is null)
                {
                    canDrawFill = false;
                }
            }

            if (SvgModelExtensions.IsValidStroke(svgEllipse, drawable.GeometryBounds))
            {
                drawable.Stroke = SvgModelExtensions.GetStrokePaint(svgEllipse, drawable.GeometryBounds, assetLoader, ignoreAttributes);
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
