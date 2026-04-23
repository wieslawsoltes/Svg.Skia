using System;
using System.Collections.Generic;
using System.Drawing;
using Jint.Native;
using Jint.Native.Object;
using Svg.Pathing;

namespace Svg.JavaScript;

internal sealed class SvgJavaScriptEventListenerEntry
{
    public SvgJavaScriptEventListenerEntry(JsValue listener, bool useCapture)
    {
        Listener = listener;
        UseCapture = useCapture;
    }

    public JsValue Listener { get; }

    public bool UseCapture { get; }
}

public sealed class SvgJavaScriptPathSegList
{
    private readonly SvgJavaScriptRuntime _runtime;
    private readonly SvgPath _path;

    internal SvgJavaScriptPathSegList(SvgJavaScriptRuntime runtime, SvgPath path)
    {
        _runtime = runtime;
        _path = path;
    }

    public int numberOfItems => GetSegments().Count;

    public int length => numberOfItems;

    public SvgJavaScriptPathSeg getItem(int index)
    {
        var segments = GetSegments();
        if (index < 0 || index >= segments.Count)
        {
            _runtime.ThrowDomException(1, "Path segment index is out of range.");
        }

        return new SvgJavaScriptPathSeg(_runtime, _path, segments[index]);
    }

    public SvgJavaScriptPathSeg appendItem(SvgJavaScriptPathSeg newItem)
    {
        if (newItem is null)
        {
            throw new ArgumentNullException(nameof(newItem));
        }

        var segment = newItem.CloneSegment();
        var segments = GetSegments();
        segments.Add(segment);
        MarkPathMutation(ReferenceEquals(segments.Owner, _path));
        return new SvgJavaScriptPathSeg(_runtime, _path, segment);
    }

    public SvgJavaScriptPathSeg insertItemBefore(SvgJavaScriptPathSeg newItem, int index)
    {
        if (newItem is null)
        {
            throw new ArgumentNullException(nameof(newItem));
        }

        var segments = GetSegments();
        if (index < 0)
        {
            index = 0;
        }
        else if (index > segments.Count)
        {
            index = segments.Count;
        }

        var segment = newItem.CloneSegment();
        segments.Insert(index, segment);
        MarkPathMutation(ReferenceEquals(segments.Owner, _path));
        return new SvgJavaScriptPathSeg(_runtime, _path, segment);
    }

    public SvgJavaScriptPathSeg removeItem(int index)
    {
        var segments = GetSegments();
        if (index < 0 || index >= segments.Count)
        {
            _runtime.ThrowDomException(1, "Path segment index is out of range.");
        }

        var removed = segments[index];
        segments.RemoveAt(index);
        MarkPathMutation(ReferenceEquals(segments.Owner, _path));
        return new SvgJavaScriptPathSeg(_runtime, _path, removed);
    }

    public void clear()
    {
        GetSegments().Clear();
        MarkPathMutation(pathAlreadyNotified: false);
    }

    private SvgPathSegmentList GetSegments()
    {
        if (_path.PathData is { } pathData)
        {
            return pathData;
        }

        var created = new SvgPathSegmentList();
        _path.PathData = created;
        return created;
    }

    private void MarkPathMutation(bool pathAlreadyNotified)
    {
        if (!pathAlreadyNotified)
        {
            _path.OnPathUpdated();
        }

        _runtime.MarkMutation();
    }
}

public sealed class SvgJavaScriptPathSeg
{
    private readonly SvgJavaScriptRuntime _runtime;
    private readonly SvgPath _path;
    private readonly SvgPathSegment _segment;

    internal SvgJavaScriptPathSeg(SvgJavaScriptRuntime runtime, SvgPath path, SvgPathSegment segment)
    {
        _runtime = runtime;
        _path = path;
        _segment = segment;
    }

