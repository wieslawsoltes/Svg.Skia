using ShimSkiaSharp.Primitives;

namespace Svg.Model.Drawables.Elements
{
    public sealed class PolylineDrawable : DrawablePath
    {
        private PolylineDrawable(IAssetLoader assetLoader)
            : base(assetLoader)
        {
        }

        public static PolylineDrawable Create(SvgPolyline svgPolyline, SKRect skOwnerBounds, DrawableBase? parent, IAssetLoader assetLoader, DrawAttributes ignoreAttributes = DrawAttributes.None)
        {
            var drawable = new PolylineDrawable(assetLoader)
            {
                Element = svgPolyline,
                Parent = parent,
                IgnoreAttributes = ignoreAttributes
            };

            drawable.IsDrawable = drawable.CanDraw(svgPolyline, drawable.IgnoreAttributes) && drawable.HasFeatures(svgPolyline, drawable.IgnoreAttributes);

            if (!drawable.IsDrawable)
            {
                return drawable;
            }

            drawable.Path = svgPolyline.Points?.ToPath(svgPolyline.FillRule, false, skOwnerBounds);
            if (drawable.Path is null || drawable.Path.IsEmpty)
            {
                drawable.IsDrawable = false;
                return drawable;
            }

            drawable.IsAntialias = SvgExtensions.IsAntialias(svgPolyline);

            drawable.GeometryBounds = drawable.Path.Bounds;

            drawable.TransformedBounds = drawable.GeometryBounds;

            drawable.Transform = SvgExtensions.ToMatrix(svgPolyline.Transforms);

            // TODO: Transform _skBounds using _skMatrix.
            drawable.TransformedBounds = drawable.Transform.MapRect(drawable.TransformedBounds);

            var canDrawFill = true;
            var canDrawStroke = true;

            if (SvgExtensions.IsValidFill(svgPolyline))
            {
                drawable.Fill = SvgExtensions.GetFillPaint(svgPolyline, drawable.GeometryBounds, assetLoader, ignoreAttributes);
                if (drawable.Fill is null)
                {
                    canDrawFill = false;
                }
            }

            if (SvgExtensions.IsValidStroke(svgPolyline, drawable.GeometryBounds))
            {
                drawable.Stroke = SvgExtensions.GetStrokePaint(svgPolyline, drawable.GeometryBounds, assetLoader, ignoreAttributes);
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

            SvgExtensions.CreateMarkers(svgPolyline, drawable.Path, skOwnerBounds, drawable, assetLoader);

            return drawable;
        }
    }
}
