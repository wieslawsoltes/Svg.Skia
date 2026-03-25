using System.ComponentModel;
using System.Globalization;
using System.Runtime.CompilerServices;
using Windows.UI;

namespace Svg.Editor.Skia.Uno.Models;

public sealed class EditorSelectionColorItem : INotifyPropertyChanged
{
    private Color _color;

    public EditorSelectionColorItem(
        Color color,
        PaintStyleTarget target,
        string summaryText,
        double strokeWidth,
        int usageCount)
    {
        _color = color;
        OriginalColor = color;
        Target = target;
        SummaryText = string.IsNullOrWhiteSpace(summaryText)
            ? ColorPickerColorHelper.ToHexRgb(color)
            : summaryText;
        StrokeWidth = strokeWidth;
        UsageCount = usageCount;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public Color OriginalColor { get; }

    public PaintStyleTarget Target { get; }

    public string SummaryText { get; }

    public double StrokeWidth { get; }

    public int UsageCount { get; }

    public string CurrentStrokeWidthText => StrokeWidth.ToString("0.##", CultureInfo.InvariantCulture);

    public Color Color
    {
        get => _color;
        set => SetField(ref _color, value);
    }

    private void RaisePropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    private bool SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return false;
        }

        field = value;
        RaisePropertyChanged(propertyName);
        return true;
    }
}
