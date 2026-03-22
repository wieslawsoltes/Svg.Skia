using System.Collections.ObjectModel;
using System.Globalization;
using Microsoft.UI.Xaml.Media;
using Svg;
using Svg.Editor.Skia.Uno.Controls;
using Svg.Editor.Skia.Uno.Models;
using Windows.UI;
using Windows.UI.Text;

namespace Svg.Editor.Skia.Uno;

public partial class SvgEditorWorkspacePage
{
    private const string LibraryTextStyleIdAttribute = "data-library-text-style-id";
    private const string LibraryTextStyleLibraryIdAttribute = "data-library-text-style-library-id";
    private const string LibraryEffectStyleIdAttribute = "data-library-effect-style-id";
    private const string LibraryEffectStyleLibraryIdAttribute = "data-library-effect-style-library-id";
    private const string TextLineHeightAttribute = "data-svgskia-text-line-height";

    private double _gridSize = DefaultGridSize;

    public ObservableCollection<EditorTextStyleItem> TextStyles { get; } = [];

    public ObservableCollection<ColorSwatchItem> ColorStyles { get; } = [];

    public ObservableCollection<EditorEffectStyleItem> EffectStyles { get; } = [];

    public ObservableCollection<EditorLayoutGuideStyleItem> LayoutGuideStyles { get; } = [];

    protected async void OnStyleRequested(object sender, EditorStyleRequestedEventArgs e)
    {
        switch (e.Kind)
        {
            case EditorStyleKind.Text:
                await HandleTextStyleRequestAsync(e);
                break;
            case EditorStyleKind.Color:
                await HandleColorStyleRequestAsync(e);
                break;
            case EditorStyleKind.Effect:
                await HandleEffectStyleRequestAsync(e);
                break;
            case EditorStyleKind.LayoutGuide:
                await HandleLayoutGuideStyleRequestAsync(e);
                break;
        }
    }

    private async Task HandleTextStyleRequestAsync(EditorStyleRequestedEventArgs e)
    {
        switch (e.Action)
        {
            case EditorStyleAction.Create:
                await CreateTextStyleAsync();
                break;
            case EditorStyleAction.Apply when e.Item is EditorTextStyleItem style:
                ApplyTextStyle(style);
                break;
            case EditorStyleAction.Edit when e.Item is EditorTextStyleItem style:
                await EditTextStyleAsync(style);
                break;
        }
    }

    private async Task HandleColorStyleRequestAsync(EditorStyleRequestedEventArgs e)
    {
        switch (e.Action)
        {
            case EditorStyleAction.Create:
                await CreateColorStyleAsync();
                break;
            case EditorStyleAction.Apply when e.Item is ColorSwatchItem style:
                ApplyColorStyle(style);
                break;
            case EditorStyleAction.Edit when e.Item is ColorSwatchItem style:
                await EditColorStyleAsync(style);
                break;
        }
    }

    private async Task HandleEffectStyleRequestAsync(EditorStyleRequestedEventArgs e)
    {
        switch (e.Action)
        {
            case EditorStyleAction.Create:
                await CreateEffectStyleAsync();
                break;
            case EditorStyleAction.Apply when e.Item is EditorEffectStyleItem style:
                ApplyEffectStyle(style);
                break;
            case EditorStyleAction.Edit when e.Item is EditorEffectStyleItem style:
                await EditEffectStyleAsync(style);
                break;
        }
    }

    private async Task HandleLayoutGuideStyleRequestAsync(EditorStyleRequestedEventArgs e)
    {
        switch (e.Action)
        {
            case EditorStyleAction.Create:
                await CreateLayoutGuideStyleAsync();
                break;
            case EditorStyleAction.Apply when e.Item is EditorLayoutGuideStyleItem style:
                ApplyLayoutGuideStyle(style);
                break;
            case EditorStyleAction.Edit when e.Item is EditorLayoutGuideStyleItem style:
                await EditLayoutGuideStyleAsync(style);
                break;
        }
    }

    private void RefreshStyleCatalogs()
    {
        TextStyles.Clear();
        ColorStyles.Clear();
        EffectStyles.Clear();
        LayoutGuideStyles.Clear();

        if (!_libraryCatalog.TryGetValue("current-file", out var definition))
        {
            return;
        }

        foreach (var style in definition.TextStyles)
        {
            TextStyles.Add(style.Clone());
        }

        foreach (var style in definition.Swatches)
        {
            ColorStyles.Add(NormalizeLibraryPaintStyle(definition, style));
        }

        foreach (var style in definition.EffectStyles)
        {
            EffectStyles.Add(style.Clone());
        }

        foreach (var style in definition.LayoutGuideStyles)
        {
            LayoutGuideStyles.Add(style.Clone());
        }
    }

    private void EnsureCurrentFileStyleCatalog()
    {
        if (GetCurrentFileLibraryDefinition() is not { } definition)
        {
            return;
        }

        MergePublishedPaintStyles(definition);
        MergePublishedTextStyles(definition);
        MergePublishedEffectStyles(definition);
        MergePublishedLayoutGuideStyles(definition);
        EnsureDefaultCurrentFileStyles(definition);
    }

    private void EnsureDefaultCurrentFileStyles(SampleLibraryDefinition definition)
    {
        if (definition.TextStyles.Count == 0)
        {
            definition.TextStyles.Add(CreateDefaultTextStyle());
        }

        if (definition.EffectStyles.Count == 0)
        {
            definition.EffectStyles.Add(CreateDefaultEffectStyle());
        }

        if (definition.LayoutGuideStyles.Count == 0)
        {
            definition.LayoutGuideStyles.Add(CreateDefaultLayoutGuideStyle());
        }

        definition.Item.ColorCount = definition.Swatches.Count;
    }

    private SampleLibraryDefinition? GetCurrentFileLibraryDefinition()
    {
        return _libraryCatalog.TryGetValue("current-file", out var definition)
            ? definition
            : null;
    }

    private void MergePublishedPaintStyles(SampleLibraryDefinition definition)
    {
        foreach (var discovered in BuildPublishedFileSwatches())
        {
            var index = definition.Swatches.FindIndex(item => ArePaintStylesEquivalent(item, discovered));
            if (index >= 0)
            {
                var existing = definition.Swatches[index];
                definition.Swatches[index] = new ColorSwatchItem(
                    discovered.Color,
                    existing.Label,
                    discovered.Target,
                    string.IsNullOrWhiteSpace(existing.StyleId) ? discovered.StyleId : existing.StyleId,
                    definition.Item.Id,
                    definition.Item.Name,
                    string.IsNullOrWhiteSpace(existing.SectionName) ? discovered.SectionName : existing.SectionName,
                    string.IsNullOrWhiteSpace(existing.SearchKeywords) ? discovered.SearchKeywords : existing.SearchKeywords,
                    discovered.StrokeWidth,
                    existing.Description);
            }
            else
            {
                definition.Swatches.Add(NormalizeLibraryPaintStyle(definition, discovered));
            }
        }

        definition.Item.ColorCount = definition.Swatches.Count;
    }

    private void MergePublishedTextStyles(SampleLibraryDefinition definition)
    {
        foreach (var discovered in BuildPublishedFileTextStyles())
        {
            var index = definition.TextStyles.FindIndex(item => AreTextStylesEquivalent(item, discovered));
            if (index >= 0)
            {
                var existing = definition.TextStyles[index];
                definition.TextStyles[index] = new EditorTextStyleItem(
                    existing.StyleId,
                    existing.Name,
                    existing.Description,
                    discovered.FontFamily,
                    discovered.FontWeight,
                    discovered.FontSize,
                    discovered.LetterSpacing,
                    discovered.LineHeightText);
            }
            else
            {
                definition.TextStyles.Add(discovered);
            }
        }
    }