    public int pathSegType => _segment switch
    {
        SvgClosePathSegment _ => 1,
        SvgMoveToSegment move when !move.IsRelative => 2,
        SvgMoveToSegment _ => 3,
        SvgLineSegment line when !line.IsRelative && !float.IsNaN(line.End.X) && !float.IsNaN(line.End.Y) => 4,
        SvgLineSegment line when line.IsRelative && !float.IsNaN(line.End.X) && !float.IsNaN(line.End.Y) => 5,
        SvgCubicCurveSegment cubic when !cubic.IsRelative && !float.IsNaN(cubic.FirstControlPoint.X) && !float.IsNaN(cubic.FirstControlPoint.Y) => 6,
        SvgCubicCurveSegment cubic when cubic.IsRelative && !float.IsNaN(cubic.FirstControlPoint.X) && !float.IsNaN(cubic.FirstControlPoint.Y) => 7,
        SvgLineSegment line when !line.IsRelative && float.IsNaN(line.End.Y) => 12,
        SvgLineSegment line when line.IsRelative && float.IsNaN(line.End.Y) => 13,
        SvgLineSegment line when !line.IsRelative && float.IsNaN(line.End.X) => 14,
        SvgLineSegment line when line.IsRelative && float.IsNaN(line.End.X) => 15,
        SvgCubicCurveSegment cubic when !cubic.IsRelative => 16,
        SvgCubicCurveSegment _ => 17,
        _ => 0
    };

    public string pathSegTypeAsLetter
    {
        get
        {
            var text = _segment.ToString() ?? string.Empty;
            return text.Length > 0 ? text.Substring(0, 1) : string.Empty;
        }
    }

    public float x
    {
        get => _segment.End.X;
        set => UpdateSegment(() =>
        {
            var end = _segment.End;
            end.X = value;
            _segment.End = end;
        });
    }

    public float y
    {
        get => _segment.End.Y;
        set => UpdateSegment(() =>
        {
            var end = _segment.End;
            end.Y = value;
            _segment.End = end;
        });
    }

    public float x1
    {
        get => _segment is SvgCubicCurveSegment cubic ? cubic.FirstControlPoint.X : 0f;
        set => UpdateCubic(cubic =>
        {
            var point = cubic.FirstControlPoint;
            point.X = value;
            cubic.FirstControlPoint = point;
        });
    }

    public float y1
    {
        get => _segment is SvgCubicCurveSegment cubic ? cubic.FirstControlPoint.Y : 0f;
        set => UpdateCubic(cubic =>
        {
            var point = cubic.FirstControlPoint;
            point.Y = value;
            cubic.FirstControlPoint = point;
        });
    }

    public float x2
    {
        get => _segment is SvgCubicCurveSegment cubic ? cubic.SecondControlPoint.X : 0f;
        set => UpdateCubic(cubic =>
        {
            var point = cubic.SecondControlPoint;
            point.X = value;
            cubic.SecondControlPoint = point;
        });
    }

    public float y2
    {
        get => _segment is SvgCubicCurveSegment cubic ? cubic.SecondControlPoint.Y : 0f;
        set => UpdateCubic(cubic =>
        {
            var point = cubic.SecondControlPoint;
            point.Y = value;
            cubic.SecondControlPoint = point;
        });
    }

    internal SvgPathSegment CloneSegment()
    {
        return _segment.Clone();
    }

    private void UpdateCubic(Action<SvgCubicCurveSegment> update)
    {
        if (_segment is not SvgCubicCurveSegment cubic)
        {
            return;
        }

        update(cubic);
        UpdateSegment(null);
    }

    private void UpdateSegment(Action? update)
    {
        update?.Invoke();
        _path.OnPathUpdated();
        _runtime.MarkMutation();
    }
}

public sealed partial class SvgJavaScriptElement
{
    private Dictionary<string, List<SvgJavaScriptEventListenerEntry>>? _eventListeners;

    public SvgJavaScriptPathSegList pathSegList => new(_runtime, RequirePathElement());

    public float getTotalLength()
    {
        return GetPathMetrics().TotalLength;
    }

    public SvgJavaScriptPoint getPointAtLength(double distance)
    {
        var metrics = GetPathMetrics();
        if (!metrics.TryGetPointAtLength(distance, out var point))
        {
            return new SvgJavaScriptPoint();
        }

        return new SvgJavaScriptPoint(point.X, point.Y);
    }

    public int getPathSegAtLength(double distance)
    {
        return GetPathMetrics().GetPathSegIndexAtLength(distance);
    }

