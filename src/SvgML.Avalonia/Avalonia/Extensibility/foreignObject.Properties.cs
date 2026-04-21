using Avalonia;
using Avalonia.Controls;
using Avalonia.Metadata;

namespace SvgML;

public partial class foreignObject
{
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
}
