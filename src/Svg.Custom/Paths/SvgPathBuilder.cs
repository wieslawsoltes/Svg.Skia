using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using Svg.Pathing;

namespace Svg
{
    public static class PointFExtensions
    {
        public static string ToSvgString(this float value)
        {
            // Use G7 format specifier to be compatible across all target frameworks.
            return value.ToString("G7", CultureInfo.InvariantCulture);
        }

        public static string ToSvgString(this PointF p)
        {
            return $"{p.X.ToSvgString()} {p.Y.ToSvgString()}";
        }
    }

    public class SvgPathBuilder : TypeConverter
    {
        /// <summary>
        /// Parses the specified string into a collection of path segments.
        /// </summary>
        /// <param name="path">A <see cref="string"/> containing path data.</param>
        public static SvgPathSegmentList Parse(ReadOnlySpan<char> path)
        {
            var segments = new SvgPathSegmentList();

            try
            {
                var pathTrimmed = path.TrimEnd();
                var commandStart = 0;
                var pathLength = pathTrimmed.Length;
                var parserContext = new ParserContext();

                for (var i = 0; i < pathLength; ++i)
                {
                    var currentChar = pathTrimmed[i];
                    if (char.IsLetter(currentChar) && currentChar != 'e' && currentChar != 'E') // e is used in scientific notiation. but not svg path
                    {
                        var start = commandStart;
                        var length = i - commandStart;
                        var command = pathTrimmed.Slice(start, length).Trim();
                        commandStart = i;

                        if (command.Length > 0)
                        {
                            var commandSetTrimmed = pathTrimmed.Slice(start, length).Trim();
                            var state = new CoordinateParserState(ref commandSetTrimmed);
                            CreatePathSegment(commandSetTrimmed[0], segments, ref state, commandSetTrimmed, ref parserContext);
                        }

                        if (pathLength == i + 1)
                        {
                            var commandSetTrimmed = pathTrimmed.Slice(i, 1).Trim();
                            var state = new CoordinateParserState(ref commandSetTrimmed);
                            CreatePathSegment(commandSetTrimmed[0], segments, ref state, commandSetTrimmed, ref parserContext);
                        }
                    }
                    else if (pathLength == i + 1)
                    {
                        var start = commandStart;
                        var length = i - commandStart + 1;
                        var command = pathTrimmed.Slice(start, length).Trim();

                        if (command.Length > 0)
                        {
                            var commandSetTrimmed = pathTrimmed.Slice(start, length).Trim();
                            var state = new CoordinateParserState(ref commandSetTrimmed);
                            CreatePathSegment(commandSetTrimmed[0], segments, ref state, commandSetTrimmed, ref parserContext);
                        }
                    }
                }
            }
            catch (Exception exc)
            {
                Trace.TraceError("Error parsing path \"{0}\": {1}", path.ToString(), exc.Message);
            }

            return segments;
        }

