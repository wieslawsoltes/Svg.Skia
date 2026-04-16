using System.Collections.Generic;

namespace Svg;

internal sealed class SvgCompatibilityLoadResult
{
    public SvgCompatibilityLoadResult(
        SvgDocument document,
        SvgElementFactory elementFactory,
        List<SvgCssStyleSource> styles,
        bool hasStagedStyles)
    {
        Document = document;
        ElementFactory = elementFactory;
        Styles = styles;
        HasStagedStyles = hasStagedStyles;
    }

    public SvgDocument Document { get; }

    public SvgElementFactory ElementFactory { get; }

    public List<SvgCssStyleSource> Styles { get; }

    public bool HasStagedStyles { get; set; }
}
