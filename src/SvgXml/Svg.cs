using System.Collections.Generic;
using Xml;

namespace Svg
{
    public class CoreAttributeAttribute : AttributeAttribute
    {
        public CoreAttributeAttribute(string name) : base(name)
        {
        }
    }

    public class PresentationAttributeAttribute : AttributeAttribute
    {
        public PresentationAttributeAttribute(string name) : base(name)
        {
        }
    }

    public class ConditionalProcessingAttributeAttribute : AttributeAttribute
    {
        public ConditionalProcessingAttributeAttribute(string name) : base(name)
        {
        }
    }

    public class StyleAttributeAttribute : AttributeAttribute
    {
        public StyleAttributeAttribute(string name) : base(name)
        {
        }
    }

    public class RegularAttributeAttribute : AttributeAttribute
    {
        public RegularAttributeAttribute(string name) : base(name)
        {
        }
    }

    public interface ISvgCoreAttributes : IElement
    {
        [CoreAttribute("id")]
        public string? Id
        {
            get => GetAttribute("id");
            set => SetAttribute("id", value);
        }

        [CoreAttribute("base")]
        public string? Base
        {
            get => GetAttribute("base");
            set => SetAttribute("base", value);
        }

        [CoreAttribute("lang")]
        public string? Lang
        {
            get => GetAttribute("lang");
            set => SetAttribute("lang", value);
        }

        [CoreAttribute("space")]
        public string? Space
        {
            get => GetAttribute("space");
            set => SetAttribute("space", value);
        }
    }

    public interface ISvgPresentationAttributes : IElement
    {
        [PresentationAttribute("alignment-baseline")]
        public string? AlignmentBaseline
        {
            get => GetAttribute("alignment-baseline");
            set => SetAttribute("alignment-baseline", value);
        }

        [PresentationAttribute("baseline-shift")]
        public string? BaselineShift
        {
            get => GetAttribute("baseline-shift");
            set => SetAttribute("baseline-shift", value);
        }

        [PresentationAttribute("clip")]
        public string? Clip
        {
            get => GetAttribute("clip");
            set => SetAttribute("clip", value);
        }

        [PresentationAttribute("clip-path")]
        public string? ClipPath
        {
            get => GetAttribute("clip-path");
            set => SetAttribute("clip-path", value);
        }

        [PresentationAttribute("clip-rule")]
        public string? ClipRule
        {
            get => GetAttribute("clip-rule");
            set => SetAttribute("clip-rule", value);
        }

        [PresentationAttribute("color")]
        public string? Color
        {
            get => GetAttribute("color");
            set => SetAttribute("color", value);
        }

        [PresentationAttribute("color-interpolation")]
        public string? ColorInterpolation
        {
            get => GetAttribute("color-interpolation");
            set => SetAttribute("color-interpolation", value);
        }

        [PresentationAttribute("color-interpolation-filters")]
        public string? ColorInterpolationFilters
        {
            get => GetAttribute("color-interpolation-filters");
            set => SetAttribute("color-interpolation-filters", value);
        }

        [PresentationAttribute("color-profile")]
        public string? ColorProfile
        {
            get => GetAttribute("color-profile");
            set => SetAttribute("color-profile", value);
        }

        [PresentationAttribute("color-rendering")]
        public string? ColorRendering
        {
            get => GetAttribute("color-rendering");
            set => SetAttribute("color-rendering", value);
        }

        [PresentationAttribute("cursor")]
        public string? Cursor
        {
            get => GetAttribute("cursor");
            set => SetAttribute("cursor", value);
        }

        [PresentationAttribute("direction")]
        public string? Direction
        {
            get => GetAttribute("direction");
            set => SetAttribute("direction", value);
        }

        [PresentationAttribute("display")]
        public string? Display
        {
            get => GetAttribute("display");
            set => SetAttribute("display", value);
        }

        [PresentationAttribute("dominant-baseline")]
        public string? DominantBaseline
        {
            get => GetAttribute("dominant-baseline");
            set => SetAttribute("dominant-baseline", value);
        }

        [PresentationAttribute("enable-background")]
        public string? EnableBackground
        {
            get => GetAttribute("enable-background");
            set => SetAttribute("enable-background", value);
        }

        [PresentationAttribute("fill")]
        public string? Fill
        {
            get => GetAttribute("fill");
            set => SetAttribute("fill", value);
        }

        [PresentationAttribute("fill-opacity")]
        public string? FillOpacity
        {
            get => GetAttribute("fill-opacity");
            set => SetAttribute("fill-opacity", value);
        }

        [PresentationAttribute("fill-rule")]
        public string? FillRule
        {
            get => GetAttribute("fill-rule");
            set => SetAttribute("fill-rule", value);
        }

