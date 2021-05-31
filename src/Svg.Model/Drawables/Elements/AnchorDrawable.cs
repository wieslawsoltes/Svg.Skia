#if USE_SKIASHARP
using SkiaSharp;
#else
using ShimSkiaSharp.Primitives;
#endif

namespace Svg.Model.Drawables.Elements
{
    public sealed class AnchorDrawable : DrawableContainer
    {
        private AnchorDrawable(IAssetLoader assetLoader)
            : base(assetLoader)
        {
        }

        public static AnchorDrawable Create(SvgAnchor svgAnchor, SKRect skOwnerBounds, DrawableBase? parent, IAssetLoader assetLoader, DrawAttributes ignoreAttributes = DrawAttributes.None)
        {
            var drawable = new AnchorDrawable(assetLoader)
            {
                Element = svgAnchor,
                Parent = parent,
                IgnoreAttributes = ignoreAttributes,
                IsDrawable = true
            };

            drawable.CreateChildren(svgAnchor, skOwnerBounds, drawable, assetLoader, ignoreAttributes);

            drawable.IsAntialias = SvgExtensions.IsAntialias(svgAnchor);

            drawable.GeometryBounds = SKRect.Empty;
            
            drawable.CreateGeometryBounds();

            drawable.Transform = SvgExtensions.ToMatrix(svgAnchor.Transforms);

            drawable.Fill = null;
            drawable.Stroke = null;

            drawable.ClipPath = null;
            drawable.MaskDrawable = null;
            drawable.Opacity = drawable.IgnoreAttributes.HasFlag(DrawAttributes.Opacity) ? null : SvgExtensions.GetOpacityPaint(svgAnchor);
            drawable.Filter = null;

            return drawable;
        }

        public override void PostProcess(SKRect? viewport, SKMatrix totalMatrix)
        {
            var element = Element;
            if (element is null)
            {
                return;
            }

            var enableOpacity = !IgnoreAttributes.HasFlag(DrawAttributes.Opacity);

            ClipPath = null;
            MaskDrawable = null;
            Opacity = enableOpacity ? SvgExtensions.GetOpacityPaint(element) : null;
            Filter = null;

            TotalTransform = totalMatrix.PreConcat(Transform);
            TransformedBounds = TotalTransform.MapRect(GeometryBounds);

            foreach (var child in ChildrenDrawables)
            {
                child.PostProcess(viewport, totalMatrix);
            }
        }
    }
}
