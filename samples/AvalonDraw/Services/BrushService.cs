using System.Collections.ObjectModel;
using Avalonia.Media;
using Svg;
using System.Linq;

namespace AvalonDraw.Services;

public class BrushService
{
    public class BrushEntry
    {
        public StrokeProfile Profile { get; }
        public string Name { get; }

        public BrushEntry(string name, StrokeProfile profile)
        {
            Name = name;
            Profile = profile;
        }

        public override string ToString() => Name;
    }

    public ObservableCollection<BrushEntry> Brushes { get; } = new();
    public BrushEntry? SelectedBrush { get; set; }

    public BrushService()
    {
        var def = new StrokeProfile();
        Brushes.Add(new BrushEntry("Default", def));
        SelectedBrush = Brushes[0];
    }

    public class SwatchEntry
    {
        public SvgLinearGradientServer Swatch { get; }
        public string Name { get; }

        public SwatchEntry(SvgLinearGradientServer swatch, string name)
        {
            Swatch = swatch;
            Name = name;
        }

        public string Color
        {
            get => ColorToString(GetColor());
            set => SetColor(ParseColor(value));
        }

        private System.Drawing.Color GetColor()
        {
            var stop = Swatch.Stops.FirstOrDefault();
            return stop?.GetColor(Swatch) ?? System.Drawing.Color.Black;
        }

        private void SetColor(System.Drawing.Color c)
        {
            Swatch.Children.Clear();
            Swatch.Children.Add(new SvgGradientStop
            {
                Offset = new SvgUnit(0f),
                StopColor = new SvgColourServer(c),
                StopOpacity = 1f
            });
            Swatch.Children.Add(new SvgGradientStop
            {
                Offset = new SvgUnit(1f),
                StopColor = new SvgColourServer(c),
                StopOpacity = 1f
            });
        }

        private static string ColorToString(System.Drawing.Color c)
            => $"#{c.A:X2}{c.R:X2}{c.G:X2}{c.B:X2}";

        private static System.Drawing.Color ParseColor(string color)
        {
            var ac = Avalonia.Media.Color.Parse(color);
            return System.Drawing.Color.FromArgb(ac.A, ac.R, ac.G, ac.B);
        }

        public override string ToString() => Name;
    }

    public ObservableCollection<SwatchEntry> Swatches { get; } = new();

    public void LoadSwatches(SvgDocument? document)
    {
        Swatches.Clear();
        if (document is null)
            return;
        int index = 1;
        foreach (var grad in document.Descendants().OfType<SvgLinearGradientServer>())
        {
            if (grad.Stops.Count == 1 || (grad.Stops.Count == 2 && grad.Stops[0].GetColor(grad) == grad.Stops[1].GetColor(grad)))
            {
                var name = string.IsNullOrEmpty(grad.ID) ? $"Swatch {index++}" : grad.ID!;
                Swatches.Add(new SwatchEntry(grad, name));
            }
        }
    }
}

