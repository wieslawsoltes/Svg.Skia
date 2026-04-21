namespace SvgML;

[ContentProperty(nameof(Child))]
public partial class foreignObject
{
    public static readonly Microsoft.Maui.Controls.BindableProperty ChildProperty =
        Microsoft.Maui.Controls.BindableProperty.Create(nameof(Child), typeof(Microsoft.Maui.Controls.View), typeof(foreignObject));

    public Microsoft.Maui.Controls.View? Child
    {
        get => (Microsoft.Maui.Controls.View?)GetValue(ChildProperty);
        set => SetValue(ChildProperty, value);
    }

    internal partial HostedControlSize MeasureHostedControl()
    {
        if (Child is null)
        {
            return HostedControlSize.Empty;
        }

        var size = Child.Measure(double.PositiveInfinity, double.PositiveInfinity);
        return HostedControlSize.From(size.Width, size.Height);
    }

    internal partial bool IsWidthSet()
    {
        return this.IsSet(widthProperty);
    }

    internal partial bool IsHeightSet()
    {
        return this.IsSet(heightProperty);
    }

    internal partial bool IsInTextTree()
    {
        for (var current = ParentElement; current is not null; current = current.ParentElement)
        {
            if (current is text_base)
            {
                return true;
            }
        }

        return false;
    }
}
