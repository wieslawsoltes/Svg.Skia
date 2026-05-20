// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
using System;
using System.ComponentModel;
using ShimSkiaSharp;
using Svg.Pathing;

namespace Svg.Model.Services;

internal static class SvgGeometryService
{
    internal readonly record struct PathLengthNormalization(float ActualLength, float SpecifiedLength)
    {
        public bool IsSpecified => SpecifiedLength > 0f && ActualLength > 0f;

        public float AuthorToActualScale => IsSpecified ? ActualLength / SpecifiedLength : 1f;

        public float ActualToAuthorScale => IsSpecified ? SpecifiedLength / ActualLength : 1f;

        public float ToActualDistance(float distance)
        {
            return IsSpecified ? distance * AuthorToActualScale : distance;
        }

        public float ToAuthorDistance(float distance)
        {
            return IsSpecified ? distance * ActualToAuthorScale : distance;
        }
    }

    internal static bool TryCreateEquivalentPath(SvgElement element, SKRect viewport, out SKPath? path)
    {
        var fillRule = element switch
        {
            SvgPath svgPath => svgPath.FillRule,
            SvgRectangle svgRectangle => svgRectangle.FillRule,
            SvgCircle svgCircle => svgCircle.FillRule,
            SvgEllipse svgEllipse => svgEllipse.FillRule,
            SvgLine svgLine => svgLine.FillRule,
            SvgPolyline svgPolyline => svgPolyline.FillRule,
            SvgPolygon svgPolygon => svgPolygon.FillRule,
            _ => SvgFillRule.NonZero
        };

        return TryCreateEquivalentPath(element, fillRule, viewport, out path);
    }

    internal static bool TryCreateEquivalentPath(SvgElement element, SvgFillRule fillRule, SKRect viewport, out SKPath? path)
    {
        path = element switch
        {
            SvgPath svgPath => TryGetComputedPathData(svgPath, out var pathData, out var hasComputedPathData)
                ? pathData.ToPath(fillRule)
                : hasComputedPathData ? null : svgPath.PathData?.ToPath(fillRule),
            SvgRectangle svgRectangle => CreateRectanglePath(svgRectangle, fillRule, viewport),
            SvgCircle svgCircle => CreateCirclePath(svgCircle, fillRule, viewport),
            SvgEllipse svgEllipse => CreateEllipsePath(svgEllipse, fillRule, viewport),
            SvgLine svgLine => CreateLinePath(svgLine, fillRule, viewport),
            SvgPolyline svgPolyline => svgPolyline.Points?.ToPath(fillRule, false, viewport),
            SvgPolygon svgPolygon => svgPolygon.Points?.ToPath(fillRule, true, viewport),
            _ => null
        };

        return path is not null;
    }

    internal static SvgUnit GetComputedUnit(SvgElement element, string propertyName, SvgUnit fallback)
    {
        return GetComputedUnit(element, propertyName, fallback, out _);
    }

    internal static SvgUnit GetComputedUnit(
        SvgElement element,
        string propertyName,
        SvgUnit fallback,
        out bool isAuto,
        out bool isAuthorSpecified)
    {
        return GetComputedUnit(element, propertyName, fallback, out isAuto, out isAuthorSpecified, defaultAuto: false);
    }

    private static SvgUnit GetComputedUnit(
        SvgElement element,
        string propertyName,
        SvgUnit fallback,
        out bool isAuto,
        bool defaultAuto = false)
    {
        return GetComputedUnit(element, propertyName, fallback, out isAuto, out _, defaultAuto);
    }