    private void MergePublishedEffectStyles(SampleLibraryDefinition definition)
    {
        foreach (var discovered in BuildPublishedFileEffectStyles())
        {
            var index = definition.EffectStyles.FindIndex(item => AreEffectStylesEquivalent(item, discovered));
            if (index >= 0)
            {
                var existing = definition.EffectStyles[index];
                definition.EffectStyles[index] = new EditorEffectStyleItem(
                    existing.StyleId,
                    existing.Name,
                    existing.Description,
                    discovered.Effects);
            }
            else
            {
                definition.EffectStyles.Add(discovered);
            }
        }
    }

    private void MergePublishedLayoutGuideStyles(SampleLibraryDefinition definition)
    {
        foreach (var discovered in BuildPublishedFileLayoutGuideStyles())
        {
            var index = definition.LayoutGuideStyles.FindIndex(item => AreLayoutGuideStylesEquivalent(item, discovered));
            if (index >= 0)
            {
                var existing = definition.LayoutGuideStyles[index];
                definition.LayoutGuideStyles[index] = new EditorLayoutGuideStyleItem(
                    existing.StyleId,
                    existing.Name,
                    existing.Description,
                    discovered.GridSize,
                    discovered.IsGridVisible,
                    discovered.IsSnapEnabled);
            }
            else
            {
                definition.LayoutGuideStyles.Add(discovered);
            }
        }
    }

    private List<EditorTextStyleItem> BuildPublishedFileTextStyles()
    {
        var styles = new List<EditorTextStyleItem>();
        foreach (var text in _pageStates.SelectMany(static state => state.Document.Descendants().OfType<SvgTextBase>()))
        {
            var discovered = CreateTextStyleFromElement(text, CreateDerivedStyleId("text-style", $"{text.FontFamily}-{text.FontSize.Value:0.##}-{text.FontWeight}"));
            if (styles.All(existing => !AreTextStylesEquivalent(existing, discovered)))
            {
                styles.Add(discovered);
            }
        }

        return styles;
    }

    private List<EditorEffectStyleItem> BuildPublishedFileEffectStyles()
    {
        var styles = new List<EditorEffectStyleItem>();
        foreach (var element in _pageStates.SelectMany(static state => state.Document.Descendants().OfType<SvgVisualElement>()))
        {
            var effects = LoadEffectItems(element);
            if (effects.Count == 0)
            {
                continue;
            }

            var discovered = CreateEffectStyleFromItems(
                effects,
                CreateDerivedStyleId("effect-style", SerializeEffects(effects)),
                effects.Count == 1 ? effects[0].DisplayName : "Effect stack");
            if (styles.All(existing => !AreEffectStylesEquivalent(existing, discovered)))
            {
                styles.Add(discovered);
            }
        }

        return styles;
    }

    private List<EditorLayoutGuideStyleItem> BuildPublishedFileLayoutGuideStyles()
    {
        var styles = new List<EditorLayoutGuideStyleItem>();
        foreach (var state in _pageStates)
        {
            var discovered = CreateLayoutGuideStyle(
                CreateDerivedStyleId("layout-guide-style", $"{state.GridSize:0.##}-{state.IsGridVisible}-{state.IsSnapEnabled}"),
                "Grid",
                string.Empty,
                state.GridSize,
                state.IsGridVisible,
                state.IsSnapEnabled);
            if (styles.All(existing => !AreLayoutGuideStylesEquivalent(existing, discovered)))
            {
                styles.Add(discovered);
            }
        }

        return styles;
    }

    private static bool ArePaintStylesEquivalent(ColorSwatchItem left, ColorSwatchItem right)
    {
        return left.Color.Equals(right.Color)
               && left.Target == right.Target
               && Math.Abs(left.StrokeWidth - right.StrokeWidth) < 0.001;
    }

    private static bool AreTextStylesEquivalent(EditorTextStyleItem left, EditorTextStyleItem right)
    {
        return string.Equals(left.FontFamily, right.FontFamily, StringComparison.OrdinalIgnoreCase)
               && left.FontWeight == right.FontWeight
               && Math.Abs(left.FontSize - right.FontSize) < 0.001
               && Math.Abs(left.LetterSpacing - right.LetterSpacing) < 0.001
               && string.Equals(left.LineHeightText, right.LineHeightText, StringComparison.OrdinalIgnoreCase);
    }

    private static bool AreEffectStylesEquivalent(EditorEffectStyleItem left, EditorEffectStyleItem right)
    {
        if (left.Effects.Count != right.Effects.Count)
        {
            return false;
        }

        for (var index = 0; index < left.Effects.Count; index++)
        {
            if (!AreEffectItemsEquivalent(left.Effects[index], right.Effects[index]))
            {
                return false;
            }
        }

        return true;
    }

    private static bool AreEffectItemsEquivalent(EditorEffectItem left, EditorEffectItem right)
    {
        return left.Kind == right.Kind
               && left.IsEnabled == right.IsEnabled
               && Math.Abs(left.OffsetX - right.OffsetX) < 0.001
               && Math.Abs(left.OffsetY - right.OffsetY) < 0.001
               && Math.Abs(left.Blur - right.Blur) < 0.001
               && Math.Abs(left.Spread - right.Spread) < 0.001
               && Math.Abs(left.Scale - right.Scale) < 0.001
               && Math.Abs(left.Amount - right.Amount) < 0.001
               && Math.Abs(left.Distortion - right.Distortion) < 0.001
               && Math.Abs(left.Saturation - right.Saturation) < 0.001
               && left.Color.Equals(right.Color);
    }

    private static bool AreLayoutGuideStylesEquivalent(EditorLayoutGuideStyleItem left, EditorLayoutGuideStyleItem right)
    {
        return Math.Abs(left.GridSize - right.GridSize) < 0.001
               && left.IsGridVisible == right.IsGridVisible
               && left.IsSnapEnabled == right.IsSnapEnabled;
    }

    private async Task CreateTextStyleAsync()
    {
        var seed = GetInitialTextStyle();
        var (result, style) = await ShowTextStyleDialogAsync("Create new text style", seed, isEditing: false);
        if (result != ContentDialogResult.Primary || style is null)
        {
            return;
        }

        SaveTextStyle(style, isNewStyle: true);
    }

    private async Task EditTextStyleAsync(EditorTextStyleItem style)
    {
        var (result, editedStyle) = await ShowTextStyleDialogAsync("Edit text style", style.Clone(), isEditing: true);
        if (result == ContentDialogResult.Primary && editedStyle is not null)
        {
            SaveTextStyle(editedStyle, isNewStyle: false);
            ReapplyLibraryTextStylesAcrossPages("current-file");
            RefreshDocumentVisual(rebuildOutline: false, reloadProperties: true);
            CanvasStatus = $"Updated {editedStyle.Name}.";
            return;
        }

        if (result == ContentDialogResult.Secondary)
        {
            DeleteTextStyle(style);
        }
    }

    private void ApplyTextStyle(EditorTextStyleItem style)
    {
        var targets = _selectedElements.OfType<SvgTextBase>().ToList();
        if (targets.Count == 0)
        {
            CanvasStatus = "Select one or more text layers to apply a text style.";
            return;
        }

        foreach (var target in targets)
        {
            ApplyTextStyleToElement(target, style, "current-file");
        }

        RefreshDocumentVisual(rebuildOutline: false, reloadProperties: true);
        CanvasStatus = $"Applied {style.Name} to {targets.Count} text layer{(targets.Count == 1 ? string.Empty : "s")}.";
    }

    private void SaveTextStyle(EditorTextStyleItem style, bool isNewStyle)
    {
        if (GetCurrentFileLibraryDefinition() is not { } definition)
        {
            return;
        }

        UpsertTextStyle(definition, style);
        RefreshLibrariesState();
        CanvasStatus = isNewStyle
            ? $"Saved {style.Name} to {DocumentTitle}."
            : CanvasStatus;
    }

