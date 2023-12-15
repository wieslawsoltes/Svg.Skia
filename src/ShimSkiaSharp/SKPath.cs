/*
 * Svg.Skia SVG rendering library.
 * Copyright (C) 2023  Wiesław Šoltés
 *
 * This program is free software: you can redistribute it and/or modify
 * it under the terms of the GNU Affero General Public License as
 * published by the Free Software Foundation, either version 3 of the
 * License, or (at your option) any later version.
 *
 * This program is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
 * GNU Affero General Public License for more details.
 *
 * You should have received a copy of the GNU Affero General Public License
 * along with this program.  If not, see <https://www.gnu.org/licenses/>.
 */
using System;
using System.Collections.Generic;

namespace ShimSkiaSharp;

public abstract record PathCommand;

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

public class SKPath
{
    public SKPathFillType FillType { get; set; }

    public IList<PathCommand>? Commands { get; private set; }

    public bool IsEmpty => Commands is null || Commands.Count == 0;

    public SKRect Bounds => GetBounds();

    public SKPath()
    {
        Commands = new List<PathCommand>();
    }

    private void ComputePointBounds(float x, float y, ref SKRect bounds)
    {
        bounds.Left = Math.Min(x, bounds.Left);
        bounds.Right = Math.Max(x, bounds.Right);
        bounds.Top = Math.Min(y, bounds.Top);
        bounds.Bottom = Math.Max(y, bounds.Bottom);
    }

    private SKRect GetBounds()
    {
        if (Commands is null || Commands.Count == 0)
        {
            return SKRect.Empty;
        }

        var bounds = new SKRect(float.MaxValue, float.MaxValue, float.MinValue, float.MinValue);

        foreach (var pathCommand in Commands)
        {
            switch (pathCommand)
            {
                case MoveToPathCommand moveToPathCommand:
                {
                    var x = moveToPathCommand.X;
                    var y = moveToPathCommand.Y;
                    ComputePointBounds(x, y, ref bounds);
                }
                    break;
                case LineToPathCommand lineToPathCommand:
                {
                    var x = lineToPathCommand.X;
                    var y = lineToPathCommand.Y;
                    ComputePointBounds(x, y, ref bounds);
                }
                    break;
                case ArcToPathCommand arcToPathCommand:
                {
                    var x = arcToPathCommand.X;
                    var y = arcToPathCommand.Y;
                    ComputePointBounds(x, y, ref bounds);
                }
                    break;
                case QuadToPathCommand quadToPathCommand:
                {
                    var x0 = quadToPathCommand.X0;
                    var y0 = quadToPathCommand.Y0;
                    var x1 = quadToPathCommand.X1;
                    var y1 = quadToPathCommand.Y1;
                    ComputePointBounds(x0, y0, ref bounds);
                    ComputePointBounds(x1, y1, ref bounds);
                }
                    break;
                case CubicToPathCommand cubicToPathCommand:
                {
                    var x0 = cubicToPathCommand.X0;
                    var y0 = cubicToPathCommand.Y0;
                    var x1 = cubicToPathCommand.X1;
                    var y1 = cubicToPathCommand.Y1;
                    var x2 = cubicToPathCommand.X2;
                    var y2 = cubicToPathCommand.Y2;
                    ComputePointBounds(x0, y0, ref bounds);
                    ComputePointBounds(x1, y1, ref bounds);
                    ComputePointBounds(x2, y2, ref bounds);
                }
                    break;
                case ClosePathCommand _:
                    break;
                case AddRectPathCommand addRectPathCommand:
                {
                    var rect = addRectPathCommand.Rect;
                    ComputePointBounds(rect.Left, rect.Top, ref bounds);
                    ComputePointBounds(rect.Right, rect.Bottom, ref bounds);
                }
                    break;
                case AddRoundRectPathCommand addRoundRectPathCommand:
                {
                    var rect = addRoundRectPathCommand.Rect;
                    ComputePointBounds(rect.Left, rect.Top, ref bounds);
                    ComputePointBounds(rect.Right, rect.Bottom, ref bounds);
                }
                    break;
                case AddOvalPathCommand addOvalPathCommand:
                {
                    var rect = addOvalPathCommand.Rect;
                    ComputePointBounds(rect.Left, rect.Top, ref bounds);
                    ComputePointBounds(rect.Right, rect.Bottom, ref bounds);
                }
                    break;
                case AddCirclePathCommand addCirclePathCommand:
                {
                    var x = addCirclePathCommand.X;
                    var y = addCirclePathCommand.Y;
                    var radius = addCirclePathCommand.Radius;
                    ComputePointBounds(x - radius, y - radius, ref bounds);
                    ComputePointBounds(x + radius, y + radius, ref bounds);
                }
                    break;
                case AddPolyPathCommand addPolyPathCommand:
                {
                    if (addPolyPathCommand.Points is { })
                    {
                        var points = addPolyPathCommand.Points;
                        foreach (var point in points)
                        {
                            ComputePointBounds(point.X, point.Y, ref bounds);
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
