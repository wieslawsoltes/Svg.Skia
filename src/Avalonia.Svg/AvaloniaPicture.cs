using System;
using System.Collections.Generic;
using Avalonia.Svg.Commands;
using ShimSkiaSharp;
using A = Avalonia;
using AM = Avalonia.Media;
using AP = Avalonia.Platform;
using AVMI = Avalonia.Media.Imaging;
using SP = Svg.Model;

namespace Avalonia.Svg;

public sealed class AvaloniaPicture : IDisposable
{
    private readonly List<DrawCommand> _commands;

    public IReadOnlyList<DrawCommand> Commands => _commands;

    private AvaloniaPicture()
    {
        _commands = [];
    }

    private static void RecordPathCommand(DrawPathCanvasCommand drawPathCanvasCommand, List<DrawCommand> commands)
    {
        if (drawPathCanvasCommand.Path is null || drawPathCanvasCommand.Paint is null)
        {
            return;
        }

        var (brush, pen) = drawPathCanvasCommand.Paint.ToBrushAndPen(drawPathCanvasCommand.Path.Bounds);

        if (drawPathCanvasCommand.Path.Commands?.Count == 1)
        {
            var pathCommand = drawPathCanvasCommand.Path.Commands[0];
            var success = false;

            switch (pathCommand)
            {
                case AddRectPathCommand addRectPathCommand:
                {
                    var rect = addRectPathCommand.Rect.ToRect();
                    commands.Add(new RectangleDrawCommand(brush, pen, rect, 0, 0));
                    success = true;
                    break;
                }
                case AddRoundRectPathCommand addRoundRectPathCommand:
                {
                    var rect = addRoundRectPathCommand.Rect.ToRect();
                    var rx = addRoundRectPathCommand.Rx;
                    var ry = addRoundRectPathCommand.Ry;
                    commands.Add(new RectangleDrawCommand(brush, pen, rect, rx, ry));
                    success = true;
                    break;
                }
                case AddOvalPathCommand addOvalPathCommand:
                {
                    var rect = addOvalPathCommand.Rect.ToRect();
                    var ellipseGeometry = new AM.EllipseGeometry(rect);
                    commands.Add(new GeometryDrawCommand(brush, pen, ellipseGeometry));
                    success = true;
                    break;
                }
                case AddCirclePathCommand addCirclePathCommand:
                {
                    var x = addCirclePathCommand.X;
                    var y = addCirclePathCommand.Y;
                    var radius = addCirclePathCommand.Radius;
                    var rect = new A.Rect(x - radius, y - radius, radius + radius, radius + radius);
                    var ellipseGeometry = new AM.EllipseGeometry(rect);
                    commands.Add(new GeometryDrawCommand(brush, pen, ellipseGeometry));
                    success = true;
                    break;
                }
                case AddPolyPathCommand addPolyPathCommand:
                {
                    if (addPolyPathCommand.Points is { })
                    {
                        var close = addPolyPathCommand.Close;
                        var polylineGeometry = addPolyPathCommand.Points.ToGeometry(close);
                        commands.Add(new GeometryDrawCommand(brush, pen, polylineGeometry));
                        success = true;
                    }
                    break;
                }
            }

            if (success)
            {
                return;
            }
        }

        if (drawPathCanvasCommand.Path.Commands?.Count == 2)
        {
            var pathCommand1 = drawPathCanvasCommand.Path.Commands[0];
            var pathCommand2 = drawPathCanvasCommand.Path.Commands[1];

            if (pathCommand1 is MoveToPathCommand moveTo && pathCommand2 is LineToPathCommand lineTo)
            {
                var p1 = new A.Point(moveTo.X, moveTo.Y);
                var p2 = new A.Point(lineTo.X, lineTo.Y);
                commands.Add(new LineDrawCommand(pen, p1, p2));
                return;
            }
        }

        var geometry = drawPathCanvasCommand.Path.ToGeometry(brush is { });
        if (geometry is { })
        {
            commands.Add(new GeometryDrawCommand(brush, pen, geometry));
        }
    }

