using System;
using Xml;

namespace Svg
{
    public interface ISvgPresentationAttributes : IElement
    {
        [Attribute("alignment-baseline", SvgElement.SvgNamespace)]
        public string? AlignmentBaseline
        {
            get => this.GetAttribute("alignment-baseline", false, "auto");
            set => this.SetAttribute("alignment-baseline", value);
        }

        [Attribute("baseline-shift", SvgElement.SvgNamespace)]
        public string? BaselineShift
        {
            get => this.GetAttribute("baseline-shift", false, "baseline");
            set => this.SetAttribute("baseline-shift", value);
        }

        [Attribute("clip", SvgElement.SvgNamespace)]
        public string? Clip
        {
            get => this.GetAttribute("clip", false, "auto");
            set => this.SetAttribute("clip", value);
        }

        [Attribute("clip-path", SvgElement.SvgNamespace)]
        public string? ClipPath
        {
            get => this.GetAttribute("clip-path", false, "none");
            set => this.SetAttribute("clip-path", value);
        }

        [Attribute("clip-rule", SvgElement.SvgNamespace)]
        public string? ClipRule
        {
            get => this.GetAttribute("clip-rule", true, "nonzero");
            set => this.SetAttribute("clip-rule", value);
        }

        [Attribute("color", SvgElement.SvgNamespace)]
        public string? Color
        {
            get => this.GetAttribute("color", true, null);
            set => this.SetAttribute("color", value);
        }

        [Attribute("color-interpolation", SvgElement.SvgNamespace)]
        public string? ColorInterpolation
        {
            get => this.GetAttribute("color-interpolation", true, "sRGB");
            set => this.SetAttribute("color-interpolation", value);
        }

        [Attribute("color-interpolation-filters", SvgElement.SvgNamespace)]
        public string? ColorInterpolationFilters
        {
            get => this.GetAttribute("color-interpolation-filters", true, "linearRGB");
            set => this.SetAttribute("color-interpolation-filters", value);
        }

        [Attribute("color-profile", SvgElement.SvgNamespace)]
        public string? ColorProfile
        {
            get => this.GetAttribute("color-profile", true, "auto");
            set => this.SetAttribute("color-profile", value);
        }

        [Attribute("color-rendering", SvgElement.SvgNamespace)]
        public string? ColorRendering
        {
            get => this.GetAttribute("color-rendering", true, "auto");
            set => this.SetAttribute("color-rendering", value);
        }

        [Attribute("cursor", SvgElement.SvgNamespace)]
        public string? Cursor
        {
            get => this.GetAttribute("cursor", true, "auto");
            set => this.SetAttribute("cursor", value);
        }

        [Attribute("direction", SvgElement.SvgNamespace)]
        public string? Direction
        {
            get => this.GetAttribute("direction", true, "ltr");
            set => this.SetAttribute("direction", value);
        }

        [Attribute("display", SvgElement.SvgNamespace)]
        public string? Display
        {
            get => this.GetAttribute("display", false, "inline");
            set => this.SetAttribute("display", value);
        }

        [Attribute("dominant-baseline", SvgElement.SvgNamespace)]
        public string? DominantBaseline
        {
            get => this.GetAttribute("dominant-baseline", false, "auto");
            set => this.SetAttribute("dominant-baseline", value);
        }

        [Attribute("enable-background", SvgElement.SvgNamespace)]
        public string? EnableBackground
        {
            get => this.GetAttribute("enable-background", false, "accumulate");
            set => this.SetAttribute("enable-background", value);
        }

        [Attribute("fill", SvgElement.SvgNamespace)]
        public string? Fill
        {
            get => this.GetAttribute("fill", true, "black");
            set => this.SetAttribute("fill", value);
        }

        [Attribute("fill-opacity", SvgElement.SvgNamespace)]
        public string? FillOpacity
        {
            get => this.GetAttribute("fill-opacity", true, "1");
            set => this.SetAttribute("fill-opacity", value);
        }

        [Attribute("fill-rule", SvgElement.SvgNamespace)]
        public string? FillRule
        {
            get => this.GetAttribute("fill-rule", true, "nonzero");
            set => this.SetAttribute("fill-rule", value);
        }

        [Attribute("filter", SvgElement.SvgNamespace)]
        public string? Filter
        {
            get => this.GetAttribute("filter", false, "none");
            set => this.SetAttribute("filter", value);
        }

        [Attribute("flood-color", SvgElement.SvgNamespace)]
        public string? FloodColor
        {
            get => this.GetAttribute("flood-color", false, "black");
            set => this.SetAttribute("flood-color", value);
        }

