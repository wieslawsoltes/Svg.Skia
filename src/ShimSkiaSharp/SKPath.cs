// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
using System;
using System.Collections.Generic;

namespace ShimSkiaSharp;

public abstract record PathCommand : IDeepCloneable<PathCommand>
{
    public PathCommand DeepClone()
    {
        return this switch
        {
            AddCirclePathCommand addCirclePathCommand => new AddCirclePathCommand(addCirclePathCommand.X, addCirclePathCommand.Y, addCirclePathCommand.Radius),
            AddOvalPathCommand addOvalPathCommand => new AddOvalPathCommand(addOvalPathCommand.Rect),
            AddPolyPathCommand addPolyPathCommand => new AddPolyPathCommand(CloneHelpers.CloneList(addPolyPathCommand.Points), addPolyPathCommand.Close),
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
    }
}

public record AddCirclePathCommand(float X, float Y, float Radius) : PathCommand;

public record AddOvalPathCommand(SKRect Rect) : PathCommand;

public record AddPolyPathCommand(IList<SKPoint>? Points, bool Close) : PathCommand;

public record AddRectPathCommand(SKRect Rect) : PathCommand;

public record AddRoundRectPathCommand(SKRect Rect, float Rx, float Ry) : PathCommand;

public record ArcToPathCommand(float Rx, float Ry, float XAxisRotate, SKPathArcSize LargeArc, SKPathDirection Sweep, float X, float Y) : PathCommand;

public record ClosePathCommand : PathCommand;

public record CubicToPathCommand(float X0, float Y0, float X1, float Y1, float X2, float Y2) : PathCommand;

public record LineToPathCommand(float X, float Y) : PathCommand;

public record MoveToPathCommand(float X, float Y) : PathCommand;

public record QuadToPathCommand(float X0, float Y0, float X1, float Y1) : PathCommand;

public class SKPath : ICloneable, IDeepCloneable<SKPath>
{
    public SKPathFillType FillType { get; set; }

    public IList<PathCommand>? Commands { get; private set; }

    public bool IsEmpty => Commands is null || Commands.Count == 0;

    public SKRect Bounds => GetBounds();

    public SKPath()
    {
        Commands = new List<PathCommand>();
    }

    public SKPath Clone()
    {
        return new SKPath
        {
            FillType = FillType,
            Commands = CloneHelpers.CloneList(Commands, command => command.DeepClone())
        };
    }

    public SKPath DeepClone() => Clone();

    object ICloneable.Clone() => Clone();


    private SKRect GetBounds()
    {
        if (Commands is null || Commands.Count == 0)
        {
            return SKRect.Empty;
        }

        var bounds = new SKRect(float.MaxValue, float.MaxValue, float.MinValue, float.MinValue);

        var last = new SKPoint();
        var haveLast = false;

        foreach (var pathCommand in Commands)
        {
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
                        if (addPolyPathCommand.Points is { })
                        {
                            var points = addPolyPathCommand.Points;
                            foreach (var point in points)
                            {
                                SKPathBoundsHelper.ComputePointBounds(point.X, point.Y, ref bounds);
                            }
                            if (points.Count > 0)
                            {
                                last = points[points.Count - 1];
                                haveLast = true;
                            }
                        }
                    }
                    break;
            }
        }

        return bounds;
    }

    public void MoveTo(float x, float y)
        => Commands?.Add(new MoveToPathCommand(x, y));

    public void LineTo(float x, float y)
        => Commands?.Add(new LineToPathCommand(x, y));

    public void ArcTo(float rx, float ry, float xAxisRotate, SKPathArcSize largeArc, SKPathDirection sweep, float x, float y)
        => Commands?.Add(new ArcToPathCommand(rx, ry, xAxisRotate, largeArc, sweep, x, y));

    public void QuadTo(float x0, float y0, float x1, float y1)
        => Commands?.Add(new QuadToPathCommand(x0, y0, x1, y1));

    public void CubicTo(float x0, float y0, float x1, float y1, float x2, float y2)
        => Commands?.Add(new CubicToPathCommand(x0, y0, x1, y1, x2, y2));

    public void Close()
        => Commands?.Add(new ClosePathCommand());

    public void AddRect(SKRect rect)
        => Commands?.Add(new AddRectPathCommand(rect));

    public void AddRoundRect(SKRect rect, float rx, float ry)
        => Commands?.Add(new AddRoundRectPathCommand(rect, rx, ry));

    public void AddOval(SKRect rect)
        => Commands?.Add(new AddOvalPathCommand(rect));

    public void AddCircle(float x, float y, float radius)
        => Commands?.Add(new AddCirclePathCommand(x, y, radius));

    public void AddPoly(SKPoint[] points, bool close = true)
        => Commands?.Add(new AddPolyPathCommand(points, close));
}
