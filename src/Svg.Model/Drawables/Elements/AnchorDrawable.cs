using Svg.Model.Primitives;

namespace Svg.Model.Drawables.Elements
{
    public sealed class AnchorDrawable : DrawableContainer
    {
        private AnchorDrawable(IAssetLoader assetLoader)
            : base(assetLoader)
        {
        }

        public static AnchorDrawable Create(SvgAnchor svgAnchor, Rect skOwnerBounds, DrawableBase? parent, IAssetLoader assetLoader, Attributes ignoreAttributes = Attributes.None)
        {
            var drawable = new AnchorDrawable(assetLoader)
            {
                Element = svgAnchor,
                Parent = parent,
                IgnoreAttributes = ignoreAttributes,
                IsDrawable = true
            };

            drawable.CreateChildren(svgAnchor, skOwnerBounds, drawable, assetLoader, ignoreAttributes);

            drawable.IsAntialias = SvgModelExtensions.IsAntialias(svgAnchor);

            var skBounds = Rect.Empty;
            
            drawable.TransformedBounds = skBounds;

            drawable.CreateTransformedBounds();

            skBounds = drawable.TransformedBounds;

            // TODO: Transform _skBounds using _skMatrix.
            drawable.TransformedBounds = drawable.Transform.MapRect(drawable.TransformedBounds);

            drawable.Transform = SvgModelExtensions.ToMatrix(svgAnchor.Transforms);

            drawable.Fill = null;
            drawable.Stroke = null;

            drawable.ClipPath = null;
            drawable.MaskDrawable = null;
            drawable.Opacity = drawable.IgnoreAttributes.HasFlag(Attributes.Opacity) ? null : SvgModelExtensions.GetOpacityPaint(svgAnchor);
            drawable.Filter = null;

            return drawable;
        }

        public override void PostProcess(Rect? viewport)
        {
            var element = Element;
            if (element is null)
            {
                return;
            }

            var enableOpacity = !IgnoreAttributes.HasFlag(Attributes.Opacity);

            ClipPath = null;
            MaskDrawable = null;
            Opacity = enableOpacity ? SvgModelExtensions.GetOpacityPaint(element) : null;
            Filter = null;

            foreach (var child in ChildrenDrawables)
            {
                child.PostProcess(viewport);
            }
        }
    }
}
