using ShimSkiaSharp.Primitives;

namespace Svg.Model.Drawables.Elements
{
    public sealed class PolygonDrawable : DrawablePath
    {
        private PolygonDrawable(IAssetLoader assetLoader)
            : base(assetLoader)
        {
        }

        public static PolygonDrawable Create(SvgPolygon svgPolygon, SKRect skOwnerBounds, DrawableBase? parent, IAssetLoader assetLoader, DrawAttributes ignoreAttributes = DrawAttributes.None)
        {
            var drawable = new PolygonDrawable(assetLoader)
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

            drawable.Path = svgPolygon.Points?.ToPath(svgPolygon.FillRule, true, skOwnerBounds);
            if (drawable.Path is null || drawable.Path.IsEmpty)
            {
                drawable.IsDrawable = false;
                return drawable;
            }

            drawable.IsAntialias = SvgExtensions.IsAntialias(svgPolygon);

            drawable.GeometryBounds = drawable.Path.Bounds;

            drawable.TransformedBounds = drawable.GeometryBounds;

            drawable.Transform = SvgExtensions.ToMatrix(svgPolygon.Transforms);

            // TODO: Transform _skBounds using _skMatrix.
            drawable.TransformedBounds = drawable.Transform.MapRect(drawable.TransformedBounds);

            var canDrawFill = true;
            var canDrawStroke = true;

            if (SvgExtensions.IsValidFill(svgPolygon))
            {
                drawable.Fill = SvgExtensions.GetFillPaint(svgPolygon, drawable.GeometryBounds, assetLoader, ignoreAttributes);
                if (drawable.Fill is null)
                {
                    canDrawFill = false;
                }
            }

            if (SvgExtensions.IsValidStroke(svgPolygon, drawable.GeometryBounds))
            {
                drawable.Stroke = SvgExtensions.GetStrokePaint(svgPolygon, drawable.GeometryBounds, assetLoader, ignoreAttributes);
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

            SvgExtensions.CreateMarkers(svgPolygon, drawable.Path, skOwnerBounds, drawable, assetLoader);

            return drawable;
        }
    }
}
