// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
using System;
using System.Collections;
using System.Collections.Generic;

namespace ShimSkiaSharp;

public abstract record PathCommand : IDeepCloneable<PathCommand>
{
    public PathCommand DeepClone() => DeepClone(new CloneContext());

    internal PathCommand DeepClone(CloneContext context)
    {
        if (context.TryGet(this, out PathCommand existing))
        {
            return existing;
        }

        context.Enter(this);
        try
        {
            PathCommand clone = this switch
            {
                AddCirclePathCommand addCirclePathCommand => new AddCirclePathCommand(addCirclePathCommand.X, addCirclePathCommand.Y, addCirclePathCommand.Radius),
                AddOvalPathCommand addOvalPathCommand => new AddOvalPathCommand(addOvalPathCommand.Rect),
                AddPolyPathCommand addPolyPathCommand => new AddPolyPathCommand(CloneHelpers.CloneList(addPolyPathCommand.Points, context), addPolyPathCommand.Close),
                AddRectPathCommand addRectPathCommand => new AddRectPathCommand(addRectPathCommand.Rect),
                AddRoundRectPathCommand addRoundRectPathCommand => new AddRoundRectPathCommand(addRoundRectPathCommand.Rect, addRoundRectPathCommand.Rx, addRoundRectPathCommand.Ry),
                ArcToPathCommand arcToPathCommand => new ArcToPathCommand(arcToPathCommand.Rx, arcToPathCommand.Ry, arcToPathCommand.XAxisRotate, arcToPathCommand.LargeArc, arcToPathCommand.Sweep, arcToPathCommand.X, arcToPathCommand.Y),
                ClosePathCommand => new ClosePathCommand(),
                CubicToPathCommand cubicToPathCommand => new CubicToPathCommand(cubicToPathCommand.X0, cubicToPathCommand.Y0, cubicToPathCommand.X1, cubicToPathCommand.Y1, cubicToPathCommand.X2, cubicToPathCommand.Y2),
                LineToPathCommand lineToPathCommand => new LineToPathCommand(lineToPathCommand.X, lineToPathCommand.Y),
                MoveToPathCommand moveToPathCommand => new MoveToPathCommand(moveToPathCommand.X, moveToPathCommand.Y),
                QuadToPathCommand quadToPathCommand => new QuadToPathCommand(quadToPathCommand.X0, quadToPathCommand.Y0, quadToPathCommand.X1, quadToPathCommand.Y1),
                _ => throw new NotSupportedException($"Unsupported {nameof(PathCommand)} type: {GetType().Name}.")
            };

            context.Add(this, clone);
            return clone;
        }
        finally
        {
            context.Exit(this);
        }
    }
}

public record AddCirclePathCommand(float X, float Y, float Radius) : PathCommand;

public record AddOvalPathCommand(SKRect Rect) : PathCommand;

public record AddPolyPathCommand : PathCommand, IList<SKPoint>
{
    private IList<SKPoint>? _points;
    private SKPoint _point0;
    private SKPoint _point1;
    private SKPoint _point2;
    private SKPoint _point3;
    private int _inlinePointCount;

    public AddPolyPathCommand(IList<SKPoint>? points, bool close)
    {
        _points = points;
        Close = close;
    }

    internal AddPolyPathCommand(SKPoint point0, SKPoint point1, SKPoint point2, bool close)
    {
        _point0 = point0;
        _point1 = point1;
        _point2 = point2;
        _inlinePointCount = 3;
        Close = close;
    }

    internal AddPolyPathCommand(SKPoint point0, SKPoint point1, SKPoint point2, SKPoint point3, bool close)
    {
        _point0 = point0;
        _point1 = point1;
        _point2 = point2;
        _point3 = point3;
        _inlinePointCount = 4;
        Close = close;
    }

    public IList<SKPoint>? Points => _points ?? (_inlinePointCount > 0 ? this : null);

    public bool Close { get; init; }

    public int Count => _points?.Count ?? _inlinePointCount;

    public bool IsReadOnly => _points?.IsReadOnly ?? false;

