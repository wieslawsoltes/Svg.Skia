using System.Collections.ObjectModel;
using System.Globalization;
using System.Security;
using System.Text;
using Svg;
using Svg.Editor.Core;
using Svg.Editor.Skia.Uno.Models;
using Svg.Editor.Svg;
using Windows.UI;
using Shim = ShimSkiaSharp;

namespace Svg.Editor.Skia.Uno;

public partial class SvgEditorWorkspacePage
{
    private const string DevSnippetSvgId = "svg";
    private const string DevSnippetCssId = "css";
    private const string DevSnippetXamlId = "xaml";
    private const string DevSnippetCSharpId = "csharp";

    private readonly ObservableCollection<EditorDevSpecItem> _devSpecs = [];
    private readonly ObservableCollection<EditorDevCodeSnippet> _devCodeSnippets = [];

    private bool _isDevInspector;
    private string _devSelectionPath = "svg";
    private string _devMeasurementSummary = "Select a layer to inspect measurements, spacing, and generated code.";
    private string _selectedDevCodeSnippetId = DevSnippetSvgId;

    public ObservableCollection<EditorDevSpecItem> DevSpecs => _devSpecs;

    public ObservableCollection<EditorDevCodeSnippet> DevCodeSnippets => _devCodeSnippets;

    public bool IsDevInspectorActive => _isDevInspector;

    public Visibility DevInspectorVisibility => _isDevInspector ? Visibility.Visible : Visibility.Collapsed;

    public string DevSelectionPath => _devSelectionPath;

    public string DevMeasurementSummary => _devMeasurementSummary;

    public string SelectedDevCodeSnippetId
    {
        get => _selectedDevCodeSnippetId;
        set
        {
            if (!SetField(ref _selectedDevCodeSnippetId, string.IsNullOrWhiteSpace(value) ? DevSnippetSvgId : value))
            {
                return;
            }

            UpdateSelectedDevSnippet();
        }
    }

    public string SelectedDevCodeSnippetTitle => GetSelectedDevSnippet()?.Title ?? "SVG";

    public string SelectedDevCodeSnippetLanguage => GetSelectedDevSnippet()?.Language ?? "SVG";

    public string SelectedDevCodeSnippetContent => GetSelectedDevSnippet()?.Content ?? string.Empty;

    protected void OnDevInspectorClick(object sender, RoutedEventArgs e)
    {
        if (_toolService.CurrentTool == ToolService.Tool.Comment)
        {
            SetTool(ToolService.Tool.Select);
        }

        SetDevInspectorActive(true);
        RefreshComputedState();
    }

    protected void OnDevModeRequested(object sender, RoutedEventArgs e)
    {
        if (_toolService.CurrentTool is not (ToolService.Tool.Select or ToolService.Tool.Hand or ToolService.Tool.Scale))
        {
            SetTool(ToolService.Tool.Select);
        }

        SetDevInspectorActive(true);
        RefreshComputedState();
    }

    protected void OnDevSnippetRequested(object sender, DevCodeSnippetRequestedEventArgs e)
    {
        SelectedDevCodeSnippetId = e.SnippetId;
        SetDevInspectorActive(true);
    }

    protected void OnCopyActiveDevSnippetRequested(object? sender, EventArgs e)
    {
        CopyActiveDevSnippetToClipboard();
    }

    protected async void OnDevCommandRequested(object? sender, EditorMainMenuCommandEventArgs e)
    {
        await ExecuteMainMenuCommandAsync(e.Command);
    }