    private void DeleteTextStyle(EditorTextStyleItem style)
    {
        if (GetCurrentFileLibraryDefinition() is not { } definition)
        {
            return;
        }

        definition.TextStyles.RemoveAll(item => string.Equals(item.StyleId, style.StyleId, StringComparison.Ordinal));
        ClearTextStyleLinksAcrossPages(style.StyleId, "current-file");
        RefreshLibrariesState();
        RefreshDocumentVisual(rebuildOutline: false, reloadProperties: true);
        CanvasStatus = $"Deleted {style.Name}.";
    }

    private async Task CreateColorStyleAsync()
    {
        var seed = GetInitialColorStyle();
        var (result, style) = await ShowColorStyleDialogAsync("Create new color style", seed, isEditing: false);
        if (result != ContentDialogResult.Primary || style is null)
        {
            return;
        }

        SaveColorStyle(style, isNewStyle: true);
    }

    private async Task EditColorStyleAsync(ColorSwatchItem style)
    {
        var (result, editedStyle) = await ShowColorStyleDialogAsync("Edit color style", style, isEditing: true);
        if (result == ContentDialogResult.Primary && editedStyle is not null)
        {
            SaveColorStyle(editedStyle, isNewStyle: false);
            ReapplyLibraryPaintStylesAcrossPages("current-file");
            RefreshDocumentVisual(rebuildOutline: false, reloadProperties: true);
            CanvasStatus = $"Updated {editedStyle.Label}.";
            return;
        }

        if (result == ContentDialogResult.Secondary)
        {
            DeleteColorStyle(style);
        }
    }

    private void ApplyColorStyle(ColorSwatchItem style)
    {
        if (_selectedElements.Count == 0)
        {
            CanvasStatus = "Select one or more layers to apply a color style.";
            return;
        }

        var target = style.Target == EditorPaintTarget.Stroke ? EditorPaintTarget.Stroke : EditorPaintTarget.Fill;
        ApplyPaintStyleToSelection(new PaintStyleRequestedEventArgs(style, target));
    }

    private void SaveColorStyle(ColorSwatchItem style, bool isNewStyle)
    {
        if (GetCurrentFileLibraryDefinition() is not { } definition)
        {
            return;
        }

        UpsertPaintStyle(definition, style);
        RefreshLibrariesState();
        CanvasStatus = isNewStyle
            ? $"Saved {style.Label} to {DocumentTitle}."
            : CanvasStatus;
    }

    private void DeleteColorStyle(ColorSwatchItem style)
    {
        if (GetCurrentFileLibraryDefinition() is not { } definition)
        {
            return;
        }

        definition.Swatches.RemoveAll(item => string.Equals(item.StyleId, style.StyleId, StringComparison.Ordinal));
        ClearPaintStyleLinksAcrossPages(style.StyleId, "current-file", style.Target);
        RefreshLibrariesState();
        RefreshDocumentVisual(rebuildOutline: false, reloadProperties: true);
        CanvasStatus = $"Deleted {style.Label}.";
    }

    private async Task CreateEffectStyleAsync()
    {
        var seed = GetInitialEffectStyle();
        var (result, style) = await ShowEffectStyleDialogAsync("Create new effect style", seed, isEditing: false);
        if (result != ContentDialogResult.Primary || style is null)
        {
            return;
        }

        SaveEffectStyle(style, isNewStyle: true);
    }

    private async Task EditEffectStyleAsync(EditorEffectStyleItem style)
    {
        var (result, editedStyle) = await ShowEffectStyleDialogAsync("Edit effect style", style.Clone(), isEditing: true);
        if (result == ContentDialogResult.Primary && editedStyle is not null)
        {
            SaveEffectStyle(editedStyle, isNewStyle: false);
            ReapplyLibraryEffectStylesAcrossPages("current-file");
            RefreshDocumentVisual(rebuildOutline: false, reloadProperties: true);
            RefreshEffectsInspectorState();
            CanvasStatus = $"Updated {editedStyle.Name}.";
            return;
        }

        if (result == ContentDialogResult.Secondary)
        {
            DeleteEffectStyle(style);
        }
    }

    private void ApplyEffectStyle(EditorEffectStyleItem style)
    {
        if (_selectedElements.Count == 0 || _document is null)
        {
            CanvasStatus = "Select one or more layers to apply an effect style.";
            return;
        }

        foreach (var element in _selectedElements)
        {
            ApplyEffectStyleToElement(element, style, "current-file");
        }

        RefreshDocumentVisual(rebuildOutline: false, reloadProperties: true);
        RefreshEffectsInspectorState();
        CanvasStatus = $"Applied {style.Name} to {_selectedElements.Count} layer{(_selectedElements.Count == 1 ? string.Empty : "s")}.";
    }

    private void SaveEffectStyle(EditorEffectStyleItem style, bool isNewStyle)
    {
        if (GetCurrentFileLibraryDefinition() is not { } definition)
        {
            return;
        }

        UpsertEffectStyle(definition, style);
        RefreshLibrariesState();
        CanvasStatus = isNewStyle
            ? $"Saved {style.Name} to {DocumentTitle}."
            : CanvasStatus;
    }

    private void DeleteEffectStyle(EditorEffectStyleItem style)
    {
        if (GetCurrentFileLibraryDefinition() is not { } definition)
        {
            return;
        }

        definition.EffectStyles.RemoveAll(item => string.Equals(item.StyleId, style.StyleId, StringComparison.Ordinal));
        ClearEffectStyleLinksAcrossPages(style.StyleId, "current-file");
        RefreshLibrariesState();
        RefreshDocumentVisual(rebuildOutline: false, reloadProperties: true);
        RefreshEffectsInspectorState();
        CanvasStatus = $"Deleted {style.Name}.";
    }

    private async Task CreateLayoutGuideStyleAsync()
    {
        var seed = GetInitialLayoutGuideStyle();
        var (result, style) = await ShowLayoutGuideStyleDialogAsync("Create new layout guide style", seed, isEditing: false);
        if (result != ContentDialogResult.Primary || style is null)
        {
            return;
        }

        SaveLayoutGuideStyle(style, isNewStyle: true);
    }

    private async Task EditLayoutGuideStyleAsync(EditorLayoutGuideStyleItem style)
    {
        var (result, editedStyle) = await ShowLayoutGuideStyleDialogAsync("Edit layout guide style", style.Clone(), isEditing: true);
        if (result == ContentDialogResult.Primary && editedStyle is not null)
        {
            SaveLayoutGuideStyle(editedStyle, isNewStyle: false);
            ReapplyLayoutGuideStylesAcrossPages("current-file");
            ApplyViewportOptions();
            CanvasStatus = $"Updated {editedStyle.Name}.";
            return;
        }

        if (result == ContentDialogResult.Secondary)
        {
            DeleteLayoutGuideStyle(style);
        }
    }

    private void ApplyLayoutGuideStyle(EditorLayoutGuideStyleItem style)
    {
        if (_activePage is null)
        {
            CanvasStatus = "Open a page before applying a layout guide style.";
            return;
        }

        ApplyLayoutGuideStyleToPage(_activePage, style, "current-file", updateLiveState: true);
        ApplyViewportOptions();
        CanvasStatus = $"Applied {style.Name} to {_activePage.Page.Title}.";
    }

    private void SaveLayoutGuideStyle(EditorLayoutGuideStyleItem style, bool isNewStyle)
    {
        if (GetCurrentFileLibraryDefinition() is not { } definition)
        {
            return;
        }

        UpsertLayoutGuideStyle(definition, style);
        RefreshLibrariesState();
        CanvasStatus = isNewStyle
            ? $"Saved {style.Name} to {DocumentTitle}."
            : CanvasStatus;
    }

    private void DeleteLayoutGuideStyle(EditorLayoutGuideStyleItem style)
    {
        if (GetCurrentFileLibraryDefinition() is not { } definition)
        {
            return;
        }

        definition.LayoutGuideStyles.RemoveAll(item => string.Equals(item.StyleId, style.StyleId, StringComparison.Ordinal));
        ClearLayoutGuideStyleLinksAcrossPages(style.StyleId, "current-file");
        RefreshLibrariesState();
        ApplyViewportOptions();
        CanvasStatus = $"Deleted {style.Name}.";
    }