        private static void CreatePathSegment(char command, SvgPathSegmentList segments, ref CoordinateParserState state, ReadOnlySpan<char> chars, ref ParserContext parserContext)
        {
            var isRelative = char.IsLower(command);
            // http://www.w3.org/TR/SVG11/paths.html#PathDataGeneralInformation

            switch (command)
            {
                case 'M': // moveto
                case 'm': // relative moveto
                    {
                        if (CoordinateParser.TryGetFloat(out var coords0, chars, ref state)
                         && CoordinateParser.TryGetFloat(out var coords1, chars, ref state))
                        {
                            var end = ResolvePoint(isRelative, coords0, coords1, parserContext);
                            segments.Add(parserContext.BearingMode
                                ? new SvgMoveToSegment(false, end)
                                : new SvgMoveToSegment(isRelative, new PointF(coords0, coords1)));
                            parserContext.Current = end;
                            parserContext.FigureStart = end;
                            parserContext.HasCurrent = true;
                        }
                        while (CoordinateParser.TryGetFloat(out coords0, chars, ref state)
                            && CoordinateParser.TryGetFloat(out coords1, chars, ref state))
                        {
                            var end = ResolvePoint(isRelative, coords0, coords1, parserContext);
                            segments.Add(parserContext.BearingMode
                                ? new SvgLineSegment(false, end)
                                : new SvgLineSegment(isRelative, new PointF(coords0, coords1)));
                            parserContext.Current = end;
                            parserContext.HasCurrent = true;
                        }
                    }
                    break;
                case 'A': // elliptical arc
                case 'a': // relative elliptical arc
                    {
                        while (CoordinateParser.TryGetFloat(out var coords0, chars, ref state)
                            && CoordinateParser.TryGetFloat(out var coords1, chars, ref state)
                            && CoordinateParser.TryGetFloat(out var coords2, chars, ref state)
                            && CoordinateParser.TryGetBool(out var size, chars, ref state)
                            && CoordinateParser.TryGetBool(out var sweep, chars, ref state)
                            && CoordinateParser.TryGetFloat(out var coords3, chars, ref state)
                            && CoordinateParser.TryGetFloat(out var coords4, chars, ref state))
                        {
                            // A|a rx ry x-axis-rotation large-arc-flag sweep-flag x y
                            var end = ResolvePoint(isRelative, coords3, coords4, parserContext);
                            segments.Add(
                                new SvgArcSegment(
                                    coords0,
                                    coords1,
                                    parserContext.BearingMode && isRelative ? coords2 + parserContext.Bearing : coords2,
                                    size ? SvgArcSize.Large : SvgArcSize.Small,
                                    sweep ? SvgArcSweep.Positive : SvgArcSweep.Negative,
                                    parserContext.BearingMode ? false : isRelative,
                                    parserContext.BearingMode ? end : new PointF(coords3, coords4)));
                            parserContext.Current = end;
                            parserContext.HasCurrent = true;
                        }
                    }
                    break;
                case 'L': // lineto
                case 'l': // relative lineto
                    {
                        while (CoordinateParser.TryGetFloat(out var coords0, chars, ref state)
                            && CoordinateParser.TryGetFloat(out var coords1, chars, ref state))
                        {
                            var end = ResolvePoint(isRelative, coords0, coords1, parserContext);
                            segments.Add(parserContext.BearingMode
                                ? new SvgLineSegment(false, end)
                                : new SvgLineSegment(isRelative, new PointF(coords0, coords1)));
                            parserContext.Current = end;
                            parserContext.HasCurrent = true;
                        }
                    }
                    break;
                case 'H': // horizontal lineto
                case 'h': // relative horizontal lineto
                    {
                        while (CoordinateParser.TryGetFloat(out var coords0, chars, ref state))
                        {
                            var end = isRelative
                                ? ResolvePoint(true, coords0, 0f, parserContext)
                                : new PointF(coords0, parserContext.Current.Y);
                            segments.Add(parserContext.BearingMode
                                ? new SvgLineSegment(false, end)
                                : new SvgLineSegment(isRelative, new PointF(coords0, float.NaN)));
                            parserContext.Current = end;
                            parserContext.HasCurrent = true;
                        }
                    }
                    break;
                case 'V': // vertical lineto
                case 'v': // relative vertical lineto
                    {
                        while (CoordinateParser.TryGetFloat(out var coords0, chars, ref state))
                        {
                            var end = isRelative
                                ? ResolvePoint(true, 0f, coords0, parserContext)
                                : new PointF(parserContext.Current.X, coords0);
                            segments.Add(parserContext.BearingMode
                                ? new SvgLineSegment(false, end)
                                : new SvgLineSegment(isRelative, new PointF(float.NaN, coords0)));
                            parserContext.Current = end;
                            parserContext.HasCurrent = true;
                        }
                    }
                    break;
                case 'Q': // quadratic bézier curveto
                case 'q': // relative quadratic bézier curveto
                    {
                        while (CoordinateParser.TryGetFloat(out var coords0, chars, ref state)
                            && CoordinateParser.TryGetFloat(out var coords1, chars, ref state)
                            && CoordinateParser.TryGetFloat(out var coords2, chars, ref state)
                            && CoordinateParser.TryGetFloat(out var coords3, chars, ref state))
                        {
                            var control = ResolvePoint(isRelative, coords0, coords1, parserContext);
                            var end = ResolvePoint(isRelative, coords2, coords3, parserContext);
                            segments.Add(
                                parserContext.BearingMode
                                    ? new SvgQuadraticCurveSegment(false, control, end)
                                    : new SvgQuadraticCurveSegment(isRelative, new PointF(coords0, coords1), new PointF(coords2, coords3)));
                            parserContext.Current = end;
                            parserContext.HasCurrent = true;
                        }
                    }
                    break;
                case 'T': // shorthand/smooth quadratic bézier curveto
                case 't': // relative shorthand/smooth quadratic bézier curveto
                    {
                        while (CoordinateParser.TryGetFloat(out var coords0, chars, ref state)
                            && CoordinateParser.TryGetFloat(out var coords1, chars, ref state))
                        {
                            var end = ResolvePoint(isRelative, coords0, coords1, parserContext);
                            segments.Add(
                                parserContext.BearingMode
                                    ? new SvgQuadraticCurveSegment(false, end)
                                    : new SvgQuadraticCurveSegment(isRelative, new PointF(coords0, coords1)));
                            parserContext.Current = end;
                            parserContext.HasCurrent = true;
                        }
                    }
                    break;
                case 'C': // curveto
                case 'c': // relative curveto
                    {
                        while (CoordinateParser.TryGetFloat(out var coords0, chars, ref state)
                            && CoordinateParser.TryGetFloat(out var coords1, chars, ref state)
                            && CoordinateParser.TryGetFloat(out var coords2, chars, ref state)
                            && CoordinateParser.TryGetFloat(out var coords3, chars, ref state)
                            && CoordinateParser.TryGetFloat(out var coords4, chars, ref state)
                            && CoordinateParser.TryGetFloat(out var coords5, chars, ref state))
                        {
                            var first = ResolvePoint(isRelative, coords0, coords1, parserContext);
                            var second = ResolvePoint(isRelative, coords2, coords3, parserContext);
                            var end = ResolvePoint(isRelative, coords4, coords5, parserContext);
                            segments.Add(
                                parserContext.BearingMode
                                    ? new SvgCubicCurveSegment(false, first, second, end)
                                    : new SvgCubicCurveSegment(isRelative, new PointF(coords0, coords1), new PointF(coords2, coords3), new PointF(coords4, coords5)));
                            parserContext.Current = end;
                            parserContext.HasCurrent = true;
                        }
                    }
                    break;
                case 'S': // shorthand/smooth curveto
                case 's': // relative shorthand/smooth curveto
                    {
                        while (CoordinateParser.TryGetFloat(out var coords0, chars, ref state)
                            && CoordinateParser.TryGetFloat(out var coords1, chars, ref state)
                            && CoordinateParser.TryGetFloat(out var coords2, chars, ref state)
                            && CoordinateParser.TryGetFloat(out var coords3, chars, ref state))
                        {
                            var second = ResolvePoint(isRelative, coords0, coords1, parserContext);
                            var end = ResolvePoint(isRelative, coords2, coords3, parserContext);
                            segments.Add(
                                parserContext.BearingMode
                                    ? new SvgCubicCurveSegment(false, second, end)
                                    : new SvgCubicCurveSegment(isRelative, new PointF(coords0, coords1), new PointF(coords2, coords3)));
                            parserContext.Current = end;
                            parserContext.HasCurrent = true;
                        }
                    }
                    break;
                case 'B': // SVG 2 bearing
                case 'b': // relative SVG 2 bearing
                    {
                        while (CoordinateParser.TryGetFloat(out var angle, chars, ref state))
                        {
                            parserContext.BearingMode = true;
                            parserContext.Bearing = isRelative ? parserContext.Bearing + angle : angle;
                        }
                    }
                    break;
                case 'Z': // closepath
                case 'z': // relative closepath
                    {
                        segments.Add(new SvgClosePathSegment(isRelative));
                        parserContext.Current = parserContext.FigureStart;
                        parserContext.HasCurrent = true;
                    }
                    break;
            }
        }

        private static PointF ResolvePoint(bool isRelative, float x, float y, ParserContext parserContext)
        {
            if (!isRelative)
            {
                return new PointF(x, y);
            }

            var rotated = Rotate(x, y, parserContext.Bearing);
            return new PointF(parserContext.Current.X + rotated.X, parserContext.Current.Y + rotated.Y);
        }

        private static PointF Rotate(float x, float y, float angle)
        {
            if (Math.Abs(angle) <= float.Epsilon)
            {
                return new PointF(x, y);
            }

            var radians = angle * Math.PI / 180d;
            var cos = (float)Math.Cos(radians);
            var sin = (float)Math.Sin(radians);
            return new PointF((x * cos) - (y * sin), (x * sin) + (y * cos));
        }

        private struct ParserContext
        {
            public PointF Current;
            public PointF FigureStart;
            public float Bearing;
            public bool BearingMode;
            public bool HasCurrent;
        }

        public override object ConvertFrom(ITypeDescriptorContext context, CultureInfo culture, object value)
        {
            if (value is string s)
                return Parse(s.AsSpan());

            return base.ConvertFrom(context, culture, value);
        }
    }
}