        [Attribute("flood-opacity", SvgElement.SvgNamespace)]
        public string? FloodOpacity
        {
            get => this.GetAttribute("flood-opacity", false, "1");
            set => this.SetAttribute("flood-opacity", value);
        }

        [Attribute("font-family", SvgElement.SvgNamespace)]
        public string? FontFamily
        {
            get => this.GetAttribute("font-family", true, null);
            set => this.SetAttribute("font-family", value);
        }

        [Attribute("font-size", SvgElement.SvgNamespace)]
        public string? FontSize
        {
            get => this.GetAttribute("font-size", true, "medium");
            set => this.SetAttribute("font-size", value);
        }

        [Attribute("font-size-adjust", SvgElement.SvgNamespace)]
        public string? FontSizeAdjust
        {
            get => this.GetAttribute("font-size-adjust", true, "none");
            set => this.SetAttribute("font-size-adjust", value);
        }

        [Attribute("font-stretch", SvgElement.SvgNamespace)]
        public string? FontStretch
        {
            get => this.GetAttribute("font-stretch", true, "normal");
            set => this.SetAttribute("font-stretch", value);
        }

        [Attribute("font-style", SvgElement.SvgNamespace)]
        public string? FontStyle
        {
            get => this.GetAttribute("font-style", true, "normal");
            set => this.SetAttribute("font-style", value);
        }

        [Attribute("font-variant", SvgElement.SvgNamespace)]
        public string? FontVariant
        {
            get => this.GetAttribute("font-variant", true, "normal");
            set => this.SetAttribute("font-variant", value);
        }

        [Attribute("font-weight", SvgElement.SvgNamespace)]
        public string? FontWeight
        {
            get => this.GetAttribute("font-weight", true, "normal");
            set => this.SetAttribute("font-weight", value);
        }

        [Attribute("glyph-orientation-horizontal", SvgElement.SvgNamespace)]
        public string? GlyphOrientationHorizontal
        {
            get => this.GetAttribute("glyph-orientation-horizontal", true, "0deg");
            set => this.SetAttribute("glyph-orientation-horizontal", value);
        }

        [Attribute("glyph-orientation-vertical", SvgElement.SvgNamespace)]
        public string? GlyphOrientationVertical
        {
            get => this.GetAttribute("glyph-orientation-vertical", true, "auto");
            set => this.SetAttribute("glyph-orientation-vertical", value);
        }

        [Attribute("image-rendering", SvgElement.SvgNamespace)]
        public string? ImageRendering
        {
            get => this.GetAttribute("image-rendering", true, "auto");
            set => this.SetAttribute("image-rendering", value);
        }

        [Attribute("kerning", SvgElement.SvgNamespace)]
        public string? Kerning
        {
            get => this.GetAttribute("kerning", true, "auto");
            set => this.SetAttribute("kerning", value);
        }

        [Attribute("letter-spacing", SvgElement.SvgNamespace)]
        public string? LetterSpacing
        {
            get => this.GetAttribute("letter-spacing", true, "normal");
            set => this.SetAttribute("letter-spacing", value);
        }

        [Attribute("lighting-color", SvgElement.SvgNamespace)]
        public string? LightingColor
        {
            get => this.GetAttribute("lighting-color", false, "white");
            set => this.SetAttribute("lighting-color", value);
        }

        [Attribute("marker-end", SvgElement.SvgNamespace)]
        public string? MarkerEnd
        {
            get => this.GetAttribute("marker-end", true, "none");
            set => this.SetAttribute("marker-end", value);
        }

        [Attribute("marker-mid", SvgElement.SvgNamespace)]
        public string? MarkerMid
        {
            get => this.GetAttribute("marker-mid", true, "none");
            set => this.SetAttribute("marker-mid", value);
        }

        [Attribute("marker-start", SvgElement.SvgNamespace)]
        public string? MarkerStart
        {
            get => this.GetAttribute("marker-start", true, "none");
            set => this.SetAttribute("marker-start", value);
        }

        [Attribute("mask", SvgElement.SvgNamespace)]
        public string? Mask
        {
            get => this.GetAttribute("mask", false, "none");
            set => this.SetAttribute("mask", value);
        }

        [Attribute("opacity", SvgElement.SvgNamespace)]
        public string? Opacity
        {
            get => this.GetAttribute("opacity", false, "1");
            set => this.SetAttribute("opacity", value);
        }

        // TODO:
        // svg, symbol, image, marker, pattern, foreignObject { overflow: hidden }
        // https://www.w3.org/TR/SVG11/styling.html#UAStyleSheet
        [Attribute("overflow", SvgElement.SvgNamespace)]
        public string? Overflow
        {
            get => this.GetAttribute("overflow", false, "visible");
            set => this.SetAttribute("overflow", value);
        }

