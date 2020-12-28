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

            drawable.TransformedBounds = Rect.Empty;

            drawable.CreateTransformedBounds();

            drawable.Transform = SvgModelExtensions.ToMatrix(svgGroup.Transforms);

            drawable.Fill = null;
            drawable.Stroke = null;

            // TODO: Transform _skBounds using _skMatrix.
            drawable.TransformedBounds = drawable.Transform.MapRect(drawable.TransformedBounds);

            return drawable;
        }
    }
}