        [PresentationAttribute("filter")]
        public string? Filter
        {
            get => GetAttribute("filter");
            set => SetAttribute("filter", value);
        }

        [PresentationAttribute("flood-color")]
        public string? FloodColor
        {
            get => GetAttribute("flood-color");
            set => SetAttribute("flood-color", value);
        }

        [PresentationAttribute("flood-opacity")]
        public string? FloodOpacity
        {
            get => GetAttribute("flood-opacity");
            set => SetAttribute("flood-opacity", value);
        }

        [PresentationAttribute("font-family")]
        public string? FontFamily
        {
            get => GetAttribute("font-family");
            set => SetAttribute("font-family", value);
        }

        [PresentationAttribute("font-size")]
        public string? FontSize
        {
            get => GetAttribute("font-size");
            set => SetAttribute("font-size", value);
        }

        [PresentationAttribute("font-size-adjust")]
        public string? FontSizeAdjust
        {
            get => GetAttribute("font-size-adjust");
            set => SetAttribute("font-size-adjust", value);
        }

        [PresentationAttribute("font-stretch")]
        public string? FontStretch
        {
            get => GetAttribute("font-stretch");
            set => SetAttribute("font-stretch", value);
        }

        [PresentationAttribute("font-style")]
        public string? FontStyle
        {
            get => GetAttribute("font-style");
            set => SetAttribute("font-style", value);
        }

        [PresentationAttribute("font-variant")]
        public string? FontVariant
        {
            get => GetAttribute("font-variant");
            set => SetAttribute("font-variant", value);
        }

        [PresentationAttribute("font-weight")]
        public string? FontWeight
        {
            get => GetAttribute("font-weight");
            set => SetAttribute("font-weight", value);
        }

        [PresentationAttribute("glyph-orientation-horizontal")]
        public string? GlyphOrientationHorizontal
        {
            get => GetAttribute("glyph-orientation-horizontal");
            set => SetAttribute("glyph-orientation-horizontal", value);
        }

        [PresentationAttribute("glyph-orientation-vertical")]
        public string? GlyphOrientationVertical
        {
            get => GetAttribute("glyph-orientation-vertical");
            set => SetAttribute("glyph-orientation-vertical", value);
        }

        [PresentationAttribute("image-rendering")]
        public string? ImageRendering
        {
            get => GetAttribute("image-rendering");
            set => SetAttribute("image-rendering", value);
        }

        [PresentationAttribute("kerning")]
        public string? Kerning
        {
            get => GetAttribute("kerning");
            set => SetAttribute("kerning", value);
        }

        [PresentationAttribute("letter-spacing")]
        public string? LetterSpacing
        {
            get => GetAttribute("letter-spacing");
            set => SetAttribute("letter-spacing", value);
        }

        [PresentationAttribute("lighting-color")]
        public string? LightingColor
        {
            get => GetAttribute("lighting-color");
            set => SetAttribute("lighting-color", value);
        }

        [PresentationAttribute("marker-end")]
        public string? MarkerEnd
        {
            get => GetAttribute("marker-end");
            set => SetAttribute("marker-end", value);
        }

        [PresentationAttribute("marker-mid")]
        public string? MarkerMid
        {
            get => GetAttribute("marker-mid");
            set => SetAttribute("marker-mid", value);
        }

        [PresentationAttribute("marker-start")]
        public string? MarkerStart
        {
            get => GetAttribute("marker-start");
            set => SetAttribute("marker-start", value);
        }

        [PresentationAttribute("mask")]
        public string? Mask
        {
            get => GetAttribute("mask");
            set => SetAttribute("mask", value);
        }

        [PresentationAttribute("opacity")]
        public string? Opacity
        {
            get => GetAttribute("opacity");
            set => SetAttribute("opacity", value);
        }

        [PresentationAttribute("overflow")]
        public string? Overflow
        {
            get => GetAttribute("overflow");
            set => SetAttribute("overflow", value);
        }

        [PresentationAttribute("pointer-events")]
        public string? PointerEvents
        {
            get => GetAttribute("pointer-events");
            set => SetAttribute("pointer-events", value);
        }

        [PresentationAttribute("shape-rendering")]
        public string? ShapeRendering
        {
            get => GetAttribute("shape-rendering");
            set => SetAttribute("shape-rendering", value);
        }

        [PresentationAttribute("stop-color")]
        public string? StopColor
        {
            get => GetAttribute("stop-color");
            set => SetAttribute("stop-color", value);
        }

