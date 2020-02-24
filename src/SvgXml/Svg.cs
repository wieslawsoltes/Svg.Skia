using System;
using System.Collections.Generic;

namespace Svg
{
    // ------------------------------------------------------------------------
    // Xml
    // ------------------------------------------------------------------------

    [AttributeUsage(AttributeTargets.Class)]
    public sealed class ElementAttribute : Attribute
    {
        public string Name { get; private set; }

        public ElementAttribute(string name)
        {
            Name = name;
        }
    }

    public abstract class Element
    {
        public string Name { get; set; }
        public string Text { get; set; }
        public List<Element> Children { get; set; }
        public Dictionary<string, string> Attributes { get; set; }

        public Element()
        {
            Name = string.Empty;
            Text = string.Empty;
            Children = new List<Element>();
            Attributes = new Dictionary<string, string>();
        }
    }

    public class UnknownElement : Element
    {
    }

    // ------------------------------------------------------------------------
    // Svg
    // ------------------------------------------------------------------------

    public abstract class SvgElement : Element
    {
    }

    public class SvgDocument : SvgFragment
    {
    }

    public class SvgElementFactory
    {
        public static Element Create(string name, string ns)
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

    // ------------------------------------------------------------------------
    // Animation
    // ------------------------------------------------------------------------

    public abstract class SvgAnimationElement : SvgElement
    {
    }

    [Element("animate")]
    public class SvgAnimate : SvgAnimationElement
    {
    }

    [Element("animateColor")]
    public class SvgAnimateColor : SvgAnimationElement
    {
    }

    [Element("animateMotion")]
    public class SvgAnimateMotion : SvgAnimationElement
    {
    }

    [Element("animateTransform")]
    public class SvgAnimateTransform : SvgAnimationElement
    {
    }

    [Element("set")]
    public class SvgSet : SvgAnimationElement
    {
    }

    [Element("mpath")]
    public class SvgMotionPath : SvgElement
    {
    }

    // ------------------------------------------------------------------------
    // Basic Shapes
    // ------------------------------------------------------------------------

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
    public class SvgCircle : SvgPathBasedElement
    {
    }

    [Element("ellipse")]
    public class SvgEllipse : SvgPathBasedElement
    {
    }

    [Element("line")]
    public class SvgLine : SvgMarkerElement
    {
    }

    [Element("polygon")]
    public class SvgPolygon : SvgMarkerElement
    {
    }

    [Element("polyline")]
    public class SvgPolyline : SvgPolygon
    {
    }

    [Element("rect")]
    public class SvgRectangle : SvgPathBasedElement
    {
    }

    // ------------------------------------------------------------------------
    // Clipping and Masking
    // ------------------------------------------------------------------------

    [Element("clipPath")]
    public class SvgClipPath : SvgElement
    {
    }

    [Element("mask")]
    public class SvgMask : SvgElement
    {
    }

    // ------------------------------------------------------------------------
    // Color
    // ------------------------------------------------------------------------

    [Element("color-profile")]
    public class SvgColorProfile : SvgElement
    {
    }

    // ------------------------------------------------------------------------
    // Document Structure
    // ------------------------------------------------------------------------

    [Element("defs")]
    public class SvgDefinitionList : SvgElement
    {
    }

    [Element("desc")]
    public class SvgDescription : SvgElement
    {
    }

    [Element("metadata")]
    public class SvgDocumentMetadata : SvgElement
    {
    }

    [Element("svg")]
    public class SvgFragment : SvgElement
    {
    }

    [Element("g")]
    public class SvgGroup : SvgMarkerElement
    {
    }

    [Element("image")]
    public class SvgImage : SvgVisualElement
    {
    }

    [Element("switch")]
    public class SvgSwitch : SvgVisualElement
    {
    }

    [Element("symbol")]
    public class SvgSymbol : SvgVisualElement
    {
    }

    [Element("title")]
    public class SvgTitle : SvgElement
    {
    }

    [Element("use")]
    public class SvgUse : SvgVisualElement
    {
    }

    // ------------------------------------------------------------------------
    // Extensibility
    // ------------------------------------------------------------------------

    [Element("foreignObject")]
    public class SvgForeignObject : SvgVisualElement
    {
    }

    // ------------------------------------------------------------------------
    // Filter Effects
    // ------------------------------------------------------------------------

    namespace FilterEffects
    {
        [Element("filter")]
        public class SvgFilter : SvgElement
        {
        }

