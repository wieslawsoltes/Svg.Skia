using System;
using System.Collections.Generic;
#if USE_SKIASHARP
using SkiaSharp;
#else
using ShimSkiaSharp.Primitives;
#endif

namespace Svg.Model.Drawables.Elements
{
    public sealed class PolylineDrawable : DrawablePath
    {
        private PolylineDrawable(IAssetLoader assetLoader, HashSet<Uri>? references)
            : base(assetLoader, references)
        {
        }

        public static PolylineDrawable Create(SvgPolyline svgPolyline, SKRect skOwnerBounds, DrawableBase? parent, IAssetLoader assetLoader, HashSet<Uri>? references, DrawAttributes ignoreAttributes = DrawAttributes.None)
        {
            var drawable = new PolylineDrawable(assetLoader, references)
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

            drawable.Transform = SvgExtensions.ToMatrix(svgPolyline.Transforms);

            var canDrawFill = true;
            var canDrawStroke = true;

            if (SvgExtensions.IsValidFill(svgPolyline))
            {
                drawable.Fill = SvgExtensions.GetFillPaint(svgPolyline, drawable.GeometryBounds, assetLoader, references, ignoreAttributes);
                if (drawable.Fill is null)
                {
                    canDrawFill = false;
                }
            }

            if (SvgExtensions.IsValidStroke(svgPolyline, drawable.GeometryBounds))
            {
                drawable.Stroke = SvgExtensions.GetStrokePaint(svgPolyline, drawable.GeometryBounds, assetLoader, references, ignoreAttributes);
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

            SvgExtensions.CreateMarkers(svgPolyline, drawable.Path, skOwnerBounds, drawable, assetLoader, references);

            return drawable;
        }
    }
}