        [PresentationAttribute("stop-opacity")]
        public string? StopOpacity
        {
            get => GetAttribute("stop-opacity");
            set => SetAttribute("stop-opacity", value);
        }

        [PresentationAttribute("stroke")]
        public string? Stroke
        {
            get => GetAttribute("stroke");
            set => SetAttribute("stroke", value);
        }

        [PresentationAttribute("stroke-dasharray")]
        public string? StrokeDasharray
        {
            get => GetAttribute("stroke-dasharray");
            set => SetAttribute("stroke-dasharray", value);
        }

        [PresentationAttribute("stroke-dashoffset")]
        public string? StrokeDashoffset
        {
            get => GetAttribute("stroke-dashoffset");
            set => SetAttribute("stroke-dashoffset", value);
        }

        [PresentationAttribute("stroke-linecap")]
        public string? StrokeLinecap
        {
            get => GetAttribute("stroke-linecap");
            set => SetAttribute("stroke-linecap", value);
        }

        [PresentationAttribute("stroke-linejoin")]
        public string? SrokeLinejoin
        {
            get => GetAttribute("stroke-linejoin");
            set => SetAttribute("stroke-linejoin", value);
        }

        [PresentationAttribute("stroke-miterlimit")]
        public string? StrokeMiterlimit
        {
            get => GetAttribute("stroke-miterlimit");
            set => SetAttribute("stroke-miterlimit", value);
        }

        [PresentationAttribute("stroke-opacity")]
        public string? StrokeOpacity
        {
            get => GetAttribute("stroke-opacity");
            set => SetAttribute("stroke-opacity", value);
        }

        [PresentationAttribute("stroke-width")]
        public string? StrokeWidth
        {
            get => GetAttribute("stroke-width");
            set => SetAttribute("stroke-width", value);
        }

        [PresentationAttribute("text-anchor")]
        public string? TextAnchor
        {
            get => GetAttribute("text-anchor");
            set => SetAttribute("text-anchor", value);
        }

        [PresentationAttribute("text-decoration")]
        public string? TextDecoration
        {
            get => GetAttribute("text-decoration");
            set => SetAttribute("text-decoration", value);
        }

        [PresentationAttribute("text-rendering")]
        public string? TextRendering
        {
            get => GetAttribute("text-rendering");
            set => SetAttribute("text-rendering", value);
        }

        [PresentationAttribute("unicode-bidi")]
        public string? UnicodeBidi
        {
            get => GetAttribute("unicode-bidi");
            set => SetAttribute("unicode-bidi", value);
        }

        [PresentationAttribute("visibility")]
        public string? Visibility
        {
            get => GetAttribute("visibility");
            set => SetAttribute("visibility", value);
        }

        [PresentationAttribute("word-spacing")]
        public string? WordSpacing
        {
            get => GetAttribute("word-spacing");
            set => SetAttribute("word-spacing", value);
        }

        [PresentationAttribute("writing-mode")]
        public string? WritingMode
        {
            get => GetAttribute("writing-mode");
            set => SetAttribute("writing-mode", value);
        }
    }

    public interface ISvgConditionalProcessingAttributes : IElement
    {
        [ConditionalProcessingAttribute("requiredFeatures")]
        public string? RequiredFeatures
        {
            get => GetAttribute("requiredFeatures");
            set => SetAttribute("requiredFeatures", value);
        }

        [ConditionalProcessingAttribute("requiredExtensions")]
        public string? RequiredExtensions
        {
            get => GetAttribute("requiredExtensions");
            set => SetAttribute("requiredExtensions", value);
        }

        [ConditionalProcessingAttribute("systemLanguage")]
        public string? SystemLanguage
        {
            get => GetAttribute("systemLanguage");
            set => SetAttribute("systemLanguage", value);
        }
    }

    public interface ISvgStyleAttributes : IElement
    {
        [StyleAttribute("class")]
        public string? Class
        {
            get => GetAttribute("class");
            set => SetAttribute("class", value);
        }

        [StyleAttribute("style")]
        public string? Style
        {
            get => GetAttribute("style");
            set => SetAttribute("style", value);
        }
    }

