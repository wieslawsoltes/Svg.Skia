using System;
using Xml;

namespace Svg
{
    public interface ISvgPresentationAttributes : IElement
    {
        [Attribute("alignment-baseline", SvgAttributes.SvgNamespace)]
        public string? AlignmentBaseline
        {
            get => GetAttribute("alignment-baseline");
            set => SetAttribute("alignment-baseline", value);
        }

        [Attribute("baseline-shift", SvgAttributes.SvgNamespace)]
        public string? BaselineShift
        {
            get => GetAttribute("baseline-shift");
            set => SetAttribute("baseline-shift", value);
        }

        [Attribute("clip", SvgAttributes.SvgNamespace)]
        public string? Clip
        {
            get => GetAttribute("clip");
            set => SetAttribute("clip", value);
        }

        [Attribute("clip-path", SvgAttributes.SvgNamespace)]
        public string? ClipPath
        {
            get => GetAttribute("clip-path");
            set => SetAttribute("clip-path", value);
        }

        [Attribute("clip-rule", SvgAttributes.SvgNamespace)]
        public string? ClipRule
        {
            get => GetAttribute("clip-rule");
            set => SetAttribute("clip-rule", value);
        }

        [Attribute("color", SvgAttributes.SvgNamespace)]
        public string? Color
        {
            get => GetAttribute("color");
            set => SetAttribute("color", value);
        }

        [Attribute("color-interpolation", SvgAttributes.SvgNamespace)]
        public string? ColorInterpolation
        {
            get => GetAttribute("color-interpolation");
            set => SetAttribute("color-interpolation", value);
        }

        [Attribute("color-interpolation-filters", SvgAttributes.SvgNamespace)]
        public string? ColorInterpolationFilters
        {
            get => GetAttribute("color-interpolation-filters");
            set => SetAttribute("color-interpolation-filters", value);
        }

        [Attribute("color-profile", SvgAttributes.SvgNamespace)]
        public string? ColorProfile
        {
            get => GetAttribute("color-profile");
            set => SetAttribute("color-profile", value);
        }

        [Attribute("color-rendering", SvgAttributes.SvgNamespace)]
        public string? ColorRendering
        {
            get => GetAttribute("color-rendering");
            set => SetAttribute("color-rendering", value);
        }

        [Attribute("cursor", SvgAttributes.SvgNamespace)]
        public string? Cursor
        {
            get => GetAttribute("cursor");
            set => SetAttribute("cursor", value);
        }

        [Attribute("direction", SvgAttributes.SvgNamespace)]
        public string? Direction
        {
            get => GetAttribute("direction");
            set => SetAttribute("direction", value);
        }

        [Attribute("display", SvgAttributes.SvgNamespace)]
        public string? Display
        {
            get => GetAttribute("display");
            set => SetAttribute("display", value);
        }

        [Attribute("dominant-baseline", SvgAttributes.SvgNamespace)]
        public string? DominantBaseline
        {
            get => GetAttribute("dominant-baseline");
            set => SetAttribute("dominant-baseline", value);
        }

        [Attribute("enable-background", SvgAttributes.SvgNamespace)]
        public string? EnableBackground
        {
            get => GetAttribute("enable-background");
            set => SetAttribute("enable-background", value);
        }

        [Attribute("fill", SvgAttributes.SvgNamespace)]
        public string? Fill
        {
            get => GetAttribute("fill");
            set => SetAttribute("fill", value);
        }

        [Attribute("fill-opacity", SvgAttributes.SvgNamespace)]
        public string? FillOpacity
        {
            get => GetAttribute("fill-opacity");
            set => SetAttribute("fill-opacity", value);
        }

        [Attribute("fill-rule", SvgAttributes.SvgNamespace)]
        public string? FillRule
        {
            get => GetAttribute("fill-rule");
            set => SetAttribute("fill-rule", value);
        }

        [Attribute("filter", SvgAttributes.SvgNamespace)]
        public string? Filter
        {
            get => GetAttribute("filter");
            set => SetAttribute("filter", value);
        }

        [Attribute("flood-color", SvgAttributes.SvgNamespace)]
        public string? FloodColor
        {
            get => GetAttribute("flood-color");
            set => SetAttribute("flood-color", value);
        }

