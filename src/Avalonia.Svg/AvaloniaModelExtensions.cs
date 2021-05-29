using System;
using System.Collections.Generic;
using ShimSkiaSharp.Painting;
using ShimSkiaSharp.Painting.PathEffects;
using ShimSkiaSharp.Painting.Shaders;
using ShimSkiaSharp.Primitives;
using ShimSkiaSharp.Primitives.PathCommands;
using A = Avalonia;
using AM = Avalonia.Media;
using AMI = Avalonia.Media.Imaging;
using AMII = Avalonia.Media.Immutable;
using AP = Avalonia.Platform;
using AVMI = Avalonia.Visuals.Media.Imaging;

namespace Avalonia.Svg
{
    public static class AvaloniaModelExtensions
    {
        private static AP.IPlatformRenderInterface Factory => A.AvaloniaLocator.Current.GetService<AP.IPlatformRenderInterface>();

        public static Point Transform(this Matrix m, Point point)
        {
            return point * m;
        }

        public static A.Point ToPoint(this SKPoint point)
        {
            return new A.Point(point.X, point.Y);
        }

        public static A.Point[] ToPoints(this IList<SKPoint> points)
        {
            var skPoints = new A.Point[points.Count];

            for (int i = 0; i < points.Count; i++)
            {
                skPoints[i] = points[i].ToPoint();
            }

            return skPoints;
        }

        public static A.Size ToSize(this SKSize size)
        {
            return new A.Size(size.Width, size.Height);
        }

        public static A.Rect ToRect(this SKRect rect)
        {
            return new A.Rect(rect.Left, rect.Top, rect.Width, rect.Height);
        }

        public static A.Matrix ToMatrix(this SKMatrix matrix)
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

        public static AMI.Bitmap? ToBitmap(this SKImage image)
        {
            if (image.Data is null)
            {
                return null;
            }
            using var memoryStream = new System.IO.MemoryStream(image.Data);
            return new AMI.Bitmap(memoryStream);
        }

        public static AM.PenLineCap ToPenLineCap(this SKStrokeCap strokeCap)
        {
            switch (strokeCap)
            {
                default:
                case SKStrokeCap.Butt:
                    return AM.PenLineCap.Flat;

                case SKStrokeCap.Round:
                    return AM.PenLineCap.Round;

                case SKStrokeCap.Square:
                    return AM.PenLineCap.Square;
            }
        }

        public static AM.PenLineJoin ToPenLineJoin(this SKStrokeJoin strokeJoin)
        {
            switch (strokeJoin)
            {
                default:
                case SKStrokeJoin.Miter:
                    return AM.PenLineJoin.Miter;

                case SKStrokeJoin.Round:
                    return AM.PenLineJoin.Round;

                case SKStrokeJoin.Bevel:
                    return AM.PenLineJoin.Bevel;
            }
        }

        public static AM.TextAlignment ToTextAlignment(this SKTextAlign textAlign)
        {
            switch (textAlign)
            {
                default:
                case SKTextAlign.Left:
                    return AM.TextAlignment.Left;

                case SKTextAlign.Center:
                    return AM.TextAlignment.Center;

                case SKTextAlign.Right:
                    return AM.TextAlignment.Right;
            }
        }

        public static AM.FontWeight ToFontWeight(this SKFontStyleWeight fontStyleWeight)
        {
            switch (fontStyleWeight)
            {
                default:
                case SKFontStyleWeight.Invisible:
                    // TODO: FontStyleWeight.Invisible
                    throw new NotSupportedException();
                case SKFontStyleWeight.Thin:
                    return AM.FontWeight.Thin;

                case SKFontStyleWeight.ExtraLight:
                    return AM.FontWeight.ExtraLight;

                case SKFontStyleWeight.Light:
                    return AM.FontWeight.Light;

                case SKFontStyleWeight.Normal:
                    return AM.FontWeight.Normal;

                case SKFontStyleWeight.Medium:
                    return AM.FontWeight.Medium;

                case SKFontStyleWeight.SemiBold:
                    return AM.FontWeight.SemiBold;

                case SKFontStyleWeight.Bold:
                    return AM.FontWeight.Bold;

                case SKFontStyleWeight.ExtraBold:
                    return AM.FontWeight.ExtraBold;

                case SKFontStyleWeight.Black:
                    return AM.FontWeight.Black;

                case SKFontStyleWeight.ExtraBlack:
                    return AM.FontWeight.ExtraBlack;
            }
        }

        public static AM.FontStyle ToFontStyle(this SKFontStyleSlant fontStyleSlant)
        {
            switch (fontStyleSlant)
            {
                default:
                case SKFontStyleSlant.Upright:
                    // TODO: FontStyleSlant.Upright
                    return AM.FontStyle.Normal;
                case SKFontStyleSlant.Italic:
                    return AM.FontStyle.Italic;

                case SKFontStyleSlant.Oblique:
                    return AM.FontStyle.Oblique;
            }
        }

