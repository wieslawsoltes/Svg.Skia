using System.Collections.ObjectModel;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Svg;
using Svg.Pathing;

namespace AvalonDraw;

public partial class PathEditorWindow : Window
{
    public class SegmentEntry
    {
        public int Index { get; set; }
        public string Text { get; set; } = string.Empty;
    }

    private readonly DataGrid _grid;
    private readonly ObservableCollection<SegmentEntry> _entries = new();

    public PathEditorWindow(SvgPath path)
    {
        InitializeComponent();
        _grid = this.FindControl<DataGrid>("SegmentsGrid");
        int i = 0;
        foreach (var seg in path.PathData)
        {
            _entries.Add(new SegmentEntry { Index = i++, Text = seg.ToString() });
        }
        _grid.ItemsSource = _entries;
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public string Result { get; private set; } = string.Empty;

    private void OkButton_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        Result = string.Join(" ", _entries.Select(s => s.Text));
        Close(Result);
    }

    private void CancelButton_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        Close(null);
    }
}
