using System.Collections.ObjectModel;
using System.Globalization;
using Microsoft.UI.Xaml;
using Svg;
using Svg.Editor.Skia.Uno.Models;
using Windows.UI;

namespace Svg.Editor.Skia.Uno;

public partial class SvgEditorWorkspacePage
{
    private const string LibraryFillStyleIdAttribute = "data-library-fill-style-id";
    private const string LibraryFillStyleLibraryIdAttribute = "data-library-fill-style-library-id";
    private const string LibraryStrokeStyleIdAttribute = "data-library-stroke-style-id";
    private const string LibraryStrokeStyleLibraryIdAttribute = "data-library-stroke-style-library-id";

    private Color _strokeColor = Color.FromArgb(255, 17, 24, 39);
    private bool _isStrokeEnabled;
    private bool _isStrokeColorEditable;
    private bool _isUpdatingStrokeState;

    public ObservableCollection<ColorSwatchItem> LibraryPaintStyles { get; } = [];

    public Color StrokeColor
    {
        get => _strokeColor;
        set
        {
            if (SetField(ref _strokeColor, value) && !_isUpdatingStrokeState)
            {
                ApplyStrokeState();
            }
        }
    }

    public bool IsStrokeEnabled
    {
        get => _isStrokeEnabled;
        set
        {
            if (SetField(ref _isStrokeEnabled, value) && !_isUpdatingStrokeState)
            {
                ApplyStrokeState();
            }
        }
    }

    public bool IsStrokeColorEditable
    {
        get => _isStrokeColorEditable;
        private set => SetField(ref _isStrokeColorEditable, value);
    }

    protected void OnPaintStyleRequested(object sender, PaintStyleRequestedEventArgs e)
    {
        if (sender is FrameworkElement { DataContext: EditorSelectionColorItem item })
        {
            ApplyPaintStyleToSelectionColor(item, e);
            return;
        }

        ApplyPaintStyleToSelection(e);
    }

    protected void OnPaintStyleCreateRequested(object sender, PaintStyleCreateRequestedEventArgs e)
    {
        if (sender is FrameworkElement { DataContext: EditorSelectionColorItem item })
        {
            CreatePaintStyleFromSelectionColor(item, e);
            return;
        }

        CreatePaintStyleFromSelection(e);
    }

    private void ApplyStrokeState()
    {
        if (_selectedElements.Count != 1 || _selectedElement is null || !IsStrokeColorEditable)
        {
            return;
        }

        if (!IsStrokeEnabled)
        {
            _selectedElement.Stroke = SvgPaintServer.None;
            _selectedElement.StrokeOpacity = 1f;
            ClearPaintStyleLink(_selectedElement, EditorPaintTarget.Stroke);
            RefreshDocumentVisual(rebuildOutline: false, reloadProperties: true);
            return;
        }

        var drawingColor = System.Drawing.Color.FromArgb(255, StrokeColor.R, StrokeColor.G, StrokeColor.B);
        var solidStroke = _selectedElement.Stroke as SvgColourServer;
        if (solidStroke is null
            || ReferenceEquals(solidStroke, SvgPaintServer.None)
            || ReferenceEquals(solidStroke, SvgPaintServer.Inherit)
            || ReferenceEquals(solidStroke, SvgPaintServer.NotSet))
        {
            _selectedElement.Stroke = new SvgColourServer(drawingColor);
        }
        else
        {
            solidStroke.Colour = drawingColor;
        }

        _selectedElement.StrokeOpacity = StrokeColor.A / 255f;
        if (_selectedElement.StrokeWidth.Value <= 0f)
        {
            _selectedElement.StrokeWidth = new SvgUnit(1f);
        }

        ClearPaintStyleLink(_selectedElement, EditorPaintTarget.Stroke);
        RefreshDocumentVisual(rebuildOutline: false, reloadProperties: true);
    }

