using Svg.Model.Primitives;

namespace Svg.Model.Drawables.Elements
{
    public sealed class EllipseDrawable : DrawablePath
    {
        private EllipseDrawable(IAssetLoader assetLoader)
            : base(assetLoader)
        {
        }

        public static EllipseDrawable Create(SvgEllipse svgEllipse, Rect skOwnerBounds, DrawableBase? parent, IAssetLoader assetLoader, Attributes ignoreAttributes = Attributes.None)
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
            drawable.TransformedBounds = drawable.Path.Bounds;
            drawable.Transform = SvgModelExtensions.ToMatrix(svgEllipse.Transforms);

            var canDrawFill = true;
            var canDrawStroke = true;

            if (SvgModelExtensions.IsValidFill(svgEllipse))
            {
                drawable.Fill = SvgModelExtensions.GetFillPaint(svgEllipse, drawable.TransformedBounds, assetLoader, ignoreAttributes);
                if (drawable.Fill is null)
                {
                    canDrawFill = false;
                }
            }

            if (SvgModelExtensions.IsValidStroke(svgEllipse, drawable.TransformedBounds))
            {
                drawable.Stroke = SvgModelExtensions.GetStrokePaint(svgEllipse, drawable.TransformedBounds, assetLoader, ignoreAttributes);
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
