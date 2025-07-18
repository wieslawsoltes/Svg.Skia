using System.Collections.ObjectModel;
using System.Linq;
using Svg;

namespace AvalonDraw.Services;

public class StrokeProfileEntry : PropertyEntry
{
    public ObservableCollection<StrokePointInfo> Points { get; }

    public StrokeProfileEntry(string text)
        : base("StrokeProfile", text, (_, __) => { })
    {
        Points = StrokeProfile.Parse(text).Points;
    }

    public override void Apply(object target)
    {
        if (target is SvgVisualElement element)
        {
            element.CustomAttributes["stroke-profile"] = ToString();
        }
    }

    public void UpdateValue()
    {
        Value = ToString();
    }

    public override string ToString()
    {
        var profile = new StrokeProfile();
        profile.Points.Clear();
        foreach (var p in Points)
            profile.Points.Add(new StrokePointInfo { Offset = p.Offset, Width = p.Width });
        return profile.ToString();
    }
}


