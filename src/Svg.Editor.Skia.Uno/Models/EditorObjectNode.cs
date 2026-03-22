using System;
using System.ComponentModel;
using Microsoft.UI.Xaml;
using Svg;
using Svg.Editor.Core;
using Svg.Editor.Svg;

namespace Svg.Editor.Skia.Uno.Models;

public sealed class EditorObjectNode : INotifyPropertyChanged
{
    private bool _isSelected;
    private bool _isVisible;
    private bool _isLocked;
    private bool _isExpanded = true;
    private bool _isPointerOver;

    public EditorObjectNode(SvgElement element, int depth)
    {
        Element = element;
        Depth = depth;
        IconKind = GetIconKind(element);
        IconGlyph = GetIconGlyph(element);
        TypeLabel = GetTypeLabel(element);
        Name = string.IsNullOrWhiteSpace(element.ID) ? TypeLabel : element.ID!;
        Subtitle = string.IsNullOrWhiteSpace(element.ID) ? "Untitled element" : TypeLabel;
        HasChildren = element.Children.OfType<SvgElement>().Any(ShouldShowAsOutlineChild);
        _isVisible = SvgElementInfo.IsVisible(element);
        _isLocked = GetInitialLockState(element);
    }

    public SvgElement Element { get; }

    public int Depth { get; }

    public Thickness Indent => new(Depth * 18, 0, 0, 0);

    public FigmaIconKind IconKind { get; }

    public string IconGlyph { get; }

    public string Name { get; }

    public string TypeLabel { get; }

    public string Subtitle { get; }

    public bool HasChildren { get; }

    public double ExpanderOpacity => HasChildren ? 1.0 : 0.0;

    public string DisclosureGlyph => HasChildren ? (_isExpanded ? "⌄" : "›") : string.Empty;

    public FigmaIconKind VisibilityIconKind => _isVisible ? FigmaIconKind.Eye : FigmaIconKind.EyeOff;

    public FigmaIconKind LockIconKind => _isLocked ? FigmaIconKind.Lock : FigmaIconKind.Unlock;

    public double VisibilityActionOpacity => _isSelected || _isPointerOver || !_isVisible ? 1.0 : 0.0;

    public double LockActionOpacity => _isSelected || _isPointerOver || _isLocked ? 1.0 : 0.0;