    public static class SvgAttributes
    {
        public static ISet<string> s_coreElements = new HashSet<string>()
        {
            "a",
            "altGlyph",
            "altGlyphDef",
            "altGlyphItem",
            "animate",
            "animateColor",
            "animateMotion",
            "animateTransform",
            "circle",
            "clipPath",
            "color-profile",
            "cursor",
            "defs",
            "desc",
            "ellipse",
            "feBlend",
            "feColorMatrix",
            "feComponentTransfer",
            "feComposite",
            "feConvolveMatrix",
            "feDiffuseLighting",
            "feDisplacementMap",
            "feDistantLight",
            "feFlood",
            "feFuncA",
            "feFuncB",
            "feFuncG",
            "feFuncR",
            "feGaussianBlur",
            "feImage",
            "feMerge",
            "feMergeNode",
            "feMorphology",
            "feOffset",
            "fePointLight",
            "feSpecularLighting",
            "feSpotLight",
            "feTile",
            "feTurbulence",
            "filter",
            "font",
            "font-face",
            "font-face-format",
            "font-face-name",
            "font-face-src",
            "font-face-uri",
            "foreignObject",
            "g",
            "glyph",
            "glyphRef",
            "hkern",
            "image",
            "line",
            "linearGradient",
            "marker",
            "mask",
            "metadata",
            "missing-glyph",
            "mpath",
            "path",
            "pattern",
            "polygon",
            "polyline",
            "radialGradient",
            "rect",
            "script",
            "set",
            "stop",
            "style",
            "svg",
            "switch",
            "symbol",
            "text",
            "textPath",
            "title",
            "tref",
            "tspan",
            "use",
            "view",
            "vkern"
        };

        public static ISet<string> s_conditionalProcessingElements = new HashSet<string>()
        {
            "a",
            "altGlyph",
            "animate",
            "animateColor",
            "animateMotion",
            "animateTransform",
            "circle",
            "clipPath",
            "cursor",
            "defs",
            "ellipse",
            "foreignObject",
            "g",
            "image",
            "line",
            "mask",
            "path",
            "pattern",
            "polygon",
            "polyline",
            "rect",
            "set",
            "svg",
            "switch",
            "text",
            "textPath",
            "tref",
            "tspan",
            "use"
        };

        public static ISet<string> s_presentationElements = new HashSet<string>()
        {
            "a",
            "altGlyph",
            "animate",
            "animateColor",
            "circle",
            "clipPath",
            "defs",
            "ellipse",
            "feBlend",
            "feColorMatrix",
            "feComponentTransfer",
            "feComposite",
            "feConvolveMatrix",
            "feDiffuseLighting",
            "feDisplacementMap",
            "feFlood",
            "feGaussianBlur",
            "feImage",
            "feMerge",
            "feMorphology",
            "feOffset",
            "feSpecularLighting",
            "feTile",
            "feTurbulence",
            "filter",
            "font",
            "foreignObject",
            "g",
            "glyph",
            "glyphRef",
            "image",
            "line",
            "linearGradient",
            "marker",
            "mask",
            "missing-glyph",
            "path",
            "pattern",
            "polygon",
            "polyline",
            "radialGradient",
            "rect",
            "stop",
            "svg",
            "switch",
            "symbol",
            "text",
            "textPath",
            "tref",
            "tspan",
            "use"
        };

        public static ISet<string> s_styleElements = new HashSet<string>()
        {
            "a",
            "altGlyph",
            "circle",
            "clipPath",
            "defs",
            "desc",
            "ellipse",
            "feBlend",
            "feColorMatrix",
            "feComponentTransfer",
            "feComposite",
            "feConvolveMatrix",
            "feDiffuseLighting",
            "feDisplacementMap",
            "feFlood",
            "feGaussianBlur",
            "feImage",
            "feMerge",
            "feMorphology",
            "feOffset",
            "feSpecularLighting",
            "feTile",
            "feTurbulence",
            "filter",
            "font",
            "foreignObject",
            "g",
            "glyph",
            "glyphRef",
            "image",
            "line",
            "linearGradient",
            "marker",
            "mask",
            "missing-glyph",
            "path",
            "pattern",
            "polygon",
            "polyline",
            "radialGradient",
            "rect",
            "stop",
            "svg",
            "switch",
            "symbol",
            "text",
            "textPath",
            "title",
            "tref",
            "tspan",
            "use"
        };
    }

    public abstract class SvgElement : Element, ISvgCoreAttributes
    {

    }

    public class SvgDocument : SvgFragment
    {
    }

    public class SvgElementFactory : IElementFactory
    {
        public ISet<string> Namespaces { get; } = new HashSet<string>
        {
            "http://www.w3.org/2000/svg",
            "http://www.w3.org/1999/xlink",
            "http://www.w3.org/XML/1998/namespace"
        };

