using Svg.Model.Primitives;

namespace Svg.Model.Drawables.Elements
{
    public sealed class LineDrawable : DrawablePath
    {
        private LineDrawable(IAssetLoader assetLoader)
            : base(assetLoader)
        {
        }

        public static LineDrawable Create(SvgLine svgLine, SKRect skOwnerBounds, DrawableBase? parent, IAssetLoader assetLoader, DrawAttributes ignoreAttributes = DrawAttributes.None)
        {
            var drawable = new LineDrawable(assetLoader)
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

            drawable.Path = svgLine.ToPath(svgLine.FillRule, skOwnerBounds);
            if (drawable.Path is null || drawable.Path.IsEmpty)
            {
                drawable.IsDrawable = false;
                return drawable;
            }

            drawable.IsAntialias = SvgModelExtensions.IsAntialias(svgLine);

            drawable.GeometryBounds = drawable.Path.Bounds;

            drawable.TransformedBounds = drawable.GeometryBounds;

            drawable.Transform = SvgModelExtensions.ToMatrix(svgLine.Transforms);

            // TODO: Transform _skBounds using _skMatrix.
            drawable.TransformedBounds = drawable.Transform.MapRect(drawable.TransformedBounds);

            var canDrawFill = true;
            var canDrawStroke = true;

            if (SvgModelExtensions.IsValidFill(svgLine))
            {
                drawable.Fill = SvgModelExtensions.GetFillPaint(svgLine, drawable.GeometryBounds, assetLoader, ignoreAttributes);
                if (drawable.Fill is null)
                {
                    canDrawFill = false;
                }
            }

            if (SvgModelExtensions.IsValidStroke(svgLine, drawable.GeometryBounds))
            {
                drawable.Stroke = SvgModelExtensions.GetStrokePaint(svgLine, drawable.GeometryBounds, assetLoader, ignoreAttributes);
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

            SvgModelExtensions.CreateMarkers(svgLine, drawable.Path, skOwnerBounds, drawable, assetLoader);

            return drawable;
        }
    }
}
