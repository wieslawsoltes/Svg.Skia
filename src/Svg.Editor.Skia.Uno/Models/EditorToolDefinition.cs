using System.ComponentModel;
using Microsoft.UI.Xaml;
using Svg.Editor.Svg;

namespace Svg.Editor.Skia.Uno.Models;

public sealed class EditorToolDefinition : INotifyPropertyChanged
{
    private bool _isSelected;

    public EditorToolDefinition(ToolService.Tool tool, string glyph, string label, string shortcut, FigmaIconKind iconKind = FigmaIconKind.Rectangle, bool hasIcon = false)
    {
        Tool = tool;
        Glyph = glyph;
        Label = label;
        Shortcut = shortcut;
        HasIcon = hasIcon;
        IconKind = iconKind;
    }

    public ToolService.Tool Tool { get; }

    public string Glyph { get; }

    public string Label { get; }

    public string Shortcut { get; }

    public FigmaIconKind IconKind { get; }

    public bool HasIcon { get; }

    public Visibility IconVisibility => HasIcon ? Visibility.Visible : Visibility.Collapsed;

    public Visibility GlyphVisibility => HasIcon ? Visibility.Collapsed : Visibility.Visible;

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
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsSelected)));
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
}