        public Element Create(string name)
        {
            return name switch
            {
                "style" => new SvgStyle() { Name = name },
                // Animation
                "animate" => new SvgAnimate() { Name = name },
                "animateColor" => new SvgAnimateColor() { Name = name },
                "animateMotion" => new SvgAnimateMotion() { Name = name },
                "animateTransform" => new SvgAnimateTransform() { Name = name },
                "set" => new SvgSet() { Name = name },
                "mpath" => new SvgMotionPath() { Name = name },
                // Basic Shapes
                "circle" => new SvgCircle() { Name = name },
                "ellipse" => new SvgEllipse() { Name = name },
                "line" => new SvgLine() { Name = name },
                "polygon" => new SvgPolygon() { Name = name },
                "polyline" => new SvgPolyline() { Name = name },
                "rect" => new SvgRectangle() { Name = name },
                // Clipping and Masking
                "clipPath" => new SvgClipPath() { Name = name },
                "mask" => new SvgMask() { Name = name },
                // Color
                "color-profile" => new SvgColorProfile() { Name = name },
                // Document Structure
                "defs" => new SvgDefinitionList() { Name = name },
                "desc" => new SvgDescription() { Name = name },
                "metadata" => new SvgDocumentMetadata() { Name = name },
                "svg" => new SvgFragment() { Name = name },
                "g" => new SvgGroup() { Name = name },
                "image" => new SvgImage() { Name = name },
                "switch" => new SvgSwitch() { Name = name },
                "symbol" => new SvgSymbol() { Name = name },
                "title" => new SvgTitle() { Name = name },
                "use" => new SvgUse() { Name = name },
                // Extensibility
                "foreignObject" => new SvgForeignObject() { Name = name },
                // Filter Effects
                "filter" => new FilterEffects.SvgFilter() { Name = name },
                "feBlend" => new FilterEffects.SvgBlend() { Name = name },
                "feColorMatrix" => new FilterEffects.SvgColourMatrix() { Name = name },
                "feComponentTransfer" => new FilterEffects.SvgComponentTransfer() { Name = name },
                "feComposite" => new FilterEffects.SvgComposite() { Name = name },
                "feConvolveMatrix" => new FilterEffects.SvgConvolveMatrix() { Name = name },
                "feDiffuseLighting" => new FilterEffects.SvgDiffuseLighting() { Name = name },
                "feDisplacementMap" => new FilterEffects.SvgDisplacementMap() { Name = name },
                "feDistantLight" => new FilterEffects.SvgDistantLight() { Name = name },
                "feFlood" => new FilterEffects.SvgFlood() { Name = name },
                "feFuncA" => new FilterEffects.SvgFuncA() { Name = name },
                "feFuncB" => new FilterEffects.SvgFuncB() { Name = name },
                "feFuncG" => new FilterEffects.SvgFuncG() { Name = name },
                "feFuncR" => new FilterEffects.SvgFuncR() { Name = name },
                "feGaussianBlur" => new FilterEffects.SvgGaussianBlur() { Name = name },
                "feImage" => new FilterEffects.SvgImage() { Name = name },
                "feMerge" => new FilterEffects.SvgMerge() { Name = name },
                "feMergeNode" => new FilterEffects.SvgMergeNode() { Name = name },
                "feMorphology" => new FilterEffects.SvgMorphology() { Name = name },
                "feOffset" => new FilterEffects.SvgOffset() { Name = name },
                "fePointLight" => new FilterEffects.SvgPointLight() { Name = name },
                "feSpecularLighting" => new FilterEffects.SvgSpecularLighting() { Name = name },
                "feSpotLight" => new FilterEffects.SvgSpotLight() { Name = name },
                "feTile" => new FilterEffects.SvgTile() { Name = name },
                "feTurbulence" => new FilterEffects.SvgTurbulence() { Name = name },
                // Interactivity
                "cursor" => new SvgCursor() { Name = name },
                // Linking
                "a" => new SvgAnchor() { Name = name },
                "view" => new SvgView() { Name = name },
                // Painting
                "stop" => new SvgGradientStop() { Name = name },
                "linearGradient" => new SvgLinearGradientServer() { Name = name },
                "marker" => new SvgMarker() { Name = name },
                "pattern" => new SvgPatternServer() { Name = name },
                "radialGradient" => new SvgRadialGradientServer() { Name = name },
                // Paths
                "path" => new SvgPath() { Name = name },
                // Scripting
                "script" => new SvgScript() { Name = name },
                // Text
                "altGlyph" => new SvgAltGlyph() { Name = name },
                "altGlyphDef" => new SvgAltGlyphDef() { Name = name },
                "altGlyphItem" => new SvgAltGlyphItem() { Name = name },
                "font" => new SvgFont() { Name = name },
                "font-face" => new SvgFontFace() { Name = name },
                "font-face-format" => new SvgFontFaceFormat() { Name = name },
                "font-face-name" => new SvgFontFaceName() { Name = name },
                "font-face-src" => new SvgFontFaceSrc() { Name = name },
                "font-face-uri" => new SvgFontFaceUri() { Name = name },
                "glyph" => new SvgGlyph() { Name = name },
                "glyphRef" => new SvgGlyphRef() { Name = name },
                "hkern" => new SvgHorizontalKern() { Name = name },
                "vkern" => new SvgVerticalKern() { Name = name },
                "missing-glyph" => new SvgMissingGlyph() { Name = name },
                "text" => new SvgText() { Name = name },
                "textPath" => new SvgTextPath() { Name = name },
                "tref" => new SvgTextRef() { Name = name },
                "tspan" => new SvgTextSpan() { Name = name },
                // Unknown
                _ => new UnknownElement() { Name = name }
            };
        }
    }