        [Attribute("pointer-events", SvgElement.SvgNamespace)]
        public string? PointerEvents
        {
            get => this.GetAttribute("pointer-events", true, "visiblePainted");
            set => this.SetAttribute("pointer-events", value);
        }

        [Attribute("shape-rendering", SvgElement.SvgNamespace)]
        public string? ShapeRendering
        {
            get => this.GetAttribute("shape-rendering", true, "auto");
            set => this.SetAttribute("shape-rendering", value);
        }

        [Attribute("stop-color", SvgElement.SvgNamespace)]
        public string? StopColor
        {
            get => this.GetAttribute("stop-color", false, "black");
            set => this.SetAttribute("stop-color", value);
        }

        [Attribute("stop-opacity", SvgElement.SvgNamespace)]
        public string? StopOpacity
        {
            get => this.GetAttribute("stop-opacity", false, "1");
            set => this.SetAttribute("stop-opacity", value);
        }

        [Attribute("stroke", SvgElement.SvgNamespace)]
        public string? Stroke
        {
            get => this.GetAttribute("stroke", true, "none");
            set => this.SetAttribute("stroke", value);
        }

        [Attribute("stroke-dasharray", SvgElement.SvgNamespace)]
        public string? StrokeDasharray
        {
            get => this.GetAttribute("stroke-dasharray", true, "none");
            set => this.SetAttribute("stroke-dasharray", value);
        }

        [Attribute("stroke-dashoffset", SvgElement.SvgNamespace)]
        public string? StrokeDashoffset
        {
            get => this.GetAttribute("stroke-dashoffset", true, "0");
            set => this.SetAttribute("stroke-dashoffset", value);
        }

        [Attribute("stroke-linecap", SvgElement.SvgNamespace)]
        public string? StrokeLinecap
        {
            get => this.GetAttribute("stroke-linecap", true, "butt");
            set => this.SetAttribute("stroke-linecap", value);
        }

        [Attribute("stroke-linejoin", SvgElement.SvgNamespace)]
        public string? SrokeLinejoin
        {
            get => this.GetAttribute("stroke-linejoin", true, "miter");
            set => this.SetAttribute("stroke-linejoin", value);
        }

        [Attribute("stroke-miterlimit", SvgElement.SvgNamespace)]
        public string? StrokeMiterlimit
        {
            get => this.GetAttribute("stroke-miterlimit", true, "4");
            set => this.SetAttribute("stroke-miterlimit", value);
        }

        [Attribute("stroke-opacity", SvgElement.SvgNamespace)]
        public string? StrokeOpacity
        {
            get => this.GetAttribute("stroke-opacity", true, "1");
            set => this.SetAttribute("stroke-opacity", value);
        }

        [Attribute("stroke-width", SvgElement.SvgNamespace)]
        public string? StrokeWidth
        {
            get => this.GetAttribute("stroke-width", true, "1");
            set => this.SetAttribute("stroke-width", value);
        }

        [Attribute("text-anchor", SvgElement.SvgNamespace)]
        public string? TextAnchor
        {
            get => this.GetAttribute("text-anchor", true, "start");
            set => this.SetAttribute("text-anchor", value);
        }

        [Attribute("text-decoration", SvgElement.SvgNamespace)]
        public string? TextDecoration
        {
            get => this.GetAttribute("text-decoration", false, "none");
            set => this.SetAttribute("text-decoration", value);
        }

        [Attribute("text-rendering", SvgElement.SvgNamespace)]
        public string? TextRendering
        {
            get => this.GetAttribute("text-rendering", true, "auto");
            set => this.SetAttribute("text-rendering", value);
        }

        [Attribute("unicode-bidi", SvgElement.SvgNamespace)]
        public string? UnicodeBidi
        {
            get => this.GetAttribute("unicode-bidi", false, "normal");
            set => this.SetAttribute("unicode-bidi", value);
        }

        [Attribute("visibility", SvgElement.SvgNamespace)]
        public string? Visibility
        {
            get => this.GetAttribute("visibility", true, "visible");
            set => this.SetAttribute("visibility", value);
        }

        [Attribute("word-spacing", SvgElement.SvgNamespace)]
        public string? WordSpacing
        {
            get => this.GetAttribute("word-spacing", true, "normal");
            set => this.SetAttribute("word-spacing", value);
        }

        [Attribute("writing-mode", SvgElement.SvgNamespace)]
        public string? WritingMode
        {
            get => this.GetAttribute("writing-mode", true, "lr-tb");
            set => this.SetAttribute("writing-mode", value);
        }
    }
}
