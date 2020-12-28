namespace Svg.Model.Drawables
{
    public sealed class AnchorDrawable : DrawableContainer
    {
        private AnchorDrawable()
            : base()
        {
        }

        public static AnchorDrawable Create(SvgAnchor svgAnchor, Rect skOwnerBounds, DrawableBase? parent, Attributes ignoreAttributes = Attributes.None)
        {
            var drawable = new AnchorDrawable
            {
                Element = svgAnchor,
                Parent = parent,
                IgnoreAttributes = ignoreAttributes,
                IsDrawable = true
            };

            drawable.CreateChildren(svgAnchor, skOwnerBounds, drawable, ignoreAttributes);

            drawable.IsAntialias = SvgModelExtensions.IsAntialias(svgAnchor);

            drawable.TransformedBounds = Rect.Empty;

            drawable.CreateTransformedBounds();

            drawable.Transform = SvgModelExtensions.ToMatrix(svgAnchor.Transforms);

            drawable.Fill = null;
            drawable.Stroke = null;

            // TODO: Transform _skBounds using _skMatrix.
            drawable.TransformedBounds = drawable.Transform.MapRect(drawable.TransformedBounds);

            drawable.ClipPath = null;
            drawable.MaskDrawable = null;
            drawable.Opacity = drawable.IgnoreAttributes.HasFlag(Attributes.Opacity) ? null : SvgModelExtensions.GetOpacityPaint(svgAnchor, drawable.Disposable);
            drawable.Filter = null;

            return drawable;
        }

        public override void PostProcess()
        {
            var element = Element;
            if (element is null)
            {
                return;
            }

            var enableOpacity = !IgnoreAttributes.HasFlag(Attributes.Opacity);

            ClipPath = null;
            MaskDrawable = null;
            Opacity = enableOpacity ? SvgModelExtensions.GetOpacityPaint(element, Disposable) : null;
            Filter = null;

            foreach (var child in ChildrenDrawables)
            {
                child.PostProcess();
            }
        }
    }
}