    private void RefreshStrokeInspectorState(SvgVisualElement element)
    {
        _isUpdatingStrokeState = true;
        try
        {
            switch (element.Stroke)
            {
                case SvgColourServer colorServer when !ReferenceEquals(colorServer, SvgPaintServer.None)
                                                     && !ReferenceEquals(colorServer, SvgPaintServer.Inherit)
                                                     && !ReferenceEquals(colorServer, SvgPaintServer.NotSet):
                    StrokeColor = Color.FromArgb(
                        (byte)Math.Clamp((int)Math.Round(element.StrokeOpacity * 255f), 0, 255),
                        colorServer.Colour.R,
                        colorServer.Colour.G,
                        colorServer.Colour.B);
                    IsStrokeEnabled = true;
                    IsStrokeColorEditable = true;
                    break;
                case null:
                case SvgPaintServer paintServer when ReferenceEquals(paintServer, SvgPaintServer.None):
                    IsStrokeEnabled = false;
                    IsStrokeColorEditable = true;
                    break;
                default:
                    if (TryResolveEffectivePaintColor(element, EditorPaintTarget.Stroke, out var inheritedStrokeColor))
                    {
                        StrokeColor = inheritedStrokeColor;
                    }

                    IsStrokeEnabled = false;
                    IsStrokeColorEditable = true;
                    break;
            }
        }
        finally
        {
            _isUpdatingStrokeState = false;
        }
    }

    private void RefreshLibraryPaintStyles()
    {
        LibraryPaintStyles.Clear();

        foreach (var definition in _libraryCatalog.Values.OrderBy(value => value.Item.Name, StringComparer.OrdinalIgnoreCase))
        {
            foreach (var style in definition.Swatches)
            {
                LibraryPaintStyles.Add(NormalizeLibraryPaintStyle(definition, style));
            }
        }
    }

    private void ApplyPaintStyleToSelection(PaintStyleRequestedEventArgs args)
    {
        if (_selectedElements.Count == 0)
        {
            return;
        }

        var style = EnsurePaintStyleImported(args.Style);
        foreach (var element in _selectedElements)
        {
            ApplyPaintStyleToElement(element, style, args.Target);
        }

        RefreshDocumentVisual(rebuildOutline: false, reloadProperties: true);
        CanvasStatus = $"Applied {style.Label} to the {GetPaintTargetLabel(args.Target)}.";
    }

    private void CreatePaintStyleFromSelection(PaintStyleCreateRequestedEventArgs args)
    {
        var style = CreateOrUpdateCurrentFilePaintStyle(args.Color, args.Target, args.StrokeWidth);
        if (style is null)
        {
            return;
        }

        ApplyPaintStyleToSelection(new PaintStyleRequestedEventArgs(style, args.Target));
        CanvasStatus = $"Saved {style.Label} to {DocumentTitle}.";
    }

    private ColorSwatchItem? CreateOrUpdateCurrentFilePaintStyle(Color color, EditorPaintTarget target, double strokeWidth)
    {
        PublishCurrentFileLibrary();

        if (!_libraryCatalog.TryGetValue("current-file", out var definition))
        {
            return null;
        }

        var styleId = BuildPaintStyleId(color, target, strokeWidth);
        var style = definition.Swatches.FirstOrDefault(item => string.Equals(item.StyleId, styleId, StringComparison.Ordinal));
        if (style is not null)
        {
            return NormalizeLibraryPaintStyle(definition, style);
        }

        style = CreateLibraryPaintStyle(
            definition.Item.Id,
            definition.Item.Name,
            BuildPaintStyleLabel(color, target, strokeWidth),
            color,
            target,
            target == EditorPaintTarget.Stroke ? "Stroke styles" : "Fill styles",
            "current file published paint style",
            strokeWidth,
            styleId);
        UpsertPaintStyle(definition, style);
        RefreshLibrariesState();
        return NormalizeLibraryPaintStyle(definition, style);
    }

    private ColorSwatchItem EnsurePaintStyleImported(ColorSwatchItem style)
    {
        if (string.IsNullOrWhiteSpace(style.LibraryId))
        {
            return style;
        }

        if (_libraryCatalog.TryGetValue(style.LibraryId, out var definition))
        {
            if (!definition.Item.IsCurrentFile && !definition.Item.IsEnabled)
            {
                definition.Item.IsEnabled = true;
                if (definition.Item.InstalledVersion < definition.Item.AvailableVersion)
                {
                    definition.Item.InstalledVersion = definition.Item.AvailableVersion;
                    definition.Item.HasUpdate = false;
                }

                SyncLibrariesAcrossPages();
                RefreshLibrariesState();
            }

            return NormalizeLibraryPaintStyle(definition, style);
        }

        return style;
    }

    private static string BuildPaintStyleId(Color color, EditorPaintTarget target, double strokeWidth)
    {
        var widthToken = strokeWidth.ToString("0.##", CultureInfo.InvariantCulture).Replace('.', '-');
        var targetToken = target switch
        {
            EditorPaintTarget.Fill => "fill",
            EditorPaintTarget.Stroke => "stroke",
            _ => "paint"
        };

        return $"{targetToken}-{color.A:x2}{color.R:x2}{color.G:x2}{color.B:x2}-{widthToken}";
    }

