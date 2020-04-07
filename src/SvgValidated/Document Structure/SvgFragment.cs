namespace SvgValidated
{
    public class SvgFragment : SvgElement, ISvgViewPort
    {
        public SvgUnit X { get; set; }
        public SvgUnit Y { get; set; }
        public SvgUnit Width { get; set; }
        public SvgUnit Height { get; set; }
        public SvgOverflow Overflow { get; set; }
        public SvgViewBox ViewBox { get; set; }
        public SvgAspectRatio AspectRatio { get; set; }
        public new SvgUnit FontSize { get; set; }
        public new string FontFamily { get; set; }
        public new XmlSpaceHandling SpaceHandling { get; set; }
    }
}
