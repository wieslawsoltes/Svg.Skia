using System;

namespace Svg;

public abstract partial class SvgMarkerElement
{
    /// <summary>
    /// Gets or sets the shorthand marker applied to the start, mid and end points of the path.
    /// </summary>
    [SvgAttribute("marker")]
    public Uri Marker
    {
        get { return GetAttribute<Uri>("marker", true); }
        set
        {
            Attributes["marker"] = value;
            Attributes["marker-start"] = value;
            Attributes["marker-mid"] = value;
            Attributes["marker-end"] = value;
        }
    }
}
