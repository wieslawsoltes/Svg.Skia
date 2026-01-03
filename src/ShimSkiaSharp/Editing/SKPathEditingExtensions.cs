// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
using System;

namespace ShimSkiaSharp.Editing;

public static class SKPathEditingExtensions
{
    public static int UpdateCommands(
        this SKPath path,
        Func<PathCommand, bool> predicate,
        Func<PathCommand, PathCommand> replace)
    {
        if (path is null)
        {
            throw new ArgumentNullException(nameof(path));
        }

        if (predicate is null)
        {
            throw new ArgumentNullException(nameof(predicate));
        }

        if (replace is null)
        {
            throw new ArgumentNullException(nameof(replace));
        }

        var commands = path.Commands;
        if (commands is null)
        {
            return 0;
        }

        var count = 0;
        for (var i = 0; i < commands.Count; i++)
        {
            var command = commands[i];
            if (!predicate(command))
            {
                continue;
            }

            var next = replace(command);
            if (!ReferenceEquals(next, command))
            {
                commands[i] = next;
            }

            count++;
        }

        return count;
    }

    public static void Transform(this SKPath path, SKMatrix matrix)
    {
        if (path is null)
        {
            throw new ArgumentNullException(nameof(path));
        }

        if (!IsAxisAligned(matrix))
        {
            throw new NotSupportedException("Only translation/scale matrices are supported.");
        }

        var commands = path.Commands;
        if (commands is null)
        {
            return;
        }

        var absScaleX = Math.Abs(matrix.ScaleX);
        var absScaleY = Math.Abs(matrix.ScaleY);
        var uniformScale = absScaleX == absScaleY;
        var flipSweep = matrix.ScaleX * matrix.ScaleY < 0f;

        for (var i = 0; i < commands.Count; i++)
        {
            var command = commands[i];
            switch (command)
            {
                case MoveToPathCommand moveTo:
                    {
                        var mapped = matrix.MapPoint(new SKPoint(moveTo.X, moveTo.Y));
                        commands[i] = new MoveToPathCommand(mapped.X, mapped.Y);
                        break;
                    }
                case LineToPathCommand lineTo:
                    {
                        var mapped = matrix.MapPoint(new SKPoint(lineTo.X, lineTo.Y));
                        commands[i] = new LineToPathCommand(mapped.X, mapped.Y);
                        break;
                    }
                case QuadToPathCommand quadTo:
                    {
                        var p0 = matrix.MapPoint(new SKPoint(quadTo.X0, quadTo.Y0));
                        var p1 = matrix.MapPoint(new SKPoint(quadTo.X1, quadTo.Y1));
                        commands[i] = new QuadToPathCommand(p0.X, p0.Y, p1.X, p1.Y);
                        break;
                    }
                case CubicToPathCommand cubicTo:
                    {
                        var p0 = matrix.MapPoint(new SKPoint(cubicTo.X0, cubicTo.Y0));
                        var p1 = matrix.MapPoint(new SKPoint(cubicTo.X1, cubicTo.Y1));
                        var p2 = matrix.MapPoint(new SKPoint(cubicTo.X2, cubicTo.Y2));
                        commands[i] = new CubicToPathCommand(p0.X, p0.Y, p1.X, p1.Y, p2.X, p2.Y);
                        break;
                    }
                case ArcToPathCommand arcTo:
                    {
                        var end = matrix.MapPoint(new SKPoint(arcTo.X, arcTo.Y));
                        var rx = arcTo.Rx * absScaleX;
                        var ry = arcTo.Ry * absScaleY;
                        var sweep = arcTo.Sweep;
                        if (flipSweep)
                        {
                            sweep = sweep == SKPathDirection.Clockwise
                                ? SKPathDirection.CounterClockwise
                                : SKPathDirection.Clockwise;
                        }
                        commands[i] = new ArcToPathCommand(rx, ry, arcTo.XAxisRotate, arcTo.LargeArc, sweep, end.X, end.Y);
                        break;
                    }
                case AddRectPathCommand addRect:
                    {
                        var rect = matrix.MapRect(addRect.Rect);
                        commands[i] = new AddRectPathCommand(rect);
                        break;
                    }
                case AddRoundRectPathCommand addRoundRect:
                    {
                        var rect = matrix.MapRect(addRoundRect.Rect);
                        var rx = addRoundRect.Rx * absScaleX;
                        var ry = addRoundRect.Ry * absScaleY;
                        commands[i] = new AddRoundRectPathCommand(rect, rx, ry);
                        break;
                    }
                case AddOvalPathCommand addOval:
                    {
                        var rect = matrix.MapRect(addOval.Rect);
                        commands[i] = new AddOvalPathCommand(rect);
                        break;
                    }
                case AddCirclePathCommand addCircle:
                    {
                        var center = matrix.MapPoint(new SKPoint(addCircle.X, addCircle.Y));
                        var rx = addCircle.Radius * absScaleX;
                        var ry = addCircle.Radius * absScaleY;
                        if (uniformScale)
                        {
                            commands[i] = new AddCirclePathCommand(center.X, center.Y, rx);
                        }
                        else
                        {
                            var rect = SKRect.Create(center.X - rx, center.Y - ry, rx * 2, ry * 2);
                            commands[i] = new AddOvalPathCommand(rect);
                        }
                        break;
                    }
                case AddPolyPathCommand addPoly:
                    {
                        if (addPoly.Points is { } points)
                        {
                            var mapped = new SKPoint[points.Count];
                            for (var j = 0; j < points.Count; j++)
                            {
                                mapped[j] = matrix.MapPoint(points[j]);
                            }
                            commands[i] = new AddPolyPathCommand(mapped, addPoly.Close);
                        }
                        break;
                    }
            }
        }
    }

    private static bool IsAxisAligned(SKMatrix matrix)
    {
        return matrix.SkewX == 0f &&
               matrix.SkewY == 0f &&
               matrix.Persp0 == 0f &&
               matrix.Persp1 == 0f &&
               matrix.Persp2 == 1f;
    }
}
