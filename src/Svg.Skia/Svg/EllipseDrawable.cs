#if !USE_PICTURE
using SkiaSharp;
using CropRect = SkiaSharp.SKImageFilter.CropRect;
#endif
#if USE_PICTURE
using SKRect = Svg.Picture.Rect;
#endif

namespace Svg.Skia
{
    internal sealed class EllipseDrawable : DrawablePath
    {
        private EllipseDrawable()
            : base()
        {
        }

        public static EllipseDrawable Create(SvgEllipse svgEllipse, SKRect skOwnerBounds, DrawableBase? parent, Attributes ignoreAttributes = Attributes.None)
        {
            var drawable = new EllipseDrawable
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

            drawable.Path = svgEllipse.ToSKPath(svgEllipse.FillRule, skOwnerBounds, drawable.Disposable);
            if (drawable.Path is null || drawable.Path.IsEmpty)
            {
                drawable.IsDrawable = false;
                return drawable;
            }

            drawable.IsAntialias = SvgExtensions.IsAntialias(svgEllipse);
            drawable.TransformedBounds = drawable.Path.Bounds;
            drawable.Transform = SvgExtensions.ToSKMatrix(svgEllipse.Transforms);

            bool canDrawFill = true;
            bool canDrawStroke = true;

            if (SvgExtensions.IsValidFill(svgEllipse))
            {
                drawable.Fill = SvgExtensions.GetFillSKPaint(svgEllipse, drawable.TransformedBounds, ignoreAttributes, drawable.Disposable);
                if (drawable.Fill is null)
                {
                    canDrawFill = false;
                }
            }

            if (SvgExtensions.IsValidStroke(svgEllipse, drawable.TransformedBounds))
            {
                drawable.Stroke = SvgExtensions.GetStrokeSKPaint(svgEllipse, drawable.TransformedBounds, ignoreAttributes, drawable.Disposable);
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
