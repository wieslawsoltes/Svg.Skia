﻿using System;
using System.Collections.Generic;
using A = Avalonia;
using AM = Avalonia.Media;
using AVMI = Avalonia.Visuals.Media.Imaging;

namespace Svg.Picture.Avalonia
{
    public sealed class AvaloniaPicture : IDisposable
    {
        private readonly List<DrawCommand>? _commands;

        public IReadOnlyList<DrawCommand>? Commands => _commands;

        private AvaloniaPicture()
        {
            _commands = new List<DrawCommand>();
        }

        private static void Record(CanvasCommand canvasCommand, AvaloniaPicture avaloniaPicture)
        {
            if (avaloniaPicture == null || avaloniaPicture._commands == null)
            {
                return;
            }

            switch (canvasCommand)
            {
                case ClipPathCanvasCommand clipPathCanvasCommand:
                    {
                        var path = clipPathCanvasCommand.ClipPath.ToGeometry(false);
                        if (path != null)
                        {
                            // TODO: clipPathCanvasCommand.Operation;
                            // TODO: clipPathCanvasCommand.Antialias;
                            avaloniaPicture._commands.Add(new GeometryClipDrawCommand(path));
                        }
                    }
                    break;
                case ClipRectCanvasCommand clipRectCanvasCommand:
                    {
                        var rect = clipRectCanvasCommand.Rect.ToSKRect();
                        // TODO: clipRectCanvasCommand.Operation;
                        // TODO: clipRectCanvasCommand.Antialias;
                        avaloniaPicture._commands.Add(new ClipDrawCommand(rect));
                    }
                    break;
                case SaveCanvasCommand _:
                    {
                        // TODO:
                        avaloniaPicture._commands.Add(new SaveDrawCommand());
                    }
                    break;
                case RestoreCanvasCommand _:
                    {
                        // TODO:
                        avaloniaPicture._commands.Add(new RestoreDrawCommand());
                    }
                    break;
                case SetMatrixCanvasCommand setMatrixCanvasCommand:
                    {
                        var matrix = setMatrixCanvasCommand.Matrix.ToMatrix();
                        avaloniaPicture._commands.Add(new SetTransformDrawCommand(matrix));
                    }
                    break;
                case SaveLayerCanvasCommand saveLayerCanvasCommand:
                    {
                        // TODO:
                        avaloniaPicture._commands.Add(new SaveLayerDrawCommand());
                    }
                    break;
                case DrawImageCanvasCommand drawImageCanvasCommand:
                    {
                        if (drawImageCanvasCommand.Image != null)
                        {
                            var image = drawImageCanvasCommand.Image.ToBitmap();
                            if (image != null)
                            {
                                var source = drawImageCanvasCommand.Source.ToSKRect();
                                var dest = drawImageCanvasCommand.Dest.ToSKRect();
                                var bitmapInterpolationMode = drawImageCanvasCommand.Paint?.FilterQuality.ToBitmapInterpolationMode() ?? AVMI.BitmapInterpolationMode.Default;
                                avaloniaPicture._commands.Add(new ImageDrawCommand(image, source, dest, bitmapInterpolationMode));
                            }
                        }
                    }
                    break;
                case DrawPathCanvasCommand drawPathCanvasCommand:
                    {
                        if (drawPathCanvasCommand.Path != null && drawPathCanvasCommand.Paint != null)
                        {
                            (var brush, var pen) = drawPathCanvasCommand.Paint.ToBrushAndPen();

                            if (drawPathCanvasCommand.Path.Commands?.Count == 1)
                            {
                                var pathCommand = drawPathCanvasCommand.Path.Commands[0];
                                var success = false;

                                switch (pathCommand)
                                {
                                    case AddRectPathCommand addRectPathCommand:
                                        {
                                            var rect = addRectPathCommand.Rect.ToSKRect();
                                            avaloniaPicture._commands.Add(new RectangleDrawCommand(brush, pen, rect, 0, 0));
                                            success = true;
                                        }
                                        break;
                                    case AddRoundRectPathCommand addRoundRectPathCommand:
                                        {
                                            var rect = addRoundRectPathCommand.Rect.ToSKRect();
                                            var rx = addRoundRectPathCommand.Rx;
                                            var ry = addRoundRectPathCommand.Ry;
                                            avaloniaPicture._commands.Add(new RectangleDrawCommand(brush, pen, rect, rx, ry));
                                            success = true;
                                        }
                                        break;
                                    case AddOvalPathCommand addOvalPathCommand:
                                        {
                                            var rect = addOvalPathCommand.Rect.ToSKRect();
                                            var ellipseGeometry = new AM.EllipseGeometry(rect);
                                            avaloniaPicture._commands.Add(new GeometryDrawCommand(brush, pen, ellipseGeometry));
                                            success = true;
                                        }
                                        break;
                                    case AddCirclePathCommand addCirclePathCommand:
                                        {
                                            var x = addCirclePathCommand.X;
                                            var y = addCirclePathCommand.Y;
                                            var radius = addCirclePathCommand.Radius;
                                            var rect = new A.Rect(x - radius, y - radius, radius + radius, radius + radius);
                                            var ellipseGeometry = new AM.EllipseGeometry(rect);
                                            avaloniaPicture._commands.Add(new GeometryDrawCommand(brush, pen, ellipseGeometry));
                                            success = true;
                                        }
                                        break;
                                    case AddPolyPathCommand addPolyPathCommand:
                                        {
                                            if (addPolyPathCommand.Points != null)
                                            {
                                                var points = addPolyPathCommand.Points.ToPoints();
                                                var close = addPolyPathCommand.Close;
                                                var polylineGeometry = new AM.PolylineGeometry(points, close);
                                                avaloniaPicture._commands.Add(new GeometryDrawCommand(brush, pen, polylineGeometry));
                                                success = true;
                                            }
                                        }
                                        break;
                                }

                                if (success)
                                {
                                    break;
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
                                    avaloniaPicture._commands.Add(new LineDrawCommand(pen, p1, p2));
                                    break;
                                }
                            }

                            var geometry = drawPathCanvasCommand.Path.ToGeometry(brush != null);
                            if (geometry != null)
                            {
                                avaloniaPicture._commands.Add(new GeometryDrawCommand(brush, pen, geometry));
                            }
                        }
                    }
                    break;
                case DrawTextBlobCanvasCommand drawPositionedTextCanvasCommand:
                    {
                        // TODO:
                    }
                    break;
                case DrawTextCanvasCommand drawTextCanvasCommand:
                    {
                        if (drawTextCanvasCommand.Paint != null)
                        {
                            (var brush, _) = drawTextCanvasCommand.Paint.ToBrushAndPen();
                            var text = drawTextCanvasCommand.Paint.ToFormattedText(drawTextCanvasCommand.Text);
                            var x = drawTextCanvasCommand.X;
                            var y = drawTextCanvasCommand.Y;
                            var origin = new A.Point(x, y - drawTextCanvasCommand.Paint.TextSize);
                            avaloniaPicture._commands.Add(new TextDrawCommand(brush, origin, text));
                        }
                    }
                    break;
                case DrawTextOnPathCanvasCommand drawTextOnPathCanvasCommand:
                    {
                        // TODO:
                    }
                    break;
                default:
                    break;
            }
        }