    private static string BuildPaintStyleLabel(Color color, EditorPaintTarget target, double strokeWidth)
    {
        var hex = ColorPickerColorHelper.ToHexRgb(color);
        return target switch
        {
            EditorPaintTarget.Fill => $"Fill / {hex}",
            EditorPaintTarget.Stroke => $"Stroke / {hex} / {strokeWidth:0.##} px",
            _ => $"Paint / {hex}"
        };
    }

    private static ColorSwatchItem CreateLibraryPaintStyle(
        string libraryId,
        string libraryName,
        string label,
        Color color,
        EditorPaintTarget target,
        string sectionName,
        string searchKeywords,
        double strokeWidth = 1.0,
        string? styleId = null,
        string? description = null)
    {
        return new ColorSwatchItem(
            color,
            label,
            target,
            styleId ?? BuildPaintStyleId(color, target, strokeWidth),
            libraryId,
            libraryName,
            sectionName,
            searchKeywords,
            strokeWidth,
            description);
    }

    private static ColorSwatchItem NormalizeLibraryPaintStyle(SampleLibraryDefinition definition, ColorSwatchItem style)
    {
        return new ColorSwatchItem(
            style.Color,
            style.Label,
            style.Target,
            string.IsNullOrWhiteSpace(style.StyleId) ? BuildPaintStyleId(style.Color, style.Target, style.StrokeWidth) : style.StyleId,
            string.IsNullOrWhiteSpace(style.LibraryId) ? definition.Item.Id : style.LibraryId,
            string.IsNullOrWhiteSpace(style.LibraryName) ? definition.Item.Name : style.LibraryName,
            style.SectionName,
            style.SearchKeywords,
            style.StrokeWidth,
            style.Description);
    }

    private static void UpsertPaintStyle(SampleLibraryDefinition definition, ColorSwatchItem style)
    {
        var existing = definition.Swatches.FindIndex(item => string.Equals(item.StyleId, style.StyleId, StringComparison.Ordinal));
        if (existing >= 0)
        {
            definition.Swatches[existing] = style;
        }
        else
        {
            definition.Swatches.Insert(0, style);
        }

        definition.Item.ColorCount = definition.Swatches.Count;
    }

    private void AppendPublishedPaintStyle(
        List<ColorSwatchItem> swatches,
        HashSet<string> seen,
        SvgPaintServer? paint,
        float opacity,
        EditorPaintTarget target,
        double strokeWidth)
    {
        if (paint is not SvgColourServer colorServer
            || ReferenceEquals(paint, SvgPaintServer.None)
            || ReferenceEquals(paint, SvgPaintServer.Inherit)
            || ReferenceEquals(paint, SvgPaintServer.NotSet))
        {
            return;
        }

        var color = Color.FromArgb(
            (byte)Math.Clamp((int)Math.Round(opacity * 255f), 0, 255),
            colorServer.Colour.R,
            colorServer.Colour.G,
            colorServer.Colour.B);
        var style = CreateLibraryPaintStyle(
            "current-file",
            DocumentTitle,
            BuildPaintStyleLabel(color, target, strokeWidth),
            color,
            target,
            target == EditorPaintTarget.Stroke ? "Stroke styles" : "Fill styles",
            "published file paint style",
            strokeWidth);
        if (seen.Add(style.StyleId))
        {
            swatches.Add(style);
        }
    }

    private void ReapplyLibraryPaintStylesAcrossPages(string? libraryId = null)
    {
        foreach (var state in _pageStates)
        {
            ReapplyLibraryPaintStyles(state.Document, libraryId);
        }
    }

    private void ReapplyLibraryPaintStyles(SvgDocument document, string? libraryId)
    {
        foreach (var element in document.Descendants().OfType<SvgVisualElement>())
        {
            if (TryResolveLinkedPaintStyle(element, EditorPaintTarget.Fill, out var fillStyle)
                && (libraryId is null || string.Equals(fillStyle.LibraryId, libraryId, StringComparison.Ordinal)))
            {
                ApplyPaintStyleToElement(element, fillStyle, EditorPaintTarget.Fill);
            }

            if (TryResolveLinkedPaintStyle(element, EditorPaintTarget.Stroke, out var strokeStyle)
                && (libraryId is null || string.Equals(strokeStyle.LibraryId, libraryId, StringComparison.Ordinal)))
            {
                ApplyPaintStyleToElement(element, strokeStyle, EditorPaintTarget.Stroke);
            }
        }
    }