    private void SetDevInspectorActive(bool isActive)
    {
        if (_isDevInspector == isActive && !_isPrototypeInspector && !_isCommentsInspector)
        {
            if (isActive)
            {
                RefreshDevModeState(force: true);
            }

            return;
        }

        _isDevInspector = isActive;
        if (isActive)
        {
            _isPrototypeInspector = false;
            _isCommentsInspector = false;
            RefreshDevModeState(force: true);
        }

        RaisePropertyChanged(nameof(IsDesignInspectorActive));
        RaisePropertyChanged(nameof(IsPrototypeInspectorActive));
        RaisePropertyChanged(nameof(IsDevInspectorActive));
        RaisePropertyChanged(nameof(IsCommentsInspectorActive));
        RaisePropertyChanged(nameof(DesignInspectorVisibility));
        RaisePropertyChanged(nameof(PrototypeInspectorVisibility));
        RaisePropertyChanged(nameof(DevInspectorVisibility));
        RaisePropertyChanged(nameof(CommentsInspectorVisibility));
        RaisePropertyChanged(nameof(InspectorTabsVisibility));
        RaisePropertyChanged(nameof(InspectorSelectionVisibility));
    }

    private void RefreshDevModeState(bool force = false)
    {
        if (!force && !_isDevInspector)
        {
            return;
        }

        if (_document is null)
        {
            ReplaceCollection(_devSpecs, []);
            ReplaceCollection(_devCodeSnippets, []);
            _devSelectionPath = "svg";
            _devMeasurementSummary = "No active SVG document.";
            RaisePropertyChanged(nameof(DevSelectionPath));
            RaisePropertyChanged(nameof(DevMeasurementSummary));
            UpdateSelectedDevSnippet();
            return;
        }

        var selectionRoots = GetSelectionRoots();
        var primaryElement = _selectedElement ?? selectionRoots.FirstOrDefault();
        var selectionBounds = GetSelectionBounds();

        _devSelectionPath = BuildDevSelectionPath(primaryElement);
        _devMeasurementSummary = BuildDevMeasurementSummary(selectionRoots, selectionBounds);
        ReplaceCollection(_devSpecs, BuildDevSpecs(primaryElement, selectionRoots, selectionBounds));
        ReplaceCollection(_devCodeSnippets, BuildDevCodeSnippets(primaryElement, selectionRoots, selectionBounds));

        if (_devCodeSnippets.Count == 0)
        {
            _selectedDevCodeSnippetId = DevSnippetSvgId;
        }
        else if (!_devCodeSnippets.Any(snippet => string.Equals(snippet.Id, _selectedDevCodeSnippetId, StringComparison.Ordinal)))
        {
            _selectedDevCodeSnippetId = _devCodeSnippets[0].Id;
        }

        RaisePropertyChanged(nameof(DevSpecs));
        RaisePropertyChanged(nameof(DevCodeSnippets));
        RaisePropertyChanged(nameof(DevSelectionPath));
        RaisePropertyChanged(nameof(DevMeasurementSummary));
        RaisePropertyChanged(nameof(SelectedDevCodeSnippetId));
        UpdateSelectedDevSnippet();
    }

    private void CopyActiveDevSnippetToClipboard()
    {
        RefreshDevModeState(force: true);

        var snippet = GetSelectedDevSnippet();
        if (snippet is null)
        {
            CanvasStatus = "No Dev Mode snippet is available for the current document state.";
            return;
        }

        TrySetClipboardText(
            snippet.Content,
            $"Copied the {snippet.Title.ToLowerInvariant()} snippet to the system clipboard.");
    }

    private void CopyDevSnippetToClipboard(EditorMainMenuCommand command)
    {
        RefreshDevModeState(force: true);

        var snippetId = command switch
        {
            EditorMainMenuCommand.CopyDevSvgSnippet => DevSnippetSvgId,
            EditorMainMenuCommand.CopyDevCssSnippet => DevSnippetCssId,
            EditorMainMenuCommand.CopyDevXamlSnippet => DevSnippetXamlId,
            EditorMainMenuCommand.CopyDevCSharpSnippet => DevSnippetCSharpId,
            _ => _selectedDevCodeSnippetId
        };

        SelectedDevCodeSnippetId = snippetId;
        CopyActiveDevSnippetToClipboard();
    }