    private static void RecordCommand(CanvasCommand canvasCommand, List<DrawCommand> commands)
    {
        switch (canvasCommand)
        {
            case ClipPathCanvasCommand clipPathCanvasCommand:
            {
                if (clipPathCanvasCommand.ClipPath is { })
                {
                    var path = clipPathCanvasCommand.ClipPath.ToGeometry(false);
                    if (path is { })
                    {
                        // TODO: clipPathCanvasCommand.Operation;
                        // TODO: clipPathCanvasCommand.Antialias;
                        commands.Add(new GeometryClipDrawCommand(path));
                    }
                }
                break;
            }
            case ClipRectCanvasCommand clipRectCanvasCommand:
            {
                var rect = clipRectCanvasCommand.Rect.ToRect();
                // TODO: clipRectCanvasCommand.Operation;
                // TODO: clipRectCanvasCommand.Antialias;
                commands.Add(new ClipDrawCommand(rect));
                break;
            }
            case SaveCanvasCommand _:
            {
                // TODO: SaveCanvasCommand
                commands.Add(new SaveDrawCommand());
                break;
            }
            case RestoreCanvasCommand _:
            {
                // TODO: RestoreCanvasCommand
                commands.Add(new RestoreDrawCommand());
                break;
            }
            case SetMatrixCanvasCommand setMatrixCanvasCommand:
            {
                var matrix = setMatrixCanvasCommand.DeltaMatrix.ToMatrix();
                commands.Add(new PushTransformDrawCommand(matrix));
                break;
            }
            case SaveLayerCanvasCommand saveLayerCanvasCommand:
            {
                // TODO: SaveLayerCanvasCommand
                commands.Add(new SaveLayerDrawCommand());
                break;
            }
            case DrawImageCanvasCommand drawImageCanvasCommand:
            {
                if (drawImageCanvasCommand.Image is { })
                {
                    var image = drawImageCanvasCommand.Image.ToBitmap();
                    if (image is { })
                    {
                        var source = drawImageCanvasCommand.Source.ToRect();
                        var dest = drawImageCanvasCommand.Dest.ToRect();
                        var bitmapInterpolationMode = drawImageCanvasCommand.Paint?.FilterQuality.ToBitmapInterpolationMode() ?? AVMI.BitmapInterpolationMode.None;
                        commands.Add(new ImageDrawCommand(image, source, dest, bitmapInterpolationMode));
                    }
                }
                break;
            }
            case DrawPathCanvasCommand drawPathCanvasCommand:
            {
                RecordPathCommand(drawPathCanvasCommand, commands);
                break;
            }
            case DrawTextBlobCanvasCommand drawPositionedTextCanvasCommand:
            {
                // TODO: DrawTextBlobCanvasCommand
                break;
            }
            case DrawTextCanvasCommand drawTextCanvasCommand:
            {
                if (drawTextCanvasCommand.Paint is { })
                {
                    // TOD: Calculate text bounds.
                    var bounds = new SKRect(0f, 0f, 0f, 0f);
                    var (brush, _) = drawTextCanvasCommand.Paint.ToBrushAndPen(bounds);
                    var text = drawTextCanvasCommand.Paint.ToFormattedText(drawTextCanvasCommand.Text, brush);
                    var x = drawTextCanvasCommand.X;
                    var y = drawTextCanvasCommand.Y;
                    var origin = new A.Point(x, y - drawTextCanvasCommand.Paint.TextSize);
                    commands.Add(new TextDrawCommand(origin, text));
                }
                break;
            }
            case DrawTextOnPathCanvasCommand drawTextOnPathCanvasCommand:
            {
                // TODO: DrawTextOnPathCanvasCommand
                break;
            }
            default:
            {
                break;
            }
        }
    }