    private EditorTextStyleItem GetInitialTextStyle()
    {
        if (_selectedElement is SvgTextBase text)
        {
            return CreateTextStyleFromElement(text, CreateUniqueStyleId("text-style", text.ID ?? "text"));
        }

        return CreateDefaultTextStyle();
    }

    private ColorSwatchItem GetInitialColorStyle()
    {
        if (_selectedElement is not null)
        {
            if (_selectedElement.Fill is SvgColourServer fill && !ReferenceEquals(fill, SvgPaintServer.None))
            {
                var color = Color.FromArgb(
                    (byte)Math.Clamp((int)Math.Round(_selectedElement.FillOpacity * 255f), 0, 255),
                    fill.Colour.R,
                    fill.Colour.G,
                    fill.Colour.B);
                return new ColorSwatchItem(
                    color,
                    "Color style",
                    EditorPaintTarget.Fill,
                    CreateUniqueStyleId("color-style", "fill"),
                    "current-file",
                    DocumentTitle,
                    "Fill styles",
                    "current file color style");
            }

            if (_selectedElement.Stroke is SvgColourServer stroke && !ReferenceEquals(stroke, SvgPaintServer.None))
            {
                var color = Color.FromArgb(
                    (byte)Math.Clamp((int)Math.Round(_selectedElement.StrokeOpacity * 255f), 0, 255),
                    stroke.Colour.R,
                    stroke.Colour.G,
                    stroke.Colour.B);
                return new ColorSwatchItem(
                    color,
                    "Stroke style",
                    EditorPaintTarget.Stroke,
                    CreateUniqueStyleId("color-style", "stroke"),
                    "current-file",
                    DocumentTitle,
                    "Stroke styles",
                    "current file stroke style",
                    _selectedElement.StrokeWidth.Value);
            }
        }

        return new ColorSwatchItem(
            Color.FromArgb(255, 217, 217, 217),
            "Neutral",
            EditorPaintTarget.Fill,
            CreateUniqueStyleId("color-style", "neutral"),
            "current-file",
            DocumentTitle,
            "Fill styles",
            "current file neutral color style");
    }

    private EditorEffectStyleItem GetInitialEffectStyle()
    {
        if (EffectItems.Count > 0)
        {
            return CreateEffectStyleFromItems(EffectItems, CreateUniqueStyleId("effect-style", "selection"), "Effect style");
        }

        return CreateDefaultEffectStyle();
    }

    private EditorLayoutGuideStyleItem GetInitialLayoutGuideStyle()
    {
        return CreateLayoutGuideStyle(
            CreateUniqueStyleId("layout-guide-style", _activePage?.Page.Title ?? "grid"),
            "Grid",
            string.Empty,
            _gridSize,
            _isGridVisible,
            _isSnapEnabled);
    }

    private EditorTextStyleItem CreateDefaultTextStyle()
    {
        return new EditorTextStyleItem(
            CreateUniqueStyleId("text-style", "text"),
            "Text",
            string.Empty,
            _toolService.CurrentFontFamily,
            _toolService.CurrentFontWeight,
            12.0,
            _toolService.CurrentLetterSpacing,
            "Auto");
    }

    private EditorEffectStyleItem CreateDefaultEffectStyle()
    {
        return new EditorEffectStyleItem(
            CreateUniqueStyleId("effect-style", "drop-shadow"),
            "Drop Shadow",
            string.Empty,
            [EditorEffectItem.CreateDefault(EditorEffectKind.DropShadow)]);
    }

    private EditorLayoutGuideStyleItem CreateDefaultLayoutGuideStyle()
    {
        return CreateLayoutGuideStyle(
            CreateUniqueStyleId("layout-guide-style", "grid"),
            "Grid",
            string.Empty,
            DefaultGridSize,
            true,
            false);
    }

    private EditorTextStyleItem CreateTextStyleFromElement(SvgTextBase text, string styleId)
    {
        return new EditorTextStyleItem(
            styleId,
            "Text",
            string.Empty,
            string.IsNullOrWhiteSpace(text.FontFamily) ? "Open Sans" : text.FontFamily,
            text.FontWeight,
            text.FontSize.Value <= 0 ? 12.0 : text.FontSize.Value,
            text.LetterSpacing.Value,
            ExtractLineHeightText(text));
    }

    private EditorEffectStyleItem CreateEffectStyleFromItems(IEnumerable<EditorEffectItem> effects, string styleId, string name)
    {
        return new EditorEffectStyleItem(styleId, name, string.Empty, effects);
    }

    private static EditorLayoutGuideStyleItem CreateLayoutGuideStyle(
        string styleId,
        string name,
        string description,
        double gridSize,
        bool isGridVisible,
        bool isSnapEnabled)
    {
        return new EditorLayoutGuideStyleItem(styleId, name, description, gridSize, isGridVisible, isSnapEnabled);
    }

    private static string ExtractLineHeightText(SvgTextBase text)
    {
        return text.CustomAttributes.TryGetValue(TextLineHeightAttribute, out var lineHeight)
               && !string.IsNullOrWhiteSpace(lineHeight)
            ? lineHeight
            : "Auto";
    }

    private string CreateUniqueStyleId(string prefix, string name)
    {
        return $"{NormalizeToken(prefix)}-{NormalizeToken(name)}-{++_generatedId}";
    }

    private static string CreateDerivedStyleId(string prefix, string fingerprint)
    {
        return $"{NormalizeToken(prefix)}-{NormalizeToken(fingerprint)}";
    }

    private static void UpsertTextStyle(SampleLibraryDefinition definition, EditorTextStyleItem style)
    {
        var index = definition.TextStyles.FindIndex(item => string.Equals(item.StyleId, style.StyleId, StringComparison.Ordinal));
        if (index >= 0)
        {
            definition.TextStyles[index] = style;
        }
        else
        {
            definition.TextStyles.Insert(0, style);
        }
    }

    private static void UpsertEffectStyle(SampleLibraryDefinition definition, EditorEffectStyleItem style)
    {
        var index = definition.EffectStyles.FindIndex(item => string.Equals(item.StyleId, style.StyleId, StringComparison.Ordinal));
        if (index >= 0)
        {
            definition.EffectStyles[index] = style;
        }
        else
        {
            definition.EffectStyles.Insert(0, style);
        }
    }

    private static void UpsertLayoutGuideStyle(SampleLibraryDefinition definition, EditorLayoutGuideStyleItem style)
    {
        var index = definition.LayoutGuideStyles.FindIndex(item => string.Equals(item.StyleId, style.StyleId, StringComparison.Ordinal));
        if (index >= 0)
        {
            definition.LayoutGuideStyles[index] = style;
        }
        else
        {
            definition.LayoutGuideStyles.Insert(0, style);
        }
    }

    private static void ApplyTextStyleToElement(SvgTextBase element, EditorTextStyleItem style, string libraryId)
    {
        element.FontFamily = style.FontFamily;
        element.FontWeight = style.FontWeight;
        element.FontSize = new SvgUnit((float)style.FontSize);
        element.LetterSpacing = new SvgUnit((float)style.LetterSpacing);

        if (string.Equals(style.LineHeightText, "Auto", StringComparison.OrdinalIgnoreCase))
        {
            element.CustomAttributes.Remove(TextLineHeightAttribute);
        }
        else
        {
            element.CustomAttributes[TextLineHeightAttribute] = style.LineHeightText;
        }

        SetTextStyleLink(element, style.StyleId, libraryId);
    }

