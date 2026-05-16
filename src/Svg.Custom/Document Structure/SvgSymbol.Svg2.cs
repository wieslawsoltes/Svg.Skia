namespace Svg
{
    public partial class SvgSymbol
    {
        [SvgAttribute("x")]
        public virtual SvgUnit X
        {
            get { return GetAttribute<SvgUnit>("x", false, 0f); }
            set { Attributes["x"] = value; }
        }

        [SvgAttribute("y")]
        public virtual SvgUnit Y
        {
            get { return GetAttribute<SvgUnit>("y", false, 0f); }
            set { Attributes["y"] = value; }
        }

        [SvgAttribute("width")]
        public virtual SvgUnit Width
        {
            get { return GetAttribute<SvgUnit>("width", false, SvgUnit.None); }
            set { Attributes["width"] = value; }
        }

        [SvgAttribute("height")]
        public virtual SvgUnit Height
        {
            get { return GetAttribute<SvgUnit>("height", false, SvgUnit.None); }
            set { Attributes["height"] = value; }
        }

        [SvgAttribute("refX")]
        public virtual SvgUnit RefX
        {
            get { return GetAttribute<SvgUnit>("refX", false, 0f); }
            set { Attributes["refX"] = value; }
        }

        [SvgAttribute("refY")]
        public virtual SvgUnit RefY
        {
            get { return GetAttribute<SvgUnit>("refY", false, 0f); }
            set { Attributes["refY"] = value; }
        }
    }
}
