
namespace Svg
{
    public class SvgDeferredPaintServer : SvgPaintServer
    {
        public string DeferredId { get; set; }
        public SvgPaintServer FallbackServer { get; set; }
    }
}