    public double RowContentOpacity => _isVisible ? 1.0 : 0.52;

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
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(VisibilityActionOpacity)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(LockActionOpacity)));
        }
    }

    public bool IsExpanded
    {
        get => _isExpanded;
        set
        {
            if (_isExpanded == value)
            {
                return;
            }

            _isExpanded = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsExpanded)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(DisclosureGlyph)));
        }
    }

    public bool IsVisible
    {
        get => _isVisible;
        set
        {
            if (_isVisible == value)
            {
                return;
            }

            _isVisible = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsVisible)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(VisibilityIconKind)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(VisibilityActionOpacity)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(RowContentOpacity)));
        }
    }

    public bool IsLocked
    {
        get => _isLocked;
        set
        {
            if (_isLocked == value)
            {
                return;
            }

            _isLocked = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsLocked)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(LockIconKind)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(LockActionOpacity)));
        }
    }

    public bool IsPointerOver
    {
        get => _isPointerOver;
        set
        {
            if (_isPointerOver == value)
            {
                return;
            }

            _isPointerOver = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsPointerOver)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(VisibilityActionOpacity)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(LockActionOpacity)));
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public static string GetIconGlyph(SvgElement element)
    {
        if (ToolService.TryGetSemanticTool(element, out var tool))
        {
            return tool switch
            {
                ToolService.Tool.Slice => "⌗",
                ToolService.Tool.Arrow => "↗",
                ToolService.Tool.Star => "★",
                ToolService.Tool.Brush => "B",
                ToolService.Tool.Pencil => "P",
                ToolService.Tool.Freehand => "~",
                _ => string.Empty
            };
        }

        return element switch
        {
            SvgDocument => "◫",
            SvgGroup group when FrameService.GetContainerKind(group) == FrameContainerKind.Frame => "▣",
            SvgGroup group when FrameService.GetContainerKind(group) == FrameContainerKind.Section => "▤",
            SvgGroup => "☷",
            SvgRectangle => "▭",
            SvgCircle or SvgEllipse => "◯",
            SvgLine => "╱",
            SvgPath => "◌",
            SvgPolygon or SvgPolyline => "⬠",
            SvgTextBase => "T",
            SvgImage => "▣",
            SvgUse => "⧉",
            _ => "◇"
        };
    }

    public static FigmaIconKind GetIconKind(SvgElement element)
    {
        if (ToolService.TryGetSemanticTool(element, out var tool))
        {
            return tool switch
            {
                ToolService.Tool.Slice => FigmaIconKind.Slice,
                ToolService.Tool.Arrow => FigmaIconKind.Arrow,
                ToolService.Tool.Star => FigmaIconKind.Star,
                ToolService.Tool.Brush => FigmaIconKind.Brush,
                ToolService.Tool.Pencil => FigmaIconKind.Pencil,
                ToolService.Tool.Freehand => FigmaIconKind.Brush,
                _ => element switch
                {
                    SvgPath => FigmaIconKind.Vector,
                    SvgPolygon => FigmaIconKind.Polygon,
                    SvgLine => FigmaIconKind.Line,
                    SvgRectangle => FigmaIconKind.Rectangle,
                    _ => FigmaIconKind.Vector
                }
            };
        }

        return element switch
        {
            SvgDocument => FigmaIconKind.Page,
            SvgGroup group when FrameService.GetContainerKind(group) == FrameContainerKind.Frame => FigmaIconKind.Frame,
            SvgGroup group when FrameService.GetContainerKind(group) == FrameContainerKind.Section => FigmaIconKind.Section,
            SvgGroup => FigmaIconKind.Group,
            SvgRectangle => FigmaIconKind.Rectangle,
            SvgCircle or SvgEllipse => FigmaIconKind.Ellipse,
            SvgLine => FigmaIconKind.Line,
            SvgPath => FigmaIconKind.Vector,
            SvgPolygon or SvgPolyline => FigmaIconKind.Vector,
            SvgTextBase => FigmaIconKind.Text,
            SvgImage => FigmaIconKind.Image,
            SvgUse => FigmaIconKind.Instance,
            _ => element.Children.OfType<SvgElement>().Any() ? FigmaIconKind.Frame : FigmaIconKind.Rectangle
        };
    }

    private static string GetTypeLabel(SvgElement element)
    {
        if (ToolService.TryGetSemanticTool(element, out var tool))
        {
            return tool switch
            {
                ToolService.Tool.Slice => "Slice",
                ToolService.Tool.Arrow => "Arrow",
                ToolService.Tool.Star => "Star",
                ToolService.Tool.Brush => "Brush stroke",
                ToolService.Tool.Pencil => "Pencil stroke",
                ToolService.Tool.Freehand => "Freehand stroke",
                _ => SvgElementInfo.GetElementName(element.GetType())
            };
        }

        return element switch
        {
            SvgGroup group when FrameService.GetContainerKind(group) != FrameContainerKind.Group => FrameService.GetContainerLabel(FrameService.GetContainerKind(group)),
            SvgGroup => "Group",
            _ => SvgElementInfo.GetElementName(element.GetType())
        };
    }

    private static bool GetInitialLockState(SvgElement element)
    {
        return element.CustomAttributes.TryGetValue("data-locked", out var flag)
            && string.Equals(flag, "true", StringComparison.OrdinalIgnoreCase);
    }

    private static bool ShouldShowAsOutlineChild(SvgElement child)
    {
        return (child is SvgGroup or SvgVisualElement)
            && !FrameService.IsFrameBackground(child);
    }
}
