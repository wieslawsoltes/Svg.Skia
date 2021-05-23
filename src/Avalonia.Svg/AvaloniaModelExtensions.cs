using System;
using System.Collections.Generic;
using Svg.Model.Painting;
using Svg.Model.Primitives.PathCommands;
using Svg.Model.Painting.PathEffects;
using Svg.Model.Primitives;
using Svg.Model.Painting.Shaders;
using A = Avalonia;
using AM = Avalonia.Media;
using AMI = Avalonia.Media.Imaging;
using AVMI = Avalonia.Visuals.Media.Imaging;
using SP = Svg.Model;

namespace Avalonia.Svg
{
    public static class AvaloniaModelExtensions
    {
        private static Point TransformPoint(this A.Matrix matrix, Point point)
        {
            return new Point(
                (point.X * matrix.M11) + (point.Y * matrix.M21) + matrix.M31,
                (point.X * matrix.M12) + (point.Y * matrix.M22) + matrix.M32);
        }

        public static A.Point ToPoint(this global::Svg.Model.Primitives.Point point)
        {
            return new A.Point(point.X, point.Y);
        }

        public static A.Point[] ToPoints(this IList<global::Svg.Model.Primitives.Point> points)
        {
            var skPoints = new A.Point[points.Count];

            for (int i = 0; i < points.Count; i++)
            {
                skPoints[i] = points[i].ToPoint();
            }

            return skPoints;
        }

        public static A.Size ToSize(this global::Svg.Model.Primitives.Size size)
        {
            return new A.Size(size.Width, size.Height);
        }

        public static A.Rect ToRect(this global::Svg.Model.Primitives.Rect rect)
        {
            return new A.Rect(rect.Left, rect.Top, rect.Width, rect.Height);
        }

        public static A.Matrix ToMatrix(this global::Svg.Model.Primitives.Matrix matrix)
        {
            // The Persp0, Persp1 and Persp2 are not used.
            return new A.Matrix(
                matrix.ScaleX,
                matrix.SkewY,
                matrix.SkewX,
                matrix.ScaleY,
                matrix.TransX,
                matrix.TransY);
        }

