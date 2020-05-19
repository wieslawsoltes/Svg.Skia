using System;
using System.Collections.Generic;
using AM = Avalonia.Media;

namespace Svg.Model.Avalonia
{
    public class AvaloniaPicture : IDisposable
    {
        internal readonly IList<DrawCommand>? Commands;

        public AvaloniaPicture()
        {
            Commands = new List<DrawCommand>();
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
            if (Commands == null)
            {
                return;
            }

            using var transformContainerState = context.PushTransformContainer();

            var pushedStates = new Stack<Stack<IDisposable>>();

            foreach (var command in Commands)
            {
                Draw(context, command, pushedStates);
            }
        }

        public void Dispose()
        {
            if (Commands == null)
            {
                return;
            }

            foreach (var command in Commands)
            {
                command.Dispose();
            }
        }
    }
}