    public SvgJavaScriptPathSeg createSVGPathSegMovetoAbs(float x, float y)
    {
        return new SvgJavaScriptPathSeg(_runtime, RequirePathElement(), new SvgMoveToSegment(false, new PointF(x, y)));
    }

    public SvgJavaScriptPathSeg createSVGPathSegCurvetoCubicAbs(float x, float y, float x1, float y1, float x2, float y2)
    {
        return new SvgJavaScriptPathSeg(
            _runtime,
            RequirePathElement(),
            new SvgCubicCurveSegment(false, new PointF(x1, y1), new PointF(x2, y2), new PointF(x, y)));
    }

    public SvgJavaScriptPathSeg createSVGPathSegClosePath()
    {
        return new SvgJavaScriptPathSeg(_runtime, RequirePathElement(), new SvgClosePathSegment(true));
    }

    public void addEventListener(string? type, JsValue listener, bool useCapture)
    {
        var normalizedType = NormalizeListenerEventType(type);
        if (normalizedType.Length == 0 ||
            listener.Equals(JsValue.Null) ||
            listener.Equals(JsValue.Undefined))
        {
            return;
        }

        var listeners = _eventListeners ??= new Dictionary<string, List<SvgJavaScriptEventListenerEntry>>(StringComparer.OrdinalIgnoreCase);
        if (!listeners.TryGetValue(normalizedType, out var registrations))
        {
            registrations = new List<SvgJavaScriptEventListenerEntry>();
            listeners[normalizedType] = registrations;
        }

        foreach (var registration in registrations)
        {
            if (registration.UseCapture == useCapture &&
                AreSameListener(registration.Listener, listener))
            {
                return;
            }
        }

        registrations.Add(new SvgJavaScriptEventListenerEntry(listener, useCapture));
    }

    public void removeEventListener(string? type, JsValue listener, bool useCapture)
    {
        var normalizedType = NormalizeListenerEventType(type);
        List<SvgJavaScriptEventListenerEntry>? registrations = null;
        if (normalizedType.Length == 0 ||
            listener.Equals(JsValue.Null) ||
            listener.Equals(JsValue.Undefined) ||
            _eventListeners is null ||
            !_eventListeners.TryGetValue(normalizedType, out registrations))
        {
            return;
        }

        for (var i = registrations.Count - 1; i >= 0; i--)
        {
            if (registrations[i].UseCapture == useCapture &&
                AreSameListener(registrations[i].Listener, listener))
            {
                registrations.RemoveAt(i);
            }
        }

        if (registrations.Count == 0)
        {
            _eventListeners.Remove(normalizedType);
        }
    }

    internal SvgJavaScriptEventResult DispatchRegisteredEventListeners(
        string eventType,
        object targetNode,
        object? relatedTargetNode,
        SvgJavaScriptEventInput? input)
    {
        var eventFacade = new SvgJavaScriptEvent(
            NormalizeListenerEventType(eventType),
            targetNode,
            this,
            relatedTargetNode,
            input);
        return DispatchRegisteredEventListeners(eventType, eventFacade, useCapture: false);
    }

    internal SvgJavaScriptEventResult DispatchRegisteredEventListeners(
        string eventType,
        SvgJavaScriptEvent eventFacade,
        bool useCapture)
    {
        var normalizedType = NormalizeListenerEventType(eventType);
        if (normalizedType.Length == 0 ||
            _eventListeners is null ||
            !_eventListeners.TryGetValue(normalizedType, out var registrations) ||
            registrations.Count == 0)
        {
            return SvgJavaScriptEventResult.NotExecuted;
        }

        var before = _runtime.MutationVersion;
        var snapshot = registrations.ToArray();
        var executed = false;

        foreach (var registration in snapshot)
        {
            if (registration.UseCapture != useCapture)
            {
                continue;
            }

            executed = true;
            eventFacade.SetCurrentTarget(this);
            _runtime.ExecuteEventListener(registration.Listener, this, eventFacade);
            if (eventFacade.cancelBubble || !eventFacade.bubbles)
            {
                break;
            }
        }

        return executed
            ? new SvgJavaScriptEventResult(
                executed: true,
                mutated: _runtime.MutationVersion != before,
                cancelBubble: eventFacade.cancelBubble,
                defaultPrevented: eventFacade.defaultPrevented)
            : SvgJavaScriptEventResult.NotExecuted;
    }

