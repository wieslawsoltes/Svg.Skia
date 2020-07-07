#if !USE_PICTURE
using SkiaSharp;
using CropRect = SkiaSharp.SKImageFilter.CropRect;
#endif
#if USE_PICTURE
using SKRect = Svg.Picture.Rect;
#endif

namespace Svg.Skia
{
    internal sealed class PathDrawable : DrawablePath
    {
        private PathDrawable()
            : base()
        {
        }

        public static PathDrawable Create(SvgPath svgPath, SKRect skOwnerBounds, DrawableBase? parent, Attributes ignoreAttributes = Attributes.None)
        {
            var drawable = new PathDrawable
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

            drawable.Path = svgPath.PathData?.ToSKPath(svgPath.FillRule, drawable.Disposable);
            if (drawable.Path is null || drawable.Path.IsEmpty)
            {
                drawable.IsDrawable = false;
                return drawable;
            }

            drawable.IsAntialias = SvgExtensions.IsAntialias(svgPath);

            drawable.TransformedBounds = drawable.Path.Bounds;

            drawable.Transform = SvgExtensions.ToSKMatrix(svgPath.Transforms);

            bool canDrawFill = true;
            bool canDrawStroke = true;

            if (SvgExtensions.IsValidFill(svgPath))
            {
                drawable.Fill = SvgExtensions.GetFillSKPaint(svgPath, drawable.TransformedBounds, ignoreAttributes, drawable.Disposable);
                if (drawable.Fill is null)
                {
                    canDrawFill = false;
                }
            }

            if (SvgExtensions.IsValidStroke(svgPath, drawable.TransformedBounds))
            {
                drawable.Stroke = SvgExtensions.GetStrokeSKPaint(svgPath, drawable.TransformedBounds, ignoreAttributes, drawable.Disposable);
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

            SvgExtensions.CreateMarkers(svgPath, drawable.Path, skOwnerBounds, ref drawable.MarkerDrawables, drawable.Disposable);

            // TODO: Transform _skBounds using _skMatrix.
            drawable.TransformedBounds = drawable.Transform.MapRect(drawable.TransformedBounds);

            return drawable;
        }
    }
}
