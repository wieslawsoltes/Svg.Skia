using System.Collections.Specialized;
using System.ComponentModel;
using Microsoft.UI.Xaml;
using Svg;
using Svg.Editor.Skia.Uno.Models;
using Windows.UI;

namespace Svg.Editor.Skia.Uno;

public partial class SvgEditorWorkspacePage
{
    private void OnSelectionColorItemsCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.OldItems is not null)
        {
            foreach (var item in e.OldItems.OfType<EditorSelectionColorItem>())
            {
                item.PropertyChanged -= OnSelectionColorItemPropertyChanged;
            }
        }

        if (e.NewItems is not null)
        {
            foreach (var item in e.NewItems.OfType<EditorSelectionColorItem>())
            {
                item.PropertyChanged += OnSelectionColorItemPropertyChanged;
            }
        }
    }

    private void OnSelectionColorItemPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (_isUpdatingSelectionColorItems
            || sender is not EditorSelectionColorItem item
            || e.PropertyName is not nameof(EditorSelectionColorItem.Color))
        {
            return;
        }

        ApplySelectionColorItemChange(item);
    }

    private void ApplySelectionColorItemChange(EditorSelectionColorItem item)
    {
        if (_selectedElements.Count == 0)
        {
            return;
        }

        var affectedCount = 0;
        foreach (var element in _selectedElements)
        {
            if (!MatchesSelectionColorItem(element, item))
            {
                continue;
            }

            ApplyColorToElementPaint(element, item.Target, item.Color, item.StrokeWidth);
            affectedCount++;
        }

        if (affectedCount == 0)
        {
            return;
        }

        RefreshDocumentVisual(rebuildOutline: false, reloadProperties: true);
        CanvasStatus = $"Updated {affectedCount} matching {GetPaintTargetLabel(item.Target)} color{(affectedCount == 1 ? string.Empty : "s")}.";
    }

    private List<EditorSelectionColorItem> BuildSelectionColorItems()
    {
        var buckets = new Dictionary<int, SelectionColorBucket>();

        foreach (var element in _selectedElements)
        {
            AppendSelectionColorItem(buckets, element, PaintStyleTarget.Fill, element.Fill, element.FillOpacity, 1.0);
            AppendSelectionColorItem(buckets, element, PaintStyleTarget.Stroke, element.Stroke, element.StrokeOpacity, Math.Max(element.StrokeWidth.Value, 1.0));
        }

        return buckets.Values
            .Select(bucket => new EditorSelectionColorItem(
                bucket.Color,
                bucket.Target,
                bucket.Label,
                bucket.StrokeWidth,
                bucket.UsageCount))
            .ToList();
    }

    private void AppendSelectionColorItem(
        Dictionary<int, SelectionColorBucket> buckets,
        SvgVisualElement element,
        PaintStyleTarget target,
        SvgPaintServer? paint,
        float opacity,
        double strokeWidth)
    {
        if (!TryGetPaintColor(paint, opacity, out var color))
        {
            return;
        }

        var key = GetSelectionColorKey(target, color);
        if (buckets.TryGetValue(key, out var bucket))
        {
            bucket.UsageCount++;
            bucket.StrokeWidth = Math.Max(bucket.StrokeWidth, strokeWidth);
            return;
        }

        buckets[key] = new SelectionColorBucket
        {
            Color = color,
            Target = target,
            Label = BuildSelectionColorLabel(element, target, color),
            StrokeWidth = strokeWidth,
            UsageCount = 1
        };
    }

    private string BuildSelectionColorLabel(SvgVisualElement element, PaintStyleTarget target, Color color)
    {
        if (TryResolveLinkedPaintStyle(element, target, out var style))
        {
            return style.Label;
        }

        return ColorPickerColorHelper.ToHexRgb(color);
    }

    private void ApplyPaintStyleToSelectionColor(EditorSelectionColorItem item, PaintStyleRequestedEventArgs args)
    {
        if (_selectedElements.Count == 0)
        {
            return;
        }

        var style = EnsurePaintStyleImported(args.Style);
        var affectedCount = 0;
        foreach (var element in _selectedElements)
        {
            if (!MatchesSelectionColorItem(element, item))
            {
                continue;
            }

            ApplyPaintStyleToElement(element, style, item.Target);
            affectedCount++;
        }

        if (affectedCount == 0)
        {
            return;
        }

        RefreshDocumentVisual(rebuildOutline: false, reloadProperties: true);
        CanvasStatus = $"Applied {style.Label} to {affectedCount} matching {GetPaintTargetLabel(item.Target)} color{(affectedCount == 1 ? string.Empty : "s")}.";
    }

    private void CreatePaintStyleFromSelectionColor(EditorSelectionColorItem item, PaintStyleCreateRequestedEventArgs args)
    {
        var style = CreateOrUpdateCurrentFilePaintStyle(args.Color, item.Target, args.StrokeWidth);
        if (style is null)
        {
            return;
        }

        ApplyPaintStyleToSelectionColor(item, new PaintStyleRequestedEventArgs(style, item.Target));
        CanvasStatus = $"Saved {style.Label} to {DocumentTitle}.";
    }

    private bool MatchesSelectionColorItem(SvgVisualElement element, EditorSelectionColorItem item)
    {
        return item.Target switch
        {
            PaintStyleTarget.Fill => TryGetPaintColor(element.Fill, element.FillOpacity, out var fillColor) && ColorsEqual(fillColor, item.OriginalColor),
            PaintStyleTarget.Stroke => TryGetPaintColor(element.Stroke, element.StrokeOpacity, out var strokeColor) && ColorsEqual(strokeColor, item.OriginalColor),
            _ => false
        };
    }

    private void ApplyColorToElementPaint(SvgVisualElement element, PaintStyleTarget target, Color color, double strokeWidth)
    {
        var drawingColor = System.Drawing.Color.FromArgb(255, color.R, color.G, color.B);
        switch (target)
        {
            case PaintStyleTarget.Fill:
                element.Fill = new SvgColourServer(drawingColor);
                element.FillOpacity = color.A / 255f;
                ClearPaintStyleLink(element, PaintStyleTarget.Fill);
                break;
            case PaintStyleTarget.Stroke:
                element.Stroke = new SvgColourServer(drawingColor);
                element.StrokeOpacity = color.A / 255f;
                if (element.StrokeWidth.Value <= 0f)
                {
                    element.StrokeWidth = new SvgUnit((float)Math.Max(strokeWidth, 1.0));
                }

                ClearPaintStyleLink(element, PaintStyleTarget.Stroke);
                break;
        }
    }

    private static bool TryGetPaintColor(SvgPaintServer? paint, float opacity, out Color color)
    {
        color = default;
        if (paint is not SvgColourServer colorServer
            || ReferenceEquals(paint, SvgPaintServer.None)
            || ReferenceEquals(paint, SvgPaintServer.Inherit)
            || ReferenceEquals(paint, SvgPaintServer.NotSet))
        {
            return false;
        }

        color = Color.FromArgb(
            (byte)Math.Clamp((int)Math.Round(opacity * 255f), 0, 255),
            colorServer.Colour.R,
            colorServer.Colour.G,
            colorServer.Colour.B);
        return true;
    }

    private static bool ColorsEqual(Color left, Color right)
    {
        return left.A == right.A
            && left.R == right.R
            && left.G == right.G
            && left.B == right.B;
    }

    private static int GetSelectionColorKey(PaintStyleTarget target, Color color)
    {
        return HashCode.Combine((int)target, color.A, color.R, color.G, color.B);
    }

    private sealed class SelectionColorBucket
    {
        public Color Color { get; set; }

        public PaintStyleTarget Target { get; set; }

        public string Label { get; set; } = string.Empty;

        public double StrokeWidth { get; set; }

        public int UsageCount { get; set; }
    }
}