    private EditorDevCodeSnippet? GetSelectedDevSnippet()
    {
        return _devCodeSnippets.FirstOrDefault(snippet => string.Equals(snippet.Id, _selectedDevCodeSnippetId, StringComparison.Ordinal))
            ?? _devCodeSnippets.FirstOrDefault();
    }

    private void UpdateSelectedDevSnippet()
    {
        foreach (var snippet in _devCodeSnippets)
        {
            snippet.IsSelected = string.Equals(snippet.Id, _selectedDevCodeSnippetId, StringComparison.Ordinal);
        }

        RaisePropertyChanged(nameof(SelectedDevCodeSnippetTitle));
        RaisePropertyChanged(nameof(SelectedDevCodeSnippetLanguage));
        RaisePropertyChanged(nameof(SelectedDevCodeSnippetContent));
    }

    private IReadOnlyList<EditorDevSpecItem> BuildDevSpecs(
        SvgVisualElement? primaryElement,
        IReadOnlyList<SvgVisualElement> selectionRoots,
        Shim.SKRect? selectionBounds)
    {
        var specs = new List<EditorDevSpecItem>();
        var selectionLabel = selectionRoots.Count switch
        {
            0 => "Document",
            1 when primaryElement is not null => BuildDevElementLabel(primaryElement),
            _ => $"{selectionRoots.Count} layers"
        };

        specs.Add(new EditorDevSpecItem("Selection", selectionLabel, selectionRoots.Count <= 1
            ? "Current handoff target from the live SVG AST."
            : "Computed from the current multi-selection bounds."));

        if (selectionBounds is { } bounds)
        {
            specs.Add(new EditorDevSpecItem("Position",
                $"X {FormatDevNumber(bounds.Left)} · Y {FormatDevNumber(bounds.Top)}",
                "Picture-space coordinates after transforms."));
            specs.Add(new EditorDevSpecItem("Size",
                $"W {FormatDevNumber(bounds.Width)} · H {FormatDevNumber(bounds.Height)}",
                "Transformed bounds for the current selection."));

            if (TryGetSelectionSpacing(selectionRoots, bounds, out var spacing))
            {
                specs.Add(new EditorDevSpecItem("Spacing", spacing, "Distances to the shared parent or frame."));
            }
        }
        else
        {
            specs.Add(new EditorDevSpecItem("Canvas", $"{PageTitle} · {CanvasLabel}", "No focused layer is selected."));
        }

        if (primaryElement is not null)
        {
            specs.Add(new EditorDevSpecItem("Fill", FormatPaint(primaryElement.Fill), "Resolved from the current SVG paint server."));
            specs.Add(new EditorDevSpecItem("Stroke", $"{FormatPaint(primaryElement.Stroke)} · {FormatDevNumber(primaryElement.StrokeWidth.Value)}px"));
            specs.Add(new EditorDevSpecItem("Opacity", $"{FormatDevNumber(primaryElement.Opacity * 100f)}%"));

            if (primaryElement is SvgRectangle rect)
            {
                specs.Add(new EditorDevSpecItem(
                    "Corner radius",
                    $"{FormatDevNumber(rect.CornerRadiusX.Value)} / {FormatDevNumber(rect.CornerRadiusY.Value)}",
                    "SVG rectangle radii."));
            }
            else if (primaryElement is SvgEllipse ellipse)
            {
                specs.Add(new EditorDevSpecItem(
                    "Radii",
                    $"{FormatDevNumber(ellipse.RadiusX.Value)} / {FormatDevNumber(ellipse.RadiusY.Value)}",
                    "Ellipse radii in user units."));
            }
            else if (primaryElement is SvgTextBase text)
            {
                specs.Add(new EditorDevSpecItem("Text", text.Text ?? string.Empty, "Current text content."));
                specs.Add(new EditorDevSpecItem(
                    "Typography",
                    $"{(string.IsNullOrWhiteSpace(text.FontFamily) ? "inherit" : text.FontFamily)} · {FormatDevNumber(text.FontSize.Value)}px · {text.TextAnchor}",
                    "Typography derived from SVG text attributes."));
            }

            if (primaryElement is SvgGroup group && FrameService.GetContainerKind(group) != FrameContainerKind.Group)
            {
                specs.Add(new EditorDevSpecItem(
                    "Container",
                    FrameService.GetContainerLabel(FrameService.GetContainerKind(group)),
                    "SVG group metadata used to preserve frame semantics."));
            }
        }
        else if (_document is not null)
        {
            specs.Add(new EditorDevSpecItem("ViewBox",
                $"{FormatDevNumber(_document.ViewBox.MinX)}, {FormatDevNumber(_document.ViewBox.MinY)}, {FormatDevNumber(_document.ViewBox.Width)}, {FormatDevNumber(_document.ViewBox.Height)}",
                "Document viewBox used for exported code."));
            specs.Add(new EditorDevSpecItem("Libraries", LibrariesSummary));
            specs.Add(new EditorDevSpecItem("Comments", CommentsSummary));
        }

        return specs;
    }

