using Svg.Model.Primitives;

namespace Svg.Model.Drawables.Elements
{
    public sealed class SwitchDrawable : DrawableBase
    {
        public DrawableBase? FirstChild { get; set; }

        private SwitchDrawable(IAssetLoader assetLoader)
            : base(assetLoader)
        {
        }

        public static SwitchDrawable Create(SvgSwitch svgSwitch, Rect skOwnerBounds, DrawableBase? parent, IAssetLoader assetLoader, Attributes ignoreAttributes = Attributes.None)
        {
            var drawable = new SwitchDrawable(assetLoader)
            {
                Element = svgSwitch,
                Parent = parent,

                IgnoreAttributes = ignoreAttributes
            };
            drawable.IsDrawable = drawable.CanDraw(svgSwitch, drawable.IgnoreAttributes) && drawable.HasFeatures(svgSwitch, drawable.IgnoreAttributes);

            if (!drawable.IsDrawable)
            {
                return drawable;
            }

            foreach (var child in svgSwitch.Children)
            {
                if (!child.IsKnownElement())
                {
                    continue;
                }

                var hasRequiredFeatures = child.HasRequiredFeatures();
                var hasRequiredExtensions = child.HasRequiredExtensions();
                var hasSystemLanguage = child.HasSystemLanguage();

                if (hasRequiredFeatures && hasRequiredExtensions && hasSystemLanguage)
                {
                    var childDrawable = DrawableFactory.Create(child, skOwnerBounds, parent, assetLoader, ignoreAttributes);
                    if (childDrawable is { })
                    {
                        drawable.FirstChild = childDrawable;
                    }
                    break;
                }
            }

            if (drawable.FirstChild is null)
            {
                drawable.IsDrawable = false;
                return drawable;
            }

            drawable.IsAntialias = SvgModelExtensions.IsAntialias(svgSwitch);

            drawable.GeometryBounds = drawable.FirstChild.TransformedBounds;

            drawable.TransformedBounds = drawable.GeometryBounds;

            drawable.Transform = SvgModelExtensions.ToMatrix(svgSwitch.Transforms);

            // TODO: Transform _skBounds using _skMatrix.
            drawable.TransformedBounds = drawable.Transform.MapRect(drawable.TransformedBounds);

            drawable.Fill = null;
            drawable.Stroke = null;

            return drawable;
        }

        public override void OnDraw(Canvas canvas, Attributes ignoreAttributes, DrawableBase? until)
        {
            if (until is { } && this == until)
            {
                return;
            }

            FirstChild?.Draw(canvas, ignoreAttributes, until, true);
        }

        public override void PostProcess(Rect? viewport)
        {
            base.PostProcess(viewport);
            FirstChild?.PostProcess(viewport);
        }
    }
}