    [Element("style")]
    public class SvgStyle : SvgElement
    {
    }

    // Animation

    public abstract class SvgAnimationElement : SvgElement
    {
    }

    [Element("animate")]
    public class SvgAnimate : SvgAnimationElement, ISvgPresentationAttributes, ISvgConditionalProcessingAttributes
    {
    }

    [Element("animateColor")]
    public class SvgAnimateColor : SvgAnimationElement, ISvgPresentationAttributes, ISvgConditionalProcessingAttributes
    {
    }

    [Element("animateMotion")]
    public class SvgAnimateMotion : SvgAnimationElement, ISvgConditionalProcessingAttributes
    {
    }

    [Element("animateTransform")]
    public class SvgAnimateTransform : SvgAnimationElement, ISvgConditionalProcessingAttributes
    {
    }

    [Element("set")]
    public class SvgSet : SvgAnimationElement, ISvgConditionalProcessingAttributes
    {
    }

    [Element("mpath")]
    public class SvgMotionPath : SvgElement
    {
    }

    // Basic Shapes

    public abstract class SvgVisualElement : SvgElement
    {
    }

    public abstract class SvgPathBasedElement : SvgVisualElement
    {
    }

    public abstract class SvgMarkerElement : SvgPathBasedElement
    {
    }

    [Element("circle")]
    public class SvgCircle : SvgPathBasedElement, ISvgPresentationAttributes, ISvgConditionalProcessingAttributes, ISvgStyleAttributes
    {
    }

    [Element("ellipse")]
    public class SvgEllipse : SvgPathBasedElement, ISvgPresentationAttributes, ISvgConditionalProcessingAttributes, ISvgStyleAttributes
    {
    }

    [Element("line")]
    public class SvgLine : SvgMarkerElement, ISvgPresentationAttributes, ISvgConditionalProcessingAttributes, ISvgStyleAttributes
    {
    }

    [Element("polygon")]
    public class SvgPolygon : SvgMarkerElement, ISvgPresentationAttributes, ISvgConditionalProcessingAttributes, ISvgStyleAttributes
    {
    }

    [Element("polyline")]
    public class SvgPolyline : SvgPolygon, ISvgPresentationAttributes, ISvgConditionalProcessingAttributes, ISvgStyleAttributes
    {
    }

    [Element("rect")]
    public class SvgRectangle : SvgPathBasedElement, ISvgPresentationAttributes, ISvgConditionalProcessingAttributes, ISvgStyleAttributes
    {
    }

    // Clipping and Masking

    [Element("clipPath")]
    public class SvgClipPath : SvgElement, ISvgPresentationAttributes, ISvgConditionalProcessingAttributes, ISvgStyleAttributes
    {
    }

    [Element("mask")]
    public class SvgMask : SvgElement, ISvgPresentationAttributes, ISvgConditionalProcessingAttributes, ISvgStyleAttributes
    {
    }

    // Color

    [Element("color-profile")]
    public class SvgColorProfile : SvgElement
    {
    }

    // Document Structure

    [Element("defs")]
    public class SvgDefinitionList : SvgElement, ISvgPresentationAttributes, ISvgConditionalProcessingAttributes, ISvgStyleAttributes
    {
    }

    [Element("desc")]
    public class SvgDescription : SvgElement, ISvgStyleAttributes
    {
    }

    [Element("metadata")]
    public class SvgDocumentMetadata : SvgElement
    {
    }

    [Element("svg")]
    public class SvgFragment : SvgElement, ISvgPresentationAttributes, ISvgConditionalProcessingAttributes, ISvgStyleAttributes
    {
    }

    [Element("g")]
    public class SvgGroup : SvgMarkerElement, ISvgPresentationAttributes, ISvgConditionalProcessingAttributes, ISvgStyleAttributes
    {
    }

    [Element("image")]
    public class SvgImage : SvgVisualElement, ISvgPresentationAttributes, ISvgConditionalProcessingAttributes, ISvgStyleAttributes
    {
    }

