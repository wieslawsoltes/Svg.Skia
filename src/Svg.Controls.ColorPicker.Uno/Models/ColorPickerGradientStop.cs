using System.ComponentModel;
using System.Runtime.CompilerServices;
using Microsoft.UI.Xaml;
using Windows.UI;

namespace Svg.Controls.ColorPicker.Uno.Models;

public sealed class ColorPickerGradientStop : INotifyPropertyChanged
{
    private Color _color;
    private double _offset;
    private string _name;
    private bool _isSelected;

    public ColorPickerGradientStop(Color color, double offset, string? name = null)
    {
        _color = color;
        _offset = Math.Clamp(offset, 0.0, 1.0);
        _name = name ?? string.Empty;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public Color Color
    {
        get => _color;
        set
        {
            if (_color.Equals(value))
            {
                return;
            }

            _color = value;
            RaisePropertyChanged();
            RaisePropertyChanged(nameof(ColorLabel));
            RaisePropertyChanged(nameof(Summary));
        }
    }

    public double Offset
    {
        get => _offset;
        set
        {
            var clamped = Math.Clamp(value, 0.0, 1.0);
            if (Math.Abs(_offset - clamped) < 0.0001)
            {
                return;
            }

            _offset = clamped;
            RaisePropertyChanged();
            RaisePropertyChanged(nameof(OffsetLabel));
            RaisePropertyChanged(nameof(Label));
            RaisePropertyChanged(nameof(Summary));
        }
    }

    public string Name
    {
        get => _name;
        set
        {
            if (string.Equals(_name, value, StringComparison.Ordinal))
            {
                return;
            }

            _name = value ?? string.Empty;
            RaisePropertyChanged();
            RaisePropertyChanged(nameof(Label));
            RaisePropertyChanged(nameof(Summary));
        }
    }

    public bool IsSelected
    {
        get => _isSelected;
        set
        {
            if (_isSelected == value)
            {
                return;
            }

            _isSelected = value;
            RaisePropertyChanged();
            RaisePropertyChanged(nameof(SelectionVisibility));
        }
    }

    public string Label => string.IsNullOrWhiteSpace(Name) ? OffsetLabel : Name;

    public string OffsetLabel => $"{Math.Round(Offset * 100.0):0}%";

    public string ColorLabel => $"#{ColorPickerColorHelper.ToHexRgb(Color)}";

    public string Summary => $"{Label} · {ColorLabel} · {OffsetLabel}";

    public Visibility SelectionVisibility => IsSelected ? Visibility.Visible : Visibility.Collapsed;

    private void RaisePropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