    public SKPoint this[int index]
    {
        get
        {
            if (_points is not null)
            {
                return _points[index];
            }

            return index switch
            {
                0 when _inlinePointCount > 0 => _point0,
                1 when _inlinePointCount > 1 => _point1,
                2 when _inlinePointCount > 2 => _point2,
                3 when _inlinePointCount > 3 => _point3,
                _ => throw new ArgumentOutOfRangeException(nameof(index))
            };
        }
        set
        {
            if (_points is not null)
            {
                _points[index] = value;
                return;
            }

            switch (index)
            {
                case 0 when _inlinePointCount > 0:
                    _point0 = value;
                    break;
                case 1 when _inlinePointCount > 1:
                    _point1 = value;
                    break;
                case 2 when _inlinePointCount > 2:
                    _point2 = value;
                    break;
                case 3 when _inlinePointCount > 3:
                    _point3 = value;
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(index));
            }
        }
    }

    public void Add(SKPoint item)
    {
        EnsurePointList(_inlinePointCount + 1).Add(item);
    }

    public void Clear()
    {
        if (_points is not null)
        {
            _points.Clear();
            return;
        }

        _point0 = default;
        _point1 = default;
        _point2 = default;
        _point3 = default;
        _inlinePointCount = 0;
    }

    public bool Contains(SKPoint item) => IndexOf(item) >= 0;

    public void CopyTo(SKPoint[] array, int arrayIndex)
    {
        if (_points is not null)
        {
            _points.CopyTo(array, arrayIndex);
            return;
        }

        if (array is null)
        {
            throw new ArgumentNullException(nameof(array));
        }

        if (arrayIndex < 0 || array.Length - arrayIndex < _inlinePointCount)
        {
            throw new ArgumentOutOfRangeException(nameof(arrayIndex));
        }

        for (var i = 0; i < _inlinePointCount; i++)
        {
            array[arrayIndex + i] = this[i];
        }
    }

    public IEnumerator<SKPoint> GetEnumerator()
    {
        return _points is not null
            ? _points.GetEnumerator()
            : new InlinePointEnumerator(_point0, _point1, _point2, _point3, _inlinePointCount);
    }

    public int IndexOf(SKPoint item)
    {
        if (_points is not null)
        {
            return _points.IndexOf(item);
        }

        var comparer = EqualityComparer<SKPoint>.Default;
        for (var i = 0; i < _inlinePointCount; i++)
        {
            if (comparer.Equals(this[i], item))
            {
                return i;
            }
        }

        return -1;
    }

    public void Insert(int index, SKPoint item)
    {
        EnsurePointList(_inlinePointCount + 1).Insert(index, item);
    }

    public bool Remove(SKPoint item)
    {
        if (_points is not null)
        {
            return _points.Remove(item);
        }

        var index = IndexOf(item);
        if (index < 0)
        {
            return false;
        }

        RemoveAt(index);
        return true;
    }

    public void RemoveAt(int index)
    {
        if (_points is not null)
        {
            _points.RemoveAt(index);
            return;
        }

        if (index < 0 || index >= _inlinePointCount)
        {
            throw new ArgumentOutOfRangeException(nameof(index));
        }

        var list = EnsurePointList(_inlinePointCount);
        list.RemoveAt(index);
    }

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    private IList<SKPoint> EnsurePointList(int capacity)
    {
        if (_points is not null)
        {
            return _points;
        }

        var points = new List<SKPoint>(capacity);
        for (var i = 0; i < _inlinePointCount; i++)
        {
            points.Add(this[i]);
        }

        _point0 = default;
        _point1 = default;
        _point2 = default;
        _point3 = default;
        _inlinePointCount = 0;
        _points = points;
        return points;
    }

    private sealed class InlinePointEnumerator : IEnumerator<SKPoint>
    {
        private readonly SKPoint _point0;
        private readonly SKPoint _point1;
        private readonly SKPoint _point2;
        private readonly SKPoint _point3;
        private readonly int _count;
        private int _index = -1;

        public InlinePointEnumerator(SKPoint point0, SKPoint point1, SKPoint point2, SKPoint point3, int count)
        {
            _point0 = point0;
            _point1 = point1;
            _point2 = point2;
            _point3 = point3;
            _count = count;
        }

        public SKPoint Current
        {
            get
            {
                return _index switch
                {
                    0 => _point0,
                    1 => _point1,
                    2 => _point2,
                    3 => _point3,
                    _ => throw new InvalidOperationException()
                };
            }
        }

        object? IEnumerator.Current => Current;

        public bool MoveNext()
        {
            if (_index + 1 >= _count)
            {
                return false;
            }

            _index++;
            return true;
        }

        public void Reset()
        {
            _index = -1;
        }

        public void Dispose()
        {
        }
    }
}