    private SvgPath RequirePathElement()
    {
        if (Element is SvgPath path)
        {
            return path;
        }

        _runtime.ThrowDomException(11, "The element does not implement SVGPathElement.");
        return null!;
    }

    private SvgJavaScriptPathMetrics GetPathMetrics()
    {
        return SvgJavaScriptPathMetrics.Create(RequirePathElement());
    }

    private static bool AreSameListener(JsValue left, JsValue right)
    {
        return left.Equals(right);
    }

    private static string NormalizeListenerEventType(string? type)
    {
        if (string.IsNullOrWhiteSpace(type))
        {
            return string.Empty;
        }

        var normalized = (type ?? string.Empty).Trim();
        return normalized.StartsWith("on", StringComparison.OrdinalIgnoreCase)
            ? normalized.Substring(2)
            : normalized;
    }
}

internal sealed class SvgJavaScriptPathMetrics
{
    private readonly SvgPath _path;
    private readonly float _totalLength;
    private readonly List<float> _segmentEndLengths;
    private readonly List<PathSegmentMetric> _segmentMetrics;
    private readonly PointF _firstPoint;

    private SvgJavaScriptPathMetrics(
        SvgPath path,
        float totalLength,
        List<float> segmentEndLengths,
        List<PathSegmentMetric> segmentMetrics,
        PointF firstPoint)
    {
        _path = path;
        _totalLength = totalLength;
        _segmentEndLengths = segmentEndLengths;
        _segmentMetrics = segmentMetrics;
        _firstPoint = firstPoint;
    }

    public float TotalLength => _totalLength;

    public static SvgJavaScriptPathMetrics Create(SvgPath path)
    {
        var segmentEndLengths = new List<float>();
        var segmentMetrics = new List<PathSegmentMetric>();
        var totalLength = 0f;
        var firstPoint = PointF.Empty;

        if (path.PathData is { Count: > 0 } pathData)
        {
            totalLength = MeasurePathSegments(pathData, segmentEndLengths, segmentMetrics, out firstPoint);
        }

        return new SvgJavaScriptPathMetrics(path, totalLength, segmentEndLengths, segmentMetrics, firstPoint);
    }

    public bool TryGetPointAtLength(double distance, out PointF point)
    {
        point = default;
        if (_path.PathData?.Count <= 0)
        {
            return false;
        }

        if (_segmentMetrics.Count == 0)
        {
            point = _firstPoint;
            return true;
        }

        var scaledDistance = ScaleDistance(distance, _totalLength);
        if (scaledDistance <= 0f)
        {
            point = _firstPoint;
            return true;
        }

        for (var index = 0; index < _segmentMetrics.Count; index++)
        {
            var metric = _segmentMetrics[index];
            if (scaledDistance > metric.EndLength && index + 1 < _segmentMetrics.Count)
            {
                continue;
            }

            point = metric.ResolvePoint(scaledDistance);
            return true;
        }

        point = _segmentMetrics[_segmentMetrics.Count - 1].End;
        return true;
    }

    public int GetPathSegIndexAtLength(double distance)
    {
        if (_segmentEndLengths.Count == 0)
        {
            return 0;
        }

        var scaledDistance = ScaleDistance(distance, _totalLength);
        for (var index = 0; index < _segmentEndLengths.Count; index++)
        {
            if (scaledDistance <= _segmentEndLengths[index])
            {
                return index;
            }
        }

        return _segmentEndLengths.Count - 1;
    }

    private float ScaleDistance(double distance, float actualLength)
    {
        if (actualLength <= 0f)
        {
            return 0f;
        }

        var requested = double.IsNaN(distance) || double.IsInfinity(distance) ? 0d : distance;
        if (_path.PathLength > 0f)
        {
            requested = requested / _path.PathLength * actualLength;
        }

        return Clamp((float)requested, 0f, actualLength);
    }

