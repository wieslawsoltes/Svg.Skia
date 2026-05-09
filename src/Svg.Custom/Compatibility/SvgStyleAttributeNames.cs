#nullable enable

using System;
using System.Collections.Generic;

namespace Svg;

internal static class SvgStyleAttributeNames
{
    public const string RawTextDecorationAttributeKey = "__svgskia:text-decoration-raw";

    public static IReadOnlyList<string> All => Names.Value;

    public static bool Contains(string? name)
    {
        if (name is null)
        {
            return false;
        }

        return name switch
        {
            "alignment-baseline" or
            "baseline-shift" or
            "clip" or
            "clip-path" or
            "clip-rule" or
            "color" or
            "color-interpolation" or
            "color-interpolation-filters" or
            "color-profile" or
            "color-rendering" or
            "cursor" or
            "direction" or
            "display" or
            "dominant-baseline" or
            "enable-background" or
            "fill" or
            "fill-opacity" or
            "fill-rule" or
            "filter" or
            "flood-color" or
            "flood-opacity" or
            "font" or
            "font-family" or
            "font-size" or
            "font-size-adjust" or
            "font-stretch" or
            "font-style" or
            "font-variant" or
            "font-weight" or
            "glyph-orientation-horizontal" or
            "glyph-orientation-vertical" or
            "image-rendering" or
            "isolation" or
            "kerning" or
            "letter-spacing" or
            "lighting-color" or
            "marker" or
            "marker-end" or
            "marker-mid" or
            "marker-start" or
            "mask" or
            "mix-blend-mode" or
            "opacity" or
            "overflow" or
            "pointer-events" or
            "shape-rendering" or
            "stop-color" or
            "stop-opacity" or
            "stroke" or
            "stroke-dasharray" or
            "stroke-dashoffset" or
            "stroke-linecap" or
            "stroke-linejoin" or
            "stroke-miterlimit" or
            "stroke-opacity" or
            "stroke-width" or
            "text-anchor" or
            "text-decoration" or
            "text-rendering" or
            "text-transform" or
            "unicode-bidi" or
            "visibility" or
            "word-spacing" or
            "writing-mode" => true,
            _ => ContainsAsciiUppercase(name) && ContainsIgnoreCase(name)
        };
    }

    private static bool ContainsAsciiUppercase(string name)
    {
        for (var i = 0; i < name.Length; i++)
        {
            if (name[i] is >= 'A' and <= 'Z')
            {
                return true;
            }
        }

        return false;
    }

