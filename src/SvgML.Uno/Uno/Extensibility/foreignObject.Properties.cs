using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Markup;
using Windows.Foundation;

namespace SvgML;

[ContentProperty(Name = nameof(Child))]
public partial class foreignObject
{
    public static readonly DependencyProperty ChildProperty =
        DependencyProperty.Register(
            nameof(Child),
            typeof(UIElement),
            typeof(foreignObject),
            new PropertyMetadata(null, OnSvgPropertyChanged));

    public UIElement? Child
    {
        get => (UIElement?)GetValue(ChildProperty);
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
        return ReadLocalValue(widthProperty) != DependencyProperty.UnsetValue;
    }

    internal partial bool IsHeightSet()
    {
        return ReadLocalValue(heightProperty) != DependencyProperty.UnsetValue;
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
