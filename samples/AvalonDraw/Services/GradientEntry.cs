using System;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using Avalonia.Media;
using Svg;

namespace AvalonDraw.Services;

public class GradientStopInfo
{
    public double Offset { get; set; }
    public string Color { get; set; } = "#000000";
}

public class GradientStopsEntry : PropertyEntry
{
    public ObservableCollection<GradientStopInfo> Stops { get; }
    public SvgGradientServer Gradient { get; private set; }

    public GradientStopsEntry(SvgGradientServer gradient)
        : base("Stops", $"{gradient.Stops.Count} stops", (_, __) => { })
    {
        Gradient = gradient;
        Stops = new ObservableCollection<GradientStopInfo>(
            gradient.Stops.Select(s => new GradientStopInfo
            {
                Offset = s.Offset.Value,
                Color = ColorToString(s.GetColor(gradient))
            }));
    }

    public void SetGradient(SvgGradientServer gradient)
    {
        Gradient = gradient;
        Stops.Clear();
        foreach (var s in gradient.Stops)
            Stops.Add(new GradientStopInfo { Offset = s.Offset.Value, Color = ColorToString(s.GetColor(gradient)) });
        UpdateValue();
    }

    internal static string ColorToString(System.Drawing.Color c)
        => $"#{c.A:X2}{c.R:X2}{c.G:X2}{c.B:X2}";

    internal static System.Drawing.Color ParseColor(string color)
    {
        var ac = Color.Parse(color);
        return System.Drawing.Color.FromArgb(ac.A, ac.R, ac.G, ac.B);
    }

    public override void Apply(object target)
    {
        if (target is not SvgGradientServer grad)
            return;
        grad.Children.Clear();
        foreach (var info in Stops)
        {
            var stop = new SvgGradientStop
            {
                Offset = new SvgUnit((float)info.Offset),
                StopColor = new SvgColourServer(ParseColor(info.Color)),
                StopOpacity = 1f
            };
            grad.Children.Add(stop);
        }
    }

    public void UpdateValue() => Value = $"{Stops.Count} stops";
}