    public static AvaloniaPicture Record(SKPicture picture)
    {
        var avaloniaPicture = new AvaloniaPicture();

        if (picture.Commands is null)
        {
            return avaloniaPicture;
        }

        foreach (var canvasCommand in picture.Commands)
        {
            RecordCommand(canvasCommand, avaloniaPicture._commands);
        }

        return avaloniaPicture;
    }

    private void Draw(AM.DrawingContext context, DrawCommand command, Stack<Stack<IDisposable>> pushedStates)
    {
        switch (command)
        {
            case GeometryClipDrawCommand geometryClipDrawCommand:
            {
                if (geometryClipDrawCommand.Clip is { })
                {
                    var geometryPushedState = context.PushGeometryClip(geometryClipDrawCommand.Clip);
                    var currentPushedStates = pushedStates.Peek();
                    currentPushedStates.Push(geometryPushedState);
                }
                break;
            }
            case ClipDrawCommand clipDrawCommand:
            {
                var clipPushedState = context.PushClip(clipDrawCommand.Clip);
                var currentPushedStates = pushedStates.Peek();
                currentPushedStates.Push(clipPushedState);
                break;
            }
            case SaveDrawCommand _:
            {
                pushedStates.Push(new Stack<IDisposable>());
                break;
            }
            case RestoreDrawCommand _:
            {
                var currentPushedStates = pushedStates.Pop();
                while (currentPushedStates.Count > 0)
                {
                    var pushedState = currentPushedStates.Pop();
                    pushedState.Dispose();
                }
                break;
            }
            case PushTransformDrawCommand pushTransformDrawCommand:
            {
                var transformPreTransform = context.PushTransform(pushTransformDrawCommand.Matrix);
                var currentPushedStates = pushedStates.Peek();
                currentPushedStates.Push(transformPreTransform);
                break;
            }
            case SaveLayerDrawCommand saveLayerDrawCommand:
            {
                pushedStates.Push(new Stack<IDisposable>());
                break;
            }
            case ImageDrawCommand imageDrawCommand:
            {
                if (imageDrawCommand.Source is { })
                {
                    // TODO: imageDrawCommand.BitmapInterpolationMode
                    context.DrawImage(
                        imageDrawCommand.Source,
                        imageDrawCommand.SourceRect,
                        imageDrawCommand.DestRect);
                }
                break;
            }
            case GeometryDrawCommand geometryDrawCommand:
            {
                if (geometryDrawCommand.Geometry is { })
                {
                    context.DrawGeometry(
                        geometryDrawCommand.Brush,
                        geometryDrawCommand.Pen,
                        geometryDrawCommand.Geometry);
                }
                break;
            }
            case LineDrawCommand lineDrawCommand:
            {
                if (lineDrawCommand.Pen is { })
                {
                    context.DrawLine(
                        lineDrawCommand.Pen,
                        lineDrawCommand.P1,
                        lineDrawCommand.P2);
                }
                break;
            }
            case RectangleDrawCommand rectangleDrawCommand:
            {
                context.DrawRectangle(
                    rectangleDrawCommand.Brush,
                    rectangleDrawCommand.Pen,
                    rectangleDrawCommand.Rect,
                    rectangleDrawCommand.RadiusX,
                    rectangleDrawCommand.RadiusY);
                break;
            }
            case TextDrawCommand textDrawCommand:
            {
                if (textDrawCommand.FormattedText is { })
                {
                    context.DrawText(
                        textDrawCommand.FormattedText,
                        textDrawCommand.Origin);
                }
                break;
            }
        }
    }

    public void Draw(AM.DrawingContext context)
    {
        using var transformContainerState = context.PushTransform(Matrix.Identity);
        var pushedStates = new Stack<Stack<IDisposable>>();

        foreach (var command in _commands)
        {
            Draw(context, command, pushedStates);
        }
    }

    public void Dispose()
    {
        foreach (var command in _commands)
        {
            command.Dispose();
        }
    }
}
