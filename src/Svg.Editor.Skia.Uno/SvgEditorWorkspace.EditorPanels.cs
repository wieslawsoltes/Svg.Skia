using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Svg.Editor.Skia;

namespace Svg.Editor.Skia.Uno;

public sealed partial class SvgEditorWorkspace
{
    private sealed record EditorBrushChoice(SvgBrushDefinition Definition)
    { public override string ToString() => Definition.Name + " · " + Definition.Kind; }
    private sealed record EditorResourceChoice(SvgElement Element, string Caption)
    { public override string ToString() => Caption; }
    private StudioColorEditor? _editorColorPanel;
    private StudioGradientEditor? _editorGradientPanel;
    private StudioWrapPanel? _editorSwatches;
    private ListView? _editorBrushes, _editorSymbols, _editorLinks;
    private TextBlock? _editorLinkSummary;
    private ComboBox? _editorTextAlignment;
    private StudioTextField? _editorLetterSpacing, _editorWordSpacing;
    private SvgEditorController? _editorPanelController;
    private SvgDocument? _editorPanelDocument;
    private readonly StudioPanelRefreshState _editorBrushRefresh = new(), _editorSymbolRefresh = new(), _editorLinkRefresh = new(), _editorSwatchRefresh = new();
    private readonly StudioPanelRefreshState _editorParagraphRefresh = new(observeSelection: true);
    private (string Paint, string Label)[]? _editorSwatchChoices;
    private static readonly string[] DefaultEditorSwatches = { "none", "#FFFFFF", "#000000", "#808080", "#C1272D", "#ED1C24", "#F7931E", "#FCEE21", "#39B54A", "#009245", "#00A99D", "#29ABE2", "#0071BC", "#2E3192", "#662D91", "#93278F" };
    private bool _refreshingEditorPanels;

    private void BuildEditorPanels()
    {
        void BeforePaint()
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            CommitInspectorEdits();
            Canvas.CancelGesture();
        }
        var color = StudioPanel("color", "Color");
        _editorColorPanel = new StudioColorEditor { BeforeApply = BeforePaint };
        RegisterEditorPaintTarget(_editorColorPanel, "the Color panel");
        color.Children.Add(_editorColorPanel);
        var gradient = StudioPanel("gradient", "Gradient");
        _editorGradientPanel = new StudioGradientEditor { BeforeApply = BeforePaint };
        RegisterEditorPaintTarget(_editorGradientPanel, "the Gradient panel");
        gradient.Children.Add(_editorGradientPanel);
        var swatches = StudioPanel("swatches", "Swatches");
        swatches.Children.Add(StudioHint("Apply a swatch to the active fill or stroke. X switches the paint target."));
        _editorSwatches = new StudioWrapPanel { Spacing = 3 };
        swatches.Children.Add(_editorSwatches);
        swatches.Children.Add(StudioCommand("swatches", "Edit document swatches…"));

        var brushes = StudioPanel("brushes", "Brushes");
        brushes.Children.Add(StudioHint("Select a document brush to apply it to selected paths. Manage vector stamps and width profiles in the library."));
        _editorBrushes = EditorPanelList("Document brushes");
        _editorBrushes.ItemClick += (_, args) =>
        {
            if (args.ClickedItem is not EditorBrushChoice choice || Controller is not { } controller)
                return;
            ApplySafely(() =>
            {
                CommitInspectorEdits();
                controller.EnsureSelectionEditable();
                Canvas.ActiveBrush = choice.Definition;
                var paths = controller.SelectedElements.OfType<SvgPath>().ToArray();
                if (controller.SelectedElements.Count == 0)
                {
                    StatusText.Text = choice.Definition.Name + " brush selected for new strokes.";
                    return;
                }
                if (paths.Length != controller.SelectedElements.Count)
                    throw new InvalidOperationException("Select paths before applying a document brush.");
                controller.Execute("Apply document brush", () =>
                {
                    foreach (var path in paths)
                        SvgBrushLibraryService.Apply(path, choice.Definition);
                });
            });
        };
        brushes.Children.Add(_editorBrushes);
        brushes.Children.Add(StudioCommand("brushes", "Manage brush library…"));

