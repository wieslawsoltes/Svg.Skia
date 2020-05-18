using System;
using System.Collections.Generic;
using Svg.Model;
using A = Avalonia;
using AM = Avalonia.Media;
using AMI = Avalonia.Media.Imaging;
using AVMI = Avalonia.Visuals.Media.Imaging;

namespace Svg.Skia
{
    public static class AvaloniaModelExtensions
    {
        public static A.Point ToPoint(this Point point)
        {
            return new A.Point(point.X, point.Y);
        }

        public static A.Point[] ToPoints(this IList<Point> points)
        {
            var skPoints = new A.Point[points.Count];

            for (int i = 0; i < points.Count; i++)
            {
                skPoints[i] = points[i].ToPoint();
            }

            return skPoints;
        }

        public static A.Size ToSKSize(this Size size)
        {
            return new A.Size(size.Width, size.Height);
        }

        public static A.Rect ToSKRect(this Rect rect)
        {
            return new A.Rect(rect.Left, rect.Top, rect.Width, rect.Height);
        }

        public static A.Matrix ToMatrix(this Matrix matrix)
        {
            return new A.Matrix(
                matrix.ScaleX,
                matrix.SkewY,
                matrix.SkewX,
                matrix.ScaleY,
                matrix.TransX,
                matrix.TransY);
            // TODO: matrix.Persp0
            // TODO: matrix.Persp1
            // TODO: matrix.Persp2
        }

        public static AMI.Bitmap ToBitmap(this Image image)
        {
            using var memoryStream = new System.IO.MemoryStream(image.Data);
            return new AMI.Bitmap(memoryStream);
        }

        public static AM.PenLineCap ToPenLineCap(this StrokeCap strokeCap)
        {
            switch (strokeCap)
            {
                default:
                case StrokeCap.Butt:
                    return AM.PenLineCap.Flat;
                case StrokeCap.Round:
                    return AM.PenLineCap.Round;
                case StrokeCap.Square:
                    return AM.PenLineCap.Square;
            }
        }

        public static AM.PenLineJoin ToPenLineJoin(this StrokeJoin strokeJoin)
        {
            switch (strokeJoin)
            {
                default:
                case StrokeJoin.Miter:
                    return AM.PenLineJoin.Miter;
                case StrokeJoin.Round:
                    return AM.PenLineJoin.Round;
                case StrokeJoin.Bevel:
                    return AM.PenLineJoin.Bevel;
            }
        }

        public static AM.TextAlignment ToTextAlignment(this TextAlign textAlign)
        {
            switch (textAlign)
            {
                default:
                case TextAlign.Left:
                    return AM.TextAlignment.Left;
                case TextAlign.Center:
                    return AM.TextAlignment.Center;
                case TextAlign.Right:
                    return AM.TextAlignment.Right;
            }
        }

        public static AM.FontWeight ToFontWeight(this FontStyleWeight fontStyleWeight)
        {
            switch (fontStyleWeight)
            {
                default:
                case FontStyleWeight.Invisible:
                    throw new NotSupportedException(); // TODO:
                case FontStyleWeight.Thin:
                    return AM.FontWeight.Thin;
                case FontStyleWeight.ExtraLight:
                    return AM.FontWeight.ExtraLight;
                case FontStyleWeight.Light:
                    return AM.FontWeight.Light;
                case FontStyleWeight.Normal:
                    return AM.FontWeight.Normal;
                case FontStyleWeight.Medium:
                    return AM.FontWeight.Medium;
                case FontStyleWeight.SemiBold:
                    return AM.FontWeight.SemiBold;
                case FontStyleWeight.Bold:
                    return AM.FontWeight.Bold;
                case FontStyleWeight.ExtraBold:
                    return AM.FontWeight.ExtraBold;
                case FontStyleWeight.Black:
                    return AM.FontWeight.Black;
                case FontStyleWeight.ExtraBlack:
                    return AM.FontWeight.ExtraBlack;
            }
        }

        public static AM.FontStyle ToFontStyle(this FontStyleSlant fontStyleSlant)
        {
            switch (fontStyleSlant)
            {
                default:
                case FontStyleSlant.Upright:
                    return AM.FontStyle.Normal; // TODO:
                case FontStyleSlant.Italic:
                    return AM.FontStyle.Italic;
                case FontStyleSlant.Oblique:
                    return AM.FontStyle.Oblique;
            }
        }

