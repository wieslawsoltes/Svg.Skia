using System;
using System.Collections.Generic;
using System.Globalization;
using SkiaSharp;

namespace Svg.Skia;

internal readonly struct SvgAnimationEventTimingParseResult
{
    public SvgAnimationEventTimingParseResult(SvgElementAddress eventAddress, SvgPointerEventType eventType, TimeSpan offset)
    {
        EventAddress = eventAddress;
        EventType = eventType;
        Offset = offset;
    }

    public SvgElementAddress EventAddress { get; }

    public SvgPointerEventType EventType { get; }

    public TimeSpan Offset { get; }
}

internal static class SvgAnimationParser
{
    private static readonly char[] s_semicolonSeparators = { ';' };
    private static readonly char[] s_coordinateSeparators = { ',', ' ', '\t', '\r', '\n' };
    private static readonly CultureInfo s_invariantCulture = CultureInfo.InvariantCulture;

    internal static bool TryGetTrimmedString(string? value, out string trimmed)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            trimmed = string.Empty;
            return false;
        }

        var span = Trim(value.AsSpan());
        if (span.Length == 0)
        {
            trimmed = string.Empty;
            return false;
        }

        trimmed = span.Length == value!.Length
            ? value
            : span.ToString();
        return true;
    }

    internal static List<string> SplitSemicolonList(string? value)
    {
        var results = new List<string>();
        if (string.IsNullOrWhiteSpace(value))
        {
            return results;
        }

        var remaining = value.AsSpan();
        while (TryReadSeparatedToken(ref remaining, s_semicolonSeparators, out var token))
        {
            var trimmed = Trim(token);
            if (trimmed.Length > 0)
            {
                results.Add(trimmed.ToString());
            }
        }

        return results;
    }

    internal static bool TryGetSemicolonSegment(string? value, int segmentIndex, out string segment)
    {
        segment = string.Empty;
        if (segmentIndex < 0 || string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var remaining = value.AsSpan();
        var currentIndex = 0;
        while (TryReadSeparatedToken(ref remaining, s_semicolonSeparators, out var token))
        {
            var trimmed = Trim(token);
            if (trimmed.Length == 0)
            {
                continue;
            }

            if (currentIndex == segmentIndex)
            {
                segment = trimmed.ToString();
                return true;
            }

            currentIndex++;
        }

        return false;
    }

    internal static bool TryGetFirstSemicolonSegment(string? value, out string segment)
    {
        return TryGetSemicolonSegment(value, 0, out segment);
    }

    internal static bool EqualsKeywordIgnoreCase(ReadOnlySpan<char> value, string keyword)
    {
        return EqualsAsciiIgnoreCase(Trim(value), keyword);
    }

    internal static bool TryParseClockValue(string? value, out TimeSpan result)
    {
        result = default;
        return !string.IsNullOrWhiteSpace(value) && TryParseClockValue(value.AsSpan(), out result);
    }

    internal static bool TryParseClockValue(ReadOnlySpan<char> value, out TimeSpan result)
    {
        result = default;

        var part = Trim(value);
        if (part.Length == 0 || EqualsAsciiIgnoreCase(part, "indefinite"))
        {
            return false;
        }

        var sign = 1;
        if (part[0] is '+' or '-')
        {
            sign = part[0] == '-' ? -1 : 1;
            part = Trim(part.Slice(1));
            if (part.Length == 0 || EqualsAsciiIgnoreCase(part, "indefinite"))
            {
                return false;
            }
        }

        if (TryParseColonClockValue(part, out result))
        {
            if (sign < 0)
            {
                result = -result;
            }

            return true;
        }

        double scalar;
        if (TryParseClockValueWithSuffix(part, "ms", out scalar))
        {
            return TryCreateTimeSpan(scalar, sign, ClockTimeUnit.Milliseconds, out result);
        }
        else if (TryParseClockValueWithSuffix(part, "min", out scalar))
        {
            return TryCreateTimeSpan(scalar, sign, ClockTimeUnit.Minutes, out result);
        }
        else if (TryParseClockValueWithSuffix(part, "h", out scalar))
        {
            return TryCreateTimeSpan(scalar, sign, ClockTimeUnit.Hours, out result);
        }
        else if (TryParseClockValueWithSuffix(part, "s", out scalar))
        {
            return TryCreateTimeSpan(scalar, sign, ClockTimeUnit.Seconds, out result);
        }
        else if (TryParseInvariantDouble(part, out scalar))
        {
            return TryCreateTimeSpan(scalar, sign, ClockTimeUnit.Seconds, out result);
        }
        else
        {
            return false;
        }
    }

    internal static bool TryParseEventTimingSpec(
        string value,
        SvgDocument? document,
        SvgElementAddress defaultEventAddress,
        out SvgAnimationEventTimingParseResult result)
    {
        result = default;

        if (!TryGetTrimmedString(value, out var trimmedValue))
        {
            return false;
        }

        var span = trimmedValue.AsSpan();
        var signIndex = FindEventTimingSignIndex(span);

        var eventSegment = signIndex >= 0
            ? Trim(span.Slice(0, signIndex))
            : span;
        if (eventSegment.Length == 0)
        {
            return false;
        }

        var eventAddress = defaultEventAddress;
        var eventName = eventSegment;
        var dotIndex = eventSegment.LastIndexOf('.');
        if (dotIndex >= 0)
        {
            var eventId = Trim(eventSegment.Slice(0, dotIndex));
            eventName = Trim(eventSegment.Slice(dotIndex + 1));
            if (eventId.Length == 0 || eventName.Length == 0)
            {
                return false;
            }

            var eventElement = document?.GetElementById(eventId.ToString());
            if (eventElement is null)
            {
                return false;
            }

            eventAddress = SvgElementAddress.Create(eventElement);
        }

        if (!TryMapEventName(eventName, out var eventType))
        {
            return false;
        }

        var offset = TimeSpan.Zero;
        if (signIndex >= 0)
        {
            var sign = span[signIndex];
            var offsetText = Trim(span.Slice(signIndex + 1));
            if (offsetText.Length == 0 || !TryParseClockValue(offsetText, out offset))
            {
                return false;
            }

            if (sign == '-')
            {
                offset = -offset;
            }
        }

        result = new SvgAnimationEventTimingParseResult(eventAddress, eventType, offset);
        return true;
    }

    internal static bool TryParseMotionCoordinatePair(string value, SvgElement owner, out SKPoint point)
    {
        point = default;
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var remaining = value.AsSpan();
        if (!TryReadSeparatedToken(ref remaining, s_coordinateSeparators, out var xToken) ||
            !TryReadSeparatedToken(ref remaining, s_coordinateSeparators, out var yToken))
        {
            return false;
        }

        if (TryReadSeparatedToken(ref remaining, s_coordinateSeparators, out _))
        {
            return false;
        }

        if (!TryParseMotionCoordinate(xToken, UnitRenderingType.Horizontal, owner, out var x) ||
            !TryParseMotionCoordinate(yToken, UnitRenderingType.Vertical, owner, out var y))
        {
            return false;
        }

        point = new SKPoint(x, y);
        return true;
    }

    internal static bool TryParseMotionCoordinate(ReadOnlySpan<char> value, UnitRenderingType renderingType, SvgElement owner, out float coordinate)
    {
        coordinate = default;

        var trimmed = Trim(value);
        if (trimmed.Length == 0)
        {
            return false;
        }

        try
        {
            var unit = SvgUnitConverter.Parse(trimmed);
            coordinate = ToMotionCoordinate(unit, renderingType, owner);
            return true;
        }
        catch
        {
            return false;
        }
    }

    internal static bool TryResolveMotionRotation(string? rotateValue, SKPoint tangent, out float angle)
    {
        angle = 0f;

        if (!TryGetTrimmedString(rotateValue, out var trimmed))
        {
            return false;
        }

        var trimmedSpan = trimmed.AsSpan();
        if (EqualsAsciiIgnoreCase(trimmedSpan, "auto") ||
            EqualsAsciiIgnoreCase(trimmedSpan, "auto-reverse"))
        {
            angle = (float)(Math.Atan2(tangent.Y, tangent.X) * 180d / Math.PI);
            if (EqualsAsciiIgnoreCase(trimmedSpan, "auto-reverse"))
            {
                angle += 180f;
            }

            return true;
        }

        return TryParseInvariantFloat(trimmedSpan, out angle) && angle != 0f;
    }

    internal static bool TryParseSvgUnit(string value, out SvgUnit unit)
    {
        unit = default;
        if (!TryGetTrimmedString(value, out var trimmed))
        {
            return false;
        }

        try
        {
            unit = SvgUnitConverter.Parse(trimmed.AsSpan());
            return true;
        }
        catch
        {
            return false;
        }
    }

    internal static float[] ParseNumberList(string value)
    {
        if (!TryGetTrimmedString(value, out var trimmed))
        {
            return Array.Empty<float>();
        }

        try
        {
            var numbers = SvgNumberCollectionConverter.Parse(trimmed.AsSpan());
            return numbers.ToArray();
        }
        catch
        {
            return Array.Empty<float>();
        }
    }

    internal static bool TryParseNumberListSegment(string? value, int segmentIndex, out float[] values)
    {
        values = Array.Empty<float>();
        if (!TryGetSemicolonSegment(value, segmentIndex, out var segment))
        {
            return false;
        }

        try
        {
            values = SvgNumberCollectionConverter.Parse(segment.AsSpan()).ToArray();
            return values.Length > 0;
        }
        catch
        {
            values = Array.Empty<float>();
            return false;
        }
    }

    internal static bool TryParseSplineSegment(string? keySplines, int segmentIndex, out CubicBezierSpline spline)
    {
        spline = default;

        if (!TryParseNumberListSegment(keySplines, segmentIndex, out var values) || values.Length != 4)
        {
            return false;
        }

        spline = new CubicBezierSpline(values[0], values[1], values[2], values[3]);
        return true;
    }

    internal static bool TryParseInvariantFloat(string value, out float result)
    {
        result = default;
        return !string.IsNullOrWhiteSpace(value) && TryParseInvariantFloat(value.AsSpan(), out result);
    }

    internal static bool TryParseInvariantFloat(ReadOnlySpan<char> value, out float result)
    {
        value = Trim(value);
        if (value.Length == 0)
        {
            result = default;
            return false;
        }

#if NETSTANDARD2_1 || NETCOREAPP2_1_OR_GREATER
        if (!float.TryParse(value, NumberStyles.Float, s_invariantCulture, out result))
        {
            return false;
        }
#else
        if (!float.TryParse(value.ToString(), NumberStyles.Float, s_invariantCulture, out result))
        {
            return false;
        }
#endif

        return IsFinite(result);
    }

    internal static bool TryParseInvariantDouble(ReadOnlySpan<char> value, out double result)
    {
        value = Trim(value);
        if (value.Length == 0)
        {
            result = default;
            return false;
        }

#if NETSTANDARD2_1 || NETCOREAPP2_1_OR_GREATER
        if (!double.TryParse(value, NumberStyles.Float, s_invariantCulture, out result))
        {
            return false;
        }
#else
        if (!double.TryParse(value.ToString(), NumberStyles.Float, s_invariantCulture, out result))
        {
            return false;
        }
#endif

        return IsFinite(result);
    }

    internal static bool TryParseInvariantInt(ReadOnlySpan<char> value, out int result)
    {
        value = Trim(value);
        if (value.Length == 0)
        {
            result = default;
            return false;
        }

#if NETSTANDARD2_1 || NETCOREAPP2_1_OR_GREATER
        return int.TryParse(value, NumberStyles.None, s_invariantCulture, out result);
#else
        return int.TryParse(value.ToString(), NumberStyles.None, s_invariantCulture, out result);
#endif
    }

    private static bool TryParseClockValueWithSuffix(ReadOnlySpan<char> value, string suffix, out double scalar)
    {
        scalar = default;
        if (!TryStripSuffixIgnoreCase(value, suffix, out var scalarText))
        {
            return false;
        }

        scalarText = Trim(scalarText);
        return scalarText.Length > 0 && TryParseInvariantDouble(scalarText, out scalar);
    }

    private static bool TryParseColonClockValue(ReadOnlySpan<char> value, out TimeSpan result)
    {
        result = default;

        var firstColonIndex = value.IndexOf(':');
        if (firstColonIndex < 0)
        {
            return false;
        }

        var remaining = value.Slice(firstColonIndex + 1);
        var secondRelativeIndex = remaining.IndexOf(':');

        ReadOnlySpan<char> hoursText = default;
        ReadOnlySpan<char> minutesText;
        ReadOnlySpan<char> secondsText;

        if (secondRelativeIndex >= 0)
        {
            var secondColonIndex = firstColonIndex + secondRelativeIndex + 1;
            if (value.Slice(secondColonIndex + 1).IndexOf(':') >= 0)
            {
                return false;
            }

            hoursText = value.Slice(0, firstColonIndex);
            minutesText = value.Slice(firstColonIndex + 1, secondColonIndex - firstColonIndex - 1);
            secondsText = value.Slice(secondColonIndex + 1);
        }
        else
        {
            minutesText = value.Slice(0, firstColonIndex);
            secondsText = value.Slice(firstColonIndex + 1);
        }

        var hours = 0;
        if (hoursText.Length > 0 && !TryParseInvariantInt(hoursText, out hours))
        {
            return false;
        }

        if (!TryParseInvariantInt(minutesText, out var minutes) ||
            !TryParseInvariantDouble(secondsText, out var seconds))
        {
            return false;
        }

        if (hours < 0 || minutes < 0 || minutes >= 60 || seconds < 0d || seconds >= 60d)
        {
            return false;
        }

        result = TimeSpan.FromHours(hours) +
                 TimeSpan.FromMinutes(minutes) +
                 TimeSpan.FromSeconds(seconds);
        return true;
    }

    private static bool TryMapEventName(ReadOnlySpan<char> eventName, out SvgPointerEventType eventType)
    {
        if (EqualsAsciiIgnoreCase(eventName, "click"))
        {
            eventType = SvgPointerEventType.Click;
            return true;
        }

        if (EqualsAsciiIgnoreCase(eventName, "mousedown"))
        {
            eventType = SvgPointerEventType.Press;
            return true;
        }

        if (EqualsAsciiIgnoreCase(eventName, "mouseup"))
        {
            eventType = SvgPointerEventType.Release;
            return true;
        }

        if (EqualsAsciiIgnoreCase(eventName, "mousemove"))
        {
            eventType = SvgPointerEventType.Move;
            return true;
        }

        if (EqualsAsciiIgnoreCase(eventName, "mouseover"))
        {
            eventType = SvgPointerEventType.Enter;
            return true;
        }

        if (EqualsAsciiIgnoreCase(eventName, "mouseout"))
        {
            eventType = SvgPointerEventType.Leave;
            return true;
        }

        if (EqualsAsciiIgnoreCase(eventName, "mousescroll"))
        {
            eventType = SvgPointerEventType.Wheel;
            return true;
        }

        eventType = default;
        return false;
    }

    internal static float ToMotionCoordinate(SvgUnit unit, UnitRenderingType renderingType, SvgElement owner)
    {
        var ppi = owner.OwnerDocument?.Ppi ?? SvgDocument.PointsPerInch;

        switch (unit.Type)
        {
            case SvgUnitType.Inch:
                return unit.Value * ppi;
            case SvgUnitType.Centimeter:
                return (unit.Value / 2.54f) * ppi;
            case SvgUnitType.Millimeter:
                return (unit.Value / 25.4f) * ppi;
            case SvgUnitType.Pica:
                return ((unit.Value * 12f) / 72f) * ppi;
            case SvgUnitType.Point:
                return (unit.Value / 72f) * ppi;
            case SvgUnitType.Percentage:
                var document = owner.OwnerDocument;
                var viewBox = document?.ViewBox;
                var dimension = renderingType == UnitRenderingType.Horizontal
                    ? (viewBox?.Width ?? 0f)
                    : (viewBox?.Height ?? 0f);

                if (dimension == 0f && document is not null)
                {
                    dimension = renderingType == UnitRenderingType.Horizontal
                        ? ToViewportDimension(document.Width, document)
                        : ToViewportDimension(document.Height, document);
                }

                return dimension == 0f ? unit.Value : (dimension * unit.Value / 100f);
            default:
                return unit.Value;
        }
    }

    private static int FindEventTimingSignIndex(ReadOnlySpan<char> value)
    {
        for (var index = value.Length - 1; index > 0; index--)
        {
            if (value[index] is not ('+' or '-'))
            {
                continue;
            }

            var offsetText = Trim(value.Slice(index + 1));
            if (offsetText.Length == 0 || !TryParseClockValue(offsetText, out _))
            {
                continue;
            }

            return index;
        }

        return -1;
    }

    private static bool TryCreateTimeSpan(double scalar, int sign, ClockTimeUnit unit, out TimeSpan result)
    {
        try
        {
            result = unit switch
            {
                ClockTimeUnit.Milliseconds => TimeSpan.FromMilliseconds(scalar),
                ClockTimeUnit.Minutes => TimeSpan.FromMinutes(scalar),
                ClockTimeUnit.Hours => TimeSpan.FromHours(scalar),
                _ => TimeSpan.FromSeconds(scalar)
            };
        }
        catch (OverflowException)
        {
            result = default;
            return false;
        }

        if (sign < 0)
        {
            result = -result;
        }

        return true;
    }

    private static bool IsFinite(float value)
    {
        return !float.IsNaN(value) && !float.IsInfinity(value);
    }

    private static bool IsFinite(double value)
    {
        return !double.IsNaN(value) && !double.IsInfinity(value);
    }

    private enum ClockTimeUnit
    {
        Milliseconds,
        Seconds,
        Minutes,
        Hours
    }

    private static float ToViewportDimension(SvgUnit unit, SvgElement owner)
    {
        var ppi = owner.OwnerDocument?.Ppi ?? SvgDocument.PointsPerInch;

        switch (unit.Type)
        {
            case SvgUnitType.Inch:
                return unit.Value * ppi;
            case SvgUnitType.Centimeter:
                return (unit.Value / 2.54f) * ppi;
            case SvgUnitType.Millimeter:
                return (unit.Value / 25.4f) * ppi;
            case SvgUnitType.Pica:
                return ((unit.Value * 12f) / 72f) * ppi;
            case SvgUnitType.Point:
                return (unit.Value / 72f) * ppi;
            case SvgUnitType.Percentage:
                return 0f;
            default:
                return unit.Value;
        }
    }

    private static bool TryReadSeparatedToken(ref ReadOnlySpan<char> remaining, ReadOnlySpan<char> separators, out ReadOnlySpan<char> token)
    {
        remaining = TrimSeparators(remaining, separators);
        if (remaining.Length == 0)
        {
            token = default;
            return false;
        }

        var separatorIndex = FindSeparatorIndex(remaining, separators);
        if (separatorIndex < 0)
        {
            token = remaining;
            remaining = ReadOnlySpan<char>.Empty;
            return true;
        }

        token = remaining.Slice(0, separatorIndex);
        remaining = remaining.Slice(separatorIndex + 1);
        return true;
    }

    private static int FindSeparatorIndex(ReadOnlySpan<char> value, ReadOnlySpan<char> separators)
    {
        for (var index = 0; index < value.Length; index++)
        {
            if (IsSeparator(value[index], separators))
            {
                return index;
            }
        }

        return -1;
    }

    private static ReadOnlySpan<char> Trim(ReadOnlySpan<char> value)
    {
        var start = 0;
        var end = value.Length - 1;

        while (start <= end && char.IsWhiteSpace(value[start]))
        {
            start++;
        }

        while (end >= start && char.IsWhiteSpace(value[end]))
        {
            end--;
        }

        return start > end
            ? ReadOnlySpan<char>.Empty
            : value.Slice(start, end - start + 1);
    }

    private static ReadOnlySpan<char> TrimSeparators(ReadOnlySpan<char> value, ReadOnlySpan<char> separators)
    {
        var start = 0;
        while (start < value.Length && IsSeparator(value[start], separators))
        {
            start++;
        }

        return start == 0
            ? value
            : value.Slice(start);
    }

    private static bool IsSeparator(char value, ReadOnlySpan<char> separators)
    {
        for (var index = 0; index < separators.Length; index++)
        {
            if (value == separators[index])
            {
                return true;
            }
        }

        return false;
    }

    private static bool TryStripSuffixIgnoreCase(ReadOnlySpan<char> value, string suffix, out ReadOnlySpan<char> remaining)
    {
        if (value.Length < suffix.Length)
        {
            remaining = default;
            return false;
        }

        var startIndex = value.Length - suffix.Length;
        if (!EqualsAsciiIgnoreCase(value.Slice(startIndex), suffix))
        {
            remaining = default;
            return false;
        }

        remaining = value.Slice(0, startIndex);
        return true;
    }

    private static bool EqualsAsciiIgnoreCase(ReadOnlySpan<char> value, string expected)
    {
        if (value.Length != expected.Length)
        {
            return false;
        }

        for (var index = 0; index < value.Length; index++)
        {
            if (ToLowerAscii(value[index]) != ToLowerAscii(expected[index]))
            {
                return false;
            }
        }

        return true;
    }

    private static char ToLowerAscii(char value)
    {
        return value is >= 'A' and <= 'Z'
            ? (char)(value + ('a' - 'A'))
            : value;
    }
}
