using Svg.Document_Structure;
using Svg.Model.Primitives;

namespace Svg.Model.Drawables.Elements
{
    public sealed class SymbolDrawable : DrawableContainer
    {
        private SymbolDrawable(IAssetLoader assetLoader)
            : base(assetLoader)
        {
        }

        public static SymbolDrawable Create(SvgSymbol svgSymbol, float x, float y, float width, float height, SKRect skOwnerBounds, DrawableBase? parent, IAssetLoader assetLoader, Attributes ignoreAttributes)
        {
            var drawable = new SymbolDrawable(assetLoader)
            {
                Element = svgSymbol,
                Parent = parent,
                IgnoreAttributes = ignoreAttributes
            };

            drawable.IsDrawable = drawable.CanDraw(svgSymbol, drawable.IgnoreAttributes) && drawable.HasFeatures(svgSymbol, drawable.IgnoreAttributes);

            if (!drawable.IsDrawable)
            {
                return drawable;
            }

            if (svgSymbol.CustomAttributes.TryGetValue("width", out var _widthString))
            {
                if (new SvgUnitConverter().ConvertFromString(_widthString) is SvgUnit _width)
                {
                    width = _width.ToDeviceValue(UnitRenderingType.Horizontal, svgSymbol, skOwnerBounds);
                }
            }

            if (svgSymbol.CustomAttributes.TryGetValue("height", out var heightString))
            {
                if (new SvgUnitConverter().ConvertFromString(heightString) is SvgUnit _height)
                {
                    height = _height.ToDeviceValue(UnitRenderingType.Vertical, svgSymbol, skOwnerBounds);
                }
            }

            var svgOverflow = SvgOverflow.Hidden;
            if (svgSymbol.TryGetAttribute("overflow", out string overflowString))
            {
                if (new SvgOverflowConverter().ConvertFromString(overflowString) is SvgOverflow _svgOverflow)
                {
                    svgOverflow = _svgOverflow;
                }
            }

            switch (svgOverflow)
            {
                case SvgOverflow.Auto:
                case SvgOverflow.Visible:
                case SvgOverflow.Inherit:
                    break;

                default:
                    drawable.Overflow = SKRect.Create(x, y, width, height);
                    break;
            }

            drawable.CreateChildren(svgSymbol, skOwnerBounds, drawable, assetLoader, ignoreAttributes);

            drawable.IsAntialias = SvgModelExtensions.IsAntialias(svgSymbol);

            drawable.GeometryBounds = SKRect.Empty;

            drawable.CreateGeometryBounds();

            drawable.TransformedBounds = drawable.GeometryBounds;

            drawable.Transform = SvgModelExtensions.ToMatrix(svgSymbol.Transforms);
            var skMatrixViewBox = SvgModelExtensions.ToMatrix(svgSymbol.ViewBox, svgSymbol.AspectRatio, x, y, width, height);
            drawable.Transform = drawable.Transform.PreConcat(skMatrixViewBox);

            // TODO: Transform _skBounds using _skMatrix.
            drawable.TransformedBounds = drawable.Transform.MapRect(drawable.TransformedBounds);

            drawable.Fill = null;
            drawable.Stroke = null;

            return drawable;
        }
    }
}