        public static AvaloniaPicture Record(Picture picture)
        {
            var avaloniaPicture = new AvaloniaPicture();

            if (picture.Commands == null)
            {
                return avaloniaPicture;
            }

            foreach (var canvasCommand in picture.Commands)
            {
                Record(canvasCommand, avaloniaPicture);
            }

            return avaloniaPicture;
        }

        private void Draw(AM.DrawingContext context, DrawCommand command, Stack<Stack<IDisposable>> pushedStates)
        {
            switch (command)
            {
                case GeometryClipDrawCommand geometryClipDrawCommand:
                    {
                        var geometryPushedState = context.PushGeometryClip(geometryClipDrawCommand.Clip);
                        var currentPushedStates = pushedStates.Peek();
                        currentPushedStates.Push(geometryPushedState);
                    }
                    break;
                case ClipDrawCommand clipDrawCommand:
                    {
                        var clipPushedState = context.PushClip(clipDrawCommand.Clip);
                        var currentPushedStates = pushedStates.Peek();
                        currentPushedStates.Push(clipPushedState);
                    }
                    break;
                case SaveDrawCommand _:
                    {
                        pushedStates.Push(new Stack<IDisposable>());
                    }
                    break;
                case RestoreDrawCommand _:
                    {
                        var currentPushedStates = pushedStates.Pop();
                        while (currentPushedStates.Count > 0)
                        {
                            var pushedState = currentPushedStates.Pop();
                            pushedState.Dispose();
                        }
                    }
                    break;
                case SetTransformDrawCommand setTransformDrawCommand:
                    {
                        var transformPreTransform = context.PushSetTransform(setTransformDrawCommand.Matrix);
                        var currentPushedStates = pushedStates.Peek();
                        currentPushedStates.Push(transformPreTransform);
                    }
                    break;
                case SaveLayerDrawCommand saveLayerDrawCommand:
                    {
                        pushedStates.Push(new Stack<IDisposable>());
                    }
                    break;
                case ImageDrawCommand imageDrawCommand:
                    {
                        context.DrawImage(
                            imageDrawCommand.Source,
                            imageDrawCommand.SourceRect,
                            imageDrawCommand.DestRect,
                            imageDrawCommand.BitmapInterpolationMode);
                    }
                    break;
                case GeometryDrawCommand geometryDrawCommand:
                    {
                        context.DrawGeometry(
                            geometryDrawCommand.Brush,
                            geometryDrawCommand.Pen,
                            geometryDrawCommand.Geometry);
                    }
                    break;
                case LineDrawCommand lineDrawCommand:
                    {
                        context.DrawLine(
                            lineDrawCommand.Pen,
                            lineDrawCommand.P1,
                            lineDrawCommand.P2);
                    }
                    break;
                case RectangleDrawCommand rectangleDrawCommand:
                    {
                        context.DrawRectangle(
                            rectangleDrawCommand.Brush,
                            rectangleDrawCommand.Pen,
                            rectangleDrawCommand.Rect,
                            rectangleDrawCommand.RadiusX,
                            rectangleDrawCommand.RadiusY);
                    }
                    break;
                case TextDrawCommand textDrawCommand:
                    {
                        context.DrawText(
                            textDrawCommand.Brush,
                            textDrawCommand.Origin,
                            textDrawCommand.FormattedText);
                    }
                    break;
            }
        }

        public void Draw(AM.DrawingContext context)
        {
            if (_commands == null)
            {
                return;
            }

            using var transformContainerState = context.PushTransformContainer();

            var pushedStates = new Stack<Stack<IDisposable>>();

            foreach (var command in _commands)
            {
                Draw(context, command, pushedStates);
            }
        }

        public void Dispose()
        {
            if (_commands == null)
            {
                return;
            }

            foreach (var command in _commands)
            {
                command.Dispose();
            }
        }
    }
}
