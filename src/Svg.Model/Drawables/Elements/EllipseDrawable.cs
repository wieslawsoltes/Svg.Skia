using System;
using System.Collections.Generic;
#if USE_SKIASHARP
using SkiaSharp;
#else
using ShimSkiaSharp.Primitives;
#endif

namespace Svg.Model.Drawables.Elements
{
    public sealed class EllipseDrawable : DrawablePath
    {
        private EllipseDrawable(IAssetLoader assetLoader, HashSet<Uri>? references)
            : base(assetLoader, references)
        {
        }

        public static EllipseDrawable Create(SvgEllipse svgEllipse, SKRect skOwnerBounds, DrawableBase? parent, IAssetLoader assetLoader, HashSet<Uri>? references, DrawAttributes ignoreAttributes = DrawAttributes.None)
        {
            var drawable = new EllipseDrawable(assetLoader, references)
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

            drawable.IsAntialias = SvgExtensions.IsAntialias(svgEllipse);
            drawable.Transform = SvgExtensions.ToMatrix(svgEllipse.Transforms);

            drawable.GeometryBounds = drawable.Path.Bounds;

            var canDrawFill = true;
            var canDrawStroke = true;

            if (SvgExtensions.IsValidFill(svgEllipse))
            {
                drawable.Fill = SvgExtensions.GetFillPaint(svgEllipse, drawable.GeometryBounds, assetLoader, references, ignoreAttributes);
                if (drawable.Fill is null)
                {
                    canDrawFill = false;
                }
            }

            if (SvgExtensions.IsValidStroke(svgEllipse, drawable.GeometryBounds))
            {
                drawable.Stroke = SvgExtensions.GetStrokePaint(svgEllipse, drawable.GeometryBounds, assetLoader, references, ignoreAttributes);
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
