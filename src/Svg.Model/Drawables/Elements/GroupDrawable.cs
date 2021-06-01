using System;
using System.Collections.Generic;
#if USE_SKIASHARP
using SkiaSharp;
#else
using ShimSkiaSharp.Primitives;
#endif
namespace Svg.Model.Drawables.Elements
{
    public sealed class GroupDrawable : DrawableContainer
    {
        private GroupDrawable(IAssetLoader assetLoader, HashSet<Uri>? references)
            : base(assetLoader, references)
        {
        }

        public static GroupDrawable Create(SvgGroup svgGroup, SKRect skOwnerBounds, DrawableBase? parent, IAssetLoader assetLoader, HashSet<Uri>? references, DrawAttributes ignoreAttributes = DrawAttributes.None)
        {
            var drawable = new GroupDrawable(assetLoader, references)
            {
                Element = svgGroup,
                Parent = parent,
                IgnoreAttributes = ignoreAttributes
            };

            drawable.IsDrawable = drawable.CanDraw(svgGroup, drawable.IgnoreAttributes) && drawable.HasFeatures(svgGroup, drawable.IgnoreAttributes);

            // NOTE: Call AddMarkers only once.
            SvgExtensions.AddMarkers(svgGroup);

            drawable.CreateChildren(svgGroup, skOwnerBounds, drawable, assetLoader, references, ignoreAttributes);

            // TODO: Check if children are explicitly set to be visible.
            //foreach (var child in drawable.ChildrenDrawables)
            //{
            //    if (child.IsDrawable)
            //    {
            //        IsDrawable = true;
            //        break;
            //    }
            //}

            if (!drawable.IsDrawable)
            {
                return drawable;
            }

            drawable.IsAntialias = SvgExtensions.IsAntialias(svgGroup);

            drawable.GeometryBounds = SKRect.Empty;

            drawable.CreateGeometryBounds();

            drawable.Transform = SvgExtensions.ToMatrix(svgGroup.Transforms);

            if (SvgExtensions.IsValidFill(svgGroup))
            {
                drawable.Fill = SvgExtensions.GetFillPaint(svgGroup, drawable.GeometryBounds, assetLoader, references, ignoreAttributes);
            }

            if (SvgExtensions.IsValidStroke(svgGroup, drawable.GeometryBounds))
            {
                drawable.Stroke = SvgExtensions.GetStrokePaint(svgGroup, drawable.GeometryBounds, assetLoader, references, ignoreAttributes);
            }

            return drawable;
        }
    }
}