    private static bool ContainsIgnoreCase(string name)
    {
        return name.Equals("alignment-baseline", StringComparison.OrdinalIgnoreCase) ||
               name.Equals("baseline-shift", StringComparison.OrdinalIgnoreCase) ||
               name.Equals("clip", StringComparison.OrdinalIgnoreCase) ||
               name.Equals("clip-path", StringComparison.OrdinalIgnoreCase) ||
               name.Equals("clip-rule", StringComparison.OrdinalIgnoreCase) ||
               name.Equals("color", StringComparison.OrdinalIgnoreCase) ||
               name.Equals("color-interpolation", StringComparison.OrdinalIgnoreCase) ||
               name.Equals("color-interpolation-filters", StringComparison.OrdinalIgnoreCase) ||
               name.Equals("color-profile", StringComparison.OrdinalIgnoreCase) ||
               name.Equals("color-rendering", StringComparison.OrdinalIgnoreCase) ||
               name.Equals("cursor", StringComparison.OrdinalIgnoreCase) ||
               name.Equals("direction", StringComparison.OrdinalIgnoreCase) ||
               name.Equals("display", StringComparison.OrdinalIgnoreCase) ||
               name.Equals("dominant-baseline", StringComparison.OrdinalIgnoreCase) ||
               name.Equals("enable-background", StringComparison.OrdinalIgnoreCase) ||
               name.Equals("fill", StringComparison.OrdinalIgnoreCase) ||
               name.Equals("fill-opacity", StringComparison.OrdinalIgnoreCase) ||
               name.Equals("fill-rule", StringComparison.OrdinalIgnoreCase) ||
               name.Equals("filter", StringComparison.OrdinalIgnoreCase) ||
               name.Equals("flood-color", StringComparison.OrdinalIgnoreCase) ||
               name.Equals("flood-opacity", StringComparison.OrdinalIgnoreCase) ||
               name.Equals("font", StringComparison.OrdinalIgnoreCase) ||
               name.Equals("font-family", StringComparison.OrdinalIgnoreCase) ||
               name.Equals("font-size", StringComparison.OrdinalIgnoreCase) ||
               name.Equals("font-size-adjust", StringComparison.OrdinalIgnoreCase) ||
               name.Equals("font-stretch", StringComparison.OrdinalIgnoreCase) ||
               name.Equals("font-style", StringComparison.OrdinalIgnoreCase) ||
               name.Equals("font-variant", StringComparison.OrdinalIgnoreCase) ||
               name.Equals("font-weight", StringComparison.OrdinalIgnoreCase) ||
               name.Equals("glyph-orientation-horizontal", StringComparison.OrdinalIgnoreCase) ||
               name.Equals("glyph-orientation-vertical", StringComparison.OrdinalIgnoreCase) ||
               name.Equals("image-rendering", StringComparison.OrdinalIgnoreCase) ||
               name.Equals("isolation", StringComparison.OrdinalIgnoreCase) ||
               name.Equals("kerning", StringComparison.OrdinalIgnoreCase) ||
               name.Equals("letter-spacing", StringComparison.OrdinalIgnoreCase) ||
               name.Equals("lighting-color", StringComparison.OrdinalIgnoreCase) ||
               name.Equals("marker", StringComparison.OrdinalIgnoreCase) ||
               name.Equals("marker-end", StringComparison.OrdinalIgnoreCase) ||
               name.Equals("marker-mid", StringComparison.OrdinalIgnoreCase) ||
               name.Equals("marker-start", StringComparison.OrdinalIgnoreCase) ||
               name.Equals("mask", StringComparison.OrdinalIgnoreCase) ||
               name.Equals("mix-blend-mode", StringComparison.OrdinalIgnoreCase) ||
               name.Equals("opacity", StringComparison.OrdinalIgnoreCase) ||
               name.Equals("overflow", StringComparison.OrdinalIgnoreCase) ||
               name.Equals("pointer-events", StringComparison.OrdinalIgnoreCase) ||
               name.Equals("shape-rendering", StringComparison.OrdinalIgnoreCase) ||
               name.Equals("stop-color", StringComparison.OrdinalIgnoreCase) ||
               name.Equals("stop-opacity", StringComparison.OrdinalIgnoreCase) ||
               name.Equals("stroke", StringComparison.OrdinalIgnoreCase) ||
               name.Equals("stroke-dasharray", StringComparison.OrdinalIgnoreCase) ||
               name.Equals("stroke-dashoffset", StringComparison.OrdinalIgnoreCase) ||
               name.Equals("stroke-linecap", StringComparison.OrdinalIgnoreCase) ||
               name.Equals("stroke-linejoin", StringComparison.OrdinalIgnoreCase) ||
               name.Equals("stroke-miterlimit", StringComparison.OrdinalIgnoreCase) ||
               name.Equals("stroke-opacity", StringComparison.OrdinalIgnoreCase) ||
               name.Equals("stroke-width", StringComparison.OrdinalIgnoreCase) ||
               name.Equals("text-anchor", StringComparison.OrdinalIgnoreCase) ||
               name.Equals("text-decoration", StringComparison.OrdinalIgnoreCase) ||
               name.Equals("text-rendering", StringComparison.OrdinalIgnoreCase) ||
               name.Equals("text-transform", StringComparison.OrdinalIgnoreCase) ||
               name.Equals("unicode-bidi", StringComparison.OrdinalIgnoreCase) ||
               name.Equals("visibility", StringComparison.OrdinalIgnoreCase) ||
               name.Equals("word-spacing", StringComparison.OrdinalIgnoreCase) ||
               name.Equals("writing-mode", StringComparison.OrdinalIgnoreCase);
    }

    private static class Names
    {
        internal static readonly string[] Value =
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
            "isolation",
            "kerning",
            "letter-spacing",
            "lighting-color",
            "marker",
            "marker-end",
            "marker-mid",
            "marker-start",
            "mask",
            "mix-blend-mode",
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
    }
}
