#nullable enable

using System;
using System.Collections.Generic;

namespace Svg;

internal static class SvgStyleAttributeNames
{
    public const string RawTextDecorationAttributeKey = "__svgskia:text-decoration-raw";

    private static readonly HashSet<string> s_names = new(StringComparer.OrdinalIgnoreCase)
    {
        "alignment-baseline",
        "baseline-shift",
        "clip",
        "clip-path",
        "clip-rule",
        "color",
        "color-interpolation",
        "color-interpolation-filters",
        "color-profile",
        "color-rendering",
        "cursor",
        "direction",
        "display",
        "dominant-baseline",
        "enable-background",
        "fill",
        "fill-opacity",
        "fill-rule",
        "filter",
        "flood-color",
        "flood-opacity",
        "font",
        "font-family",
        "font-size",
        "font-size-adjust",
        "font-stretch",
        "font-style",
        "font-variant",
        "font-weight",
        "glyph-orientation-horizontal",
        "glyph-orientation-vertical",
        "image-rendering",
        "kerning",
        "letter-spacing",
        "lighting-color",
        "marker",
        "marker-end",
        "marker-mid",
        "marker-start",
        "mask",
        "opacity",
        "overflow",
        "pointer-events",
        "shape-rendering",
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
        "text-rendering",
        "text-transform",
        "unicode-bidi",
        "visibility",
        "word-spacing",
        "writing-mode"
    };

    public static IReadOnlyCollection<string> All => s_names;

    public static bool Contains(string? name)
    {
        return name is not null && s_names.Contains(name);
    }
}
