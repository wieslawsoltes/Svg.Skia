
namespace SvgValidated.FilterEffects
{
    public abstract class SvgComponentTransferFunction : SvgElement
    {
        public SvgComponentTransferType Type { get; set; }
        public SvgNumberCollection TableValues { get; set; }
        public float Slope { get; set; }
        public float Intercept { get; set; }
        public float Amplitude { get; set; }
        public float Exponent { get; set; }
        public float Offset { get; set; }
    }
}
