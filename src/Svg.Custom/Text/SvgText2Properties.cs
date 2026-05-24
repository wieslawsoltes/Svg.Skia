using System;
using System.ComponentModel;
using System.Globalization;
using Svg.Pathing;

namespace Svg
{
    [TypeConverter(typeof(SvgWhiteSpaceConverter))]
    public enum SvgWhiteSpace
    {
        Normal,
        Pre,
        NoWrap,
        PreWrap,
        BreakSpaces,
        PreLine
    }

    public sealed class SvgWhiteSpaceConverter : EnumBaseConverter<SvgWhiteSpace>
    {
        public SvgWhiteSpaceConverter() : base(CaseHandling.KebabCase)
        {
        }

        public override object ConvertTo(ITypeDescriptorContext context, CultureInfo culture, object value, Type destinationType)
        {
            if (destinationType == typeof(string) && value is SvgWhiteSpace.NoWrap)
            {
                return "nowrap";
            }

            return base.ConvertTo(context, culture, value, destinationType);
        }
    }

    [TypeConverter(typeof(SvgTextPathSideConverter))]
    public enum SvgTextPathSide
    {
        Left,
        Right
    }

    public sealed class SvgTextPathSideConverter : EnumBaseConverter<SvgTextPathSide>
    {
        public SvgTextPathSideConverter() : base(CaseHandling.LowerCase)
        {
        }
    }

    public partial class SvgElement
    {
        [SvgAttribute("white-space")]
        public virtual SvgWhiteSpace WhiteSpace
        {
            get
            {
                return ComputedStyle.TryGetWhiteSpace(out var whiteSpace)
                    ? whiteSpace
                    : SvgWhiteSpace.Normal;
            }
            set { Attributes["white-space"] = value; IsPathDirty = true; }
        }

        [SvgAttribute("text-overflow")]
        public virtual string TextOverflow
        {
            get { return ComputedStyle.TextOverflow; }
            set { Attributes["text-overflow"] = value; IsPathDirty = true; }
        }

        [SvgAttribute("white-space-collapse")]
        public virtual string WhiteSpaceCollapse
        {
            get { return ComputedStyle.WhiteSpaceCollapse; }
            set { Attributes["white-space-collapse"] = value; IsPathDirty = true; }
        }

        [SvgAttribute("text-wrap-mode")]
        public virtual string TextWrapMode
        {
            get { return ComputedStyle.TextWrapMode; }
            set { Attributes["text-wrap-mode"] = value; IsPathDirty = true; }
        }

        [SvgAttribute("white-space-trim")]
        public virtual string WhiteSpaceTrim
        {
            get { return ComputedStyle.WhiteSpaceTrim; }
            set { Attributes["white-space-trim"] = value; IsPathDirty = true; }
        }

        [SvgAttribute("overflow-wrap")]
        public virtual string OverflowWrap
        {
            get { return ComputedStyle.OverflowWrap; }
            set { Attributes["overflow-wrap"] = value; IsPathDirty = true; }
        }

        [SvgAttribute("word-break")]
        public virtual string WordBreak
        {
            get { return ComputedStyle.WordBreak; }
            set { Attributes["word-break"] = value; IsPathDirty = true; }
        }

        [SvgAttribute("line-break")]
        public virtual string LineBreak
        {
            get { return ComputedStyle.LineBreak; }
            set { Attributes["line-break"] = value; IsPathDirty = true; }
        }

        [SvgAttribute("line-height")]
        public virtual string LineHeight
        {
            get { return ComputedStyle.LineHeight; }
            set { Attributes["line-height"] = value; IsPathDirty = true; }
        }

        [SvgAttribute("font-feature-settings")]
        public virtual string FontFeatureSettings
        {
            get { return ComputedStyle.FontFeatureSettings; }
            set { Attributes["font-feature-settings"] = value; IsPathDirty = true; }
        }

        [SvgAttribute("font-kerning")]
        public virtual string FontKerning
        {
            get { return ComputedStyle.FontKerning; }
            set { Attributes["font-kerning"] = value; IsPathDirty = true; }
        }

        [SvgAttribute("font-variant-ligatures")]
        public virtual string FontVariantLigatures
        {
            get { return ComputedStyle.FontVariantLigatures; }
            set { Attributes["font-variant-ligatures"] = value; IsPathDirty = true; }
        }

        [SvgAttribute("inline-size")]
        public virtual string InlineSize
        {
            get { return ComputedStyle.InlineSize; }
            set { Attributes["inline-size"] = value; IsPathDirty = true; }
        }

        [SvgAttribute("shape-inside")]
        public virtual string ShapeInside
        {
            get { return ComputedStyle.ShapeInside; }
            set { Attributes["shape-inside"] = value; IsPathDirty = true; }
        }

        [SvgAttribute("shape-subtract")]
        public virtual string ShapeSubtract
        {
            get { return ComputedStyle.ShapeSubtract; }
            set { Attributes["shape-subtract"] = value; IsPathDirty = true; }
        }

        [SvgAttribute("shape-padding")]
        public virtual string ShapePadding
        {
            get { return ComputedStyle.ShapePadding; }
            set { Attributes["shape-padding"] = value; IsPathDirty = true; }
        }

        [SvgAttribute("shape-margin")]
        public virtual string ShapeMargin
        {
            get { return ComputedStyle.ShapeMargin; }
            set { Attributes["shape-margin"] = value; IsPathDirty = true; }
        }