public record AddRectPathCommand(SKRect Rect) : PathCommand;

public record AddRoundRectPathCommand(SKRect Rect, float Rx, float Ry) : PathCommand;

public record ArcToPathCommand(float Rx, float Ry, float XAxisRotate, SKPathArcSize LargeArc, SKPathDirection Sweep, float X, float Y) : PathCommand;

public record ClosePathCommand : PathCommand;

public record CubicToPathCommand(float X0, float Y0, float X1, float Y1, float X2, float Y2) : PathCommand;

public record LineToPathCommand(float X, float Y) : PathCommand;

public record MoveToPathCommand(float X, float Y) : PathCommand;

public record QuadToPathCommand(float X0, float Y0, float X1, float Y1) : PathCommand;

public class SKPath : ICloneable, IDeepCloneable<SKPath>, IList<PathCommand>
{
    private const int InlineCommandCapacity = 2;

    private SKPathFillType _fillType;
    private SKRect _cachedBounds;
    private int _cachedBoundsVersion = -1;
    private int _version;
    private List<PathCommand>? _commands;
    private PathCommand? _command0;
    private PathCommand? _command1;
    private int _commandCount;

    public IList<PathCommand>? Commands => this;

    public bool IsEmpty => Count == 0;

    public SKRect Bounds => GetBounds();

    internal int Version => _version;

    public int Count => _commands?.Count ?? _commandCount;

    public bool IsReadOnly => false;

    public SKPathFillType FillType
    {
        get => _fillType;
        set
        {
            if (_fillType == value)
            {
                return;
            }

            _fillType = value;
            _version++;
        }
    }

    public SKPath()
    {
    }

