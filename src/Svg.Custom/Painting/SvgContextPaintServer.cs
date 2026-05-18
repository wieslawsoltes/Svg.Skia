namespace Svg
{
    public enum SvgContextPaintKind
    {
        Fill,
        Stroke
    }

    public partial class SvgContextPaintServer : SvgPaintServer
    {
        public SvgContextPaintServer()
        {
        }

        public SvgContextPaintServer(SvgContextPaintKind kind)
        {
            Kind = kind;
        }

        public SvgContextPaintKind Kind { get; set; }

        public override SvgElement DeepCopy()
        {
            return DeepCopy<SvgContextPaintServer>();
        }

        public override SvgElement DeepCopy<T>()
        {
            var newObj = base.DeepCopy<T>() as SvgContextPaintServer;
            newObj.Kind = Kind;
            return newObj;
        }

        public override bool Equals(object obj)
        {
            return obj is SvgContextPaintServer other && other.Kind == Kind;
        }

        public override int GetHashCode()
        {
            return Kind.GetHashCode();
        }

        public override string ToString()
        {
            return Kind == SvgContextPaintKind.Stroke ? "context-stroke" : "context-fill";
        }
    }
}
