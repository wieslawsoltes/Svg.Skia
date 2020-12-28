using Svg.Model.Drawables;
using Svg.Model.Primitives;

namespace Svg.Model
{
    public static class DrawableFactory
    {
        public static DrawableBase? Create(SvgElement svgElement, Rect skOwnerBounds, DrawableBase? parent, IAssetLoader assetLoader, Attributes ignoreAttributes = Attributes.None)
        {
            return svgElement switch
            {
                SvgAnchor svgAnchor => AnchorDrawable.Create(svgAnchor, skOwnerBounds, parent, assetLoader, ignoreAttributes),
                SvgFragment svgFragment => FragmentDrawable.Create(svgFragment, skOwnerBounds, parent, assetLoader, ignoreAttributes),
                SvgImage svgImage => ImageDrawable.Create(svgImage, skOwnerBounds, parent, assetLoader, ignoreAttributes),
                SvgSwitch svgSwitch => SwitchDrawable.Create(svgSwitch, skOwnerBounds, parent, assetLoader, ignoreAttributes),
                SvgUse svgUse => UseDrawable.Create(svgUse, skOwnerBounds, parent, assetLoader, ignoreAttributes),
                SvgCircle svgCircle => CircleDrawable.Create(svgCircle, skOwnerBounds, parent, assetLoader, ignoreAttributes),
                SvgEllipse svgEllipse => EllipseDrawable.Create(svgEllipse, skOwnerBounds, parent, assetLoader, ignoreAttributes),
                SvgRectangle svgRectangle => RectangleDrawable.Create(svgRectangle, skOwnerBounds, parent, assetLoader, ignoreAttributes),
                SvgGroup svgGroup => GroupDrawable.Create(svgGroup, skOwnerBounds, parent, assetLoader, ignoreAttributes),
                SvgLine svgLine => LineDrawable.Create(svgLine, skOwnerBounds, parent, assetLoader, ignoreAttributes),
                SvgPath svgPath => PathDrawable.Create(svgPath, skOwnerBounds, parent, assetLoader, ignoreAttributes),
                SvgPolyline svgPolyline => PolylineDrawable.Create(svgPolyline, skOwnerBounds, parent, assetLoader, ignoreAttributes),
                SvgPolygon svgPolygon => PolygonDrawable.Create(svgPolygon, skOwnerBounds, parent, assetLoader, ignoreAttributes),
                SvgText svgText => TextDrawable.Create(svgText, skOwnerBounds, parent, assetLoader, ignoreAttributes),
                _ => null,
            };
        }
    }
}