    public PathCommand this[int index]
    {
        get
        {
            if (_commands is not null)
            {
                return _commands[index];
            }

            return index switch
            {
                0 when _commandCount > 0 => _command0!,
                1 when _commandCount > 1 => _command1!,
                _ => throw new ArgumentOutOfRangeException(nameof(index))
            };
        }
        set
        {
            if (_commands is not null)
            {
                _commands[index] = value;
                IncrementVersion();
                return;
            }

            switch (index)
            {
                case 0 when _commandCount > 0:
                    _command0 = value;
                    break;
                case 1 when _commandCount > 1:
                    _command1 = value;
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(index));
            }

            IncrementVersion();
        }
    }

    public SKPath Clone() => DeepClone(new CloneContext());

    public SKPath DeepClone() => Clone();

    object ICloneable.Clone() => Clone();

    internal SKPath DeepClone(CloneContext context)
    {
        if (context.TryGet(this, out SKPath existing))
        {
            return existing;
        }

        var clone = new SKPath();
        context.Add(this, clone);

        clone.FillType = FillType;
        for (var i = 0; i < Count; i++)
        {
            clone.AddInitial(this[i].DeepClone(context));
        }

        return clone;
    }

    private void IncrementVersion()
    {
        _version++;
    }

    private SKRect GetBounds()
    {
        if (_cachedBoundsVersion == _version)
        {
            return _cachedBounds;
        }

        var bounds = ComputeBounds(out var canCache);
        if (canCache)
        {
            _cachedBounds = bounds;
            _cachedBoundsVersion = _version;
        }

        return bounds;
    }

    private SKRect ComputeBounds(out bool canCache)
    {
        canCache = true;
        if (Count == 0)
        {
            return SKRect.Empty;
        }

        var commandCount = Count;
        var bounds = new SKRect(float.MaxValue, float.MaxValue, float.MinValue, float.MinValue);

        var last = new SKPoint();
        var haveLast = false;

        for (var i = 0; i < commandCount; i++)
        {
            var pathCommand = this[i];
            switch (pathCommand)
            {
                case MoveToPathCommand moveToPathCommand:
                    {
                        var x = moveToPathCommand.X;
                        var y = moveToPathCommand.Y;
                        SKPathBoundsHelper.ComputePointBounds(x, y, ref bounds);
                        last = new SKPoint(x, y);
                        haveLast = true;
                    }
                    break;
                case LineToPathCommand lineToPathCommand:
                    {
                        var x = lineToPathCommand.X;
                        var y = lineToPathCommand.Y;
                        if (haveLast)
                        {
                            SKPathBoundsHelper.AddLineBounds(last.X, last.Y, x, y, ref bounds);
                        }
                        else
                        {
                            SKPathBoundsHelper.ComputePointBounds(x, y, ref bounds);
                        }
                        last = new SKPoint(x, y);
                        haveLast = true;
                    }
                    break;
                case ArcToPathCommand arcToPathCommand:
                    {
                        var end = new SKPoint(arcToPathCommand.X, arcToPathCommand.Y);
                        if (haveLast)
                        {
                            SKPathBoundsHelper.AddArcBounds(last, end, arcToPathCommand.Rx, arcToPathCommand.Ry, arcToPathCommand.XAxisRotate, arcToPathCommand.LargeArc, arcToPathCommand.Sweep, ref bounds);
                        }
                        else
                        {
                            SKPathBoundsHelper.ComputePointBounds(end.X, end.Y, ref bounds);
                        }
                        last = end;
                        haveLast = true;
                    }
                    break;
                case QuadToPathCommand quadToPathCommand:
                    {
                        var p1 = new SKPoint(quadToPathCommand.X0, quadToPathCommand.Y0);
                        var p2 = new SKPoint(quadToPathCommand.X1, quadToPathCommand.Y1);
                        if (haveLast)
                        {
                            SKPathBoundsHelper.AddQuadBounds(last, p1, p2, ref bounds);
                        }
                        else
                        {
                            SKPathBoundsHelper.ComputePointBounds(p1.X, p1.Y, ref bounds);
                            SKPathBoundsHelper.ComputePointBounds(p2.X, p2.Y, ref bounds);
                        }
                        last = p2;
                        haveLast = true;
                    }
                    break;
                case CubicToPathCommand cubicToPathCommand:
                    {
                        var p1 = new SKPoint(cubicToPathCommand.X0, cubicToPathCommand.Y0);
                        var p2 = new SKPoint(cubicToPathCommand.X1, cubicToPathCommand.Y1);
                        var p3 = new SKPoint(cubicToPathCommand.X2, cubicToPathCommand.Y2);
                        if (haveLast)
                        {
                            SKPathBoundsHelper.AddCubicBounds(last, p1, p2, p3, ref bounds);
                        }
                        else
                        {
                            SKPathBoundsHelper.ComputePointBounds(p1.X, p1.Y, ref bounds);
                            SKPathBoundsHelper.ComputePointBounds(p2.X, p2.Y, ref bounds);
                            SKPathBoundsHelper.ComputePointBounds(p3.X, p3.Y, ref bounds);
                        }
                        last = p3;
                        haveLast = true;
                    }
                    break;
                case ClosePathCommand _:
                    break;
                case AddRectPathCommand addRectPathCommand:
                    {
                        var rect = addRectPathCommand.Rect;
                        SKPathBoundsHelper.ComputePointBounds(rect.Left, rect.Top, ref bounds);
                        SKPathBoundsHelper.ComputePointBounds(rect.Right, rect.Bottom, ref bounds);
                        last = rect.BottomRight;
                        haveLast = true;
                    }
                    break;
                case AddRoundRectPathCommand addRoundRectPathCommand:
                    {
                        var rect = addRoundRectPathCommand.Rect;
                        SKPathBoundsHelper.ComputePointBounds(rect.Left, rect.Top, ref bounds);
                        SKPathBoundsHelper.ComputePointBounds(rect.Right, rect.Bottom, ref bounds);
                        last = rect.BottomRight;
                        haveLast = true;
                    }
                    break;
                case AddOvalPathCommand addOvalPathCommand:
                    {
                        var rect = addOvalPathCommand.Rect;
                        SKPathBoundsHelper.ComputePointBounds(rect.Left, rect.Top, ref bounds);
                        SKPathBoundsHelper.ComputePointBounds(rect.Right, rect.Bottom, ref bounds);
                        last = rect.BottomRight;
                        haveLast = true;
                    }
                    break;
                case AddCirclePathCommand addCirclePathCommand:
                    {
                        var x = addCirclePathCommand.X;
                        var y = addCirclePathCommand.Y;
                        var radius = addCirclePathCommand.Radius;
                        SKPathBoundsHelper.ComputePointBounds(x - radius, y - radius, ref bounds);
                        SKPathBoundsHelper.ComputePointBounds(x + radius, y + radius, ref bounds);
                        last = new SKPoint(x + radius, y + radius);
                        haveLast = true;
                    }
                    break;
                case AddPolyPathCommand addPolyPathCommand:
                    {
                        canCache = false;
                        if (addPolyPathCommand.Points is { })
                        {
                            var points = addPolyPathCommand.Points;
                            var pointCount = points.Count;
                            for (var pointIndex = 0; pointIndex < pointCount; pointIndex++)
                            {
                                var point = points[pointIndex];
                                SKPathBoundsHelper.ComputePointBounds(point.X, point.Y, ref bounds);
                            }
                            if (pointCount > 0)
                            {
                                last = points[pointCount - 1];
                                haveLast = true;
                            }
                        }
                    }
                    break;
            }
        }

        return bounds;
    }

    public void Add(PathCommand item)
    {
        if (_commands is not null)
        {
            _commands.Add(item);
            IncrementVersion();
            return;
        }

        if (_commandCount < InlineCommandCapacity)
        {
            SetInline(_commandCount, item);
            _commandCount++;
            IncrementVersion();
            return;
        }

        PromoteToList(_commandCount + 1);
        _commands!.Add(item);
        IncrementVersion();
    }

    public void Clear()
    {
        if (Count == 0)
        {
            return;
        }

        if (_commands is not null)
        {
            _commands.Clear();
        }
        else
        {
            ClearInline();
        }

        IncrementVersion();
    }

    public bool Contains(PathCommand item)
    {
        return _commands is not null
            ? _commands.Contains(item)
            : IndexOfInline(item) >= 0;
    }

    public void CopyTo(PathCommand[] array, int arrayIndex)
    {
        if (_commands is not null)
        {
            _commands.CopyTo(array, arrayIndex);
            return;
        }

        if (array is null)
        {
            throw new ArgumentNullException(nameof(array));
        }

        if (arrayIndex < 0 || array.Length - arrayIndex < _commandCount)
        {
            throw new ArgumentOutOfRangeException(nameof(arrayIndex));
        }

        if (_commandCount > 0)
        {
            array[arrayIndex] = _command0!;
        }

        if (_commandCount > 1)
        {
            array[arrayIndex + 1] = _command1!;
        }
    }

    public IEnumerator<PathCommand> GetEnumerator()
    {
        return _commands is not null
            ? _commands.GetEnumerator()
            : new InlineCommandEnumerator(_command0, _command1, _commandCount);
    }

    public int IndexOf(PathCommand item)
    {
        return _commands is not null
            ? _commands.IndexOf(item)
            : IndexOfInline(item);
    }

    public void Insert(int index, PathCommand item)
    {
        if (_commands is not null)
        {
            _commands.Insert(index, item);
            IncrementVersion();
            return;
        }

        if (index < 0 || index > _commandCount)
        {
            throw new ArgumentOutOfRangeException(nameof(index));
        }

        if (_commandCount >= InlineCommandCapacity)
        {
            PromoteToList(_commandCount + 1);
            _commands!.Insert(index, item);
            IncrementVersion();
            return;
        }

        if (index == 0)
        {
            _command1 = _command0;
            _command0 = item;
        }
        else
        {
            _command1 = item;
        }

        _commandCount++;
        IncrementVersion();
    }

    public bool Remove(PathCommand item)
    {
        if (_commands is not null)
        {
            var removed = _commands.Remove(item);
            if (removed)
            {
                IncrementVersion();
            }

            return removed;
        }

        var index = IndexOfInline(item);
        if (index < 0)
        {
            return false;
        }

        RemoveAt(index);
        return true;
    }

    public void RemoveAt(int index)
    {
        if (_commands is not null)
        {
            _commands.RemoveAt(index);
            IncrementVersion();
            return;
        }

        if (index < 0 || index >= _commandCount)
        {
            throw new ArgumentOutOfRangeException(nameof(index));
        }

        if (index == 0 && _commandCount > 1)
        {
            _command0 = _command1;
        }

        _command1 = default;
        _commandCount--;
        if (_commandCount == 0)
        {
            _command0 = default;
        }

        IncrementVersion();
    }

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    public void MoveTo(float x, float y)
        => Add(new MoveToPathCommand(x, y));

    public void LineTo(float x, float y)
        => Add(new LineToPathCommand(x, y));

    public void ArcTo(float rx, float ry, float xAxisRotate, SKPathArcSize largeArc, SKPathDirection sweep, float x, float y)
        => Add(new ArcToPathCommand(rx, ry, xAxisRotate, largeArc, sweep, x, y));

    public void QuadTo(float x0, float y0, float x1, float y1)
        => Add(new QuadToPathCommand(x0, y0, x1, y1));

    public void CubicTo(float x0, float y0, float x1, float y1, float x2, float y2)
        => Add(new CubicToPathCommand(x0, y0, x1, y1, x2, y2));

    public void Close()
        => Add(new ClosePathCommand());

    public void AddRect(SKRect rect)
        => Add(new AddRectPathCommand(rect));

    public void AddRoundRect(SKRect rect, float rx, float ry)
        => Add(new AddRoundRectPathCommand(rect, rx, ry));

    public void AddOval(SKRect rect)
        => Add(new AddOvalPathCommand(rect));

    public void AddCircle(float x, float y, float radius)
        => Add(new AddCirclePathCommand(x, y, radius));

    public void AddPoly(SKPoint[] points, bool close = true)
        => Add(new AddPolyPathCommand(points, close));

    public void AddPoly(SKPoint point0, SKPoint point1, SKPoint point2, bool close = true)
        => Add(new AddPolyPathCommand(point0, point1, point2, close));

    public void AddPoly(SKPoint point0, SKPoint point1, SKPoint point2, SKPoint point3, bool close = true)
        => Add(new AddPolyPathCommand(point0, point1, point2, point3, close));

    private void AddInitial(PathCommand item)
    {
        if (_commands is not null)
        {
            _commands.Add(item);
            return;
        }

        if (_commandCount < InlineCommandCapacity)
        {
            SetInline(_commandCount, item);
            _commandCount++;
            return;
        }

        PromoteToList(_commandCount + 1);
        _commands!.Add(item);
    }

    private void SetInline(int index, PathCommand item)
    {
        if (index == 0)
        {
            _command0 = item;
        }
        else
        {
            _command1 = item;
        }
    }

    private void ClearInline()
    {
        _command0 = default;
        _command1 = default;
        _commandCount = 0;
    }

    private int IndexOfInline(PathCommand item)
    {
        var comparer = EqualityComparer<PathCommand>.Default;
        if (_commandCount > 0 && comparer.Equals(_command0!, item))
        {
            return 0;
        }

        if (_commandCount > 1 && comparer.Equals(_command1!, item))
        {
            return 1;
        }

        return -1;
    }

    private void PromoteToList(int capacity)
    {
        var commands = new List<PathCommand>(capacity);
        if (_commandCount > 0)
        {
            commands.Add(_command0!);
        }

        if (_commandCount > 1)
        {
            commands.Add(_command1!);
        }

        _command0 = default;
        _command1 = default;
        _commandCount = 0;
        _commands = commands;
    }

    private sealed class InlineCommandEnumerator : IEnumerator<PathCommand>
    {
        private readonly PathCommand? _command0;
        private readonly PathCommand? _command1;
        private readonly int _count;
        private int _index = -1;

        public InlineCommandEnumerator(PathCommand? command0, PathCommand? command1, int count)
        {
            _command0 = command0;
            _command1 = command1;
            _count = count;
        }

        public PathCommand Current
        {
            get
            {
                return _index switch
                {
                    0 => _command0!,
                    1 => _command1!,
                    _ => throw new InvalidOperationException()
                };
            }
        }

        object? IEnumerator.Current => Current;

        public bool MoveNext()
        {
            if (_index + 1 >= _count)
            {
                return false;
            }

            _index++;
            return true;
        }

        public void Reset()
        {
            _index = -1;
        }

        public void Dispose()
        {
        }
    }
}