        public abstract class SvgFilterPrimitive : SvgElement
        {
        }

        [Element("feBlend")]
        public class SvgBlend : SvgFilterPrimitive
        {
        }

        [Element("feColorMatrix")]
        public class SvgColourMatrix : SvgFilterPrimitive
        {
        }

        [Element("feComponentTransfer")]
        public class SvgComponentTransfer : SvgFilterPrimitive
        {
        }

        [Element("feComposite")]
        public class SvgComposite : SvgFilterPrimitive
        {
        }

        [Element("feConvolveMatrix")]
        public class SvgConvolveMatrix : SvgFilterPrimitive
        {
        }

        [Element("feDiffuseLighting")]
        public class SvgDiffuseLighting : SvgFilterPrimitive
        {
        }

        [Element("feDisplacementMap")]
        public class SvgDisplacementMap : SvgFilterPrimitive
        {
        }

        [Element("feDistantLight")]
        public class SvgDistantLight : SvgElement
        {
        }

        [Element("feFlood")]
        public class SvgFlood : SvgFilterPrimitive
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
        public class SvgGaussianBlur : SvgFilterPrimitive
        {
        }

        [Element("feImage")]
        public class SvgImage : SvgFilterPrimitive
        {
        }

        [Element("feMerge")]
        public class SvgMerge : SvgFilterPrimitive
        {
        }

        [Element("feMergeNode")]
        public class SvgMergeNode : SvgElement
        {
        }

        [Element("feMorphology")]
        public class SvgMorphology : SvgFilterPrimitive
        {
        }

        [Element("feOffset")]
        public class SvgOffset : SvgFilterPrimitive
        {
        }

        [Element("fePointLight")]
        public class SvgPointLight : SvgElement
        {
        }

        [Element("feSpecularLighting")]
        public class SvgSpecularLighting : SvgFilterPrimitive
        {
        }

        [Element("feSpotLight")]
        public class SvgSpotLight : SvgElement
        {
        }

        [Element("feTile")]
        public class SvgTile : SvgFilterPrimitive
        {
        }

        [Element("feTurbulence")]
        public class SvgTurbulence : SvgFilterPrimitive
        {
        }
    }

    // ------------------------------------------------------------------------
    // Interactivity
    // ------------------------------------------------------------------------

    [Element("cursor")]
    public class SvgCursor : SvgElement
    {
    }

    // ------------------------------------------------------------------------
    // Linking
    // ------------------------------------------------------------------------

    [Element("a")]
    public class SvgAnchor : SvgElement
    {
    }

    [Element("view")]
    public class SvgView : SvgElement
    {
    }

    // ------------------------------------------------------------------------
    // Painting
    // ------------------------------------------------------------------------

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
    public class SvgGradientStop : SvgElement
    {
    }

    [Element("linearGradient")]
    public class SvgLinearGradientServer : SvgGradientServer
    {
    }

    [Element("marker")]
    public class SvgMarker : SvgPathBasedElement
    {
    }

    [Element("pattern")]
    public class SvgPatternServer : SvgPaintServer
    {
    }

    [Element("radialGradient")]
    public class SvgRadialGradientServer : SvgGradientServer
    {

    }

    // ------------------------------------------------------------------------
    // Paths
    // ------------------------------------------------------------------------

    [Element("path")]
    public class SvgPath : SvgMarkerElement
    {
    }

    // ------------------------------------------------------------------------
    // Scripting
    // ------------------------------------------------------------------------

    [Element("script")]
    public class SvgScript : SvgElement
    {
    }

    // ------------------------------------------------------------------------
    // Text
    // ------------------------------------------------------------------------

    [Element("altGlyph")]
    public class SvgAltGlyph : SvgElement
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
    public class SvgFont : SvgElement
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
    public class SvgGlyph : SvgPathBasedElement
    {
    }

    [Element("glyphRef")]
    public class SvgGlyphRef : SvgElement
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
    public class SvgMissingGlyph : SvgGlyph
    {
    }

    public abstract class SvgTextBase : SvgVisualElement
    {
    }

    [Element("text")]
    public class SvgText : SvgTextBase
    {
    }

    [Element("textPath")]
    public class SvgTextPath : SvgTextBase
    {
    }

    [Element("tref")]
    public class SvgTextRef : SvgTextBase
    {
    }

    [Element("tspan")]
    public class SvgTextSpan : SvgTextBase
    {
    }
}