    private static float MeasurePathSegments(
        SvgPathSegmentList pathData,
        List<float> segmentEndLengths,
        List<PathSegmentMetric> segmentMetrics,
        out PointF firstPoint)
    {
        var current = PointF.Empty;
        var subpathStart = PointF.Empty;
        var totalLength = 0f;
        var lastCubicSecondControlPoint = PointF.Empty;
        var hasLastCubicSecondControlPoint = false;
        var hasFirstPoint = false;

        firstPoint = PointF.Empty;

        for (var index = 0; index < pathData.Count; index++)
        {
            var segment = pathData[index];
            switch (segment)
            {
                case SvgMoveToSegment move:
                    current = ToAbsolute(move.End, move.IsRelative, current);
                    subpathStart = current;
                    hasLastCubicSecondControlPoint = false;
                    if (!hasFirstPoint)
                    {
                        firstPoint = current;
                        hasFirstPoint = true;
                    }
                    break;
                case SvgLineSegment line:
                    {
                        var start = current;
                        var end = ToAbsolute(line.End, line.IsRelative, current);
                        var startLength = totalLength;
                        totalLength += Distance(start, end);
                        segmentMetrics.Add(new PathSegmentMetric(index, startLength, totalLength, start, end, default, default, isCubic: false));
                        current = end;
                        hasLastCubicSecondControlPoint = false;
                        break;
                    }
                case SvgCubicCurveSegment cubic:
                    {
                        var start = current;
                        var first = cubic.FirstControlPoint;
                        if (float.IsNaN(first.X) || float.IsNaN(first.Y))
                        {
                            first = hasLastCubicSecondControlPoint
                                ? Reflect(lastCubicSecondControlPoint, current)
                                : current;
                        }
                        else
                        {
                            first = ToAbsolute(first, cubic.IsRelative, current);
                        }

                        var second = ToAbsolute(cubic.SecondControlPoint, cubic.IsRelative, current);
                        var end = ToAbsolute(cubic.End, cubic.IsRelative, current);
                        var startLength = totalLength;
                        totalLength += MeasureCubic(start, first, second, end);
                        segmentMetrics.Add(new PathSegmentMetric(index, startLength, totalLength, start, end, first, second, isCubic: true));
                        current = end;
                        lastCubicSecondControlPoint = second;
                        hasLastCubicSecondControlPoint = true;
                        break;
                    }
                case SvgClosePathSegment _:
                    {
                        var start = current;
                        var startLength = totalLength;
                        totalLength += Distance(start, subpathStart);
                        segmentMetrics.Add(new PathSegmentMetric(index, startLength, totalLength, start, subpathStart, default, default, isCubic: false));
                        current = subpathStart;
                        hasLastCubicSecondControlPoint = false;
                        break;
                    }
                default:
                    hasLastCubicSecondControlPoint = false;
                    break;
            }

            segmentEndLengths.Add(totalLength);
        }

        return totalLength;
    }

    private static float MeasureCubic(PointF start, PointF first, PointF second, PointF end)
    {
        var total = 0f;
        var previous = start;
        const int steps = 64;

        for (var step = 1; step <= steps; step++)
        {
            var t = step / (float)steps;
            var current = EvaluateCubic(start, first, second, end, t);
            total += Distance(previous, current);
            previous = current;
        }

        return total;
    }

    private static PointF Reflect(PointF point, PointF mirror)
    {
        var dx = Math.Abs(mirror.X - point.X);
        var dy = Math.Abs(mirror.Y - point.Y);
        return new PointF(
            mirror.X + (mirror.X >= point.X ? dx : -dx),
            mirror.Y + (mirror.Y >= point.Y ? dy : -dy));
    }

    private static PointF ToAbsolute(PointF point, bool isRelative, PointF start)
    {
        if (float.IsNaN(point.X))
        {
            point.X = start.X;
        }
        else if (isRelative)
        {
            point.X += start.X;
        }

        if (float.IsNaN(point.Y))
        {
            point.Y = start.Y;
        }
        else if (isRelative)
        {
            point.Y += start.Y;
        }

        return point;
    }