    private void ApplyEffectStyleToElement(SvgVisualElement element, EditorEffectStyleItem style, string libraryId)
    {
        if (_document is null)
        {
            return;
        }

        var definitions = EnsureDefinitions(_document);
        var filter = EnsureEffectFilter(element, definitions);
        var effects = style.Effects.Select(EditorEffectStyleItem.CloneEffect).ToList();
        BuildEffectFilter(element, filter, effects);
        element.Filter = new Uri($"#{filter.ID}", UriKind.Relative);
        SetEffectStyleLink(element, style.StyleId, libraryId);
    }

    private static void SetTextStyleLink(SvgTextBase element, string styleId, string libraryId)
    {
        if (string.IsNullOrWhiteSpace(styleId) || string.IsNullOrWhiteSpace(libraryId))
        {
            ClearTextStyleLink(element);
            return;
        }

        element.CustomAttributes[LibraryTextStyleIdAttribute] = styleId;
        element.CustomAttributes[LibraryTextStyleLibraryIdAttribute] = libraryId;
    }

    private static void ClearTextStyleLink(SvgTextBase element)
    {
        element.CustomAttributes.Remove(LibraryTextStyleIdAttribute);
        element.CustomAttributes.Remove(LibraryTextStyleLibraryIdAttribute);
    }

    private static void SetEffectStyleLink(SvgVisualElement element, string styleId, string libraryId)
    {
        if (string.IsNullOrWhiteSpace(styleId) || string.IsNullOrWhiteSpace(libraryId))
        {
            ClearEffectStyleLink(element);
            return;
        }

        element.CustomAttributes[LibraryEffectStyleIdAttribute] = styleId;
        element.CustomAttributes[LibraryEffectStyleLibraryIdAttribute] = libraryId;
    }

    private static void ClearEffectStyleLink(SvgVisualElement element)
    {
        element.CustomAttributes.Remove(LibraryEffectStyleIdAttribute);
        element.CustomAttributes.Remove(LibraryEffectStyleLibraryIdAttribute);
    }

    private bool TryResolveLinkedTextStyle(SvgTextBase element, out EditorTextStyleItem style)
    {
        style = null!;
        if (!element.CustomAttributes.TryGetValue(LibraryTextStyleIdAttribute, out var styleId)
            || string.IsNullOrWhiteSpace(styleId)
            || !element.CustomAttributes.TryGetValue(LibraryTextStyleLibraryIdAttribute, out var libraryId)
            || string.IsNullOrWhiteSpace(libraryId)
            || !_libraryCatalog.TryGetValue(libraryId, out var definition))
        {
            return false;
        }

        style = definition.TextStyles.FirstOrDefault(item => string.Equals(item.StyleId, styleId, StringComparison.Ordinal))!;
        return style is not null;
    }

    private bool TryResolveLinkedEffectStyle(SvgVisualElement element, out EditorEffectStyleItem style)
    {
        style = null!;
        if (!element.CustomAttributes.TryGetValue(LibraryEffectStyleIdAttribute, out var styleId)
            || string.IsNullOrWhiteSpace(styleId)
            || !element.CustomAttributes.TryGetValue(LibraryEffectStyleLibraryIdAttribute, out var libraryId)
            || string.IsNullOrWhiteSpace(libraryId)
            || !_libraryCatalog.TryGetValue(libraryId, out var definition))
        {
            return false;
        }

        style = definition.EffectStyles.FirstOrDefault(item => string.Equals(item.StyleId, styleId, StringComparison.Ordinal))!;
        return style is not null;
    }

    private void ReapplyLibraryTextStylesAcrossPages(string? libraryId = null)
    {
        foreach (var state in _pageStates)
        {
            foreach (var text in state.Document.Descendants().OfType<SvgTextBase>())
            {
                if (TryResolveLinkedTextStyle(text, out var style)
                    && (libraryId is null
                        || text.CustomAttributes.TryGetValue(LibraryTextStyleLibraryIdAttribute, out var linkedLibraryId)
                        && string.Equals(linkedLibraryId, libraryId, StringComparison.Ordinal)))
                {
                    ApplyTextStyleToElement(text, style, text.CustomAttributes[LibraryTextStyleLibraryIdAttribute]);
                }
            }
        }
    }

    private void ReapplyLibraryEffectStylesAcrossPages(string? libraryId = null)
    {
        foreach (var state in _pageStates)
        {
            var previousDocument = _document;
            _document = state.Document;
            try
            {
                foreach (var element in state.Document.Descendants().OfType<SvgVisualElement>())
                {
                    if (TryResolveLinkedEffectStyle(element, out var style)
                        && (libraryId is null
                            || element.CustomAttributes.TryGetValue(LibraryEffectStyleLibraryIdAttribute, out var linkedLibraryId)
                            && string.Equals(linkedLibraryId, libraryId, StringComparison.Ordinal)))
                    {
                        ApplyEffectStyleToElement(element, style, element.CustomAttributes[LibraryEffectStyleLibraryIdAttribute]);
                    }
                }
            }
            finally
            {
                _document = previousDocument;
            }
        }
    }

    private static bool ElementUsesLibraryTextStyle(SvgVisualElement element, string libraryId)
    {
        return element is SvgTextBase
               && element.CustomAttributes.TryGetValue(LibraryTextStyleLibraryIdAttribute, out var linkedLibraryId)
               && string.Equals(linkedLibraryId, libraryId, StringComparison.Ordinal);
    }

    private static bool ElementUsesLibraryEffectStyle(SvgVisualElement element, string libraryId)
    {
        return element.CustomAttributes.TryGetValue(LibraryEffectStyleLibraryIdAttribute, out var linkedLibraryId)
               && string.Equals(linkedLibraryId, libraryId, StringComparison.Ordinal);
    }

    private static bool PageUsesLibraryLayoutGuideStyle(EditorPageState state, string libraryId)
    {
        return !string.IsNullOrWhiteSpace(state.LayoutGuideStyleId)
               && string.Equals(state.LayoutGuideStyleLibraryId, libraryId, StringComparison.Ordinal);
    }

    private void ClearTextStyleLinksAcrossPages(string styleId, string libraryId)
    {
        foreach (var text in _pageStates.SelectMany(static state => state.Document.Descendants().OfType<SvgTextBase>()))
        {
            if (text.CustomAttributes.TryGetValue(LibraryTextStyleIdAttribute, out var linkedStyleId)
                && string.Equals(linkedStyleId, styleId, StringComparison.Ordinal)
                && text.CustomAttributes.TryGetValue(LibraryTextStyleLibraryIdAttribute, out var linkedLibraryId)
                && string.Equals(linkedLibraryId, libraryId, StringComparison.Ordinal))
            {
                ClearTextStyleLink(text);
            }
        }
    }

    private void ClearEffectStyleLinksAcrossPages(string styleId, string libraryId)
    {
        foreach (var element in _pageStates.SelectMany(static state => state.Document.Descendants().OfType<SvgVisualElement>()))
        {
            if (element.CustomAttributes.TryGetValue(LibraryEffectStyleIdAttribute, out var linkedStyleId)
                && string.Equals(linkedStyleId, styleId, StringComparison.Ordinal)
                && element.CustomAttributes.TryGetValue(LibraryEffectStyleLibraryIdAttribute, out var linkedLibraryId)
                && string.Equals(linkedLibraryId, libraryId, StringComparison.Ordinal))
            {
                ClearEffectStyleLink(element);
            }
        }
    }

    private void ClearPaintStyleLinksAcrossPages(string styleId, string libraryId, EditorPaintTarget target)
    {
        foreach (var element in _pageStates.SelectMany(static state => state.Document.Descendants().OfType<SvgVisualElement>()))
        {
            var idAttribute = target == EditorPaintTarget.Stroke ? LibraryStrokeStyleIdAttribute : LibraryFillStyleIdAttribute;
            var libraryAttribute = target == EditorPaintTarget.Stroke ? LibraryStrokeStyleLibraryIdAttribute : LibraryFillStyleLibraryIdAttribute;
            if (element.CustomAttributes.TryGetValue(idAttribute, out var linkedStyleId)
                && string.Equals(linkedStyleId, styleId, StringComparison.Ordinal)
                && element.CustomAttributes.TryGetValue(libraryAttribute, out var linkedLibraryId)
                && string.Equals(linkedLibraryId, libraryId, StringComparison.Ordinal))
            {
                ClearPaintStyleLink(element, target);
            }
        }
    }

