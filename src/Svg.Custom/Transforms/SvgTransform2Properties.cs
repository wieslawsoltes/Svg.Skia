using System.ComponentModel;

namespace Svg
{
    [TypeConverter(typeof(SvgTransformBoxConverter))]
    public enum SvgTransformBox
    {
        ContentBox,
        BorderBox,
        FillBox,
        StrokeBox,
        ViewBox
    }

    public sealed class SvgTransformBoxConverter : EnumBaseConverter<SvgTransformBox>
    {
        public SvgTransformBoxConverter() : base(CaseHandling.KebabCase)
        {
        }
    }

    public partial class SvgElement
    {
        [SvgAttribute("transform-box")]
        public virtual SvgTransformBox TransformBox
        {
            get { return GetAttribute("transform-box", false, SvgTransformBox.ViewBox); }
            set { Attributes["transform-box"] = value; IsPathDirty = true; }
        }

        [SvgAttribute("transform-origin")]
        public virtual string TransformOrigin
        {
            get { return GetAttribute("transform-origin", false, "0 0"); }
            set { Attributes["transform-origin"] = value; IsPathDirty = true; }
        }
    }
}
