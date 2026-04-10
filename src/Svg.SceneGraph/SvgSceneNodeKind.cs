using Svg;

namespace Svg.Skia;

public enum SvgSceneNodeKind
{
    Unknown,
    Fragment,
    Group,
    Anchor,
    Use,
    Switch,
    Image,
    Text,
    Marker,
    Path,
    Shape,
    Mask,
    Container
}

internal static class SvgSceneNodeKindExtensions
{
    public static SvgSceneNodeKind FromElement(SvgElement element)
    {
        return element switch
        {
            SvgFragment => SvgSceneNodeKind.Fragment,
            SvgGroup => SvgSceneNodeKind.Group,
            SvgAnchor => SvgSceneNodeKind.Anchor,
            SvgUse => SvgSceneNodeKind.Use,
            SvgSwitch => SvgSceneNodeKind.Switch,
            SvgImage => SvgSceneNodeKind.Image,
            SvgTextBase => SvgSceneNodeKind.Text,
            SvgMarker => SvgSceneNodeKind.Marker,
            SvgPath => SvgSceneNodeKind.Path,
            SvgCircle or SvgEllipse or SvgRectangle or SvgLine or SvgPolyline or SvgPolygon => SvgSceneNodeKind.Shape,
            SvgMask => SvgSceneNodeKind.Mask,
            _ => SvgSceneNodeKind.Unknown
        };
    }
}
