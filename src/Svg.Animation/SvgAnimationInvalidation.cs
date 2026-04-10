using System;
using System.Collections.Generic;

namespace Svg.Skia;

public static class SvgAnimationInvalidation
{
    private static readonly HashSet<string> s_inheritedAttributes = new(StringComparer.Ordinal)
    {
        "alphabetic",
        "ascent",
        "ascent-height",
        "clip",
        "clip-rule",
        "color",
        "color-interpolation",
        "color-interpolation-filters",
        "descent",
        "dominant-baseline",
        "fill",
        "fill-opacity",
        "fill-rule",
        "flood-color",
        "flood-opacity",
        "font",
        "font-family",
        "font-size",
        "font-stretch",
        "font-style",
        "font-variant",
        "font-weight",
        "glyph-name",
        "horiz-adv-x",
        "horiz-origin-x",
        "horiz-origin-y",
        "k",
        "lengthAdjust",
        "letter-spacing",
        "shape-rendering",
        "space",
        "stop-color",
        "stop-opacity",
        "stroke",
        "stroke-dasharray",
        "stroke-dashoffset",
        "stroke-linecap",
        "stroke-linejoin",
        "stroke-miterlimit",
        "stroke-opacity",
        "stroke-width",
        "text-anchor",
        "text-decoration",
        "text-transform",
        "textLength",
        "units-per-em",
        "vert-adv-y",
        "vert-origin-x",
        "vert-origin-y",
        "visibility",
        "word-spacing",
        "x-height"
    };

    public static bool AffectsDescendantSubtree(string attributeName)
    {
        return !string.IsNullOrWhiteSpace(attributeName) &&
               s_inheritedAttributes.Contains(attributeName);
    }
}