    private static float Distance(PointF a, PointF b)
    {
        var dx = b.X - a.X;
        var dy = b.Y - a.Y;
        return (float)Math.Sqrt(dx * dx + dy * dy);
    }

    private static float Clamp(float value, float min, float max)
    {
        if (value < min)
        {
            return min;
        }

        if (value > max)
        {
            return max;
        }

        return value;
    }

    private static PointF EvaluateCubic(PointF start, PointF first, PointF second, PointF end, float t)
    {
        var mt = 1f - t;
        var mt2 = mt * mt;
        var t2 = t * t;
        return new PointF(
            mt2 * mt * start.X + 3f * mt2 * t * first.X + 3f * mt * t2 * second.X + t2 * t * end.X,
            mt2 * mt * start.Y + 3f * mt2 * t * first.Y + 3f * mt * t2 * second.Y + t2 * t * end.Y);
    }

    private readonly struct PathSegmentMetric
    {
        public PathSegmentMetric(
            int index,
            float startLength,
            float endLength,
            PointF start,
            PointF end,
            PointF firstControl,
            PointF secondControl,
            bool isCubic)
        {
            Index = index;
            StartLength = startLength;
            EndLength = endLength;
            Start = start;
            End = end;
            FirstControl = firstControl;
            SecondControl = secondControl;
            IsCubic = isCubic;
        }

        public int Index { get; }

        public float StartLength { get; }

        public float EndLength { get; }

        public PointF Start { get; }

        public PointF End { get; }

        public PointF FirstControl { get; }

        public PointF SecondControl { get; }

        public bool IsCubic { get; }

        public PointF ResolvePoint(float length)
        {
            if (EndLength <= StartLength)
            {
                return End;
            }

            var local = Clamp((length - StartLength) / (EndLength - StartLength), 0f, 1f);
            if (!IsCubic)
            {
                return new PointF(
                    Start.X + (End.X - Start.X) * local,
                    Start.Y + (End.Y - Start.Y) * local);
            }

            var previous = Start;
            var previousLength = StartLength;
            const int steps = 64;
            for (var step = 1; step <= steps; step++)
            {
                var t = step / (float)steps;
                var current = EvaluateCubic(Start, FirstControl, SecondControl, End, t);
                var segmentLength = Distance(previous, current);
                var nextLength = previousLength + segmentLength;
                if (length <= nextLength || step == steps)
                {
                    var segmentProgress = segmentLength <= 0f ? 0f : Clamp((length - previousLength) / segmentLength, 0f, 1f);
                    return new PointF(
                        previous.X + (current.X - previous.X) * segmentProgress,
                        previous.Y + (current.Y - previous.Y) * segmentProgress);
                }

                previous = current;
                previousLength = nextLength;
            }

            return End;
        }
    }
}

public sealed partial class SvgJavaScriptRuntime
{
    internal SvgJavaScriptEventResult ExecuteEventListeners(
        SvgElement element,
        object targetNode,
        object? relatedTargetNode,
        string eventType,
        SvgJavaScriptEventInput? input)
    {
        return GetElement(element).DispatchRegisteredEventListeners(eventType, targetNode, relatedTargetNode, input);
    }

    internal void ExecuteEventListener(JsValue listener, object currentTarget, SvgJavaScriptEvent eventFacade)
    {
        try
        {
            if (listener.Equals(JsValue.Null) || listener.Equals(JsValue.Undefined))
            {
                return;
            }

            var args = new[] { JsValue.FromObject(_engine, eventFacade) };
            var listenerObject = listener.ToObject() as ObjectInstance;
            if (listenerObject is not null)
            {
                var handleEvent = listenerObject.Get("handleEvent");
                if (!handleEvent.Equals(JsValue.Null) && !handleEvent.Equals(JsValue.Undefined))
                {
                    _engine.Call(handleEvent, listener, args);
                    DrainPendingTimeouts();
                    return;
                }
            }

            _engine.Call(listener, JsValue.FromObject(_engine, currentTarget), args);
            DrainPendingTimeouts();
        }
        catch (Exception ex)
        {
            HandleScriptError(ex, "event listener");
        }
    }
}
