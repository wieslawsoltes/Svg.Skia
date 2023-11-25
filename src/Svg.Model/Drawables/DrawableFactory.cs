using System;
using System.Collections.Generic;
using Svg.Model.Drawables.Elements;
using ShimSkiaSharp;

namespace Svg.Model.Drawables;

public static class DrawableFactory
{
    public static DrawableBase? Create(SvgElement svgElement, SKRect skViewport, DrawableBase? parent, IAssetLoader assetLoader, HashSet<Uri>? references, DrawAttributes ignoreAttributes = DrawAttributes.None)
    {
        return svgElement switch
        {
            SvgAnchor svgAnchor => AnchorDrawable.Create(svgAnchor, skViewport, parent, assetLoader, references, ignoreAttributes),
            SvgFragment svgFragment => FragmentDrawable.Create(svgFragment, skViewport, parent, assetLoader, references, ignoreAttributes),
            SvgImage svgImage => ImageDrawable.Create(svgImage, skViewport, parent, assetLoader, references, ignoreAttributes),
            SvgSwitch svgSwitch => SwitchDrawable.Create(svgSwitch, skViewport, parent, assetLoader, references, ignoreAttributes),
            SvgUse svgUse => UseDrawable.Create(svgUse, skViewport, parent, assetLoader, references, ignoreAttributes),
            SvgCircle svgCircle => CircleDrawable.Create(svgCircle, skViewport, parent, assetLoader, references, ignoreAttributes),
            SvgEllipse svgEllipse => EllipseDrawable.Create(svgEllipse, skViewport, parent, assetLoader, references, ignoreAttributes),
            SvgRectangle svgRectangle => RectangleDrawable.Create(svgRectangle, skViewport, parent, assetLoader, references, ignoreAttributes),
            SvgGroup svgGroup => GroupDrawable.Create(svgGroup, skViewport, parent, assetLoader, references, ignoreAttributes),
            SvgLine svgLine => LineDrawable.Create(svgLine, skViewport, parent, assetLoader, references, ignoreAttributes),
            SvgPath svgPath => PathDrawable.Create(svgPath, skViewport, parent, assetLoader, references, ignoreAttributes),
            SvgPolyline svgPolyline => PolylineDrawable.Create(svgPolyline, skViewport, parent, assetLoader, references, ignoreAttributes),
            SvgPolygon svgPolygon => PolygonDrawable.Create(svgPolygon, skViewport, parent, assetLoader, references, ignoreAttributes),
            SvgText svgText => TextDrawable.Create(svgText, skViewport, parent, assetLoader, references, ignoreAttributes),
            _ => null,
        };
    }
}