    private static SvgUnit GetComputedUnit(
        SvgElement element,
        string propertyName,
        SvgUnit fallback,
        out bool isAuto,
        out bool isAuthorSpecified,
        bool defaultAuto = false)
    {
        isAuthorSpecified = false;
        if (!element.TryGetOwnCascadedCssDeclarationValue(propertyName, out var rawValue) ||
            string.IsNullOrWhiteSpace(rawValue))
        {
            isAuto = defaultAuto && !element.ContainsAttribute(propertyName);
            isAuthorSpecified = element.ContainsAttribute(propertyName);
            return fallback;
        }

        rawValue = rawValue.Trim();
        if (string.Equals(rawValue, "inherit", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(rawValue, "unset", StringComparison.OrdinalIgnoreCase))
        {
            if (!element.ComputedStyle.TryGetPropertyValue(propertyName, out rawValue) ||
                string.IsNullOrWhiteSpace(rawValue))
            {
                isAuthorSpecified = false;
                isAuto = defaultAuto;
                return SvgUnit.None;
            }

            rawValue = rawValue.Trim();
            isAuthorSpecified = true;
        }

        if (string.Equals(rawValue, "initial", StringComparison.OrdinalIgnoreCase))
        {
            isAuto = defaultAuto;
            isAuthorSpecified = false;
            return SvgUnit.None;
        }

        if (string.Equals(rawValue, "auto", StringComparison.OrdinalIgnoreCase))
        {
            isAuto = true;
            isAuthorSpecified = true;
            return SvgUnit.None;
        }

        try
        {
            if (TypeDescriptor.GetConverter(typeof(SvgUnit)).ConvertFromInvariantString(rawValue) is SvgUnit unit)
            {
                isAuto = false;
                isAuthorSpecified = true;
                return unit;
            }
        }
        catch
        {
        }

        isAuto = false;
        isAuthorSpecified = element.ContainsAttribute(propertyName);
        return fallback;
    }

    private static SKPath? CreateRectanglePath(SvgRectangle svgRectangle, SvgFillRule fillRule, SKRect viewport)
    {
        var x = GetComputedUnit(svgRectangle, "x", svgRectangle.X);
        var y = GetComputedUnit(svgRectangle, "y", svgRectangle.Y);
        var width = GetComputedUnit(svgRectangle, "width", svgRectangle.Width);
        var height = GetComputedUnit(svgRectangle, "height", svgRectangle.Height);
        var rx = GetComputedUnit(svgRectangle, "rx", svgRectangle.CornerRadiusX, out var rxAuto, defaultAuto: true);
        var ry = GetComputedUnit(svgRectangle, "ry", svgRectangle.CornerRadiusY, out var ryAuto, defaultAuto: true);

        var deviceX = x.ToDeviceValue(UnitRenderingType.Horizontal, svgRectangle, viewport);
        var deviceY = y.ToDeviceValue(UnitRenderingType.Vertical, svgRectangle, viewport);
        var deviceWidth = width.ToDeviceValue(UnitRenderingType.Horizontal, svgRectangle, viewport);
        var deviceHeight = height.ToDeviceValue(UnitRenderingType.Vertical, svgRectangle, viewport);
        var deviceRx = rx.ToDeviceValue(UnitRenderingType.Horizontal, svgRectangle, viewport);
        var deviceRy = ry.ToDeviceValue(UnitRenderingType.Vertical, svgRectangle, viewport);

        if (deviceWidth <= 0f || deviceHeight <= 0f)
        {
            return null;
        }

        if (rxAuto && ryAuto)
        {
            deviceRx = 0f;
            deviceRy = 0f;
        }
        else if (rxAuto)
        {
            deviceRx = deviceRy;
        }
        else if (ryAuto)
        {
            deviceRy = deviceRx;
        }

        if (deviceRx < 0f && deviceRy < 0f)
        {
            deviceRx = 0f;
            deviceRy = 0f;
        }

        if (deviceRx == 0f || deviceRy == 0f)
        {
            deviceRx = 0f;
            deviceRy = 0f;
        }

        if (deviceRx < 0f)
        {
            deviceRx = Math.Abs(deviceRx);
        }

        if (deviceRy < 0f)
        {
            deviceRy = Math.Abs(deviceRy);
        }

        if (deviceRx > 0f)
        {
            deviceRx = Math.Min(deviceRx, deviceWidth / 2f);
        }

        if (deviceRy > 0f)
        {
            deviceRy = Math.Min(deviceRy, deviceHeight / 2f);
        }

        var path = new SKPath
        {
            FillType = GetFillType(fillRule)
        };
        var rect = SKRect.Create(deviceX, deviceY, deviceWidth, deviceHeight);
        if (deviceRx > 0f && deviceRy > 0f)
        {
            path.AddRoundRect(rect, deviceRx, deviceRy);
        }
        else
        {
            path.AddRect(rect);
        }

        return path;
    }

    private static SKPath? CreateCirclePath(SvgCircle svgCircle, SvgFillRule fillRule, SKRect viewport)
    {
        var cx = GetComputedUnit(svgCircle, "cx", svgCircle.CenterX).ToDeviceValue(UnitRenderingType.Horizontal, svgCircle, viewport);
        var cy = GetComputedUnit(svgCircle, "cy", svgCircle.CenterY).ToDeviceValue(UnitRenderingType.Vertical, svgCircle, viewport);
        var radius = GetComputedUnit(svgCircle, "r", svgCircle.Radius).ToDeviceValue(UnitRenderingType.Other, svgCircle, viewport);
        if (radius <= 0f)
        {
            return null;
        }

        var path = new SKPath
        {
            FillType = GetFillType(fillRule)
        };
        path.AddCircle(cx, cy, radius);
        return path;
    }

    private static SKPath? CreateEllipsePath(SvgEllipse svgEllipse, SvgFillRule fillRule, SKRect viewport)
    {
        var cx = GetComputedUnit(svgEllipse, "cx", svgEllipse.CenterX).ToDeviceValue(UnitRenderingType.Horizontal, svgEllipse, viewport);
        var cy = GetComputedUnit(svgEllipse, "cy", svgEllipse.CenterY).ToDeviceValue(UnitRenderingType.Vertical, svgEllipse, viewport);
        var rxUnit = GetComputedUnit(svgEllipse, "rx", svgEllipse.RadiusX, out var rxAuto, defaultAuto: true);
        var ryUnit = GetComputedUnit(svgEllipse, "ry", svgEllipse.RadiusY, out var ryAuto, defaultAuto: true);
        var rx = rxUnit.ToDeviceValue(UnitRenderingType.Horizontal, svgEllipse, viewport);
        var ry = ryUnit.ToDeviceValue(UnitRenderingType.Vertical, svgEllipse, viewport);
        if (rxAuto && ryAuto)
        {
            return null;
        }

        if (rxAuto)
        {
            rx = ry;
        }
        else if (ryAuto)
        {
            ry = rx;
        }

        if (rx <= 0f || ry <= 0f)
        {
            return null;
        }

        var path = new SKPath
        {
            FillType = GetFillType(fillRule)
        };
        path.AddOval(SKRect.Create(cx - rx, cy - ry, rx + rx, ry + ry));
        return path;
    }

    private static SKPath? CreateLinePath(SvgLine svgLine, SvgFillRule fillRule, SKRect viewport)
    {
        var x0 = GetComputedUnit(svgLine, "x1", svgLine.StartX).ToDeviceValue(UnitRenderingType.Horizontal, svgLine, viewport);
        var y0 = GetComputedUnit(svgLine, "y1", svgLine.StartY).ToDeviceValue(UnitRenderingType.Vertical, svgLine, viewport);
        var x1 = GetComputedUnit(svgLine, "x2", svgLine.EndX).ToDeviceValue(UnitRenderingType.Horizontal, svgLine, viewport);
        var y1 = GetComputedUnit(svgLine, "y2", svgLine.EndY).ToDeviceValue(UnitRenderingType.Vertical, svgLine, viewport);

        var path = new SKPath
        {
            FillType = GetFillType(fillRule)
        };
        path.MoveTo(x0, y0);
        path.LineTo(x1, y1);
        return path;
    }

    private static SKPathFillType GetFillType(SvgFillRule fillRule)
        => fillRule == SvgFillRule.EvenOdd ? SKPathFillType.EvenOdd : SKPathFillType.Winding;

    private static bool TryGetComputedPathData(SvgPath svgPath, out SvgPathSegmentList pathData, out bool hasComputedPathData)
    {
        pathData = null!;
        hasComputedPathData = false;
        if (!svgPath.ComputedStyle.TryGetPropertyValue("d", out var rawValue) ||
            string.IsNullOrWhiteSpace(rawValue))
        {
            return false;
        }

        hasComputedPathData = true;
        var normalized = NormalizeCssPathData(rawValue);
        if (string.IsNullOrWhiteSpace(normalized) ||
            string.Equals(normalized, "none", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        try
        {
            pathData = SvgPathBuilder.Parse(normalized);
            pathData.Owner = svgPath;
            return pathData.Count > 0;
        }
        catch
        {
            pathData = null!;
            return false;
        }
    }

    private static string NormalizeCssPathData(string value)
    {
        var trimmed = value.Trim();
        if (!trimmed.StartsWith("path(", StringComparison.OrdinalIgnoreCase) ||
            !trimmed.EndsWith(")", StringComparison.Ordinal))
        {
            return trimmed;
        }

        var inner = trimmed.Substring(5, trimmed.Length - 6).Trim();
        if (inner.Length >= 2 &&
            ((inner[0] == '\'' && inner[inner.Length - 1] == '\'') ||
             (inner[0] == '"' && inner[inner.Length - 1] == '"')))
        {
            return inner.Substring(1, inner.Length - 2);
        }

        return inner;
    }

    internal static PathLengthNormalization CreatePathLengthNormalization(SvgElement element, SKPath? path)
    {
        return CreatePathLengthNormalization(element, path is null ? 0f : EstimatePathLength(path));
    }

    internal static PathLengthNormalization CreatePathLengthNormalization(SvgElement element, float actualLength)
    {
        return new PathLengthNormalization(actualLength, GetSpecifiedPathLength(element));
    }

    internal static float EstimatePathLength(SKPath path)
    {
        if (path.Commands is null || path.Commands.Count == 0)
        {
            return 0f;
        }

        var total = 0f;
        var current = default(SKPoint);
        var figureStart = default(SKPoint);
        var hasCurrent = false;

        for (var i = 0; i < path.Commands.Count; i++)
        {
            switch (path.Commands[i])
            {
                case MoveToPathCommand moveTo:
                    current = new SKPoint(moveTo.X, moveTo.Y);
                    figureStart = current;
                    hasCurrent = true;
                    break;

                case LineToPathCommand lineTo when hasCurrent:
                    {
                        var next = new SKPoint(lineTo.X, lineTo.Y);
                        total += Distance(current, next);
                        current = next;
                    }
                    break;

                case QuadToPathCommand quadTo when hasCurrent:
                    {
                        var control = new SKPoint(quadTo.X0, quadTo.Y0);
                        var end = new SKPoint(quadTo.X1, quadTo.Y1);
                        total += ApproximateQuadraticLength(current, control, end);
                        current = end;
                    }
                    break;

                case CubicToPathCommand cubicTo when hasCurrent:
                    {
                        var control1 = new SKPoint(cubicTo.X0, cubicTo.Y0);
                        var control2 = new SKPoint(cubicTo.X1, cubicTo.Y1);
                        var end = new SKPoint(cubicTo.X2, cubicTo.Y2);
                        total += ApproximateCubicLength(current, control1, control2, end);
                        current = end;
                    }
                    break;

                case ArcToPathCommand arcTo when hasCurrent:
                    total += ApproximateArcLength(current, arcTo);
                    current = new SKPoint(arcTo.X, arcTo.Y);
                    break;

                case ClosePathCommand when hasCurrent:
                    total += Distance(current, figureStart);
                    current = figureStart;
                    break;

                case AddRectPathCommand addRect:
                    total += GetRectPerimeter(addRect.Rect);
                    current = addRect.Rect.TopLeft;
                    figureStart = current;
                    hasCurrent = true;
                    break;

                case AddRoundRectPathCommand addRoundRect:
                    total += GetRoundRectPerimeter(addRoundRect.Rect, addRoundRect.Rx, addRoundRect.Ry);
                    current = new SKPoint(addRoundRect.Rect.Left + addRoundRect.Rx, addRoundRect.Rect.Top);
                    figureStart = current;
                    hasCurrent = true;
                    break;

                case AddOvalPathCommand addOval:
                    total += GetEllipseCircumference(addOval.Rect.Width / 2f, addOval.Rect.Height / 2f);
                    current = new SKPoint(addOval.Rect.Right, (addOval.Rect.Top + addOval.Rect.Bottom) / 2f);
                    figureStart = current;
                    hasCurrent = true;
                    break;

                case AddCirclePathCommand addCircle:
                    total += 2f * (float)Math.PI * Math.Abs(addCircle.Radius);
                    current = new SKPoint(addCircle.X + addCircle.Radius, addCircle.Y);
                    figureStart = current;
                    hasCurrent = true;
                    break;

                case AddPolyPathCommand addPoly when addPoly.Points is { Count: > 0 } points:
                    total += GetPolylineLength(points, addPoly.Close);
                    current = points[points.Count - 1];
                    figureStart = points[0];
                    hasCurrent = true;
                    break;
            }
        }

        return total;
    }

    private static float GetSpecifiedPathLength(SvgElement element)
    {
        return element switch
        {
            SvgPath { PathLength: > 0f } svgPath => svgPath.PathLength,
            SvgRectangle { PathLength: > 0f } svgRectangle => svgRectangle.PathLength,
            SvgCircle { PathLength: > 0f } svgCircle => svgCircle.PathLength,
            SvgEllipse { PathLength: > 0f } svgEllipse => svgEllipse.PathLength,
            SvgLine { PathLength: > 0f } svgLine => svgLine.PathLength,
            SvgPolyline { PathLength: > 0f } svgPolyline => svgPolyline.PathLength,
            SvgPolygon { PathLength: > 0f } svgPolygon => svgPolygon.PathLength,
            _ => 0f
        };
    }

    private static float GetRectPerimeter(SKRect rect)
    {
        return rect.Width <= 0f || rect.Height <= 0f
            ? 0f
            : 2f * (rect.Width + rect.Height);
    }

    private static float GetRoundRectPerimeter(SKRect rect, float rx, float ry)
    {
        if (rect.Width <= 0f || rect.Height <= 0f)
        {
            return 0f;
        }

        rx = Math.Min(Math.Abs(rx), rect.Width / 2f);
        ry = Math.Min(Math.Abs(ry), rect.Height / 2f);
        if (rx <= 0f || ry <= 0f)
        {
            return GetRectPerimeter(rect);
        }

        var straight = (2f * (rect.Width - (2f * rx))) + (2f * (rect.Height - (2f * ry)));
        return straight + GetEllipseCircumference(rx, ry);
    }

    private static float GetPolylineLength(System.Collections.Generic.IList<SKPoint> points, bool close)
    {
        var total = 0f;
        for (var i = 1; i < points.Count; i++)
        {
            total += Distance(points[i - 1], points[i]);
        }

        if (close && points.Count > 1)
        {
            total += Distance(points[points.Count - 1], points[0]);
        }

        return total;
    }

    private static float GetEllipseCircumference(float rx, float ry)
    {
        rx = Math.Abs(rx);
        ry = Math.Abs(ry);
        if (rx <= 0f || ry <= 0f)
        {
            return 0f;
        }

        var h = (float)(Math.Pow(rx - ry, 2d) / Math.Pow(rx + ry, 2d));
        return (float)Math.PI * (rx + ry) * (1f + (3f * h / (10f + (float)Math.Sqrt(4f - (3f * h)))));
    }

    private static float ApproximateQuadraticLength(SKPoint start, SKPoint control, SKPoint end)
    {
        const int Steps = 24;
        var length = 0f;
        var previous = start;

        for (var i = 1; i <= Steps; i++)
        {
            var point = EvaluateQuadratic(start, control, end, i / (float)Steps);
            length += Distance(previous, point);
            previous = point;
        }

        return length;
    }

    private static float ApproximateCubicLength(SKPoint start, SKPoint control1, SKPoint control2, SKPoint end)
    {
        const int Steps = 32;
        var length = 0f;
        var previous = start;

        for (var i = 1; i <= Steps; i++)
        {
            var point = EvaluateCubic(start, control1, control2, end, i / (float)Steps);
            length += Distance(previous, point);
            previous = point;
        }

        return length;
    }

    private static float ApproximateArcLength(SKPoint start, ArcToPathCommand arcTo)
    {
        var end = new SKPoint(arcTo.X, arcTo.Y);
        if (!TryGetArcParameters(start, end, arcTo.Rx, arcTo.Ry, arcTo.XAxisRotate, arcTo.LargeArc, arcTo.Sweep, out var parameters))
        {
            return Distance(start, end);
        }

        var length = 0f;
        var previous = start;
        var steps = ClampSteps((int)Math.Ceiling(Math.Abs(parameters.DeltaAngle) * Math.Max(parameters.Rx, parameters.Ry) / 4f), 6, 360);
        for (var i = 1; i <= steps; i++)
        {
            var theta = parameters.StartAngle + (parameters.DeltaAngle * i / steps);
            var cosTheta = (float)Math.Cos(theta);
            var sinTheta = (float)Math.Sin(theta);
            var point = new SKPoint(
                (parameters.CosPhi * parameters.Rx * cosTheta) - (parameters.SinPhi * parameters.Ry * sinTheta) + parameters.Center.X,
                (parameters.SinPhi * parameters.Rx * cosTheta) + (parameters.CosPhi * parameters.Ry * sinTheta) + parameters.Center.Y);
            length += Distance(previous, point);
            previous = point;
        }

        return length;
    }

    private static bool TryGetArcParameters(
        SKPoint start,
        SKPoint end,
        float rx,
        float ry,
        float angle,
        SKPathArcSize largeArc,
        SKPathDirection sweep,
        out ArcParameters parameters)
    {
        parameters = default;

        rx = Math.Abs(rx);
        ry = Math.Abs(ry);
        if (rx <= float.Epsilon || ry <= float.Epsilon || NearlyEquals(start, end))
        {
            return false;
        }

        var phi = angle * (float)Math.PI / 180f;
        var cosPhi = (float)Math.Cos(phi);
        var sinPhi = (float)Math.Sin(phi);

        var dx2 = (start.X - end.X) / 2f;
        var dy2 = (start.Y - end.Y) / 2f;
        var x1p = (cosPhi * dx2) + (sinPhi * dy2);
        var y1p = (-sinPhi * dx2) + (cosPhi * dy2);

        var rxsq = rx * rx;
        var rysq = ry * ry;
        var x1psq = x1p * x1p;
        var y1psq = y1p * y1p;

        var lambda = (x1psq / rxsq) + (y1psq / rysq);
        if (lambda > 1f)
        {
            var factor = (float)Math.Sqrt(lambda);
            rx *= factor;
            ry *= factor;
            rxsq = rx * rx;
            rysq = ry * ry;
        }

        var denominator = (rxsq * y1psq) + (rysq * x1psq);
        if (denominator <= float.Epsilon)
        {
            return false;
        }

        var sign = (largeArc == SKPathArcSize.Large) == (sweep == SKPathDirection.Clockwise) ? -1f : 1f;
        var sq = ((rxsq * rysq) - (rxsq * y1psq) - (rysq * x1psq)) / denominator;
        sq = Math.Max(sq, 0f);
        var coef = sign * (float)Math.Sqrt(sq);
        var cxp = coef * (rx * y1p / ry);
        var cyp = coef * (-ry * x1p / rx);

        var center = new SKPoint(
            (cosPhi * cxp) - (sinPhi * cyp) + ((start.X + end.X) / 2f),
            (sinPhi * cxp) + (cosPhi * cyp) + ((start.Y + end.Y) / 2f));

        var startAngle = (float)Math.Atan2((y1p - cyp) / ry, (x1p - cxp) / rx);
        var endAngle = (float)Math.Atan2((-y1p - cyp) / ry, (-x1p - cxp) / rx);
        var deltaAngle = endAngle - startAngle;
        if (sweep != SKPathDirection.Clockwise && deltaAngle > 0f)
        {
            deltaAngle -= (float)Math.PI * 2f;
        }
        else if (sweep == SKPathDirection.Clockwise && deltaAngle < 0f)
        {
            deltaAngle += (float)Math.PI * 2f;
        }

        parameters = new ArcParameters(center, rx, ry, startAngle, deltaAngle, cosPhi, sinPhi);
        return true;
    }

    private static SKPoint EvaluateQuadratic(SKPoint start, SKPoint control, SKPoint end, float t)
    {
        var oneMinusT = 1f - t;
        return new SKPoint(
            (oneMinusT * oneMinusT * start.X) + (2f * oneMinusT * t * control.X) + (t * t * end.X),
            (oneMinusT * oneMinusT * start.Y) + (2f * oneMinusT * t * control.Y) + (t * t * end.Y));
    }

    private static SKPoint EvaluateCubic(SKPoint start, SKPoint control1, SKPoint control2, SKPoint end, float t)
    {
        var oneMinusT = 1f - t;
        var oneMinusTSquared = oneMinusT * oneMinusT;
        var tSquared = t * t;
        return new SKPoint(
            (oneMinusTSquared * oneMinusT * start.X) +
            (3f * oneMinusTSquared * t * control1.X) +
            (3f * oneMinusT * tSquared * control2.X) +
            (tSquared * t * end.X),
            (oneMinusTSquared * oneMinusT * start.Y) +
            (3f * oneMinusTSquared * t * control1.Y) +
            (3f * oneMinusT * tSquared * control2.Y) +
            (tSquared * t * end.Y));
    }

    private static float Distance(SKPoint left, SKPoint right)
    {
        var dx = right.X - left.X;
        var dy = right.Y - left.Y;
        return (float)Math.Sqrt((dx * dx) + (dy * dy));
    }

    private static int ClampSteps(int value, int min, int max)
    {
        if (value < min)
        {
            return min;
        }

        return value > max ? max : value;
    }

    private static bool NearlyEquals(SKPoint left, SKPoint right)
    {
        return Math.Abs(left.X - right.X) <= 0.001f &&
               Math.Abs(left.Y - right.Y) <= 0.001f;
    }

    private readonly record struct ArcParameters(SKPoint Center, float Rx, float Ry, float StartAngle, float DeltaAngle, float CosPhi, float SinPhi);
}
