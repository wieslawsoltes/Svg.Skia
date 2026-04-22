using Avalonia;
using Avalonia.Controls;
using Avalonia.Metadata;

namespace SvgML;

public partial class foreignObject
{
    private static readonly char[] s_viewBoxSeparators = [' ', '\t', '\r', '\n', ','];

    public static readonly StyledProperty<Control?> ChildProperty =
        AvaloniaProperty.Register<foreignObject, Control?>(nameof(Child));

    [Content]
    public Control? Child
    {
        get => GetValue(ChildProperty);
        set => SetValue(ChildProperty, value);
    }

    internal partial HostedControlSize MeasureHostedControl()
    {
        if (Child is null)
        {
            return HostedControlSize.Empty;
        }

        Child.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        return HostedControlSize.From(Child.DesiredSize.Width, Child.DesiredSize.Height);
    }

    internal partial bool IsWidthSet()
    {
        return width is not null;
    }

    internal partial bool IsHeightSet()
    {
        return height is not null;
    }

    internal partial bool IsInTextTree()
    {
        for (var current = Parent as element; current is not null; current = current.Parent as element)
        {
            if (current is text_base)
            {
                return true;
            }
        }

        return false;
    }

    internal partial double ResolveHostedControlLengthReference(HostedControlLengthAxis axis)
    {
        var root = FindRootSvg();
        if (root is null)
        {
            return 0D;
        }

        if (TryGetViewBoxReference(root.viewBox, axis, out var viewBoxReference))
        {
            return viewBoxReference;
        }

        var picture = root.Picture;
        if (picture is null)
        {
            return 0D;
        }

        return axis == HostedControlLengthAxis.X
            ? picture.CullRect.Width
            : picture.CullRect.Height;
    }

    internal partial double ResolveHostedControlFontSize()
    {
        return ResolveFontSize(this);
    }

    private svg? FindRootSvg()
    {
        for (element? current = this; current is not null; current = current.Parent as element)
        {
            if (current is svg root)
            {
                return root;
            }
        }

        return null;
    }

    private static double ResolveFontSize(element element)
    {
        var inherited = element.Parent is element parent
            ? ResolveFontSize(parent)
            : 16D;

        if (element.font_size is { } fontSize)
        {
            return ConvertFontSizeToPixels(fontSize, inherited);
        }

        return TryGetStyleDeclaration(element.style, "font-size", out var styleFontSize)
            && TryParseSvgUnit(styleFontSize, out var styleFontSizeUnit)
            ? ConvertFontSizeToPixels(styleFontSizeUnit, inherited)
            : inherited;
    }

    private static double ConvertFontSizeToPixels(Svg.SvgUnit unit, double inherited)
    {
        if (unit.IsEmpty || unit.IsNone || unit.Value <= 0f)
        {
            return inherited;
        }

        return unit.Type switch
        {
            Svg.SvgUnitType.Pixel or Svg.SvgUnitType.User => unit.Value,
            Svg.SvgUnitType.Em => inherited * unit.Value,
            Svg.SvgUnitType.Ex => inherited * unit.Value * 0.5D,
            Svg.SvgUnitType.Percentage => inherited * unit.Value / 100D,
            Svg.SvgUnitType.Inch => unit.Value * 96D,
            Svg.SvgUnitType.Centimeter => unit.Value * 96D / 2.54D,
            Svg.SvgUnitType.Millimeter => unit.Value * 96D / 25.4D,
            Svg.SvgUnitType.Pica => unit.Value * 16D,
            Svg.SvgUnitType.Point => unit.Value * 96D / 72D,
            _ => inherited
        };
    }

    private static bool TryGetStyleDeclaration(string? style, string propertyName, out string? value)
    {
        value = null;

        if (string.IsNullOrWhiteSpace(style))
        {
            return false;
        }

        foreach (var declaration in style.Split(';'))
        {
            var separator = declaration.IndexOf(':');
            if (separator <= 0)
            {
                continue;
            }

            var name = declaration[..separator].Trim();
            if (!string.Equals(name, propertyName, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            value = declaration[(separator + 1)..].Trim();
            return value.Length > 0;
        }

        return false;
    }

    private static bool TryParseSvgUnit(string? value, out Svg.SvgUnit unit)
    {
        unit = default;

        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        try
        {
            if (new Svg.SvgUnitConverter().ConvertFromString(value.Trim()) is Svg.SvgUnit parsed)
            {
                unit = parsed;
                return true;
            }
        }
        catch (FormatException)
        {
        }
        catch (NotSupportedException)
        {
        }

        return false;
    }

    private static bool TryGetViewBoxReference(
        string? viewBox,
        HostedControlLengthAxis axis,
        out double reference)
    {
        reference = 0D;

        if (string.IsNullOrWhiteSpace(viewBox))
        {
            return false;
        }

        var parts = viewBox.Split(s_viewBoxSeparators, StringSplitOptions.RemoveEmptyEntries);
        var index = axis == HostedControlLengthAxis.X ? 2 : 3;
        if (parts.Length <= index
            || !double.TryParse(
                parts[index],
                System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture,
                out reference))
        {
            return false;
        }

        return reference > 0D;
    }
}
