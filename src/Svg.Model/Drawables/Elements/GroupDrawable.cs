#if USE_SKIASHARP
using SkiaSharp;
#else
using ShimSkiaSharp.Primitives;
#endif
namespace Svg.Model.Drawables.Elements
{
    public sealed class GroupDrawable : DrawableContainer
    {
        private GroupDrawable(IAssetLoader assetLoader)
            : base(assetLoader)
        {
        }

        public static GroupDrawable Create(SvgGroup svgGroup, SKRect skOwnerBounds, DrawableBase? parent, IAssetLoader assetLoader, DrawAttributes ignoreAttributes = DrawAttributes.None)
        {
            var drawable = new GroupDrawable(assetLoader)
            {
                Element = svgGroup,
                Parent = parent,
                IgnoreAttributes = ignoreAttributes
            };

            drawable.IsDrawable = drawable.CanDraw(svgGroup, drawable.IgnoreAttributes) && drawable.HasFeatures(svgGroup, drawable.IgnoreAttributes);

            // NOTE: Call AddMarkers only once.
            SvgExtensions.AddMarkers(svgGroup);

            drawable.CreateChildren(svgGroup, skOwnerBounds, drawable, assetLoader, ignoreAttributes);

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

            // TODO: Transform _skBounds using _skMatrix.
            drawable.TransformedBounds = drawable.Transform.MapRect(drawable.GeometryBounds);

            if (SvgExtensions.IsValidFill(svgGroup))
            {
                drawable.Fill = SvgExtensions.GetFillPaint(svgGroup, drawable.GeometryBounds, assetLoader, ignoreAttributes);
            }

            if (SvgExtensions.IsValidStroke(svgGroup, drawable.GeometryBounds))
            {
                drawable.Stroke = SvgExtensions.GetStrokePaint(svgGroup, drawable.GeometryBounds, assetLoader, ignoreAttributes);
            }

            return drawable;
        }
    }
}