    private IReadOnlyList<EditorDevCodeSnippet> BuildDevCodeSnippets(
        SvgVisualElement? primaryElement,
        IReadOnlyList<SvgVisualElement> selectionRoots,
        Shim.SKRect? selectionBounds)
    {
        var svgMarkup = selectionRoots.Count > 0
            ? BuildSelectionSvgMarkup(selectionRoots)
            : _documentService.GetXml(_document!);

        return
        [
            new EditorDevCodeSnippet(DevSnippetSvgId, "SVG", "SVG", svgMarkup, "Raw markup"),
            new EditorDevCodeSnippet(DevSnippetCssId, "CSS", "CSS", BuildCssSnippet(primaryElement, selectionRoots, selectionBounds), "Styles"),
            new EditorDevCodeSnippet(DevSnippetXamlId, "Uno XAML", "XAML", BuildUnoXamlSnippet(selectionBounds, svgMarkup), "Control snippet"),
            new EditorDevCodeSnippet(DevSnippetCSharpId, "C#", "C#", BuildCSharpSnippet(selectionBounds, svgMarkup), "Imperative setup")
        ];
    }

    private string BuildDevSelectionPath(SvgVisualElement? primaryElement)
    {
        if (primaryElement is null)
        {
            return "svg";
        }

        var segments = new Stack<string>();
        SvgElement? current = primaryElement;
        while (current is not null)
        {
            segments.Push(current is SvgDocument
                ? "svg"
                : BuildDevElementLabel(current));
            current = current.Parent;
        }

        return string.Join(" / ", segments.Where(static segment => !string.IsNullOrWhiteSpace(segment)));
    }

    private string BuildDevMeasurementSummary(IReadOnlyList<SvgVisualElement> selectionRoots, Shim.SKRect? selectionBounds)
    {
        if (selectionBounds is not { } bounds)
        {
            return $"{PageTitle} · {PageSubtitle} · {LibrariesSummary}";
        }

        var parts = new List<string>
        {
            $"{FormatDevNumber(bounds.Width)} × {FormatDevNumber(bounds.Height)}",
            $"x {FormatDevNumber(bounds.Left)}",
            $"y {FormatDevNumber(bounds.Top)}"
        };

        if (TryGetSelectionSpacing(selectionRoots, bounds, out var spacing))
        {
            parts.Add(spacing);
        }

        return string.Join(" · ", parts);
    }