        var symbols = StudioPanel("symbols", "Symbols");
        symbols.Children.Add(StudioHint("Insert an instance of a document symbol. Its definition stays shared by every instance."));
        _editorSymbols = EditorPanelList("Document symbols");
        _editorSymbols.ItemClick += (_, args) =>
        {
            if (args.ClickedItem is not EditorResourceChoice { Element: SvgSymbol symbol } || Controller is not { } controller)
                return;
            ApplySafely(() =>
            {
                CommitInspectorEdits();
                var bounds = symbol.ViewBox;
                var center = Canvas.Viewport.ViewToWorld(new SkiaSharp.SKPoint((float)Canvas.Viewport.Width / 2, (float)Canvas.Viewport.Height / 2));
                var width = bounds.Width > 0 ? bounds.Width : 100;
                var height = bounds.Height > 0 ? bounds.Height : 100;
                controller.AddElementInPictureCoordinates(new SvgUse
                {
                    ReferencedElement = new Uri("#" + symbol.ID, UriKind.Relative),
                    X = center.X - width / 2,
                    Y = center.Y - height / 2,
                    Width = width,
                    Height = height
                }, "Insert symbol instance");
            });
        };
        symbols.Children.Add(_editorSymbols);
        symbols.Children.Add(StudioCommand("symbol", "Create symbol from selection…", 1));

        var transparency = StudioPanel("transparency", "Transparency");
        var opacity = StudioNumber("Opacity", 0, 100, (controller, value) => controller.SetOpacity((float)value / 100));
        opacity.Suffix = "%";
        _opacities.Add(opacity);
        transparency.Children.Add(opacity);
        transparency.Children.Add(StudioSection("Opacity mask", CommandWrap(new[]
        {
            ("alphamask", "Make alpha mask"), ("luminancemask", "Make luminance mask"),
            ("releasemask", "Release mask"), ("disablemask", "Disable mask")
        }, 1)));
        transparency.Children.Add(StudioCommand("transparency", "Show transparency grid"));

        var paragraph = StudioPanel("paragraph", "Paragraph");
        _editorTextAlignment = StudioChoice("Alignment", new[] { "start", "middle", "end" }, "text-anchor");
        _editorLetterSpacing = StudioText("Letter spacing · SVG units", (controller, text) => controller.SetAttribute("letter-spacing", text));
        _editorWordSpacing = StudioText("Word spacing · SVG units", (controller, text) => controller.SetAttribute("word-spacing", text));
        paragraph.Children.Add(_editorTextAlignment);
        paragraph.Children.Add(_editorLetterSpacing);
        paragraph.Children.Add(_editorWordSpacing);
        paragraph.Children.Add(StudioCommand("textframes", "Paragraphs and linked frames…"));
        paragraph.Children.Add(StudioHint("Linked frames and styled text ranges are edited in the text story editor."));

        BuildEditorArtboardPanel();