        public static AM.Typeface? ToTypeface(this SKTypeface? typeface)
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

        public static AM.Color ToColor(this SKColor color)
        {
            return new AM.Color(color.Alpha, color.Red, color.Green, color.Blue);
        }

        public static AM.Color ToColor(this SKColorF color)
        {
            return new AM.Color(
                (byte)(color.Alpha * 255f),
                (byte)(color.Red * 255f),
                (byte)(color.Green * 255f),
                (byte)(color.Blue * 255f));
        }

        public static AM.Color[] ToColors(this SKColor[] colors)
        {
            var skColors = new AM.Color[colors.Length];

            for (int i = 0; i < colors.Length; i++)
            {
                skColors[i] = colors[i].ToColor();
            }

            return skColors;
        }

        public static AM.Color[] ToColors(this SKColorF[] colors)
        {
            var skColors = new AM.Color[colors.Length];

            for (int i = 0; i < colors.Length; i++)
            {
                skColors[i] = colors[i].ToColor();
            }

            return skColors;
        }

        public static AVMI.BitmapInterpolationMode ToBitmapInterpolationMode(this SKFilterQuality filterQuality)
        {
            switch (filterQuality)
            {
                default:
                case SKFilterQuality.None:
                    return AVMI.BitmapInterpolationMode.Default;

                case SKFilterQuality.Low:
                    return AVMI.BitmapInterpolationMode.LowQuality;

                case SKFilterQuality.Medium:
                    return AVMI.BitmapInterpolationMode.MediumQuality;

                case SKFilterQuality.High:
                    return AVMI.BitmapInterpolationMode.HighQuality;
            }
        }

        private static AM.IBrush ToSolidColorBrush(this ColorShader colorShader)
        {
            var color = colorShader.Color.ToColor();
            return new AMII.ImmutableSolidColorBrush(color);
        }

        public static AM.GradientSpreadMethod ToGradientSpreadMethod(this SKShaderTileMode shaderTileMode)
        {
            switch (shaderTileMode)
            {
                default:
                case SKShaderTileMode.Clamp:
                    return AM.GradientSpreadMethod.Pad;

                case SKShaderTileMode.Repeat:
                    return AM.GradientSpreadMethod.Repeat;

                case SKShaderTileMode.Mirror:
                    return AM.GradientSpreadMethod.Reflect;
            }
        }

        public static AM.IBrush? ToLinearGradientBrush(this LinearGradientShader linearGradientShader)
        {
            if (linearGradientShader.Colors is null || linearGradientShader.ColorPos is null)
            {
                return null;
            }

            var spreadMethod = linearGradientShader.Mode.ToGradientSpreadMethod();
            var start = linearGradientShader.Start.ToPoint();
            var end = linearGradientShader.End.ToPoint();

            if (linearGradientShader.LocalMatrix is { })
            {
                // TODO: linearGradientShader.LocalMatrix
                var localMatrix = linearGradientShader.LocalMatrix.Value.ToMatrix();
                start = localMatrix.Transform(start);
                end = localMatrix.Transform(end);
            }

            var startPoint = new A.RelativePoint(start, A.RelativeUnit.Absolute);
            var endPoint = new A.RelativePoint(end, A.RelativeUnit.Absolute);

            var gradientStops = new List<AMII.ImmutableGradientStop>();
            for (int i = 0; i < linearGradientShader.Colors.Length; i++)
            {
                var color = linearGradientShader.Colors[i].ToColor();
                var offset = linearGradientShader.ColorPos[i];
                var gradientStop = new AMII.ImmutableGradientStop(offset, color);
                gradientStops.Add(gradientStop);
            }

            return new AMII.ImmutableLinearGradientBrush(
                gradientStops,
                1,
                spreadMethod,
                startPoint,
                endPoint);
        }