    [Element("switch")]
    public class SvgSwitch : SvgVisualElement, ISvgPresentationAttributes, ISvgConditionalProcessingAttributes, ISvgStyleAttributes
    {
    }

    [Element("symbol")]
    public class SvgSymbol : SvgVisualElement, ISvgPresentationAttributes, ISvgStyleAttributes
    {
    }

    [Element("title")]
    public class SvgTitle : SvgElement, ISvgStyleAttributes
    {
    }

    [Element("use")]
    public class SvgUse : SvgVisualElement, ISvgPresentationAttributes, ISvgConditionalProcessingAttributes, ISvgStyleAttributes
    {
    }

    // Extensibility

    [Element("foreignObject")]
    public class SvgForeignObject : SvgVisualElement, ISvgPresentationAttributes, ISvgConditionalProcessingAttributes, ISvgStyleAttributes
    {
    }

    // Filter Effects

    namespace FilterEffects
    {
        [Element("filter")]
        public class SvgFilter : SvgElement, ISvgPresentationAttributes, ISvgStyleAttributes
        {
        }

        public abstract class SvgFilterPrimitive : SvgElement
        {
        }

        [Element("feBlend")]
        public class SvgBlend : SvgFilterPrimitive, ISvgPresentationAttributes, ISvgStyleAttributes
        {
        }

        [Element("feColorMatrix")]
        public class SvgColourMatrix : SvgFilterPrimitive, ISvgPresentationAttributes, ISvgStyleAttributes
        {
        }

        [Element("feComponentTransfer")]
        public class SvgComponentTransfer : SvgFilterPrimitive, ISvgPresentationAttributes, ISvgStyleAttributes
        {
        }

        [Element("feComposite")]
        public class SvgComposite : SvgFilterPrimitive, ISvgPresentationAttributes, ISvgStyleAttributes
        {
        }

        [Element("feConvolveMatrix")]
        public class SvgConvolveMatrix : SvgFilterPrimitive, ISvgPresentationAttributes, ISvgStyleAttributes
        {
        }

        [Element("feDiffuseLighting")]
        public class SvgDiffuseLighting : SvgFilterPrimitive, ISvgPresentationAttributes, ISvgStyleAttributes
        {
        }

        [Element("feDisplacementMap")]
        public class SvgDisplacementMap : SvgFilterPrimitive, ISvgPresentationAttributes, ISvgStyleAttributes
        {
        }

        [Element("feDistantLight")]
        public class SvgDistantLight : SvgElement
        {
        }

        [Element("feFlood")]
        public class SvgFlood : SvgFilterPrimitive, ISvgPresentationAttributes, ISvgStyleAttributes
        {
        }

        public abstract class SvgComponentTransferFunction : SvgElement
        {
        }

        [Element("feFuncA")]
        public class SvgFuncA : SvgComponentTransferFunction
        {
        }

        [Element("feFuncB")]
        public class SvgFuncB : SvgComponentTransferFunction
        {
        }

        [Element("feFuncG")]
        public class SvgFuncG : SvgComponentTransferFunction
        {
        }

        [Element("feFuncR")]
        public class SvgFuncR : SvgComponentTransferFunction
        {
        }

        [Element("feGaussianBlur")]
        public class SvgGaussianBlur : SvgFilterPrimitive, ISvgPresentationAttributes, ISvgStyleAttributes
        {
        }

        [Element("feImage")]
        public class SvgImage : SvgFilterPrimitive, ISvgPresentationAttributes, ISvgStyleAttributes
        {
        }

        [Element("feMerge")]
        public class SvgMerge : SvgFilterPrimitive, ISvgPresentationAttributes, ISvgStyleAttributes
        {
        }

        [Element("feMergeNode")]
        public class SvgMergeNode : SvgElement
        {
        }

        [Element("feMorphology")]
        public class SvgMorphology : SvgFilterPrimitive, ISvgPresentationAttributes, ISvgStyleAttributes
        {
        }

        [Element("feOffset")]
        public class SvgOffset : SvgFilterPrimitive, ISvgPresentationAttributes, ISvgStyleAttributes
        {
        }

        [Element("fePointLight")]
        public class SvgPointLight : SvgElement
        {
        }

        [Element("feSpecularLighting")]
        public class SvgSpecularLighting : SvgFilterPrimitive, ISvgPresentationAttributes, ISvgStyleAttributes
        {
        }

        [Element("feSpotLight")]
        public class SvgSpotLight : SvgElement
        {
        }

        [Element("feTile")]
        public class SvgTile : SvgFilterPrimitive, ISvgPresentationAttributes, ISvgStyleAttributes
        {
        }

