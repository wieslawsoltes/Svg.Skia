using Svg.Model.Primitives;

namespace Svg.Model.Drawables.Elements
{
    public sealed class PathDrawable : DrawablePath
    {
        private PathDrawable(IAssetLoader assetLoader)
            : base(assetLoader)
        {
        }

        public static PathDrawable Create(SvgPath svgPath, SKRect skOwnerBounds, DrawableBase? parent, IAssetLoader assetLoader, Attributes ignoreAttributes = Attributes.None)
        {
            var drawable = new PathDrawable(assetLoader)
            {
                Element = svgPath,
                Parent = parent,
                IgnoreAttributes = ignoreAttributes
            };

            drawable.IsDrawable = drawable.CanDraw(svgPath, drawable.IgnoreAttributes) && drawable.HasFeatures(svgPath, drawable.IgnoreAttributes);

            if (!drawable.IsDrawable)
            {
                return drawable;
            }

            drawable.Path = svgPath.PathData?.ToPath(svgPath.FillRule);
            if (drawable.Path is null || drawable.Path.IsEmpty)
            {
                drawable.IsDrawable = false;
                return drawable;
            }

            drawable.IsAntialias = SvgModelExtensions.IsAntialias(svgPath);

            drawable.GeometryBounds = drawable.Path.Bounds;

            drawable.TransformedBounds = drawable.GeometryBounds;

            drawable.Transform = SvgModelExtensions.ToMatrix(svgPath.Transforms);

            // TODO: Transform _skBounds using _skMatrix.
            drawable.TransformedBounds = drawable.Transform.MapRect(drawable.TransformedBounds);

            var canDrawFill = true;
            var canDrawStroke = true;

            if (SvgModelExtensions.IsValidFill(svgPath))
            {
                drawable.Fill = SvgModelExtensions.GetFillPaint(svgPath, drawable.GeometryBounds, assetLoader, ignoreAttributes);
                if (drawable.Fill is null)
                {
                    canDrawFill = false;
                }
            }

            if (SvgModelExtensions.IsValidStroke(svgPath, drawable.GeometryBounds))
            {
                drawable.Stroke = SvgModelExtensions.GetStrokePaint(svgPath, drawable.GeometryBounds, assetLoader, ignoreAttributes);
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

            SvgModelExtensions.CreateMarkers(svgPath, drawable.Path, skOwnerBounds, drawable, assetLoader);

            return drawable;
        }
    }
}
