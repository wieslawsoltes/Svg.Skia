using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;

namespace AvalonDraw.Services;

public class StrokePointInfo
{
    public double Offset { get; set; }
    public double Width { get; set; }
}

public class StrokeProfile
{
    public ObservableCollection<StrokePointInfo> Points { get; }

    public StrokeProfile()
    {
        Points = new ObservableCollection<StrokePointInfo>
        {
            new() { Offset = 0.0, Width = 1.0 },
            new() { Offset = 1.0, Width = 1.0 }
        };
    }

    public static StrokeProfile Parse(string text)
    {
        var profile = new StrokeProfile();
        profile.Points.Clear();
        foreach (var part in text.Split(';'))
        {
            var items = part.Split(',');
            if (items.Length != 2)
                continue;
            if (double.TryParse(items[0], NumberStyles.Float, CultureInfo.InvariantCulture, out var o) &&
                double.TryParse(items[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var w))
            {
                profile.Points.Add(new StrokePointInfo { Offset = o, Width = w });
            }
        }
        if (profile.Points.Count == 0)
        {
            profile.Points.Add(new StrokePointInfo { Offset = 0.0, Width = 1.0 });
            profile.Points.Add(new StrokePointInfo { Offset = 1.0, Width = 1.0 });
        }
        return profile;
    }

    public override string ToString()
    {
        var parts = new List<string>(Points.Count);
        parts.AddRange(Points.Select(p => $"{p.Offset.ToString(CultureInfo.InvariantCulture)},{p.Width.ToString(CultureInfo.InvariantCulture)}"));
        return string.Join(";", parts);
    }
}