    private bool TryResolveLinkedPaintStyle(SvgVisualElement element, EditorPaintTarget target, out ColorSwatchItem style)
    {
        var styleIdAttribute = GetPaintStyleIdAttribute(target);
        var libraryIdAttribute = GetPaintStyleLibraryIdAttribute(target);
        style = null!;

        if (!element.CustomAttributes.TryGetValue(styleIdAttribute, out var styleId)
            || string.IsNullOrWhiteSpace(styleId)
            || !element.CustomAttributes.TryGetValue(libraryIdAttribute, out var libraryId)
            || string.IsNullOrWhiteSpace(libraryId)
            || !_libraryCatalog.TryGetValue(libraryId, out var definition))
        {
            return false;
        }

        var match = definition.Swatches
            .Select(item => NormalizeLibraryPaintStyle(definition, item))
            .FirstOrDefault(item => string.Equals(item.StyleId, styleId, StringComparison.Ordinal));

        if (match is null)
        {
            return false;
        }

        style = match;
        return true;
    }

    private static void ApplyPaintStyleToElement(SvgVisualElement element, ColorSwatchItem style, EditorPaintTarget target)
    {
        var drawingColor = System.Drawing.Color.FromArgb(255, style.Color.R, style.Color.G, style.Color.B);
        if (target == EditorPaintTarget.Fill && style.SupportsFill)
        {
            element.Fill = new SvgColourServer(drawingColor);
            element.FillOpacity = style.Color.A / 255f;
            SetPaintStyleLink(element, style, EditorPaintTarget.Fill);
        }
        else if (target == EditorPaintTarget.Stroke && style.SupportsStroke)
        {
            element.Stroke = new SvgColourServer(drawingColor);
            element.StrokeOpacity = style.Color.A / 255f;
            element.StrokeWidth = new SvgUnit(Math.Max((float)style.StrokeWidth, 0.25f));
            SetPaintStyleLink(element, style, EditorPaintTarget.Stroke);
        }
    }

    private static void SetPaintStyleLink(SvgVisualElement element, ColorSwatchItem style, EditorPaintTarget target)
    {
        if (string.IsNullOrWhiteSpace(style.StyleId) || string.IsNullOrWhiteSpace(style.LibraryId))
        {
            ClearPaintStyleLink(element, target);
            return;
        }

        element.CustomAttributes[GetPaintStyleIdAttribute(target)] = style.StyleId;
        element.CustomAttributes[GetPaintStyleLibraryIdAttribute(target)] = style.LibraryId;
    }

    private static void ClearPaintStyleLink(SvgVisualElement element, EditorPaintTarget target)
    {
        element.CustomAttributes.Remove(GetPaintStyleIdAttribute(target));
        element.CustomAttributes.Remove(GetPaintStyleLibraryIdAttribute(target));
    }

    private static string GetPaintStyleIdAttribute(EditorPaintTarget target) => target == EditorPaintTarget.Stroke
        ? LibraryStrokeStyleIdAttribute
        : LibraryFillStyleIdAttribute;

    private static string GetPaintStyleLibraryIdAttribute(EditorPaintTarget target) => target == EditorPaintTarget.Stroke
        ? LibraryStrokeStyleLibraryIdAttribute
        : LibraryFillStyleLibraryIdAttribute;

    private bool ElementUsesLibraryPaintStyle(SvgVisualElement element, string libraryId)
    {
        return element.CustomAttributes.TryGetValue(LibraryFillStyleLibraryIdAttribute, out var fillLibraryId)
               && string.Equals(fillLibraryId, libraryId, StringComparison.Ordinal)
               || element.CustomAttributes.TryGetValue(LibraryStrokeStyleLibraryIdAttribute, out var strokeLibraryId)
               && string.Equals(strokeLibraryId, libraryId, StringComparison.Ordinal);
    }

    private string FormatPaintWithLibraryStyle(SvgVisualElement element, EditorPaintTarget target)
    {
        if (TryResolveLinkedPaintStyle(element, target, out var style))
        {
            return string.Equals(style.LibraryId, "current-file", StringComparison.Ordinal)
                ? style.Label
                : $"{style.LibraryName} · {style.Label}";
        }

        return target == EditorPaintTarget.Stroke
            ? FormatPaint(element.Stroke)
            : FormatPaint(element.Fill);
    }

    private static string GetPaintTargetLabel(EditorPaintTarget target) => target == EditorPaintTarget.Stroke ? "stroke" : "fill";
}
