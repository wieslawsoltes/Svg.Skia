using System;
using System.Collections.Generic;
using Svg.Model.Drawables.Elements;
#if USE_SKIASHARP
using SkiaSharp;
#else
using ShimSkiaSharp.Primitives;
#endif

namespace Svg.Model.Drawables
{
    public static class DrawableFactory
    {
        public static DrawableBase? Create(SvgElement svgElement, SKRect skOwnerBounds, DrawableBase? parent, IAssetLoader assetLoader, HashSet<Uri>? references, DrawAttributes ignoreAttributes = DrawAttributes.None)
        {
            return svgElement switch
            {
                SvgAnchor svgAnchor => AnchorDrawable.Create(svgAnchor, skOwnerBounds, parent, assetLoader, references, ignoreAttributes),
                SvgFragment svgFragment => FragmentDrawable.Create(svgFragment, skOwnerBounds, parent, assetLoader, references, ignoreAttributes),
                SvgImage svgImage => ImageDrawable.Create(svgImage, skOwnerBounds, parent, assetLoader, references, ignoreAttributes),
                SvgSwitch svgSwitch => SwitchDrawable.Create(svgSwitch, skOwnerBounds, parent, assetLoader, references, ignoreAttributes),
                SvgUse svgUse => UseDrawable.Create(svgUse, skOwnerBounds, parent, assetLoader, references, ignoreAttributes),
                SvgCircle svgCircle => CircleDrawable.Create(svgCircle, skOwnerBounds, parent, assetLoader, references, ignoreAttributes),
                SvgEllipse svgEllipse => EllipseDrawable.Create(svgEllipse, skOwnerBounds, parent, assetLoader, references, ignoreAttributes),
                SvgRectangle svgRectangle => RectangleDrawable.Create(svgRectangle, skOwnerBounds, parent, assetLoader, references, ignoreAttributes),
                SvgGroup svgGroup => GroupDrawable.Create(svgGroup, skOwnerBounds, parent, assetLoader, references, ignoreAttributes),
                SvgLine svgLine => LineDrawable.Create(svgLine, skOwnerBounds, parent, assetLoader, references, ignoreAttributes),
                SvgPath svgPath => PathDrawable.Create(svgPath, skOwnerBounds, parent, assetLoader, references, ignoreAttributes),
                SvgPolyline svgPolyline => PolylineDrawable.Create(svgPolyline, skOwnerBounds, parent, assetLoader, references, ignoreAttributes),
                SvgPolygon svgPolygon => PolygonDrawable.Create(svgPolygon, skOwnerBounds, parent, assetLoader, references, ignoreAttributes),
                SvgText svgText => TextDrawable.Create(svgText, skOwnerBounds, parent, assetLoader, references, ignoreAttributes),
                _ => null,
            };
        }
    }
}