        [SvgAttribute("shape-image-threshold")]
        public virtual string ShapeImageThreshold
        {
            get { return ComputedStyle.ShapeImageThreshold; }
            set { Attributes["shape-image-threshold"] = value; IsPathDirty = true; }
        }
    }

    public partial class SvgTextPath : ISvgPathElement
    {
        [SvgAttribute("path")]
        public virtual SvgPathSegmentList PathData
        {
            get { return GetAttribute<SvgPathSegmentList>("path", false); }
            set
            {
                var old = PathData;
                if (old != null)
                {
                    old.Owner = null;
                }

                Attributes["path"] = value;
                if (value != null)
                {
                    value.Owner = this;
                }

                IsPathDirty = true;
            }
        }

        [SvgAttribute("side")]
        public virtual SvgTextPathSide Side
        {
            get { return GetAttribute("side", false, SvgTextPathSide.Left); }
            set { Attributes["side"] = value; IsPathDirty = true; }
        }

        public void OnPathUpdated()
        {
            IsPathDirty = true;
            OnAttributeChanged(new AttributeEventArgs { Attribute = "path", Value = Attributes.GetAttribute<SvgPathSegmentList>("path") });
        }
    }

    [SvgElement("altGlyph")]
    public partial class SvgAltGlyph : SvgTextSpan
    {
        [SvgAttribute("href", SvgAttributeAttribute.XLinkNamespace)]
        public virtual Uri ReferencedElement
        {
            get { return GetAttribute<Uri>("href", false); }
            set { Attributes["href"] = value; IsPathDirty = true; }
        }

        [SvgAttribute("glyphRef")]
        public virtual string GlyphRef
        {
            get { return GetAttribute<string>("glyphRef", false); }
            set { Attributes["glyphRef"] = value; IsPathDirty = true; }
        }

        [SvgAttribute("format")]
        public virtual string Format
        {
            get { return GetAttribute<string>("format", false); }
            set { Attributes["format"] = value; IsPathDirty = true; }
        }

        public override SvgElement DeepCopy()
        {
            return base.DeepCopy<SvgAltGlyph>();
        }
    }

    [SvgElement("altGlyphDef")]
    public partial class SvgAltGlyphDef : SvgElement
    {
        public override SvgElement DeepCopy()
        {
            return base.DeepCopy<SvgAltGlyphDef>();
        }
    }

    [SvgElement("altGlyphItem")]
    public partial class SvgAltGlyphItem : SvgElement
    {
        public override SvgElement DeepCopy()
        {
            return base.DeepCopy<SvgAltGlyphItem>();
        }
    }

    [SvgElement("glyphRef")]
    public partial class SvgGlyphRef : SvgElement
    {
        [SvgAttribute("href", SvgAttributeAttribute.XLinkNamespace)]
        public virtual Uri ReferencedElement
        {
            get { return GetAttribute<Uri>("href", false); }
            set { Attributes["href"] = value; IsPathDirty = true; }
        }

        [SvgAttribute("glyphRef")]
        public virtual string GlyphRef
        {
            get { return GetAttribute<string>("glyphRef", false); }
            set { Attributes["glyphRef"] = value; IsPathDirty = true; }
        }

        [SvgAttribute("format")]
        public virtual string Format
        {
            get { return GetAttribute<string>("format", false); }
            set { Attributes["format"] = value; IsPathDirty = true; }
        }

        [SvgAttribute("x")]
        public virtual SvgUnit X
        {
            get { return GetAttribute("x", false, SvgUnit.Empty); }
            set { Attributes["x"] = value; IsPathDirty = true; }
        }

        [SvgAttribute("y")]
        public virtual SvgUnit Y
        {
            get { return GetAttribute("y", false, SvgUnit.Empty); }
            set { Attributes["y"] = value; IsPathDirty = true; }
        }

        [SvgAttribute("dx")]
        public virtual SvgUnit Dx
        {
            get { return GetAttribute("dx", false, SvgUnit.Empty); }
            set { Attributes["dx"] = value; IsPathDirty = true; }
        }

        [SvgAttribute("dy")]
        public virtual SvgUnit Dy
        {
            get { return GetAttribute("dy", false, SvgUnit.Empty); }
            set { Attributes["dy"] = value; IsPathDirty = true; }
        }

        public override SvgElement DeepCopy()
        {
            return base.DeepCopy<SvgGlyphRef>();
        }
    }

    public partial class SvgFontFace
    {
        [SvgAttribute("ideographic")]
        public float Ideographic
        {
            get { return GetAttribute("ideographic", true, float.MinValue); }
            set { Attributes["ideographic"] = value; }
        }

        [SvgAttribute("hanging")]
        public float Hanging
        {
            get { return GetAttribute("hanging", true, float.MinValue); }
            set { Attributes["hanging"] = value; }
        }

        [SvgAttribute("mathematical")]
        public float Mathematical
        {
            get { return GetAttribute("mathematical", true, float.MinValue); }
            set { Attributes["mathematical"] = value; }
        }

        [SvgAttribute("cap-height")]
        public float CapHeight
        {
            get { return GetAttribute("cap-height", true, float.MinValue); }
            set { Attributes["cap-height"] = value; }
        }
    }
}