    private void ApplyLayoutGuideStyleToPage(EditorPageState state, EditorLayoutGuideStyleItem style, string libraryId, bool updateLiveState)
    {
        state.GridSize = style.GridSize;
        state.IsGridVisible = style.IsGridVisible;
        state.IsSnapEnabled = style.IsSnapEnabled;
        state.LayoutGuideStyleId = style.StyleId;
        state.LayoutGuideStyleLibraryId = libraryId;

        if (!updateLiveState || !ReferenceEquals(state, _activePage))
        {
            return;
        }

        _gridSize = style.GridSize;
        IsGridVisible = style.IsGridVisible;
        IsSnapEnabled = style.IsSnapEnabled;
    }

    private void ReapplyLayoutGuideStylesAcrossPages(string? libraryId = null)
    {
        foreach (var state in _pageStates)
        {
            if (string.IsNullOrWhiteSpace(state.LayoutGuideStyleId)
                || string.IsNullOrWhiteSpace(state.LayoutGuideStyleLibraryId)
                || libraryId is not null && !string.Equals(state.LayoutGuideStyleLibraryId, libraryId, StringComparison.Ordinal)
                || !_libraryCatalog.TryGetValue(state.LayoutGuideStyleLibraryId, out var definition))
            {
                continue;
            }

            var style = definition.LayoutGuideStyles.FirstOrDefault(item => string.Equals(item.StyleId, state.LayoutGuideStyleId, StringComparison.Ordinal));
            if (style is null)
            {
                continue;
            }

            ApplyLayoutGuideStyleToPage(state, style, state.LayoutGuideStyleLibraryId, updateLiveState: ReferenceEquals(state, _activePage));
        }
    }

    private void ClearLayoutGuideStyleLinksAcrossPages(string styleId, string libraryId)
    {
        foreach (var state in _pageStates.Where(state =>
                     string.Equals(state.LayoutGuideStyleId, styleId, StringComparison.Ordinal)
                     && string.Equals(state.LayoutGuideStyleLibraryId, libraryId, StringComparison.Ordinal)))
        {
            state.LayoutGuideStyleId = null;
            state.LayoutGuideStyleLibraryId = null;
        }
    }