        public static AM.Typeface? ToTypeface(this Model.Typeface? typeface)
        {
            if (typeface == null)
            {
                return null;
            }

            var familyName = typeface.FamilyName;
            var weight = typeface.Weight.ToFontWeight();
            // TODO: typeface.Weight
            var slant = typeface.Style.ToFontStyle();

            return new AM.Typeface(familyName, weight, slant);
        }

        public static AM.Color ToColor(this Model.Color color)
        {
            return new AM.Color(color.Alpha, color.Red, color.Green, color.Blue);
        }

        public static AM.Color[] ToSKColors(this Model.Color[] colors)
        {
            var skColors = new AM.Color[colors.Length];

            for (int i = 0; i < colors.Length; i++)
            {
                skColors[i] = colors[i].ToColor();
            }

            return skColors;
        }

        public static AVMI.BitmapInterpolationMode ToBitmapInterpolationMode(this FilterQuality filterQuality)
        {
            switch (filterQuality)
            {
                default:
                case FilterQuality.None:
                    return AVMI.BitmapInterpolationMode.Default;
                case FilterQuality.Low:
                    return AVMI.BitmapInterpolationMode.LowQuality;
                case FilterQuality.Medium:
                    return AVMI.BitmapInterpolationMode.MediumQuality;
                case FilterQuality.High:
                    return AVMI.BitmapInterpolationMode.HighQuality;
            }
        }

        private static AM.SolidColorBrush ToSolidColorBrush(ColorShader colorShader)
        {
            var color = colorShader.Color.ToColor();
            return new AM.SolidColorBrush(color);
        }

        public static AM.GradientSpreadMethod ToGradientSpreadMethod(this ShaderTileMode shaderTileMode)
        {
            switch (shaderTileMode)
            {
                default:
                case ShaderTileMode.Clamp:
                    return AM.GradientSpreadMethod.Pad;
                case ShaderTileMode.Repeat:
                    return AM.GradientSpreadMethod.Repeat;
                case ShaderTileMode.Mirror:
                    return AM.GradientSpreadMethod.Reflect;
            }
        }

        public static AM.IBrush? ToLinearGradientBrush(LinearGradientShader linearGradientShader)
        {
            if (linearGradientShader.Colors != null && linearGradientShader.ColorPos != null)
            {
                var linearGradientBrush = new AM.LinearGradientBrush();

                linearGradientBrush.SpreadMethod = linearGradientShader.Mode.ToGradientSpreadMethod();

                var startPoint = linearGradientShader.Start.ToPoint();
                linearGradientBrush.StartPoint = new A.RelativePoint(startPoint, A.RelativeUnit.Relative);

                var endPoint = linearGradientShader.End.ToPoint();
                linearGradientBrush.EndPoint = new A.RelativePoint(endPoint, A.RelativeUnit.Relative);

                linearGradientBrush.GradientStops = new AM.GradientStops();

                for (int i = 0; i < linearGradientShader.Colors.Length; i++)
                {
                    var color = linearGradientShader.Colors[i].ToColor();
                    var offset = linearGradientShader.ColorPos[i];
                    var gradientStop = new AM.GradientStop(color, offset);
                    linearGradientBrush.GradientStops.Add(gradientStop);
                }

                // TODO: linearGradientShader.LocalMatrix

                return linearGradientBrush;
            }

            return null;
        }

        public static AM.IBrush? ToRadialGradientBrush(TwoPointConicalGradientShader twoPointConicalGradientShader)
        {
            if (twoPointConicalGradientShader.Colors != null && twoPointConicalGradientShader.ColorPos != null)
            {
                var radialGradientBrush = new AM.RadialGradientBrush();

                radialGradientBrush.SpreadMethod = twoPointConicalGradientShader.Mode.ToGradientSpreadMethod();

                var gradientOrigin = twoPointConicalGradientShader.Start.ToPoint();
                radialGradientBrush.GradientOrigin = new A.RelativePoint(gradientOrigin, A.RelativeUnit.Relative);

                var center = twoPointConicalGradientShader.End.ToPoint();
                radialGradientBrush.Center = new A.RelativePoint(center, A.RelativeUnit.Relative);

                // TODO: twoPointConicalGradientShader.StartRadius
                radialGradientBrush.Radius = twoPointConicalGradientShader.EndRadius;

                radialGradientBrush.GradientStops = new AM.GradientStops();

                for (int i = 0; i < twoPointConicalGradientShader.Colors.Length; i++)
                {
                    var color = twoPointConicalGradientShader.Colors[i].ToColor();
                    var offset = twoPointConicalGradientShader.ColorPos[i];
                    var gradientStop = new AM.GradientStop(color, offset);
                    radialGradientBrush.GradientStops.Add(gradientStop);
                }

                // TODO: radialGradientBrush.LocalMatrix

                return radialGradientBrush;
            }

            return null;
        }

