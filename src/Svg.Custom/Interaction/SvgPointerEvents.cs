using System.ComponentModel;

namespace Svg
{
    [TypeConverter(typeof(SvgPointerEventsConverter))]
    public enum SvgPointerEvents
    {
        VisiblePainted,
        VisibleFill,
        VisibleStroke,
        Visible,
        Painted,
        Fill,
        Stroke,
        All,
        None
    }

    public sealed class SvgPointerEventsConverter : EnumBaseConverter<SvgPointerEvents>
    {
    }

    public abstract partial class SvgVisualElement
    {
        [SvgAttribute("pointer-events")]
        public virtual SvgPointerEvents PointerEvents
        {
            get { return GetAttribute("pointer-events", true, SvgPointerEvents.VisiblePainted); }
            set { Attributes["pointer-events"] = value; }
        }
    }
}