        public static AMI.Bitmap? ToBitmap(this Image image)
        {
            if (image.Data is null)
            {
                return null;
            }
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

        public static AM.Typeface? ToTypeface(this Typeface? typeface)
        {
            if (typeface is null)
            {
                return null;
            }

            var familyName = typeface.FamilyName;
            var weight = typeface.Weight.ToFontWeight();
            // TODO: typeface.Weight
            var slant = typeface.Style.ToFontStyle();

            return new AM.Typeface(familyName, slant, weight);
        }

        public static AM.Color ToColor(this Color color)
        {
            return new AM.Color(color.Alpha, color.Red, color.Green, color.Blue);
        }

        public static AM.Color ToColor(this ColorF color)
        {
            return new AM.Color(
                (byte)(color.Alpha * 255f),
                (byte)(color.Red * 255f),
                (byte)(color.Green * 255f),
                (byte)(color.Blue * 255f));
        }

        public static AM.Color[] ToColors(this Color[] colors)
        {
            var skColors = new AM.Color[colors.Length];

            for (int i = 0; i < colors.Length; i++)
            {
                skColors[i] = colors[i].ToColor();
            }

            return skColors;
        }

        public static AM.Color[] ToColors(this ColorF[] colors)
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

        private static AM.SolidColorBrush ToSolidColorBrush(this ColorShader colorShader)
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

        public static AM.IBrush? ToLinearGradientBrush(this LinearGradientShader linearGradientShader)
        {
            if (linearGradientShader.Colors is { } && linearGradientShader.ColorPos is { })
            {
                var linearGradientBrush = new AM.LinearGradientBrush
                {
                    SpreadMethod = linearGradientShader.Mode.ToGradientSpreadMethod()
                };

                var startPoint = linearGradientShader.Start.ToPoint();
                var endPoint = linearGradientShader.End.ToPoint();

                if (linearGradientShader.LocalMatrix is { })
                {
                    // TODO: linearGradientShader.LocalMatrix
                    var localMatrix = linearGradientShader.LocalMatrix.Value.ToMatrix();
                    startPoint = localMatrix.TransformPoint(startPoint);
                    endPoint = localMatrix.TransformPoint(endPoint);
                }

                linearGradientBrush.StartPoint = new A.RelativePoint(startPoint, A.RelativeUnit.Absolute);

                linearGradientBrush.EndPoint = new A.RelativePoint(endPoint, A.RelativeUnit.Absolute);

                linearGradientBrush.GradientStops = new AM.GradientStops();

                for (int i = 0; i < linearGradientShader.Colors.Length; i++)
                {
                    var color = linearGradientShader.Colors[i].ToColor();
                    var offset = linearGradientShader.ColorPos[i];
                    var gradientStop = new AM.GradientStop(color, offset);
                    linearGradientBrush.GradientStops.Add(gradientStop);
                }

                return linearGradientBrush;
            }

            return null;
        }

        public static AM.IBrush? ToRadialGradientBrush(this TwoPointConicalGradientShader twoPointConicalGradientShader)
        {
            if (twoPointConicalGradientShader.Colors is { } && twoPointConicalGradientShader.ColorPos is { })
            {
                var radialGradientBrush = new AM.RadialGradientBrush
                {
                    SpreadMethod = twoPointConicalGradientShader.Mode.ToGradientSpreadMethod()
                };

                var gradientOrigin = twoPointConicalGradientShader.Start.ToPoint();
                var center = twoPointConicalGradientShader.End.ToPoint();
 
                if (twoPointConicalGradientShader.LocalMatrix is { })
                {
                    // TODO: radialGradientBrush.LocalMatrix
                    var localMatrix = twoPointConicalGradientShader.LocalMatrix.Value.ToMatrix();
                    gradientOrigin = localMatrix.TransformPoint(gradientOrigin);
                    center = localMatrix.TransformPoint(center);
                }
                
                radialGradientBrush.GradientOrigin = new A.RelativePoint(gradientOrigin, A.RelativeUnit.Absolute);

                radialGradientBrush.Center = new A.RelativePoint(center, A.RelativeUnit.Absolute);

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

                return radialGradientBrush;
            }

            return null;
        }

        public static AM.IBrush? ToBrush(this Shader? shader)
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

        private static AM.IPen ToPen(this Paint paint)
        {
            var brush = ToBrush(paint.Shader);
            var lineCap = paint.StrokeCap.ToPenLineCap();
            var lineJoin = paint.StrokeJoin.ToPenLineJoin();

            var dashStyle = default(AM.IDashStyle);
            if (paint.PathEffect is DashPathEffect dashPathEffect && dashPathEffect.Intervals is { })
            {
                var dashes = new List<double>();
                foreach (var interval in dashPathEffect.Intervals)
                {
                    dashes.Add(interval / paint.StrokeWidth);
                }
                var offset = dashPathEffect.Phase / paint.StrokeWidth;
                dashStyle = new AM.DashStyle(dashes, offset);
            }

            return new AM.Pen
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
            // TODO: paint.Color.ToColor()
            // TODO: paint.ColorFilter
            // TODO: paint.ImageFilter
            // TODO: paint.PathEffect
            // TODO: paint.BlendMode
            // TODO: paint.FilterQuality.ToBitmapInterpolationMode()

            return (brush, pen);
        }

        public static AM.FormattedText ToFormattedText(this Paint paint, string text)
        {
            var typeface = paint.Typeface?.ToTypeface();
            var textAlignment = paint.TextAlign.ToTextAlignment();
            var fontSize = paint.TextSize;
            // TODO: paint.TextEncoding
            // TODO: paint.LcdRenderText
            // TODO: paint.SubpixelText

            var ft = new AM.FormattedText
            {
                Text = text,
                Typeface = typeface ?? AM.Typeface.Default,
                FontSize = fontSize,
                TextAlignment = textAlignment,
                TextWrapping = AM.TextWrapping.NoWrap
            };

            return ft;
        }

        public static AM.FillRule ToFillRule(this PathFillType pathFillType)
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

        public static AM.Geometry? ToGeometry(this Path path, bool isFilled)
        {
            if (path.Commands is null)
            {
                return null;
            }

            var streamGeometry = new AM.StreamGeometry();

            using var streamGeometryContext = streamGeometry.Open();

            streamGeometryContext.SetFillRule(path.FillType.ToFillRule());

            bool endFigure = false;
            bool haveFigure = false;

            for (int i = 0; i < path.Commands.Count; i++)
            {
                var pathCommand = path.Commands[i];
                var isLast = i == path.Commands.Count - 1;

                switch (pathCommand)
                {
                    case MoveToPathCommand moveToPathCommand:
                        {
                            if (endFigure == true && haveFigure == false)
                            {
                                return null;
                            }
                            if (haveFigure == true)
                            {
                                streamGeometryContext.EndFigure(false);
                            }
                            if (isLast == true)
                            {
                                return streamGeometry;
                            }
                            else
                            {
                                if (path.Commands[i + 1] is MoveToPathCommand)
                                {
                                    return streamGeometry;
                                }

                                if (path.Commands[i + 1] is ClosePathCommand)
                                {
                                    return streamGeometry;
                                }
                            }
                            endFigure = true;
                            haveFigure = false;
                            var x = moveToPathCommand.X;
                            var y = moveToPathCommand.Y;
                            var point = new A.Point(x, y);
                            streamGeometryContext.BeginFigure(point, isFilled); // TODO: isFilled
                        }
                        break;

                    case LineToPathCommand lineToPathCommand:
                        {
                            if (endFigure == false)
                            {
                                return null;
                            }
                            haveFigure = true;
                            var x = lineToPathCommand.X;
                            var y = lineToPathCommand.Y;
                            var point = new A.Point(x, y);
                            streamGeometryContext.LineTo(point);
                        }
                        break;

                    case ArcToPathCommand arcToPathCommand:
                        {
                            if (endFigure == false)
                            {
                                return null;
                            }
                            haveFigure = true;
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
                            if (endFigure == false)
                            {
                                return null;
                            }
                            haveFigure = true;
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
                            if (endFigure == false)
                            {
                                return null;
                            }
                            haveFigure = true;
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
                            if (endFigure == false)
                            {
                                return null;
                            }
                            if (haveFigure == false)
                            {
                                return null;
                            }
                            endFigure = false;
                            haveFigure = false;
                            streamGeometryContext.EndFigure(true);
                        }
                        break;

                    default:
                        break;
                }
            }

            if (endFigure)
            {
                if (haveFigure == false)
                {
                    return null;
                }
                streamGeometryContext.EndFigure(false);
            }

            return streamGeometry;
        }

        public static AM.Geometry? ToGeometry(this ClipPath clipPath, bool isFilled)
        {
            return null; // TODO:
        }
    }
}
