namespace Svg.FilterEffects
{
    [SvgElement("feDropShadow")]
    public partial class SvgDropShadow : SvgFilterPrimitive
    {
        [SvgAttribute("dx")]
        public SvgUnit Dx
        {
            get { return GetAttribute<SvgUnit>("dx", false, 2f); }
            set { Attributes["dx"] = value; }
        }

        [SvgAttribute("dy")]
        public SvgUnit Dy
        {
            get { return GetAttribute<SvgUnit>("dy", false, 2f); }
            set { Attributes["dy"] = value; }
        }

        [SvgAttribute("stdDeviation")]
        public SvgNumberCollection StdDeviation
        {
            get { return GetAttribute("stdDeviation", false, new SvgNumberCollection() { 2f }); }
            set { Attributes["stdDeviation"] = value; }
        }

        [SvgAttribute("flood-color")]
        public virtual SvgPaintServer FloodColor
        {
            get { return GetAttribute("flood-color", true, SvgPaintServer.NotSet); }
            set { Attributes["flood-color"] = value; }
        }

        [SvgAttribute("flood-opacity")]
        public virtual float FloodOpacity
        {
            get { return GetAttribute("flood-opacity", true, 1f); }
            set { Attributes["flood-opacity"] = FixOpacityValue(value); }
        }

        public override SvgElement DeepCopy()
        {
            return DeepCopy<SvgDropShadow>();
        }
    }
}
