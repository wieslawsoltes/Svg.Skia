using System;
using System.Collections.Generic;

namespace Svg.Editor.Skia;

/// <summary>One bounded ruler tick set, reused while only the pointer marker changes.</summary>
public sealed class SvgRulerTickCache
{
    private double _origin;
    private double _scale;
    private double _length;
    private bool _hasValue;
    private IReadOnlyList<SvgRulerTick> _ticks = Array.Empty<SvgRulerTick>();

    public IReadOnlyList<SvgRulerTick> GetTicks(double origin, double scale, double length)
    {
        if (_hasValue && _origin.Equals(origin) && _scale.Equals(scale) && _length.Equals(length))
            return _ticks;

        var ticks = SvgRulerMetrics.Create(origin, scale, length);
        _ticks = ticks is List<SvgRulerTick> list ? list.AsReadOnly() : ticks;
        _origin = origin;
        _scale = scale;
        _length = length;
        _hasValue = true;
        return _ticks;
    }
}
