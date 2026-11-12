using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;

namespace Svg.Editor.Skia.Uno;

/// <summary>Observes retained ancestor visibility without subscribing to layout or polling each frame.</summary>
internal sealed class StudioEditorVisibility(FrameworkElement owner, Action changed) : IDisposable
{
    private readonly List<(DependencyObject Owner, DependencyProperty Property, long Token)> _subscriptions = new();
    public bool IsVisible { get; private set; }

    public void Attach()
    {
        Dispose();
        for (DependencyObject? current = owner; current is not null; current = VisualTreeHelper.GetParent(current))
        {
            if (current is UIElement)
                Observe(current, UIElement.VisibilityProperty);
            if (current is StudioSection)
                Observe(current, StudioSection.IsExpandedProperty);
        }
        IsVisible = ComputeVisibility();
    }

    private void Observe(DependencyObject element, DependencyProperty property)
        => _subscriptions.Add((element, property, element.RegisterPropertyChangedCallback(property, VisibilityChanged)));

    private bool ComputeVisibility()
    {
        if (!owner.IsLoaded)
            return false;
        for (DependencyObject? current = owner; current is not null; current = VisualTreeHelper.GetParent(current))
            if (current is UIElement { Visibility: Visibility.Collapsed } or StudioSection { IsExpanded: false })
                return false;
        return true;
    }

    private void VisibilityChanged(DependencyObject sender, DependencyProperty property)
    {
        var visible = ComputeVisibility();
        if (visible == IsVisible)
            return;
        IsVisible = visible;
        changed();
    }

    public void Dispose()
    {
        foreach (var (element, property, token) in _subscriptions)
            element.UnregisterPropertyChangedCallback(property, token);
        _subscriptions.Clear();
        IsVisible = false;
    }
}
