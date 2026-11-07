using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using SkiaSharp;

namespace Svg.Editor.Skia.Uno;

/// <summary>Invalidates a Skia surface when a shared semantic brush changes color.</summary>
internal sealed class StudioBrushSubscriptions(Action invalidate) : IDisposable
{
    private readonly Dictionary<DependencyProperty, (SolidColorBrush Brush, long ColorToken, long OpacityToken)> _registrations = new();

    public void Observe(DependencyProperty property, Brush? brush)
    {
        if (_registrations.Remove(property, out var previous))
        {
            previous.Brush.UnregisterPropertyChangedCallback(SolidColorBrush.ColorProperty, previous.ColorToken);
            previous.Brush.UnregisterPropertyChangedCallback(Brush.OpacityProperty, previous.OpacityToken);
        }
        if (brush is SolidColorBrush solid)
        {
            var color = solid.RegisterPropertyChangedCallback(SolidColorBrush.ColorProperty, (_, _) => invalidate());
            var opacity = solid.RegisterPropertyChangedCallback(Brush.OpacityProperty, (_, _) => invalidate());
            _registrations[property] = (solid, color, opacity);
        }
    }

    public static SKColor Color(Brush? brush, SKColor fallback)
    {
        if (brush is not SolidColorBrush solid)
            return fallback;
        var color = solid.Color;
        return new SKColor(color.R, color.G, color.B, (byte)Math.Round(color.A * solid.Opacity));
    }

    public void Dispose()
    {
        foreach (var registration in _registrations.Values)
        {
            registration.Brush.UnregisterPropertyChangedCallback(SolidColorBrush.ColorProperty, registration.ColorToken);
            registration.Brush.UnregisterPropertyChangedCallback(Brush.OpacityProperty, registration.OpacityToken);
        }
        _registrations.Clear();
    }
}
