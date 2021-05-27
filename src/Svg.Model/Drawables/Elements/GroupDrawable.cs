using Svg.Model.Primitives;

namespace Svg.Model.Drawables.Elements
{
    public sealed class GroupDrawable : DrawableContainer
    {
        private GroupDrawable(IAssetLoader assetLoader)
            : base(assetLoader)
        {
        }

        public static GroupDrawable Create(SvgGroup svgGroup, Rect skOwnerBounds, DrawableBase? parent, IAssetLoader assetLoader, Attributes ignoreAttributes = Attributes.None)
        {
            var drawable = new GroupDrawable(assetLoader)
            {
                Element = svgGroup,
                Parent = parent,
                IgnoreAttributes = ignoreAttributes
            };

            drawable.IsDrawable = drawable.CanDraw(svgGroup, drawable.IgnoreAttributes) && drawable.HasFeatures(svgGroup, drawable.IgnoreAttributes);

            // NOTE: Call AddMarkers only once.
            SvgModelExtensions.AddMarkers(svgGroup);

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

            drawable.IsAntialias = SvgModelExtensions.IsAntialias(svgGroup);

            var skBounds = Rect.Empty;

            drawable.TransformedBounds = skBounds;

            drawable.CreateTransformedBounds();

            skBounds = drawable.TransformedBounds;

            drawable.Transform = SvgModelExtensions.ToMatrix(svgGroup.Transforms);

            // TODO: Transform _skBounds using _skMatrix.
            drawable.TransformedBounds = drawable.Transform.MapRect(drawable.TransformedBounds);

            if (SvgModelExtensions.IsValidFill(svgGroup))
            {
                drawable.Fill = SvgModelExtensions.GetFillPaint(svgGroup, skBounds, assetLoader, ignoreAttributes);
            }

            if (SvgModelExtensions.IsValidStroke(svgGroup, skBounds))
            {
                drawable.Stroke = SvgModelExtensions.GetStrokePaint(svgGroup, skBounds, assetLoader, ignoreAttributes);
            }

            return drawable;
        }
    }
}