        public static AM.IBrush? ToBrush(Shader? shader)
        {
            switch (shader)
            {
                case ColorShader colorShader:
                    return ToSolidColorBrush(colorShader);
                case LinearGradientShader linearGradientShader:
                    return ToLinearGradientBrush(linearGradientShader);
                case TwoPointConicalGradientShader twoPointConicalGradientShader:
                    return ToRadialGradientBrush(twoPointConicalGradientShader);
                case PictureShader pictureShader:
                    // TODO:
                    return null;
                default:
                    return null;
            }
        }

        private static AM.IPen ToPen(Paint paint)
        {
            var brush = ToBrush(paint.Shader);
            var lineCap = paint.StrokeCap.ToPenLineCap();
            var lineJoin = paint.StrokeJoin.ToPenLineJoin();

            var dashStyle = default(AM.IDashStyle);
            if (paint.PathEffect is DashPathEffect dashPathEffect && dashPathEffect.Intervals != null)
            {
                var dashes = new List<double>();
                foreach (var interval in dashPathEffect.Intervals)
                {
                    dashes.Add(interval);
                }
                var offset = dashPathEffect.Phase;
                dashStyle = new AM.DashStyle(dashes, offset);
            }

            return new AM.Pen()
            {
                Brush = brush,
                Thickness = paint.StrokeWidth,
                LineCap = lineCap,
                LineJoin = lineJoin,
                MiterLimit = paint.StrokeMiter,
                DashStyle = dashStyle
            };
        }

        public static (AM.IBrush? brush, AM.IPen? pen) ToBrushAndPen(this Paint paint)
        {
            AM.IBrush? brush = null;
            AM.IPen? pen = null;

            if (paint.Style == PaintStyle.Fill || paint.Style == PaintStyle.StrokeAndFill)
            {
                brush = ToBrush(paint.Shader);
            }

            if (paint.Style == PaintStyle.Stroke || paint.Style == PaintStyle.StrokeAndFill)
            {
                pen = ToPen(paint);
            }

            // TODO: paint.IsAntialias
            // TODO: paint.Color
            // TODO: paint.ColorFilter
            // TODO: paint.ImageFilter
            // TODO: paint.PathEffect
            // TODO: paint.BlendMode
            // TODO: paint.FilterQuality.ToBitmapInterpolationMode()
            // TODO: paint.TextEncoding
            // TODO: paint.TextAlign.ToTextAlignment()
            // TODO: paint.Typeface?.ToTypeface()
            // TODO: paint.TextSize
            // TODO: paint.LcdRenderText
            // TODO: paint.SubpixelText

            return (brush, pen);
        }

        public static AM.FillRule ToSKPathFillType(this PathFillType pathFillType)
        {
            switch (pathFillType)
            {
                default:
                case PathFillType.Winding:
                    return AM.FillRule.NonZero;
                case PathFillType.EvenOdd:
                    return AM.FillRule.EvenOdd;
            }
        }

        public static AM.SweepDirection ToSweepDirection(this PathDirection pathDirection)
        {
            switch (pathDirection)
            {
                default:
                case PathDirection.Clockwise:
                    return AM.SweepDirection.Clockwise;
                case PathDirection.CounterClockwise:
                    return AM.SweepDirection.CounterClockwise;
            }
        }