        var links = StudioPanel("links", "Links");
        _editorLinks = EditorPanelList("Linked and embedded images");
        _editorLinkSummary = StudioHint("Select an image to inspect its source.");
        _editorLinkSummary.IsTextSelectionEnabled = true;
        AutomationProperties.SetAutomationId(_editorLinks, "EditorLinks");
        AutomationProperties.SetAutomationId(_editorLinkSummary, "EditorLinkSummary");
        _editorLinks.ItemClick += (_, args) =>
        {
            if (args.ClickedItem is not EditorResourceChoice { Element: SvgImage image } || Controller is not { } controller)
                return;
            ApplySafely(() =>
            {
                CommitInspectorEdits();
                controller.Select(image);
                Canvas.FitSelection();
                RefreshEditorLinkSelection(controller);
            });
        };
        links.Children.Add(_editorLinks);
        links.Children.Add(_editorLinkSummary);
        links.Children.Add(StudioCommand("image", "Place image…"));
        links.Children.Add(StudioCommand("panel-attributes", "Edit image attributes"));
        BuildEditorUtilityPanels();
    }

    private static ListView EditorPanelList(string label)
    {
        var list = new ListView { IsItemClickEnabled = true, SelectionMode = ListViewSelectionMode.Single, MinHeight = 72, MaxHeight = 260 };
        AutomationProperties.SetName(list, label);
        return list;
    }

    private void RefreshEditorPanels()
    {
        if (_refreshingEditorPanels)
            return;
        _refreshingEditorPanels = true;
        var syncing = _syncingStudioPanels;
        _syncingStudioPanels = true;
        try
        {
            var controller = Controller;
            if (!ReferenceEquals(controller, _editorPanelController) || !ReferenceEquals(controller?.Document, _editorPanelDocument))
            {
                // Drop foreign command targets immediately; repopulate each panel
                // only when it becomes visible in this document.
                _editorPanelController = controller;
                _editorPanelDocument = controller?.Document;
                if (_editorBrushes is not null)
                    _editorBrushes.ItemsSource = null;
                if (_editorSymbols is not null)
                    _editorSymbols.ItemsSource = null;
                if (_editorLinks is not null)
                    _editorLinks.ItemsSource = null;
                if (_editorArtboards is not null)
                    _editorArtboards.ItemsSource = null;
                _editorArtboardController = null;
                _editorArtboardRevision = -1;
                _editorSwatches?.Children.Clear();
                _editorSwatchChoices = null;
                _editorBrushRefresh.Invalidate();
                _editorSymbolRefresh.Invalidate();
                _editorLinkRefresh.Invalidate();
                _editorSwatchRefresh.Invalidate();
                _editorParagraphRefresh.Invalidate();
            }
            RefreshEditorUtilityPanels();
            if (IsStudioPanelVisible("artboards"))
                RefreshEditorArtboardPanel();
            _editorColorPanel?.BindController(controller);
            _editorGradientPanel?.BindController(controller);
            _studioColorEditor?.BindController(controller);
            _studioGradientEditor?.BindController(controller);
            if (controller?.Document is not { } source)
            {
                RefreshEditorLinkSelection(controller);
                return;
            }
            if (_editorParagraphRefresh.NeedsRefresh(IsStudioPanelVisible("paragraph"), controller))
            {
                var selected = controller.SelectedElements;
                var text = selected.FirstOrDefault() as SvgTextBase;
                var textOnly = selected.Count > 0 && selected.All(element => element is SvgTextBase && !SvgEditorController.IsEditLocked(element));
                if (_editorTextAlignment is not null)
                { _editorTextAlignment.IsEnabled = textOnly; _editorTextAlignment.SelectedItem = text?.TextAnchor.ToString().ToLowerInvariant(); }
                if (_editorLetterSpacing is not null)
                { _editorLetterSpacing.IsEnabled = textOnly; _editorLetterSpacing.Synchronize(text?.LetterSpacing.ToString()); }
                if (_editorWordSpacing is not null)
                { _editorWordSpacing.IsEnabled = textOnly; _editorWordSpacing.Synchronize(text?.WordSpacing.ToString()); }
                _editorParagraphRefresh.Complete(controller);
            }
            if (_editorBrushes is not null && _editorBrushRefresh.NeedsRefresh(IsStudioPanelVisible("brushes"), controller))
            {
                _editorBrushes.ItemsSource = SvgBrushLibraryService.Load(source).Select(definition => new EditorBrushChoice(definition)).ToArray();
                _editorBrushRefresh.Complete(controller);
            }
            if (_editorSymbols is not null && _editorSymbolRefresh.NeedsRefresh(IsStudioPanelVisible("symbols"), controller))
            {
                _editorSymbols.ItemsSource = source.Descendants().OfType<SvgSymbol>().Where(symbol => !string.IsNullOrWhiteSpace(symbol.ID))
                    .Select(symbol => new EditorResourceChoice(symbol, symbol.ID)).ToArray();
                _editorSymbolRefresh.Complete(controller);
            }
            if (IsStudioPanelVisible("links"))
            {
                if (_editorLinks is not null && _editorLinkRefresh.NeedsRefresh(true, controller))
                {
                    _editorLinks.ItemsSource = source.Descendants().OfType<SvgImage>().Select(image => new EditorResourceChoice(image,
                        (string.IsNullOrEmpty(image.ID) ? "Image" : image.ID) + (image.Href?.StartsWith("data:", StringComparison.OrdinalIgnoreCase) == true ? " · embedded" : " · linked"))).ToArray();
                    _editorLinkRefresh.Complete(controller);
                }
                RefreshEditorLinkSelection(controller);
            }
            if (_editorSwatches is not null && _editorSwatchRefresh.NeedsRefresh(IsStudioPanelVisible("swatches"), controller))
            {
                var choices = DefaultEditorSwatches.Select(paint => (Paint: paint, Label: paint)).Concat(
                    source.Descendants().Where(element => element is SvgGradientServer or SvgPatternServer)
                        .Where(element => !string.IsNullOrWhiteSpace(element.ID))
                        .Select(resource => (Paint: "url(#" + resource.ID + ")", Label: resource.ID))).ToArray();
                if (_editorSwatchChoices is null || !_editorSwatchChoices.SequenceEqual(choices))
                {
                    _editorSwatches.Children.Clear();
                    foreach (var (paint, label) in choices)
                        AddSwatch(paint, label);
                    _editorSwatchChoices = choices;
                }
                _editorSwatchRefresh.Complete(controller);
            }
        }
        finally { _syncingStudioPanels = syncing; _refreshingEditorPanels = false; }

        void AddSwatch(string paint, string label)
        {
            var chip = new StudioPaintChip { Paint = paint, Label = label, IsCompact = true, Width = 30, Height = 30, MinWidth = 0, MinHeight = 0 };
            AutomationProperties.SetName(chip, label);
            ToolTipService.SetToolTip(chip, label);
            chip.Click += (_, _) => ApplySafely(() =>
            {
                CommitInspectorEdits();
                if (Controller is not { } target)
                    return;
                if (Canvas.StrokePaintTarget)
                    target.SetStroke(paint);
                else
                    target.SetFill(paint);
            });
            _editorSwatches!.Children.Add(chip);
        }
    }

    private void RefreshEditorLinkSelection(SvgEditorController? controller)
    {
        if (_editorLinks is null || _editorLinkSummary is null)
            return;
        var image = controller?.Document is not null && controller.SelectedElements.Count == 1 ? controller.SelectedElements[0] as SvgImage : null;
        _editorLinks.SelectedItem = (_editorLinks.ItemsSource as EditorResourceChoice[])?
            .FirstOrDefault(choice => ReferenceEquals(choice.Element, image));
        _editorLinkSummary.Text = image is null
            ? controller?.Document is null ? "Open a document to inspect image sources."
                : (_editorLinks.ItemsSource as EditorResourceChoice[])?.Length == 0 ? "This document has no images."
                : "Select one image to inspect its source."
            : image.Href?.StartsWith("data:", StringComparison.OrdinalIgnoreCase) == true
                ? "Embedded image · " + image.Width + " × " + image.Height
                : image.Href ?? "No image source";
    }

    private void DisposeEditorPanels()
    {
        _editorColorPanel?.Dispose();
        _editorGradientPanel?.Dispose();
        _studioColorEditor?.Dispose();
        _studioGradientEditor?.Dispose();
        _editorPaintTargets.Clear();
        _activeEditorGradient = null;
        _editorPanelController = null;
        _editorPanelDocument = null;
        _editorBrushRefresh.Invalidate();
        _editorSymbolRefresh.Invalidate();
        _editorLinkRefresh.Invalidate();
        _editorSwatchRefresh.Invalidate();
        _editorParagraphRefresh.Invalidate();
        DisposeEditorUtilityPanels();
    }
}