        [Element("feTurbulence")]
        public class SvgTurbulence : SvgFilterPrimitive, ISvgPresentationAttributes, ISvgStyleAttributes
        {
        }
    }

    // Interactivity

    [Element("cursor")]
    public class SvgCursor : SvgElement, ISvgConditionalProcessingAttributes
    {
    }

    // Linking

    [Element("a")]
    public class SvgAnchor : SvgElement, ISvgPresentationAttributes, ISvgConditionalProcessingAttributes, ISvgStyleAttributes
    {
    }

    [Element("view")]
    public class SvgView : SvgElement
    {
    }

    // Painting

    public abstract class SvgPaintServer : SvgElement
    {
    }

    public class SvgColourServer : SvgPaintServer
    {
    }

    public class SvgDeferredPaintServer : SvgPaintServer
    {
    }

    public abstract class SvgGradientServer : SvgPaintServer
    {
    }

    [Element("stop")]
    public class SvgGradientStop : SvgElement, ISvgPresentationAttributes, ISvgStyleAttributes
    {
    }

    [Element("linearGradient")]
    public class SvgLinearGradientServer : SvgGradientServer, ISvgPresentationAttributes, ISvgStyleAttributes
    {
    }

    [Element("marker")]
    public class SvgMarker : SvgPathBasedElement, ISvgPresentationAttributes, ISvgStyleAttributes
    {
    }

    [Element("pattern")]
    public class SvgPatternServer : SvgPaintServer, ISvgPresentationAttributes, ISvgConditionalProcessingAttributes, ISvgStyleAttributes
    {
    }

    [Element("radialGradient")]
    public class SvgRadialGradientServer : SvgGradientServer, ISvgPresentationAttributes, ISvgStyleAttributes
    {

    }

    // Paths

    [Element("path")]
    public class SvgPath : SvgMarkerElement, ISvgPresentationAttributes, ISvgConditionalProcessingAttributes, ISvgStyleAttributes
    {
    }

    // Scripting

    [Element("script")]
    public class SvgScript : SvgElement
    {
    }

    // Text

    [Element("altGlyph")]
    public class SvgAltGlyph : SvgElement, ISvgPresentationAttributes, ISvgConditionalProcessingAttributes, ISvgStyleAttributes
    {
    }

    [Element("altGlyphDef")]
    public class SvgAltGlyphDef : SvgElement
    {
    }

    [Element("altGlyphItem")]
    public class SvgAltGlyphItem : SvgElement
    {
    }

    [Element("font")]
    public class SvgFont : SvgElement, ISvgPresentationAttributes, ISvgStyleAttributes
    {
    }

    [Element("font-face")]
    public class SvgFontFace : SvgElement
    {
    }

    [Element("font-face-format")]
    public class SvgFontFaceFormat : SvgElement
    {
    }

    [Element("font-face-name")]
    public class SvgFontFaceName : SvgElement
    {
    }

    [Element("font-face-src")]
    public class SvgFontFaceSrc : SvgElement
    {
    }

    [Element("font-face-uri")]
    public class SvgFontFaceUri : SvgElement
    {
    }

    [Element("glyph")]
    public class SvgGlyph : SvgPathBasedElement, ISvgPresentationAttributes, ISvgStyleAttributes
    {
    }

    [Element("glyphRef")]
    public class SvgGlyphRef : SvgElement, ISvgPresentationAttributes, ISvgStyleAttributes
    {
    }

    public abstract class SvgKern : Element
    {
    }

    [Element("hkern")]
    public class SvgHorizontalKern : SvgKern
    {
    }

    [Element("vkern")]
    public class SvgVerticalKern : SvgKern
    {
    }

    [Element("missing-glyph")]
    public class SvgMissingGlyph : SvgGlyph, ISvgPresentationAttributes, ISvgStyleAttributes
    {
    }

    public abstract class SvgTextBase : SvgVisualElement
    {
    }

    [Element("text")]
    public class SvgText : SvgTextBase, ISvgPresentationAttributes, ISvgConditionalProcessingAttributes, ISvgStyleAttributes
    {
    }

    [Element("textPath")]
    public class SvgTextPath : SvgTextBase, ISvgPresentationAttributes, ISvgConditionalProcessingAttributes, ISvgStyleAttributes
    {
    }

    [Element("tref")]
    public class SvgTextRef : SvgTextBase, ISvgPresentationAttributes, ISvgConditionalProcessingAttributes, ISvgStyleAttributes
    {
    }

    [Element("tspan")]
    public class SvgTextSpan : SvgTextBase, ISvgPresentationAttributes, ISvgConditionalProcessingAttributes, ISvgStyleAttributes
    {
    }
}
