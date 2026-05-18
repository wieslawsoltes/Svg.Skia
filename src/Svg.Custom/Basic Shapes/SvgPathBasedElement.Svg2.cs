namespace Svg
{
    public partial class SvgCircle
    {
        [SvgAttribute("pathLength")]
        public float PathLength
        {
            get { return GetAttribute<float>("pathLength", false); }
            set { Attributes["pathLength"] = value; }
        }
    }

    public partial class SvgEllipse
    {
        [SvgAttribute("pathLength")]
        public float PathLength
        {
            get { return GetAttribute<float>("pathLength", false); }
            set { Attributes["pathLength"] = value; }
        }
    }

    public partial class SvgLine
    {
        [SvgAttribute("pathLength")]
        public float PathLength
        {
            get { return GetAttribute<float>("pathLength", false); }
            set { Attributes["pathLength"] = value; }
        }
    }

    public partial class SvgPolygon
    {
        [SvgAttribute("pathLength")]
        public float PathLength
        {
            get { return GetAttribute<float>("pathLength", false); }
            set { Attributes["pathLength"] = value; }
        }
    }

    public partial class SvgRectangle
    {
        [SvgAttribute("pathLength")]
        public float PathLength
        {
            get { return GetAttribute<float>("pathLength", false); }
            set { Attributes["pathLength"] = value; }
        }
    }
}
