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
}
