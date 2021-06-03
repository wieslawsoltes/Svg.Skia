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

            drawable.Initialize(skOwnerBounds, references);
            
            return drawable;
        }

        private void Initialize(SKRect skOwnerBounds, HashSet<Uri>? references)
        {
            if (Element is not SvgPolyline svgPolyline || Path is null)
            {
                return;
            }

            IsAntialias = SvgExtensions.IsAntialias(svgPolyline);

            GeometryBounds = Path.Bounds;

            Transform = SvgExtensions.ToMatrix(svgPolyline.Transforms);

            var canDrawFill = true;
            var canDrawStroke = true;

            if (SvgExtensions.IsValidFill(svgPolyline))
            {
                Fill = SvgExtensions.GetFillPaint(svgPolyline, GeometryBounds, AssetLoader, references, IgnoreAttributes);
                if (Fill is null)
                {
                    canDrawFill = false;
                }
            }

            if (SvgExtensions.IsValidStroke(svgPolyline, GeometryBounds))
            {
                Stroke = SvgExtensions.GetStrokePaint(svgPolyline, GeometryBounds, AssetLoader, references, IgnoreAttributes);
                if (Stroke is null)
                {
                    canDrawStroke = false;
                }
            }

            if (canDrawFill && !canDrawStroke)
            {
                IsDrawable = false;
                return;
            }

            SvgExtensions.CreateMarkers(svgPolyline, Path, skOwnerBounds, this, AssetLoader, references);
        }
    }
}