        public static void ToStreamGeometry(this PathCommand pathCommand, AM.StreamGeometryContext streamGeometryContext)
        {
            bool endFigure = false;

            switch (pathCommand)
            {
                case MoveToPathCommand moveToPathCommand:
                    {
                        endFigure = true;
                        var x = moveToPathCommand.X;
                        var y = moveToPathCommand.Y;
                        var point = new A.Point(x, y);
                        streamGeometryContext.BeginFigure(point, false); // TODO: isFilled
                    }
                    break;
                case LineToPathCommand lineToPathCommand:
                    {
                        var x = lineToPathCommand.X;
                        var y = lineToPathCommand.Y;
                        var point = new A.Point(x, y);
                        streamGeometryContext.LineTo(point);
                    }
                    break;
                case ArcToPathCommand arcToPathCommand:
                    {
                        var x = arcToPathCommand.X;
                        var y = arcToPathCommand.Y;
                        var point = new A.Point(x, y);
                        var rx = arcToPathCommand.Rx;
                        var ry = arcToPathCommand.Ry;
                        var size = new A.Size(rx, ry);
                        var rotationAngle = arcToPathCommand.XAxisRotate;
                        var isLargeArc = arcToPathCommand.LargeArc == PathArcSize.Large;
                        var sweep = arcToPathCommand.Sweep.ToSweepDirection();
                        streamGeometryContext.ArcTo(point, size, rotationAngle, isLargeArc, sweep);
                    }
                    break;
                case QuadToPathCommand quadToPathCommand:
                    {
                        var x0 = quadToPathCommand.X0;
                        var y0 = quadToPathCommand.Y0;
                        var x1 = quadToPathCommand.X1;
                        var y1 = quadToPathCommand.Y1;
                        var control = new A.Point(x0, y0);
                        var endPoint = new A.Point(x1, y1);
                        streamGeometryContext.QuadraticBezierTo(control, endPoint);
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
                        var point1 = new A.Point(x0, y0);
                        var point2 = new A.Point(x1, y1);
                        var point3 = new A.Point(x2, y2);
                        streamGeometryContext.CubicBezierTo(point1, point2, point3);
                    }
                    break;
                case ClosePathCommand _:
                    {
                        endFigure = false;
                        streamGeometryContext.EndFigure(true);
                    }
                    break;
                case AddRectPathCommand addRectPathCommand:
                    {
                        var rect = addRectPathCommand.Rect.ToSKRect();
                        // TODO:
                    }
                    break;
                case AddRoundRectPathCommand addRoundRectPathCommand:
                    {
                        var rect = addRoundRectPathCommand.Rect.ToSKRect();
                        var rx = addRoundRectPathCommand.Rx;
                        var ry = addRoundRectPathCommand.Ry;
                        // TODO:
                    }
                    break;
                case AddOvalPathCommand addOvalPathCommand:
                    {
                        var rect = addOvalPathCommand.Rect.ToSKRect();
                        // TODO:
                    }
                    break;
                case AddCirclePathCommand addCirclePathCommand:
                    {
                        var x = addCirclePathCommand.X;
                        var y = addCirclePathCommand.Y;
                        var radius = addCirclePathCommand.Radius;
                        // TODO:
                    }
                    break;
                case AddPolyPathCommand addPolyPathCommand:
                    {
                        if (addPolyPathCommand.Points != null)
                        {
                            var points = addPolyPathCommand.Points.ToPoints();
                            var close = addPolyPathCommand.Close;
                            // TODO:
                        }
                    }
                    break;
                default:
                    break;
            }

            if (endFigure)
            {
                streamGeometryContext.EndFigure(false);
            }
        }

        public static AM.Geometry ToGeometry(this Path path)
        {
            var streamGeometry = new AM.StreamGeometry();

            using var streamGeometryContext = streamGeometry.Open();

            streamGeometryContext.SetFillRule(path.FillType.ToSKPathFillType());

            if (path.Commands == null)
            {
                return streamGeometry;
            }

            foreach (var pathCommand in path.Commands)
            {
                pathCommand.ToStreamGeometry(streamGeometryContext);
            }

            return streamGeometry;
        }

