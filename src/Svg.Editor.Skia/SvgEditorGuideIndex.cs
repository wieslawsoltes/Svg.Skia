using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using SkiaSharp;

namespace Svg.Editor.Skia;

/// <summary>
/// Retains ordered guide geometry and narrows axis-aligned snapping queries without
/// changing the nearest-line, intersection, or equal-distance ordering rules.
/// </summary>
/// <remarks>Query scratch space is reused; an instance belongs to one editor thread.</remarks>
public sealed class SvgEditorGuideIndex
{
    // Unit directions and bounded origins make the perpendicular component of
    // Project exactly the indexed coordinate, with no overflow in its offset.
    // Other geometry stays in the general set and uses the original algorithm.
    private const float SafeCoordinate = float.MaxValue / 4;
    private readonly struct AxisGuide
    {
        public AxisGuide(float position, int index)
        { Position = position; Index = index; }
        public float Position { get; }
        public int Index { get; }
    }

    private readonly SvgEditorGuide[] _guides;
    private readonly AxisGuide[] _vertical;
    private readonly AxisGuide[] _horizontal;
    private readonly int[] _general;
    private readonly List<int> _candidateIndices = new();
    private readonly List<SvgEditorGuide> _candidates = new();

    public SvgEditorGuideIndex(IReadOnlyList<SvgEditorGuide> guides)
    {
        if (guides is null)
            throw new ArgumentNullException(nameof(guides));
        _guides = new SvgEditorGuide[guides.Count];
        var vertical = new List<AxisGuide>();
        var horizontal = new List<AxisGuide>();
        var general = new List<int>();
        for (var index = 0; index < guides.Count; index++)
        {
            var guide = _guides[index] = guides[index];
            if (Safe(guide.Origin.X) && Safe(guide.Origin.Y)
                && guide.Direction.X == 0 && Math.Abs(guide.Direction.Y) == 1)
                vertical.Add(new AxisGuide(guide.Origin.X, index));
            else if (Safe(guide.Origin.X) && Safe(guide.Origin.Y)
                && guide.Direction.Y == 0 && Math.Abs(guide.Direction.X) == 1)
                horizontal.Add(new AxisGuide(guide.Origin.Y, index));
            else
                general.Add(index);
        }
        vertical.Sort(CompareAxisGuides);
        horizontal.Sort(CompareAxisGuides);
        _vertical = vertical.ToArray();
        _horizontal = horizontal.ToArray();
        _general = general.ToArray();
        Guides = new ReadOnlyCollection<SvgEditorGuide>(_guides);
    }

    /// <summary>The immutable snapshot in original document order.</summary>
    public IReadOnlyList<SvgEditorGuide> Guides { get; }

    /// <summary>Applies the existing snap rules to a conservative candidate set.</summary>
    public SKPoint Snap(SKPoint point, float tolerance, out string? firstGuide, out string? secondGuide)
    {
        // Keep validation and extreme-coordinate behavior identical to the live
        // service, including its float projection and distance arithmetic.
        if (!Safe(point.X) || !Safe(point.Y) || float.IsNaN(tolerance)
            || float.IsInfinity(tolerance) || tolerance < 0 || _general.Length == _guides.Length)
            return SvgEditorGuideService.Snap(point, tolerance, _guides, out firstGuide, out secondGuide);

        _candidateIndices.Clear();
        _candidateIndices.AddRange(_general);
        AddCandidates(_vertical, point.X, tolerance);
        AddCandidates(_horizontal, point.Y, tolerance);
        // Dense queries are already inexpensive to enumerate directly. Avoid
        // sorting/copying their whole result while retaining exact tie order.
        if (_candidateIndices.Count >= _guides.Length / 2)
            return SvgEditorGuideService.Snap(point, tolerance, _guides, out firstGuide, out secondGuide);

        _candidateIndices.Sort();
        _candidates.Clear();
        for (var index = 0; index < _candidateIndices.Count; index++)
            _candidates.Add(_guides[_candidateIndices[index]]);
        return SvgEditorGuideService.Snap(point, tolerance, _candidates, out firstGuide, out secondGuide);
    }

    private void AddCandidates(AxisGuide[] guides, float center, float tolerance)
    {
        var start = 0;
        var end = guides.Length;
        while (start < end)
        {
            var middle = start + (end - start) / 2;
            var position = guides[middle].Position;
            if (position < center && AxisDistance(center, position) > tolerance)
                start = middle + 1;
            else
                end = middle;
        }
        for (var index = start; index < guides.Length; index++)
        {
            var position = guides[index].Position;
            if (position > center && AxisDistance(center, position) > tolerance)
                break;
            _candidateIndices.Add(guides[index].Index);
        }
    }

    // Use the same float distance operation as Snap, rather than subtracting a
    // tolerance interval in double precision. This includes boundary candidates
    // that round onto the tolerance, even for zero and subnormal distances.
    private static float AxisDistance(float first, float second)
        => SKPoint.Distance(new SKPoint(first, 0), new SKPoint(second, 0));

    private static int CompareAxisGuides(AxisGuide first, AxisGuide second)
        => first.Position.CompareTo(second.Position);

    private static bool Safe(float value) => value >= -SafeCoordinate && value <= SafeCoordinate;
}
