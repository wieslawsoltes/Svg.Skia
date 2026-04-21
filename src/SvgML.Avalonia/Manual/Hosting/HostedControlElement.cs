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
        return GetHostSlotSize().OrFallback();
    }

    internal HostedControlSize GetHostSlotSize()
    {
        var measured = MeasureHostedControl();
        var widthValue = TryGetPositiveLength(width, out var explicitWidth)
            ? explicitWidth
            : measured.Width;
        var heightValue = TryGetPositiveLength(height, out var explicitHeight)
            ? explicitHeight
            : measured.Height;

        return HostedControlSize.From(widthValue, heightValue);
    }

    internal partial HostedControlSize MeasureHostedControl();

    internal partial bool IsWidthSet();

    internal partial bool IsHeightSet();

    internal partial bool IsInTextTree();

    private static bool TryGetPositiveLength(Svg.SvgUnit? unit, out double value)
    {
        value = 0D;
        return unit is { } actual && TryGetPositiveLength(actual, out value);
    }

    private static bool TryGetPositiveLength(Svg.SvgUnit unit, out double value)
    {
        value = 0D;

        if (unit.IsEmpty || unit.IsNone || unit.Value <= 0f)
        {
            return false;
        }

        value = unit.Type switch
        {
            Svg.SvgUnitType.Pixel or Svg.SvgUnitType.User => unit.Value,
            Svg.SvgUnitType.Inch => unit.Value * 96D,
            Svg.SvgUnitType.Centimeter => unit.Value * 96D / 2.54D,
            Svg.SvgUnitType.Millimeter => unit.Value * 96D / 25.4D,
            Svg.SvgUnitType.Pica => unit.Value * 16D,
            Svg.SvgUnitType.Point => unit.Value * 96D / 72D,
            _ => 0D
        };

        return value > 0D;
    }
}
