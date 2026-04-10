using System;
using System.ComponentModel;
using System.Drawing;

namespace Svg
{
    /// <summary>
    /// Svg.Custom copy of the upstream gradient stop element.
    ///
    /// The original stop getters rely on the generic inherited-attribute path. That path loses the
    /// distinction between the SVG keyword <c>inherit</c> and the shared
    /// <see cref="SvgPaintServer.Inherit"/> sentinel, which turns inherited stop colors into an
    /// accidental black color in cases like <c>pservers-grad-18-b</c>. It also misses
    /// <c>stop-opacity="inherit"</c> when the parser had to keep the raw string form.
    ///
    /// This local copy keeps the public API and parse behavior identical to upstream, but resolves
    /// stop inheritance through <see cref="SvgStopInheritanceResolver"/> so Svg.Skia can honor the
    /// same Chrome/W3C semantics without modifying the vendored SVG submodule.
    /// </summary>
    [SvgElement("stop")]
    public partial class SvgGradientStop : SvgElement
    {
        private SvgUnit _offset;

        [SvgAttribute("offset")]
        public SvgUnit Offset
        {
            get { return _offset; }
            set
            {
                var unit = value;
                if (unit.Type == SvgUnitType.Percentage)
                {
                    unit = new SvgUnit(unit.Type, Math.Min(Math.Max(unit.Value, 0f), 100f));
                }
                else if (unit.Type == SvgUnitType.User)
                {
                    unit = new SvgUnit(unit.Type, Math.Min(Math.Max(unit.Value, 0f), 1f));
                }

                _offset = unit.ToPercentage();
                Attributes["offset"] = unit;
            }
        }

        [SvgAttribute("stop-color")]
        [TypeConverter(typeof(SvgPaintServerFactory))]
        public SvgPaintServer StopColor
        {
            get { return SvgStopInheritanceResolver.ResolveStopColor(this, false, new SvgColourServer(System.Drawing.Color.Black)); }
            set { Attributes["stop-color"] = value; }
        }

        [SvgAttribute("stop-opacity")]
        public float StopOpacity
        {
            get { return SvgStopInheritanceResolver.ResolveStopOpacity(this, false, 1f); }
            set { Attributes["stop-opacity"] = FixOpacityValue(value); }
        }

        public SvgGradientStop()
        {
            _offset = new SvgUnit(0f);
        }

        public SvgGradientStop(SvgUnit offset, Color colour)
        {
            _offset = offset;
        }

        public Color GetColor(SvgElement parent)
        {
            var core = SvgDeferredPaintServer.TryGet<SvgColourServer>(StopColor, parent);
            if (core == null)
            {
                throw new InvalidOperationException("Invalid paint server for gradient stop detected.");
            }

            return core.Colour;
        }

        public override SvgElement DeepCopy()
        {
            return DeepCopy<SvgGradientStop>();
        }

        public override SvgElement DeepCopy<T>()
        {
            var newObj = base.DeepCopy<T>() as SvgGradientStop;
            newObj._offset = _offset;
            return newObj;
        }
    }
}
