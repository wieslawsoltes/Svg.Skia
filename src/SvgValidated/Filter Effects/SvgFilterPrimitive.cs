namespace SvgValidated.FilterEffects
{
    public abstract class SvgFilterPrimitive : SvgElement
    {
        public SvgUnit X { get; set; }
        public SvgUnit Y { get; set; }
        public SvgUnit Width { get; set; }
        public SvgUnit Height { get; set; }
        public string Input { get; set; }
        public string Result { get; set; }
    }
}