    private async Task<(ContentDialogResult Result, EditorTextStyleItem? Style)> ShowTextStyleDialogAsync(string title, EditorTextStyleItem draft, bool isEditing)
    {
        if (XamlRoot is null)
        {
            return default;
        }

        var previewText = new TextBlock
        {
            Text = "Rag 123",
            Style = (Style)Resources["ShellTitleStyle"],
            FontSize = draft.FontSize,
            FontFamily = new FontFamily(draft.FontFamily),
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };

        var preview = new Border
        {
            Background = (Brush)Resources["PickerSectionBrush"],
            BorderBrush = (Brush)Resources["SurfaceStrokeBrush"],
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(16),
            Height = 180,
            Child = previewText
        };

        var nameBox = new TextBox { PlaceholderText = "New text style", Text = draft.Name };
        var descriptionBox = new TextBox { PlaceholderText = "What's it for?", Text = draft.Description };
        var familyBox = new TextBox { Text = draft.FontFamily };
        var weightBox = CreateFontWeightComboBox(draft.FontWeight);
        var sizeBox = new TextBox { Text = draft.FontSize.ToString("0.##", CultureInfo.InvariantCulture) };
        var lineHeightBox = new TextBox { Text = draft.LineHeightText };
        var letterSpacingBox = new TextBox { Text = draft.LetterSpacing.ToString("0.##", CultureInfo.InvariantCulture) };

        void UpdatePreview()
        {
            previewText.FontFamily = new FontFamily(string.IsNullOrWhiteSpace(familyBox.Text) ? "Open Sans" : familyBox.Text);
            previewText.FontSize = TryParseDouble(sizeBox.Text, out var value) ? value : draft.FontSize;
            previewText.FontWeight = ToWindowsFontWeight(GetSelectedFontWeight(weightBox));
        }

        familyBox.TextChanged += (_, _) => UpdatePreview();
        sizeBox.TextChanged += (_, _) => UpdatePreview();
        weightBox.SelectionChanged += (_, _) => UpdatePreview();
        UpdatePreview();

        var content = new StackPanel
        {
            Spacing = 14,
            Children =
            {
                preview,
                new Grid
                {
                    ColumnDefinitions =
                    {
                        new ColumnDefinition { Width = new GridLength(104) },
                        new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) }
                    },
                    RowDefinitions =
                    {
                        new RowDefinition { Height = GridLength.Auto },
                        new RowDefinition { Height = GridLength.Auto },
                        new RowDefinition { Height = GridLength.Auto },
                        new RowDefinition { Height = GridLength.Auto },
                        new RowDefinition { Height = GridLength.Auto }
                    },
                    ColumnSpacing = 12,
                    RowSpacing = 12,
                    Children =
                    {
                        CreateLabeledField("Name", nameBox, 0),
                        CreateLabeledField("Description", descriptionBox, 1),
                        CreateLabeledField("Family", familyBox, 2),
                        CreateLabeledField("Weight", weightBox, 3),
                        CreateLabeledPair("Size", sizeBox, "Line height", lineHeightBox, 4)
                    }
                },
                new Grid
                {
                    ColumnDefinitions =
                    {
                        new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) },
                        new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) }
                    },
                    ColumnSpacing = 12,
                    Children =
                    {
                        CreateFieldStack("Letter spacing", letterSpacingBox)
                    }
                }
            }
        };

        var dialog = CreateStyleDialog(title, content, nameBox.Text, isEditing);
        nameBox.TextChanged += (_, _) => dialog.IsPrimaryButtonEnabled = !string.IsNullOrWhiteSpace(nameBox.Text);
        dialog.Opened += (_, _) =>
        {
            nameBox.Focus(FocusState.Programmatic);
            nameBox.SelectAll();
        };

        var result = await dialog.ShowAsync();
        if (result != ContentDialogResult.Primary)
        {
            return (result, null);
        }

        var fontSize = TryParseDouble(sizeBox.Text, out var parsedSize) ? parsedSize : draft.FontSize;
        var letterSpacing = TryParseDouble(letterSpacingBox.Text, out var parsedLetterSpacing) ? parsedLetterSpacing : draft.LetterSpacing;
        var style = new EditorTextStyleItem(
            draft.StyleId,
            nameBox.Text,
            descriptionBox.Text,
            familyBox.Text,
            GetSelectedFontWeight(weightBox),
            fontSize,
            letterSpacing,
            lineHeightBox.Text);
        return (result, style);
    }

    private async Task<(ContentDialogResult Result, ColorSwatchItem? Style)> ShowColorStyleDialogAsync(string title, ColorSwatchItem draft, bool isEditing)
    {
        if (XamlRoot is null)
        {
            return default;
        }

        var textBoxStyle = (Style)Resources["ShellTextBoxStyle"];
        var pickerSectionBrush = (Brush)Resources["PickerSectionBrush"];
        var surfaceStrokeBrush = (Brush)Resources["SurfaceStrokeBrush"];
        var surfaceBrush = (Brush)Resources["SurfaceBrush"];

        var nameBox = new TextBox
        {
            PlaceholderText = "New color style",
            Text = draft.Label,
            Style = textBoxStyle
        };
        var descriptionBox = new TextBox
        {
            PlaceholderText = "What's it for?",
            Text = draft.Description,
            Style = textBoxStyle
        };
        var targetBox = new ComboBox
        {
            Items =
            {
                new ComboBoxItem { Content = "Fill", Tag = EditorPaintTarget.Fill },
                new ComboBoxItem { Content = "Stroke", Tag = EditorPaintTarget.Stroke }
            },
            SelectedIndex = draft.Target == EditorPaintTarget.Stroke ? 1 : 0,
            IsEnabled = !isEditing,
            Style = (Style)Resources["PickerComboBoxStyle"]
        };
        var strokeWidthBox = new TextBox
        {
            Text = draft.StrokeWidth.ToString("0.##", CultureInfo.InvariantCulture),
            Style = textBoxStyle
        };
        var colorPicker = new FigmaColorPicker
        {
            SelectedColor = draft.Color,
            Swatches = DocumentColorSwatches,
            LibraryStyles = LibraryPaintStyles,
            PaintTarget = draft.Target,
            CurrentStrokeWidthText = draft.StrokeWidthLabel,
            ShowAddButton = false,
            ShowCloseButton = false
        };

        var previewGlyph = new Border
        {
            Width = 84,
            Height = 84,
            Background = surfaceBrush,
            BorderBrush = surfaceStrokeBrush,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(20)
        };
        var previewChip = new Border
        {
            Margin = new Thickness(14),
            CornerRadius = new CornerRadius(14)
        };
        previewGlyph.Child = previewChip;

        var previewTitle = new TextBlock
        {
            Style = (Style)Resources["SectionHeadingStyle"],
            FontSize = 15.5
        };
        var previewMeta = new TextBlock
        {
            Style = (Style)Resources["SectionCaptionStyle"],
            FontSize = 11.5
        };
        var previewDescription = new TextBlock
        {
            Style = (Style)Resources["ShellBodyStyle"],
            FontSize = 12.25,
            TextWrapping = TextWrapping.WrapWholeWords,
            MaxWidth = 248
        };

        var previewCard = new Border
        {
            Background = pickerSectionBrush,
            BorderBrush = surfaceStrokeBrush,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(18),
            Padding = new Thickness(18),
            Child = new Grid
            {
                ColumnDefinitions =
                {
                    new ColumnDefinition { Width = GridLength.Auto },
                    new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) }
                },
                ColumnSpacing = 16,
                Children =
                {
                    previewGlyph
                }
            }
        };

        var previewCopy = new StackPanel
        {
            Spacing = 6,
            VerticalAlignment = VerticalAlignment.Center,
            Children =
            {
                previewTitle,
                previewMeta,
                previewDescription
            }
        };
        Grid.SetColumn(previewCopy, 1);
        ((Grid)previewCard.Child).Children.Add(previewCopy);

        var strokeWidthField = CreateFieldStack("Stroke width", strokeWidthBox);
        var detailsPanel = new StackPanel
        {
            Width = 304,
            Spacing = 14,
            Children =
            {
                previewCard,
                CreateFieldStack("Style name", nameBox),
                CreateFieldStack("Usage", descriptionBox),
                CreateFieldStack("Target", targetBox),
                strokeWidthField
            }
        };

        void UpdateColorPreview()
        {
            var target = GetSelectedPaintTarget(targetBox);
            var strokeWidth = TryParseDouble(strokeWidthBox.Text, out var parsedStrokeWidth) ? parsedStrokeWidth : draft.StrokeWidth;
            var brush = new SolidColorBrush(colorPicker.SelectedColor);
            var opacityPercent = ColorPickerColorHelper.ToPercent(colorPicker.SelectedColor.A);
            var hex = ColorPickerColorHelper.ToHexRgb(colorPicker.SelectedColor);
            var styleName = string.IsNullOrWhiteSpace(nameBox.Text) ? "Untitled color style" : nameBox.Text.Trim();
            var detailText = string.IsNullOrWhiteSpace(descriptionBox.Text)
                ? target == EditorPaintTarget.Stroke
                    ? "Reusable stroke token for outlines, dividers, and focused states."
                    : "Reusable fill token for surfaces, accents, and key blocks."
                : descriptionBox.Text.Trim();

            colorPicker.PaintTarget = target;
            colorPicker.CurrentStrokeWidthText = strokeWidthBox.Text;

            previewTitle.Text = styleName;
            previewMeta.Text = target == EditorPaintTarget.Stroke
                ? $"Stroke · {strokeWidth:0.##} px · #{hex} · {opacityPercent}%"
                : $"Fill · #{hex} · {opacityPercent}%";
            previewDescription.Text = detailText;

            previewChip.Background = target == EditorPaintTarget.Fill
                ? brush
                : new SolidColorBrush(Color.FromArgb(18, colorPicker.SelectedColor.R, colorPicker.SelectedColor.G, colorPicker.SelectedColor.B));
            previewChip.BorderBrush = brush;
            previewChip.BorderThickness = target == EditorPaintTarget.Stroke
                ? new Thickness(Math.Clamp(strokeWidth, 1.0, 6.0))
                : new Thickness(0);

            strokeWidthField.Visibility = target == EditorPaintTarget.Stroke ? Visibility.Visible : Visibility.Collapsed;
        }

        colorPicker.RegisterPropertyChangedCallback(FigmaColorPicker.SelectedColorProperty, (_, _) => UpdateColorPreview());
        targetBox.SelectionChanged += (_, _) => UpdateColorPreview();
        nameBox.TextChanged += (_, _) => UpdateColorPreview();
        descriptionBox.TextChanged += (_, _) => UpdateColorPreview();
        strokeWidthBox.TextChanged += (_, _) => UpdateColorPreview();
        UpdateColorPreview();

        var content = new Grid
        {
            Width = 792,
            ColumnDefinitions =
            {
                new ColumnDefinition { Width = GridLength.Auto },
                new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) }
            },
            ColumnSpacing = 18,
            Children =
            {
                colorPicker
            }
        };
        Grid.SetColumn(detailsPanel, 1);
        content.Children.Add(detailsPanel);

        var dialog = CreateStyleDialog(title, content, nameBox.Text, isEditing);
        nameBox.TextChanged += (_, _) => dialog.IsPrimaryButtonEnabled = !string.IsNullOrWhiteSpace(nameBox.Text);
        dialog.Opened += (_, _) =>
        {
            nameBox.Focus(FocusState.Programmatic);
            nameBox.SelectAll();
        };

        var result = await dialog.ShowAsync();
        if (result != ContentDialogResult.Primary)
        {
            return (result, null);
        }

        var target = GetSelectedPaintTarget(targetBox);
        var strokeWidth = TryParseDouble(strokeWidthBox.Text, out var parsedStrokeWidth) ? parsedStrokeWidth : draft.StrokeWidth;
        var sectionName = target == EditorPaintTarget.Stroke ? "Stroke styles" : "Fill styles";
        var style = new ColorSwatchItem(
            colorPicker.SelectedColor,
            nameBox.Text,
            target,
            draft.StyleId,
            "current-file",
            DocumentTitle,
            sectionName,
            $"{nameBox.Text} {descriptionBox.Text}",
            target == EditorPaintTarget.Stroke ? strokeWidth : 1.0,
            descriptionBox.Text);
        return (result, style);
    }

    private async Task<(ContentDialogResult Result, EditorEffectStyleItem? Style)> ShowEffectStyleDialogAsync(string title, EditorEffectStyleItem draft, bool isEditing)
    {
        if (XamlRoot is null)
        {
            return default;
        }

        var nameBox = new TextBox { PlaceholderText = "New effect style", Text = draft.Name };
        var descriptionBox = new TextBox { PlaceholderText = "What's it for?", Text = draft.Description };
        var effects = new ObservableCollection<EditorEffectItem>(draft.Effects.Select(EditorEffectStyleItem.CloneEffect));
        var editor = new FigmaEffectsEditor
        {
            ItemsSource = effects,
            IsEditable = true,
            ShowHeader = true,
            Swatches = DocumentColorSwatches
        };

        var content = new StackPanel
        {
            Spacing = 14,
            Children =
            {
                CreateFieldGrid(
                    ("Name", (UIElement)nameBox),
                    ("Description", descriptionBox)),
                editor
            }
        };

        var dialog = CreateStyleDialog(title, content, nameBox.Text, isEditing);
        nameBox.TextChanged += (_, _) => dialog.IsPrimaryButtonEnabled = !string.IsNullOrWhiteSpace(nameBox.Text);
        dialog.Opened += (_, _) =>
        {
            nameBox.Focus(FocusState.Programmatic);
            nameBox.SelectAll();
        };

        var result = await dialog.ShowAsync();
        if (result != ContentDialogResult.Primary)
        {
            return (result, null);
        }

        var style = new EditorEffectStyleItem(draft.StyleId, nameBox.Text, descriptionBox.Text, effects);
        return (result, style);
    }

    private async Task<(ContentDialogResult Result, EditorLayoutGuideStyleItem? Style)> ShowLayoutGuideStyleDialogAsync(string title, EditorLayoutGuideStyleItem draft, bool isEditing)
    {
        if (XamlRoot is null)
        {
            return default;
        }

        var preview = new Border
        {
            Height = 180,
            Background = new SolidColorBrush(Color.FromArgb(255, 249, 250, 251)),
            BorderBrush = (Brush)Resources["SurfaceStrokeBrush"],
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(16),
            Child = new Viewbox
            {
                Child = new FigmaIcon
                {
                    Kind = FigmaIconKind.AutoLayoutGrid,
                    Width = 32,
                    Height = 32,
                    IconStroke = (Brush)Resources["TextBrush"]
                }
            }
        };

        var nameBox = new TextBox { PlaceholderText = "New layout guide style", Text = draft.Name };
        var descriptionBox = new TextBox { PlaceholderText = "What's it for?", Text = draft.Description };
        var gridSizeBox = new TextBox { Text = draft.GridSize.ToString("0.##", CultureInfo.InvariantCulture) };
        var gridVisibleCheckBox = new CheckBox { Content = "Show grid", IsChecked = draft.IsGridVisible };
        var snapCheckBox = new CheckBox { Content = "Snap to grid", IsChecked = draft.IsSnapEnabled };

        var content = new StackPanel
        {
            Spacing = 14,
            Children =
            {
                preview,
                CreateFieldGrid(
                    ("Name", (UIElement)nameBox),
                    ("Description", descriptionBox),
                    ("Grid size", gridSizeBox)),
                gridVisibleCheckBox,
                snapCheckBox
            }
        };

        var dialog = CreateStyleDialog(title, content, nameBox.Text, isEditing);
        nameBox.TextChanged += (_, _) => dialog.IsPrimaryButtonEnabled = !string.IsNullOrWhiteSpace(nameBox.Text);
        dialog.Opened += (_, _) =>
        {
            nameBox.Focus(FocusState.Programmatic);
            nameBox.SelectAll();
        };

        var result = await dialog.ShowAsync();
        if (result != ContentDialogResult.Primary)
        {
            return (result, null);
        }

        var gridSize = TryParseDouble(gridSizeBox.Text, out var parsedGridSize) ? parsedGridSize : draft.GridSize;
        var style = CreateLayoutGuideStyle(
            draft.StyleId,
            nameBox.Text,
            descriptionBox.Text,
            gridSize,
            gridVisibleCheckBox.IsChecked == true,
            snapCheckBox.IsChecked == true);
        return (result, style);
    }

    private ContentDialog CreateStyleDialog(string title, UIElement content, string name, bool isEditing)
    {
        return new ContentDialog
        {
            Title = title,
            Content = new ScrollViewer
            {
                Content = content,
                MaxHeight = 720
            },
            PrimaryButtonText = isEditing ? "Save style" : "Create style",
            SecondaryButtonText = isEditing ? "Delete" : string.Empty,
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Primary,
            IsPrimaryButtonEnabled = !string.IsNullOrWhiteSpace(name),
            XamlRoot = XamlRoot
        };
    }

    private static Grid CreateFieldGrid(params (string Label, UIElement Element)[] fields)
    {
        var grid = new Grid
        {
            RowSpacing = 12
        };

        for (var index = 0; index < fields.Length; index++)
        {
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            var row = new Grid
            {
                ColumnSpacing = 12,
                ColumnDefinitions =
                {
                    new ColumnDefinition { Width = new GridLength(104) },
                    new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) }
                }
            };

            row.Children.Add(new TextBlock
            {
                Text = fields[index].Label,
                Style = (Style)Application.Current.Resources["ShellBodyStyle"],
                VerticalAlignment = VerticalAlignment.Center
            });

            Grid.SetColumn(fields[index].Element, 1);
            row.Children.Add(fields[index].Element);
            Grid.SetRow(row, index);
            grid.Children.Add(row);
        }

        return grid;
    }

    private static UIElement CreateLabeledField(string label, UIElement field, int row)
    {
        var panel = CreateFieldStack(label, field);
        Grid.SetColumn(panel, 1);
        Grid.SetRow(panel, row);
        return panel;
    }

    private static UIElement CreateLabeledPair(string leftLabel, UIElement leftField, string rightLabel, UIElement rightField, int row)
    {
        var grid = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) },
                new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) }
            },
            ColumnSpacing = 12
        };
        grid.Children.Add(CreateFieldStack(leftLabel, leftField));
        var right = CreateFieldStack(rightLabel, rightField);
        Grid.SetColumn(right, 1);
        grid.Children.Add(right);
        Grid.SetColumn(grid, 1);
        Grid.SetRow(grid, row);
        return grid;
    }

    private static StackPanel CreateFieldStack(string label, UIElement field)
    {
        return new StackPanel
        {
            Spacing = 6,
            Children =
            {
                new TextBlock
                {
                    Text = label,
                    Style = (Style)Application.Current.Resources["SectionCaptionStyle"]
                },
                field
            }
        };
    }

    private static ComboBox CreateFontWeightComboBox(SvgFontWeight selectedWeight)
    {
        var comboBox = new ComboBox
        {
            Style = (Style)Application.Current.Resources["PickerComboBoxStyle"]
        };
        comboBox.Items.Add(new ComboBoxItem { Content = "Regular", Tag = SvgFontWeight.Normal });
        comboBox.Items.Add(new ComboBoxItem { Content = "Medium", Tag = SvgFontWeight.W500 });
        comboBox.Items.Add(new ComboBoxItem { Content = "Semibold", Tag = SvgFontWeight.W600 });
        comboBox.Items.Add(new ComboBoxItem { Content = "Bold", Tag = SvgFontWeight.Bold });

        foreach (var item in comboBox.Items.OfType<ComboBoxItem>())
        {
            if (item.Tag is SvgFontWeight weight && weight == selectedWeight)
            {
                comboBox.SelectedItem = item;
                break;
            }
        }

        comboBox.SelectedIndex = comboBox.SelectedIndex < 0 ? 0 : comboBox.SelectedIndex;
        return comboBox;
    }

    private static SvgFontWeight GetSelectedFontWeight(ComboBox comboBox)
    {
        return comboBox.SelectedItem is ComboBoxItem { Tag: SvgFontWeight weight }
            ? weight
            : SvgFontWeight.Normal;
    }

    private static EditorPaintTarget GetSelectedPaintTarget(ComboBox comboBox)
    {
        return comboBox.SelectedItem is ComboBoxItem { Tag: EditorPaintTarget target }
            ? target
            : EditorPaintTarget.Fill;
    }

    private static FontWeight ToWindowsFontWeight(SvgFontWeight weight)
    {
        return weight switch
        {
            SvgFontWeight.Bold or SvgFontWeight.W700 => Microsoft.UI.Text.FontWeights.Bold,
            SvgFontWeight.W500 => Microsoft.UI.Text.FontWeights.Medium,
            SvgFontWeight.W600 => Microsoft.UI.Text.FontWeights.SemiBold,
            SvgFontWeight.W300 => Microsoft.UI.Text.FontWeights.Light,
            _ => Microsoft.UI.Text.FontWeights.Normal
        };
    }
}