        static System.Reflection.MethodBase PushSetTransform = typeof(AM.DrawingContext).GetMethod("PushSetTransform", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

        public static void Draw(this CanvasCommand canvasCommand, AM.DrawingContext context, Stack<Stack<IDisposable>> pushedStates)
        {
            switch (canvasCommand)
            {
                case ClipPathCanvasCommand clipPathCanvasCommand:
                    {
                        var path = clipPathCanvasCommand.Path.ToGeometry();
                        // TODO: clipPathCanvasCommand.Operation;
                        // TODO: clipPathCanvasCommand.Antialias;
                        var geometryPushedState = context.PushGeometryClip(path);
                        var currentPushedStates = pushedStates.Peek();
                        currentPushedStates.Push(geometryPushedState);
                    }
                    break;
                case ClipRectCanvasCommand clipRectCanvasCommand:
                    {
                        var rect = clipRectCanvasCommand.Rect.ToSKRect();
                        // TODO: clipRectCanvasCommand.Operation;
                        // TODO: clipRectCanvasCommand.Antialias;
                        var clipPushedState = context.PushClip(rect);
                        var currentPushedStates = pushedStates.Peek();
                        currentPushedStates.Push(clipPushedState);
                    }
                    break;
                case SaveCanvasCommand _:
                    {
                        // TODO:
                        //throw new NotImplementedException();
                        pushedStates.Push(new Stack<IDisposable>());
                    }
                    break;
                case RestoreCanvasCommand _:
                    {
                        // TODO:
                        //throw new NotImplementedException();
                        var currentPushedStates = pushedStates.Pop();
                        while (currentPushedStates.Count > 0)
                        {
                            var pushedState = currentPushedStates.Pop();
                            pushedState.Dispose();
                        }
                    }
                    break;
                case SetMatrixCanvasCommand setMatrixCanvasCommand:
                    {
                        var matrix = setMatrixCanvasCommand.Matrix.ToMatrix();
                        // TODO:
                        //var transformPreTransform = context.PushPreTransform(matrix); // TODO: PushSetTransform
                        // HACK: https://github.com/AvaloniaUI/Avalonia/issues/3954
                        var transformPreTransform = (AM.DrawingContext.PushedState)PushSetTransform.Invoke(context, new object[] { matrix });
                        var currentPushedStates = pushedStates.Peek();
                        currentPushedStates.Push(transformPreTransform);
                    }
                    break;
                case SaveLayerCanvasCommand saveLayerCanvasCommand:
                    {
                        // TODO:
                        //throw new NotImplementedException();
                        pushedStates.Push(new Stack<IDisposable>());
                    }
                    break;
                case DrawImageCanvasCommand drawImageCanvasCommand:
                    {
                        if (drawImageCanvasCommand.Image != null)
                        {
                            var image = drawImageCanvasCommand.Image.ToBitmap();
                            var source = drawImageCanvasCommand.Source.ToSKRect();
                            var dest = drawImageCanvasCommand.Dest.ToSKRect();
                            var bitmapInterpolationMode = drawImageCanvasCommand.Paint?.FilterQuality.ToBitmapInterpolationMode() ?? AVMI.BitmapInterpolationMode.Default;
                            context.DrawImage(image, source, dest, bitmapInterpolationMode);
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
                                            context.DrawRectangle(brush, pen, rect);
                                            success = true;
                                        }
                                        break;
                                    case AddRoundRectPathCommand addRoundRectPathCommand:
                                        {
                                            var rect = addRoundRectPathCommand.Rect.ToSKRect();
                                            var rx = addRoundRectPathCommand.Rx;
                                            var ry = addRoundRectPathCommand.Ry;
                                            context.DrawRectangle(brush, pen, rect, rx, ry);
                                            success = true;
                                        }
                                        break;
                                    case AddOvalPathCommand addOvalPathCommand:
                                        {
                                            var rect = addOvalPathCommand.Rect.ToSKRect();
                                            var ellipseGeometry = new AM.EllipseGeometry(rect);
                                            context.DrawGeometry(brush, pen, ellipseGeometry);
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
                                            context.DrawGeometry(brush, pen, ellipseGeometry);
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
                                                context.DrawGeometry(brush, pen, polylineGeometry);
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
                                    context.DrawLine(pen, p1, p2);
                                    break;
                                }
                            }

                            var geometry = drawPathCanvasCommand.Path.ToGeometry();
                            context.DrawGeometry(brush, pen, geometry);
                        }
                    }
                    break;
                case DrawPositionedTextCanvasCommand drawPositionedTextCanvasCommand:
                    {
                        // TODO:
                    }
                    break;
                case DrawTextCanvasCommand drawTextCanvasCommand:
                    {
                        if (drawTextCanvasCommand.Paint != null)
                        {
                            var x = drawTextCanvasCommand.X;
                            var y = drawTextCanvasCommand.Y;
                            var origin = new A.Point(x, y);

                            (var brush, _) = drawTextCanvasCommand.Paint.ToBrushAndPen();

                            var text = drawTextCanvasCommand.Text;
                            var typeface = drawTextCanvasCommand.Paint.Typeface?.ToTypeface();
                            var textAlignment = drawTextCanvasCommand.Paint.TextAlign.ToTextAlignment();
                            var fontSize = drawTextCanvasCommand.Paint.TextSize;
                            // TODO: drawTextCanvasCommand.Paint.LcdRenderText
                            // TODO: drawTextCanvasCommand.Paint.SubpixelText
                            var ft = new AM.FormattedText()
                            {
                                Text = text,
                                Typeface = typeface,
                                FontSize = fontSize,
                                TextAlignment = textAlignment,
                                TextWrapping = AM.TextWrapping.NoWrap
                            };
                            context.DrawText(brush, origin, ft);
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

        public static void Draw(this Picture picture, AM.DrawingContext context)
        {
            if (picture.Commands == null)
            {
                return;
            }

            var transformContainerState = context.PushTransformContainer();

            var pushedStates = new Stack<Stack<IDisposable>>();

            foreach (var canvasCommand in picture.Commands)
            {
                canvasCommand.Draw(context, pushedStates);
            }

            transformContainerState.Dispose();
        }
    }
}