        public static AM.IBrush? ToRadialGradientBrush(this TwoPointConicalGradientShader twoPointConicalGradientShader)
        {
            if (twoPointConicalGradientShader.Colors is null || twoPointConicalGradientShader.ColorPos is null)
            {
                return null;
            }

            var spreadMethod = twoPointConicalGradientShader.Mode.ToGradientSpreadMethod();
            var center = twoPointConicalGradientShader.Start.ToPoint();
            var gradientOrigin = twoPointConicalGradientShader.End.ToPoint();

            if (twoPointConicalGradientShader.LocalMatrix is { })
            {
                // TODO: radialGradientBrush.LocalMatrix
                var localMatrix = twoPointConicalGradientShader.LocalMatrix.Value.ToMatrix();
                gradientOrigin = localMatrix.Transform(gradientOrigin);
                center = localMatrix.Transform(center);
            }

            var gradientOriginPoint = new A.RelativePoint(gradientOrigin, A.RelativeUnit.Absolute);
            var centerPoint = new A.RelativePoint(center, A.RelativeUnit.Absolute);

            // NOTE: twoPointConicalGradientShader.StartRadius is always 0.0
            var startRadius = twoPointConicalGradientShader.StartRadius;

            // TODO: Avalonia is passing 'radius' to 'SKShader.CreateTwoPointConicalGradient' as 'startRadius'
            // TODO: but we need to pass it as 'endRadius' to 'SKShader.CreateTwoPointConicalGradient'
            var endRadius = twoPointConicalGradientShader.EndRadius;
            var radius = 0.5; // endRadius

            var gradientStops = new List<AMII.ImmutableGradientStop>();
            for (int i = 0; i < twoPointConicalGradientShader.Colors.Length; i++)
            {
                var color = twoPointConicalGradientShader.Colors[i].ToColor();
                var offset = twoPointConicalGradientShader.ColorPos[i];
                var gradientStop = new AMII.ImmutableGradientStop(offset, color);
                gradientStops.Add(gradientStop);
            }

            return new AMII.ImmutableRadialGradientBrush(
                gradientStops,
                1,
                spreadMethod,
                centerPoint,
                gradientOriginPoint,
                radius);
        }

        public static AM.IBrush? ToBrush(this SKShader? shader)
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
                    // TODO: pictureShader
                    return null;

                default:
                    return null;
            }
        }

        private static AM.IPen ToPen(this SKPaint paint)
        {
            var brush = ToBrush(paint.Shader);
            var lineCap = paint.StrokeCap.ToPenLineCap();
            var lineJoin = paint.StrokeJoin.ToPenLineJoin();

            var dashStyle = default(AMII.ImmutableDashStyle);
            if (paint.PathEffect is DashPathEffect dashPathEffect && dashPathEffect.Intervals is { })
            {
                var dashes = new List<double>();
                foreach (var interval in dashPathEffect.Intervals)
                {
                    dashes.Add(interval / paint.StrokeWidth);
                }
                var offset = dashPathEffect.Phase / paint.StrokeWidth;
                dashStyle = new AMII.ImmutableDashStyle(dashes, offset);
            }

            return new AMII.ImmutablePen(
                brush,
                paint.StrokeWidth,
                dashStyle,
                lineCap,
                lineJoin,
                paint.StrokeMiter
            );
        }

        public static (AM.IBrush? brush, AM.IPen? pen) ToBrushAndPen(this SKPaint paint)
        {
            AM.IBrush? brush = null;
            AM.IPen? pen = null;

            if (paint.Style == SKPaintStyle.Fill || paint.Style == SKPaintStyle.StrokeAndFill)
            {
                brush = ToBrush(paint.Shader);
            }

            if (paint.Style == SKPaintStyle.Stroke || paint.Style == SKPaintStyle.StrokeAndFill)
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

        public static AM.FormattedText ToFormattedText(this SKPaint paint, string text)
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

        public static AM.FillRule ToFillRule(this SKPathFillType pathFillType)
        {
            switch (pathFillType)
            {
                default:
                case SKPathFillType.Winding:
                    return AM.FillRule.NonZero;

                case SKPathFillType.EvenOdd:
                    return AM.FillRule.EvenOdd;
            }
        }

        public static AM.SweepDirection ToSweepDirection(this SKPathDirection pathDirection)
        {
            switch (pathDirection)
            {
                default:
                case SKPathDirection.Clockwise:
                    return AM.SweepDirection.Clockwise;

                case SKPathDirection.CounterClockwise:
                    return AM.SweepDirection.CounterClockwise;
            }
        }

        public static AP.IGeometryImpl? ToGeometry(this IList<SKPoint> points, bool isFilled)
        {
            var geometry = Factory.CreateStreamGeometry();

            using var context = geometry.Open();

            if (points.Count > 0)
            {
                context.BeginFigure(points[0].ToPoint(), isFilled);

                for (int i = 1; i < points.Count; i++)
                {
                    context.LineTo(points[i].ToPoint());
                }

                context.EndFigure(isFilled);
            }

            return geometry;
        }

        public static AP.IGeometryImpl? ToGeometry(this SKPath path, bool isFilled)
        {
            if (path.Commands is null)
            {
                return null;
            }

            var streamGeometry = Factory.CreateStreamGeometry();

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
                            // TODO: isFilled
                            streamGeometryContext.BeginFigure(point, isFilled);
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
                            var isLargeArc = arcToPathCommand.LargeArc == SKPathArcSize.Large;
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
            // TODO: clipPath
            return null;
        }
    }
}
