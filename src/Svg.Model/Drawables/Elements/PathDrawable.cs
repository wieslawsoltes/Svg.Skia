using System;
using System.Collections.Generic;
#if USE_SKIASHARP
using SkiaSharp;
#else
using ShimSkiaSharp.Primitives;
#endif

namespace Svg.Model.Drawables.Elements
{
    public sealed class PathDrawable : DrawablePath
    {
        private PathDrawable(IAssetLoader assetLoader, HashSet<Uri>? references)
            : base(assetLoader, references)
        {
        }

        public static PathDrawable Create(SvgPath svgPath, SKRect skOwnerBounds, DrawableBase? parent, IAssetLoader assetLoader, HashSet<Uri>? references, DrawAttributes ignoreAttributes = DrawAttributes.None)
        {
            var drawable = new PathDrawable(assetLoader, references)
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

            drawable.IsAntialias = SvgExtensions.IsAntialias(svgPath);

            drawable.GeometryBounds = drawable.Path.Bounds;

            drawable.Transform = SvgExtensions.ToMatrix(svgPath.Transforms);

            var canDrawFill = true;
            var canDrawStroke = true;

            if (SvgExtensions.IsValidFill(svgPath))
            {
                drawable.Fill = SvgExtensions.GetFillPaint(svgPath, drawable.GeometryBounds, assetLoader, references, ignoreAttributes);
                if (drawable.Fill is null)
                {
                    canDrawFill = false;
                }
            }

            if (SvgExtensions.IsValidStroke(svgPath, drawable.GeometryBounds))
            {
                drawable.Stroke = SvgExtensions.GetStrokePaint(svgPath, drawable.GeometryBounds, assetLoader, references, ignoreAttributes);
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

            SvgExtensions.CreateMarkers(svgPath, drawable.Path, skOwnerBounds, drawable, assetLoader, references);

            return drawable;
        }
    }
}