    private bool TryGetSelectionSpacing(IReadOnlyList<SvgVisualElement> selectionRoots, Shim.SKRect selectionBounds, out string spacing)
    {
        spacing = string.Empty;

        if (selectionRoots.Count == 0)
        {
            return false;
        }

        if (!TryGetSelectionParent(selectionRoots, out var parent) || parent is null)
        {
            return false;
        }

        if (!TryGetContainerBounds(parent, out var parentBounds))
        {
            return false;
        }

        spacing = $"T {FormatDevNumber(selectionBounds.Top - parentBounds.Top)} · " +
                  $"R {FormatDevNumber(parentBounds.Right - selectionBounds.Right)} · " +
                  $"B {FormatDevNumber(parentBounds.Bottom - selectionBounds.Bottom)} · " +
                  $"L {FormatDevNumber(selectionBounds.Left - parentBounds.Left)}";
        return true;
    }

    private bool TryGetContainerBounds(SvgElement parent, out Shim.SKRect bounds)
    {
        bounds = default;

        if (parent is SvgVisualElement visual && TryGetElementBounds([visual], out bounds))
        {
            return true;
        }

        if (_document is not null)
        {
            var viewBox = _document.ViewBox;
            bounds = new Shim.SKRect(
                viewBox.MinX,
                viewBox.MinY,
                viewBox.MinX + viewBox.Width,
                viewBox.MinY + viewBox.Height);
            return viewBox.Width > 0 && viewBox.Height > 0;
        }

        return false;
    }

    private string BuildSelectionSvgMarkup(IReadOnlyList<SvgVisualElement> selectionRoots)
    {
        if (selectionRoots.Count == 0)
        {
            return _document is null ? string.Empty : _documentService.GetXml(_document);
        }

        if (selectionRoots.Count == 1)
        {
            return selectionRoots[0].GetXML();
        }

        var wrapper = new SvgGroup
        {
            ID = "selection"
        };

        foreach (var element in selectionRoots)
        {
            wrapper.Children.Add((SvgElement)element.DeepCopy());
        }

        return wrapper.GetXML();
    }

