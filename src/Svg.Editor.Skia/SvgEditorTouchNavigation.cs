using System;
using System.Collections.Generic;
using SkiaSharp;

namespace Svg.Editor.Skia;

/// <summary>
/// Tracks a two-contact camera gesture independently of document tools and UI capture.
/// Once navigation starts, remaining contacts stay navigation-only until all lift.
/// </summary>
public sealed class SvgEditorTouchNavigation
{
    private readonly Dictionary<uint, SKPoint> _contacts = new();
    private readonly List<uint> _order = new();
    private SKPoint _center;
    private double _distance;

    public int ContactCount => _contacts.Count;
    public bool IsNavigating { get; private set; }

    public bool Contains(uint pointerId) => _contacts.ContainsKey(pointerId);

    /// <summary>Returns true only when the second contact starts navigation.</summary>
    public bool AddContact(uint pointerId, SKPoint point)
    {
        if (!IsFinite(point) || _contacts.ContainsKey(pointerId))
            return false;
        _contacts.Add(pointerId, point);
        _order.Add(pointerId);
        var started = !IsNavigating && _contacts.Count == 2;
        IsNavigating |= started;
        if (_contacts.Count == 2)
            Rebase();
        return started;
    }

    /// <summary>Updates the camera and returns whether this contact belongs to navigation.</summary>
    public bool MoveContact(uint pointerId, SKPoint point, SvgEditorViewport viewport)
    {
        if (viewport is null)
            throw new ArgumentNullException(nameof(viewport));
        if (!_contacts.ContainsKey(pointerId))
            return false;
        if (!IsFinite(point))
            return IsNavigating;
        _contacts[pointerId] = point;
        if (!IsNavigating)
            return false;
        if (_contacts.Count >= 2 && (pointerId == _order[0] || pointerId == _order[1]))
        {
            GetPair(out var center, out var distance);
            // Nearly coincident contacts can pan, but cannot produce a stable scale.
            var scale = _distance > 1 && distance > 1 ? distance / _distance : 1;
            viewport.PanAndZoomAt(scale, _center, center);
            _center = center;
            _distance = distance;
        }
        return true;
    }

    /// <summary>Returns whether the removed contact belonged to navigation.</summary>
    public bool RemoveContact(uint pointerId)
    {
        if (!_contacts.Remove(pointerId))
            return false;
        var navigating = IsNavigating;
        var replacedPair = _order.IndexOf(pointerId) < 2;
        _order.Remove(pointerId);
        if (_contacts.Count == 0)
            Reset();
        else if (IsNavigating && replacedPair && _contacts.Count >= 2)
            Rebase();
        return navigating;
    }

    public void Reset()
    {
        _contacts.Clear();
        _order.Clear();
        IsNavigating = false;
        _center = default;
        _distance = 0;
    }

    private void Rebase() => GetPair(out _center, out _distance);

    private void GetPair(out SKPoint center, out double distance)
    {
        var first = _contacts[_order[0]];
        var second = _contacts[_order[1]];
        center = new SKPoint((float)(((double)first.X + second.X) / 2), (float)(((double)first.Y + second.Y) / 2));
        var x = (double)second.X - first.X;
        var y = (double)second.Y - first.Y;
        distance = Math.Sqrt(x * x + y * y);
    }

    private static bool IsFinite(SKPoint point) => !float.IsNaN(point.X) && !float.IsInfinity(point.X)
        && !float.IsNaN(point.Y) && !float.IsInfinity(point.Y);
}