        [Attribute("flood-opacity", SvgAttributes.SvgNamespace)]
        public string? FloodOpacity
        {
            get => GetAttribute("flood-opacity");
            set => SetAttribute("flood-opacity", value);
        }

        [Attribute("font-family", SvgAttributes.SvgNamespace)]
        public string? FontFamily
        {
            get => GetAttribute("font-family");
            set => SetAttribute("font-family", value);
        }

        [Attribute("font-size", SvgAttributes.SvgNamespace)]
        public string? FontSize
        {
            get => GetAttribute("font-size");
            set => SetAttribute("font-size", value);
        }

        [Attribute("font-size-adjust", SvgAttributes.SvgNamespace)]
        public string? FontSizeAdjust
        {
            get => GetAttribute("font-size-adjust");
            set => SetAttribute("font-size-adjust", value);
        }

        [Attribute("font-stretch", SvgAttributes.SvgNamespace)]
        public string? FontStretch
        {
            get => GetAttribute("font-stretch");
            set => SetAttribute("font-stretch", value);
        }

        [Attribute("font-style", SvgAttributes.SvgNamespace)]
        public string? FontStyle
        {
            get => GetAttribute("font-style");
            set => SetAttribute("font-style", value);
        }

        [Attribute("font-variant", SvgAttributes.SvgNamespace)]
        public string? FontVariant
        {
            get => GetAttribute("font-variant");
            set => SetAttribute("font-variant", value);
        }

        [Attribute("font-weight", SvgAttributes.SvgNamespace)]
        public string? FontWeight
        {
            get => GetAttribute("font-weight");
            set => SetAttribute("font-weight", value);
        }

        [Attribute("glyph-orientation-horizontal", SvgAttributes.SvgNamespace)]
        public string? GlyphOrientationHorizontal
        {
            get => GetAttribute("glyph-orientation-horizontal");
            set => SetAttribute("glyph-orientation-horizontal", value);
        }

        [Attribute("glyph-orientation-vertical", SvgAttributes.SvgNamespace)]
        public string? GlyphOrientationVertical
        {
            get => GetAttribute("glyph-orientation-vertical");
            set => SetAttribute("glyph-orientation-vertical", value);
        }

        [Attribute("image-rendering", SvgAttributes.SvgNamespace)]
        public string? ImageRendering
        {
            get => GetAttribute("image-rendering");
            set => SetAttribute("image-rendering", value);
        }

        [Attribute("kerning", SvgAttributes.SvgNamespace)]
        public string? Kerning
        {
            get => GetAttribute("kerning");
            set => SetAttribute("kerning", value);
        }

        [Attribute("letter-spacing", SvgAttributes.SvgNamespace)]
        public string? LetterSpacing
        {
            get => GetAttribute("letter-spacing");
            set => SetAttribute("letter-spacing", value);
        }

        [Attribute("lighting-color", SvgAttributes.SvgNamespace)]
        public string? LightingColor
        {
            get => GetAttribute("lighting-color");
            set => SetAttribute("lighting-color", value);
        }

        [Attribute("marker-end", SvgAttributes.SvgNamespace)]
        public string? MarkerEnd
        {
            get => GetAttribute("marker-end");
            set => SetAttribute("marker-end", value);
        }

        [Attribute("marker-mid", SvgAttributes.SvgNamespace)]
        public string? MarkerMid
        {
            get => GetAttribute("marker-mid");
            set => SetAttribute("marker-mid", value);
        }

        [Attribute("marker-start", SvgAttributes.SvgNamespace)]
        public string? MarkerStart
        {
            get => GetAttribute("marker-start");
            set => SetAttribute("marker-start", value);
        }

        [Attribute("mask", SvgAttributes.SvgNamespace)]
        public string? Mask
        {
            get => GetAttribute("mask");
            set => SetAttribute("mask", value);
        }

        [Attribute("opacity", SvgAttributes.SvgNamespace)]
        public string? Opacity
        {
            get => GetAttribute("opacity");
            set => SetAttribute("opacity", value);
        }

        [Attribute("overflow", SvgAttributes.SvgNamespace)]
        public string? Overflow
        {
            get => GetAttribute("overflow");
            set => SetAttribute("overflow", value);
        }