    private string BuildCssSnippet(
        SvgVisualElement? primaryElement,
        IReadOnlyList<SvgVisualElement> selectionRoots,
        Shim.SKRect? selectionBounds)
    {
        if (selectionBounds is null)
        {
            return ":root {\n  /* Select a layer to generate a focused CSS snippet. */\n}";
        }

        var selector = selectionRoots.Count > 1
            ? ".selection-group"
            : $".{BuildCssClassName(primaryElement)}";

        var lines = new List<string>
        {
            $"{selector} {{",
            "  position: absolute;",
            $"  left: {FormatDevNumber(selectionBounds.Value.Left)}px;",
            $"  top: {FormatDevNumber(selectionBounds.Value.Top)}px;",
            $"  width: {FormatDevNumber(selectionBounds.Value.Width)}px;",
            $"  height: {FormatDevNumber(selectionBounds.Value.Height)}px;"
        };

        if (primaryElement is not null)
        {
            if (TryGetCssColor(primaryElement.Fill, primaryElement.FillOpacity * primaryElement.Opacity, out var fill))
            {
                lines.Add($"  background: {fill};");
            }

            if (TryGetCssColor(primaryElement.Stroke, primaryElement.StrokeOpacity * primaryElement.Opacity, out var stroke))
            {
                lines.Add($"  border: {FormatDevNumber(primaryElement.StrokeWidth.Value)}px solid {stroke};");
            }

            if (primaryElement.Opacity < 0.999f)
            {
                lines.Add($"  opacity: {FormatDevNumber(primaryElement.Opacity)};");
            }

            if (primaryElement is SvgRectangle rect && (rect.CornerRadiusX.Value > 0f || rect.CornerRadiusY.Value > 0f))
            {
                lines.Add($"  border-radius: {FormatDevNumber(rect.CornerRadiusX.Value)}px / {FormatDevNumber(rect.CornerRadiusY.Value)}px;");
            }
            else if (primaryElement is SvgEllipse)
            {
                lines.Add("  border-radius: 9999px;");
            }

            if (primaryElement is SvgTextBase text)
            {
                if (!string.IsNullOrWhiteSpace(text.FontFamily))
                {
                    lines.Add($"  font-family: \"{text.FontFamily}\";");
                }

                if (text.FontSize.Value > 0f)
                {
                    lines.Add($"  font-size: {FormatDevNumber(text.FontSize.Value)}px;");
                }

                lines.Add($"  color: {(TryGetCssColor(text.Fill, text.FillOpacity * text.Opacity, out var textColor) ? textColor : "#111111")};");
            }
        }

        lines.Add("}");
        return string.Join(Environment.NewLine, lines);
    }

    private string BuildUnoXamlSnippet(Shim.SKRect? selectionBounds, string svgMarkup)
    {
        var width = selectionBounds?.Width ?? Math.Max((float)(_document?.ViewBox.Width ?? 320f), 1f);
        var height = selectionBounds?.Height ?? Math.Max((float)(_document?.ViewBox.Height ?? 240f), 1f);

        return $"""
using:Uno.Svg.Skia

<svg:Svg
    Width="{FormatDevNumber(width)}"
    Height="{FormatDevNumber(height)}"
    Stretch="Uniform"
    Source="{SecurityElement.Escape(svgMarkup) ?? string.Empty}" />
""";
    }

    private string BuildCSharpSnippet(Shim.SKRect? selectionBounds, string svgMarkup)
    {
        var width = selectionBounds?.Width ?? Math.Max((float)(_document?.ViewBox.Width ?? 320f), 1f);
        var height = selectionBounds?.Height ?? Math.Max((float)(_document?.ViewBox.Height ?? 240f), 1f);

        return $@"using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using Uno.Svg.Skia;

var svgView = new Svg
{{
    Width = {FormatDevNumber(width)},
    Height = {FormatDevNumber(height)},
    Stretch = Stretch.Uniform,
    Source = {ToVerbatimStringLiteral(svgMarkup)}
}};";
    }

    private static string BuildCssClassName(SvgVisualElement? element)
    {
        if (element is null)
        {
            return "svg-layer";
        }

        var seed = string.IsNullOrWhiteSpace(element.ID)
            ? GetElementTypeLabel(element).ToLowerInvariant()
            : element.ID!;

        var builder = new StringBuilder(seed.Length);
        foreach (var character in seed)
        {
            builder.Append(char.IsLetterOrDigit(character) ? char.ToLowerInvariant(character) : '-');
        }

        var value = builder.ToString().Trim('-');
        return string.IsNullOrWhiteSpace(value) ? "svg-layer" : value;
    }

    private static string BuildDevElementLabel(SvgElement element)
    {
        var typeLabel = GetElementTypeLabel(element);
        return string.IsNullOrWhiteSpace(element.ID)
            ? typeLabel
            : $"{typeLabel} #{element.ID}";
    }

    private static string FormatDevNumber(double value)
    {
        return value.ToString("0.##", CultureInfo.InvariantCulture);
    }

    private static string ToVerbatimStringLiteral(string value)
    {
        return $"\"{value.Replace("\"", "\"\"", StringComparison.Ordinal)}\"";
    }

    private static bool TryGetCssColor(SvgPaintServer? paint, float opacity, out string color)
    {
        if (paint is SvgColourServer colourServer
            && !ReferenceEquals(paint, SvgPaintServer.None)
            && !ReferenceEquals(paint, SvgPaintServer.NotSet)
            && !ReferenceEquals(paint, SvgPaintServer.Inherit))
        {
            color = opacity >= 0.999f
                ? $"#{colourServer.Colour.R:X2}{colourServer.Colour.G:X2}{colourServer.Colour.B:X2}"
                : $"rgba({colourServer.Colour.R}, {colourServer.Colour.G}, {colourServer.Colour.B}, {opacity.ToString("0.###", CultureInfo.InvariantCulture)})";
            return true;
        }

        color = string.Empty;
        return false;
    }

    private static void ReplaceCollection<T>(ObservableCollection<T> target, IReadOnlyList<T> items)
    {
        target.Clear();
        foreach (var item in items)
        {
            target.Add(item);
        }
    }
}
