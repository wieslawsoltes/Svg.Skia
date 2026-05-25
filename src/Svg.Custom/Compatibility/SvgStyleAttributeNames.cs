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
            "cx" or
            "cy" or
            "cursor" or
            "d" or
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
            "font-feature-settings" or
            "font-kerning" or
            "font-size" or
            "font-size-adjust" or
            "font-stretch" or
            "font-style" or
            "font-variant" or
            "font-variant-ligatures" or
            "font-weight" or
            "glyph-orientation-horizontal" or
            "glyph-orientation-vertical" or
            "height" or
            "image-rendering" or
            "inline-size" or
            "isolation" or
            "kerning" or
            "letter-spacing" or
            "line-break" or
            "line-height" or
            "lighting-color" or
            "marker" or
            "marker-end" or
            "marker-mid" or
            "marker-start" or
            "mask" or
            "mask-type" or
            "mix-blend-mode" or
            "opacity" or
            "overflow" or
            "overflow-wrap" or
            "paint-order" or
            "pointer-events" or
            "r" or
            "rx" or
            "ry" or
            "shape-rendering" or
            "shape-inside" or
            "shape-subtract" or
            "shape-padding" or
            "shape-margin" or
            "shape-image-threshold" or
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
            "text-overflow" or
            "text-rendering" or
            "text-transform" or
            "text-wrap-mode" or
            "transform-box" or
            "transform-origin" or
            "unicode-bidi" or
            "vector-effect" or
            "visibility" or
            "white-space" or
            "white-space-collapse" or
            "white-space-trim" or
            "width" or
            "word-break" or
            "word-spacing" or
            "writing-mode" or
            "x" or
            "x1" or
            "x2" or
            "y" or
            "y1" or
            "y2" => true,
            _ => ContainsAsciiUppercase(name) && ContainsIgnoreCase(name)
        };
    }

    public static bool IsCssOnlyProperty(string? name)
    {
        return name is not null &&
               (name.Equals("isolation", StringComparison.OrdinalIgnoreCase) ||
                name.Equals("mix-blend-mode", StringComparison.OrdinalIgnoreCase));
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
               name.Equals("cx", StringComparison.OrdinalIgnoreCase) ||
               name.Equals("cy", StringComparison.OrdinalIgnoreCase) ||
               name.Equals("cursor", StringComparison.OrdinalIgnoreCase) ||
               name.Equals("d", StringComparison.OrdinalIgnoreCase) ||
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
               name.Equals("font-feature-settings", StringComparison.OrdinalIgnoreCase) ||
               name.Equals("font-kerning", StringComparison.OrdinalIgnoreCase) ||
               name.Equals("font-size", StringComparison.OrdinalIgnoreCase) ||
               name.Equals("font-size-adjust", StringComparison.OrdinalIgnoreCase) ||
               name.Equals("font-stretch", StringComparison.OrdinalIgnoreCase) ||
               name.Equals("font-style", StringComparison.OrdinalIgnoreCase) ||
               name.Equals("font-variant", StringComparison.OrdinalIgnoreCase) ||
               name.Equals("font-variant-ligatures", StringComparison.OrdinalIgnoreCase) ||
               name.Equals("font-weight", StringComparison.OrdinalIgnoreCase) ||
               name.Equals("glyph-orientation-horizontal", StringComparison.OrdinalIgnoreCase) ||
               name.Equals("glyph-orientation-vertical", StringComparison.OrdinalIgnoreCase) ||
               name.Equals("height", StringComparison.OrdinalIgnoreCase) ||
               name.Equals("image-rendering", StringComparison.OrdinalIgnoreCase) ||
               name.Equals("inline-size", StringComparison.OrdinalIgnoreCase) ||
               name.Equals("isolation", StringComparison.OrdinalIgnoreCase) ||
               name.Equals("kerning", StringComparison.OrdinalIgnoreCase) ||
               name.Equals("letter-spacing", StringComparison.OrdinalIgnoreCase) ||
               name.Equals("line-break", StringComparison.OrdinalIgnoreCase) ||
               name.Equals("line-height", StringComparison.OrdinalIgnoreCase) ||
               name.Equals("lighting-color", StringComparison.OrdinalIgnoreCase) ||
               name.Equals("marker", StringComparison.OrdinalIgnoreCase) ||
               name.Equals("marker-end", StringComparison.OrdinalIgnoreCase) ||
               name.Equals("marker-mid", StringComparison.OrdinalIgnoreCase) ||
               name.Equals("marker-start", StringComparison.OrdinalIgnoreCase) ||
               name.Equals("mask", StringComparison.OrdinalIgnoreCase) ||
               name.Equals("mask-type", StringComparison.OrdinalIgnoreCase) ||
               name.Equals("mix-blend-mode", StringComparison.OrdinalIgnoreCase) ||
               name.Equals("opacity", StringComparison.OrdinalIgnoreCase) ||
               name.Equals("overflow", StringComparison.OrdinalIgnoreCase) ||
               name.Equals("overflow-wrap", StringComparison.OrdinalIgnoreCase) ||
               name.Equals("paint-order", StringComparison.OrdinalIgnoreCase) ||
               name.Equals("pointer-events", StringComparison.OrdinalIgnoreCase) ||
               name.Equals("r", StringComparison.OrdinalIgnoreCase) ||
               name.Equals("rx", StringComparison.OrdinalIgnoreCase) ||
               name.Equals("ry", StringComparison.OrdinalIgnoreCase) ||
               name.Equals("shape-rendering", StringComparison.OrdinalIgnoreCase) ||
               name.Equals("shape-inside", StringComparison.OrdinalIgnoreCase) ||
               name.Equals("shape-subtract", StringComparison.OrdinalIgnoreCase) ||
               name.Equals("shape-padding", StringComparison.OrdinalIgnoreCase) ||
               name.Equals("shape-margin", StringComparison.OrdinalIgnoreCase) ||
               name.Equals("shape-image-threshold", StringComparison.OrdinalIgnoreCase) ||
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
               name.Equals("text-overflow", StringComparison.OrdinalIgnoreCase) ||
               name.Equals("text-rendering", StringComparison.OrdinalIgnoreCase) ||
               name.Equals("text-transform", StringComparison.OrdinalIgnoreCase) ||
               name.Equals("text-wrap-mode", StringComparison.OrdinalIgnoreCase) ||
               name.Equals("transform-box", StringComparison.OrdinalIgnoreCase) ||
               name.Equals("transform-origin", StringComparison.OrdinalIgnoreCase) ||
               name.Equals("unicode-bidi", StringComparison.OrdinalIgnoreCase) ||
               name.Equals("vector-effect", StringComparison.OrdinalIgnoreCase) ||
               name.Equals("visibility", StringComparison.OrdinalIgnoreCase) ||
               name.Equals("white-space", StringComparison.OrdinalIgnoreCase) ||
               name.Equals("white-space-collapse", StringComparison.OrdinalIgnoreCase) ||
               name.Equals("white-space-trim", StringComparison.OrdinalIgnoreCase) ||
               name.Equals("width", StringComparison.OrdinalIgnoreCase) ||
               name.Equals("word-break", StringComparison.OrdinalIgnoreCase) ||
               name.Equals("word-spacing", StringComparison.OrdinalIgnoreCase) ||
               name.Equals("writing-mode", StringComparison.OrdinalIgnoreCase) ||
               name.Equals("x", StringComparison.OrdinalIgnoreCase) ||
               name.Equals("x1", StringComparison.OrdinalIgnoreCase) ||
               name.Equals("x2", StringComparison.OrdinalIgnoreCase) ||
               name.Equals("y", StringComparison.OrdinalIgnoreCase) ||
               name.Equals("y1", StringComparison.OrdinalIgnoreCase) ||
               name.Equals("y2", StringComparison.OrdinalIgnoreCase);
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
            "cx",
            "cy",
            "cursor",
            "d",
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
            "font-feature-settings",
            "font-kerning",
            "font-size",
            "font-size-adjust",
            "font-stretch",
            "font-style",
            "font-variant",
            "font-variant-ligatures",
            "font-weight",
            "glyph-orientation-horizontal",
            "glyph-orientation-vertical",
            "height",
            "image-rendering",
            "inline-size",
            "isolation",
            "kerning",
            "letter-spacing",
            "line-break",
            "line-height",
            "lighting-color",
            "marker",
            "marker-end",
            "marker-mid",
            "marker-start",
            "mask",
            "mask-type",
            "mix-blend-mode",
            "opacity",
            "overflow",
            "overflow-wrap",
            "paint-order",
            "pointer-events",
            "r",
            "rx",
            "ry",
            "shape-rendering",
            "shape-inside",
            "shape-subtract",
            "shape-padding",
            "shape-margin",
            "shape-image-threshold",
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
            "text-overflow",
            "text-rendering",
            "text-transform",
            "text-wrap-mode",
            "transform-box",
            "transform-origin",
            "unicode-bidi",
            "vector-effect",
            "visibility",
            "white-space",
            "white-space-collapse",
            "white-space-trim",
            "width",
            "word-break",
            "word-spacing",
            "writing-mode",
            "x",
            "x1",
            "x2",
            "y",
            "y1",
            "y2"
        };
    }
}
