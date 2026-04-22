using System.Collections.Generic;

namespace SvgML;

internal interface IHostedControlElement
{
    object? HostedControl { get; }

    HostedControlSize GetHostedControlSize();
}

internal readonly record struct HostedControlSize(double Width, double Height)
{
    public static HostedControlSize Empty { get; } = new(0D, 0D);

    public bool IsEmpty => Width <= 0D || Height <= 0D;

    public static HostedControlSize From(double width, double height)
    {
        return width > 0D && height > 0D
            ? new HostedControlSize(width, height)
            : Empty;
    }

    public HostedControlSize OrFallback(double fallbackWidth = 16D, double fallbackHeight = 16D)
    {
        return IsEmpty ? new HostedControlSize(fallbackWidth, fallbackHeight) : this;
    }
}

internal readonly record struct HostedControlSlot(double X, double Y, double Width, double Height)
{
    public bool IsEmpty => Width <= 0D || Height <= 0D;
}

internal enum HostedControlLengthAxis
{
    X,
    Y
}

internal static class HostedControlTree
{
    public static IEnumerable<(element Element, IHostedControlElement Host)> Enumerate(element root)
    {
        if (root is IHostedControlElement host && host.HostedControl is not null)
        {
            yield return (root, host);
        }

        foreach (var child in root.Children)
        {
            foreach (var entry in Enumerate(child))
            {
                yield return entry;
            }
        }
    }
}

public partial class foreignObject : IHostedControlElement
{
    private readonly string _generatedMappingId = $"svgml-foreign-object-{System.Guid.NewGuid():N}";

    internal string EffectiveMappingId => GetSvgMappingId() ?? _generatedMappingId;

    internal override string? GetSvgMappingId()
    {
        return string.IsNullOrWhiteSpace(id) ? _generatedMappingId : id;
    }

    object? IHostedControlElement.HostedControl => Child;

    HostedControlSize IHostedControlElement.GetHostedControlSize()
    {
        var size = GetHostSlotSize();
        return HasExplicitNonPositiveHostSize() ? size : size.OrFallback();
    }

    internal HostedControlSize GetHostSlotSize()
    {
        var measured = MeasureHostedControl();
        var widthSet = IsWidthSet();
        var heightSet = IsHeightSet();
        var widthValue = ResolveHostLength(width, HostedControlLengthAxis.X, widthSet, measured.Width);
        var heightValue = ResolveHostLength(height, HostedControlLengthAxis.Y, heightSet, measured.Height);
        if ((widthSet && widthValue <= 0D) || (heightSet && heightValue <= 0D))
        {
            return HostedControlSize.Empty;
        }

        return HostedControlSize.From(widthValue, heightValue);
    }

    internal HostedControlSlot GetHostSlot()
    {
        var size = GetHostSlotSize();
        if (!HasExplicitNonPositiveHostSize())
        {
            size = size.OrFallback();
        }

        var xValue = ResolveHostCoordinate(x, HostedControlLengthAxis.X);
        var yValue = ResolveHostCoordinate(y, HostedControlLengthAxis.Y);

        return new HostedControlSlot(xValue, yValue, size.Width, size.Height);
    }

    internal partial HostedControlSize MeasureHostedControl();

    internal partial bool IsWidthSet();

    internal partial bool IsHeightSet();

    internal partial bool IsInTextTree();

    internal partial double ResolveHostedControlLengthReference(HostedControlLengthAxis axis);

    internal partial double ResolveHostedControlFontSize();

    private double ResolveHostLength(
        Svg.SvgUnit? unit,
        HostedControlLengthAxis axis,
        bool isSet,
        double fallback)
    {
        if (!isSet)
        {
            return fallback;
        }

        return unit is { } actual && TryGetLength(actual, axis, out var value)
            ? value
            : 0D;
    }

    private double ResolveHostCoordinate(Svg.SvgUnit? unit, HostedControlLengthAxis axis)
    {
        return unit is { } actual && TryGetLength(actual, axis, out var value)
            ? value
            : 0D;
    }

    private bool HasExplicitNonPositiveHostSize()
    {
        return (IsWidthSet() && ResolveHostCoordinate(width, HostedControlLengthAxis.X) <= 0D)
            || (IsHeightSet() && ResolveHostCoordinate(height, HostedControlLengthAxis.Y) <= 0D);
    }

    private bool TryGetLength(Svg.SvgUnit unit, HostedControlLengthAxis axis, out double value)
    {
        value = 0D;

        if (unit.IsEmpty || unit.IsNone)
        {
            return false;
        }

        var fontSize = ResolveHostedControlFontSize();
        var reference = ResolveHostedControlLengthReference(axis);

        value = unit.Type switch
        {
            Svg.SvgUnitType.Pixel or Svg.SvgUnitType.User => unit.Value,
            Svg.SvgUnitType.Em => unit.Value * fontSize,
            Svg.SvgUnitType.Ex => unit.Value * fontSize * 0.5D,
            Svg.SvgUnitType.Percentage => unit.Value * reference / 100D,
            Svg.SvgUnitType.Inch => unit.Value * 96D,
            Svg.SvgUnitType.Centimeter => unit.Value * 96D / 2.54D,
            Svg.SvgUnitType.Millimeter => unit.Value * 96D / 25.4D,
            Svg.SvgUnitType.Pica => unit.Value * 16D,
            Svg.SvgUnitType.Point => unit.Value * 96D / 72D,
            _ => 0D
        };

        return unit.Type is Svg.SvgUnitType.Pixel
            or Svg.SvgUnitType.User
            or Svg.SvgUnitType.Em
            or Svg.SvgUnitType.Ex
            or Svg.SvgUnitType.Percentage
            or Svg.SvgUnitType.Inch
            or Svg.SvgUnitType.Centimeter
            or Svg.SvgUnitType.Millimeter
            or Svg.SvgUnitType.Pica
            or Svg.SvgUnitType.Point;
    }
}
