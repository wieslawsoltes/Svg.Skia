using System.Collections.ObjectModel;

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
}