        [Attribute("pointer-events", SvgAttributes.SvgNamespace)]
        public string? PointerEvents
        {
            get => GetAttribute("pointer-events");
            set => SetAttribute("pointer-events", value);
        }

        [Attribute("shape-rendering", SvgAttributes.SvgNamespace)]
        public string? ShapeRendering
        {
            get => GetAttribute("shape-rendering");
            set => SetAttribute("shape-rendering", value);
        }

        [Attribute("stop-color", SvgAttributes.SvgNamespace)]
        public string? StopColor
        {
            get => GetAttribute("stop-color");
            set => SetAttribute("stop-color", value);
        }

        [Attribute("stop-opacity", SvgAttributes.SvgNamespace)]
        public string? StopOpacity
        {
            get => GetAttribute("stop-opacity");
            set => SetAttribute("stop-opacity", value);
        }

        [Attribute("stroke", SvgAttributes.SvgNamespace)]
        public string? Stroke
        {
            get => GetAttribute("stroke");
            set => SetAttribute("stroke", value);
        }

        [Attribute("stroke-dasharray", SvgAttributes.SvgNamespace)]
        public string? StrokeDasharray
        {
            get => GetAttribute("stroke-dasharray");
            set => SetAttribute("stroke-dasharray", value);
        }

        [Attribute("stroke-dashoffset", SvgAttributes.SvgNamespace)]
        public string? StrokeDashoffset
        {
            get => GetAttribute("stroke-dashoffset");
            set => SetAttribute("stroke-dashoffset", value);
        }

        [Attribute("stroke-linecap", SvgAttributes.SvgNamespace)]
        public string? StrokeLinecap
        {
            get => GetAttribute("stroke-linecap");
            set => SetAttribute("stroke-linecap", value);
        }

        [Attribute("stroke-linejoin", SvgAttributes.SvgNamespace)]
        public string? SrokeLinejoin
        {
            get => GetAttribute("stroke-linejoin");
            set => SetAttribute("stroke-linejoin", value);
        }

        [Attribute("stroke-miterlimit", SvgAttributes.SvgNamespace)]
        public string? StrokeMiterlimit
        {
            get => GetAttribute("stroke-miterlimit");
            set => SetAttribute("stroke-miterlimit", value);
        }

        [Attribute("stroke-opacity", SvgAttributes.SvgNamespace)]
        public string? StrokeOpacity
        {
            get => GetAttribute("stroke-opacity");
            set => SetAttribute("stroke-opacity", value);
        }

        [Attribute("stroke-width", SvgAttributes.SvgNamespace)]
        public string? StrokeWidth
        {
            get => GetAttribute("stroke-width");
            set => SetAttribute("stroke-width", value);
        }

        [Attribute("text-anchor", SvgAttributes.SvgNamespace)]
        public string? TextAnchor
        {
            get => GetAttribute("text-anchor");
            set => SetAttribute("text-anchor", value);
        }

        [Attribute("text-decoration", SvgAttributes.SvgNamespace)]
        public string? TextDecoration
        {
            get => GetAttribute("text-decoration");
            set => SetAttribute("text-decoration", value);
        }

        [Attribute("text-rendering", SvgAttributes.SvgNamespace)]
        public string? TextRendering
        {
            get => GetAttribute("text-rendering");
            set => SetAttribute("text-rendering", value);
        }

        [Attribute("unicode-bidi", SvgAttributes.SvgNamespace)]
        public string? UnicodeBidi
        {
            get => GetAttribute("unicode-bidi");
            set => SetAttribute("unicode-bidi", value);
        }

        [Attribute("visibility", SvgAttributes.SvgNamespace)]
        public string? Visibility
        {
            get => GetAttribute("visibility");
            set => SetAttribute("visibility", value);
        }

        [Attribute("word-spacing", SvgAttributes.SvgNamespace)]
        public string? WordSpacing
        {
            get => GetAttribute("word-spacing");
            set => SetAttribute("word-spacing", value);
        }

        [Attribute("writing-mode", SvgAttributes.SvgNamespace)]
        public string? WritingMode
        {
            get => GetAttribute("writing-mode");
            set => SetAttribute("writing-mode", value);
        }
    }
}
