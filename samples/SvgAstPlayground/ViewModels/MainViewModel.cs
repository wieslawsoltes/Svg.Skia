using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using ShimSkiaSharp;
using SkiaSharp;
using Svg.Ast;
using Svg.Ast.Emit;
using Svg.Model.Ast;
using Svg.Skia;

namespace SvgAstPlayground.ViewModels;

public sealed class MainViewModel : INotifyPropertyChanged, IDisposable
{
    private static readonly TimeSpan s_updateDelay = TimeSpan.FromMilliseconds(350);
    private readonly DispatcherTimer _updateTimer;
    private readonly SkiaModel _skiaModel = new(new SKSvgSettings());
    private string _svgText;
    private string _summaryText = "Ready";
    private string _sourceLabel = "Inline sample";
    private Bitmap? _previewImage;
    private int _lastNodeCount;
    private bool _isDisposed;
    private readonly List<AstTreeNodeViewModel> _flatAstNodes = new();
    private AstTreeNodeViewModel? _selectedAstNode;
    private bool _suppressEditorSelectionNotifications;

    public MainViewModel()
    {
        _svgText = global::SvgAstPlayground.SampleContent.DefaultSvg;
        _updateTimer = new DispatcherTimer { Interval = s_updateDelay };
        _updateTimer.Tick += OnUpdateTimerTick;
        ProcessSvg();
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public ObservableCollection<AstTreeNodeViewModel> AstNodes { get; } = new();

    public ObservableCollection<SvgDiagnosticViewModel> Diagnostics { get; } = new();

    public event EventHandler<TextSelectionRequestedEventArgs>? EditorSelectionRequested;

    public string UpdateDelayDescription => $"{s_updateDelay.TotalMilliseconds:0} ms";

    public string SvgText
    {
        get => _svgText;
        set
        {
            if (SetField(ref _svgText, value))
            {
                ScheduleUpdate();
            }
        }
    }

    public string SummaryText
    {
        get => _summaryText;
        private set => SetField(ref _summaryText, value);
    }

    public Bitmap? PreviewImage
    {
        get => _previewImage;
        private set
        {
            if (ReferenceEquals(_previewImage, value))
            {
                return;
            }

            _previewImage?.Dispose();
            _previewImage = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(HasPreview));
            OnPropertyChanged(nameof(ShowPreviewPlaceholder));
        }
    }

    public bool HasPreview => _previewImage is not null;

    public bool ShowPreviewPlaceholder => !HasPreview;

    public string SourceLabel => _sourceLabel;

    public AstTreeNodeViewModel? SelectedAstNode
    {
        get => _selectedAstNode;
        set
        {
            if (SetField(ref _selectedAstNode, value) && !_suppressEditorSelectionNotifications && value?.Node is SvgAstNode node)
            {
                EditorSelectionRequested?.Invoke(this, new TextSelectionRequestedEventArgs(node.Start, node.Length));
            }
        }
    }

    public void ResetToDefault()
    {
        LoadFromContent(global::SvgAstPlayground.SampleContent.DefaultSvg, "Inline sample");
    }

    public async Task LoadFromFileAsync(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
        {
            return;
        }

        try
        {
            var content = await File.ReadAllTextAsync(path).ConfigureAwait(true);
            LoadFromContent(content, Path.GetFileName(path));
        }
        catch (Exception ex)
        {
            SummaryText = $"Failed to load '{Path.GetFileName(path)}': {ex.Message}";
        }
    }

    public void LoadFromContent(string content, string? label = null)
    {
        if (content is null)
        {
            throw new ArgumentNullException(nameof(content));
        }

        _sourceLabel = string.IsNullOrWhiteSpace(label) ? "Inline sample" : label!;
        SvgText = content;
    }

    public void ReportLoadError(string message)
    {
        SummaryText = message;
    }

    public void UpdateSelectionFromEditor(int selectionStart, int selectionLength)
    {
        if (_isDisposed)
        {
            return;
        }

        var node = FindNodeForPosition(selectionStart);
        _suppressEditorSelectionNotifications = true;
        SelectedAstNode = node;
        _suppressEditorSelectionNotifications = false;
    }

    public void Dispose()
    {
        if (_isDisposed)
        {
            return;
        }

        _isDisposed = true;
        _updateTimer.Tick -= OnUpdateTimerTick;
        _updateTimer.Stop();
        PreviewImage = null;
    }

    private void OnUpdateTimerTick(object? sender, EventArgs e)
    {
        _updateTimer.Stop();
        ProcessSvg();
    }

    private void ScheduleUpdate()
    {
        _updateTimer.Stop();
        _updateTimer.Start();
    }

    private void ProcessSvg()
    {
        if (_isDisposed)
        {
            return;
        }

        try
        {
            var source = SvgSourceText.FromString(_svgText ?? string.Empty, normalizeLineEndings: true);
            var document = SvgAstBuilder.Build(source);
            var renderResult = SvgAstRenderService.Render(document);

            UpdateAst(document.RootElement);
            UpdateDiagnostics(document, renderResult, source);
            UpdatePreview(renderResult.Output);
        }
        catch (Exception ex)
        {
            Diagnostics.Clear();
            AstNodes.Clear();
            PreviewImage = null;
            _lastNodeCount = 0;
            SummaryText = $"Processing failed: {ex.Message}";
        }
    }

    private void UpdateAst(SvgAstElement? root)
    {
        var nodes = new List<AstTreeNodeViewModel>();
        var nodeCount = 0;

        if (root is not null)
        {
            nodes.Add(CreateNode(root, ref nodeCount));
        }

        ReplaceCollection(AstNodes, nodes);
        _flatAstNodes.Clear();
        FlattenAstNodes(nodes);
        _suppressEditorSelectionNotifications = true;
        SelectedAstNode = null;
        _suppressEditorSelectionNotifications = false;
        _lastNodeCount = nodeCount;
    }

    private void UpdateDiagnostics(
        SvgAstDocument document,
        SvgAstEmissionResult<ShimSkiaSharp.SKPicture?> renderResult,
        SvgSourceText source)
    {
        var diagnostics = new List<SvgDiagnosticViewModel>();

        foreach (var diagnostic in document.Diagnostics)
        {
            var position = source.GetLinePosition(diagnostic.Start);
            diagnostics.Add(new SvgDiagnosticViewModel("Parser", diagnostic, position));
        }

        foreach (var diagnostic in renderResult.Diagnostics.Skip(document.Diagnostics.Length))
        {
            var position = source.GetLinePosition(diagnostic.Start);
            diagnostics.Add(new SvgDiagnosticViewModel("Renderer", diagnostic, position));
        }

        ReplaceCollection(Diagnostics, diagnostics);
        var errorCount = diagnostics.Count(d => d.IsError);
        SummaryText = $"{_sourceLabel}: {_lastNodeCount} AST nodes · {diagnostics.Count} diagnostics ({errorCount} errors)";
    }

    private void UpdatePreview(ShimSkiaSharp.SKPicture? picture)
    {
        PreviewImage = CreateBitmap(picture);
    }

    private Bitmap? CreateBitmap(ShimSkiaSharp.SKPicture? picture)
    {
        using var skPicture = _skiaModel.ToSKPicture(picture);
        if (skPicture is null)
        {
            return null;
        }

        var rect = skPicture.CullRect;
        var width = Math.Max(1, (int)Math.Ceiling(rect.Width));
        var height = Math.Max(1, (int)Math.Ceiling(rect.Height));
        if (width <= 0 || height <= 0)
        {
            return null;
        }

        var info = new SKImageInfo(width, height, SKImageInfo.PlatformColorType, SKAlphaType.Premul);
        using var surface = SKSurface.Create(info);
        var canvas = surface.Canvas;
        canvas.Clear(SKColors.Transparent);
        canvas.DrawPicture(skPicture);
        canvas.Flush();
        using var image = surface.Snapshot();
        using var data = image.Encode(SKEncodedImageFormat.Png, 90);
        using var stream = new MemoryStream();
        data.SaveTo(stream);
        stream.Position = 0;
        return new Bitmap(stream);
    }

    private static AstTreeNodeViewModel CreateNode(SvgAstElement element, ref int nodeCount)
    {
        nodeCount++;
        var details = string.IsNullOrEmpty(element.Name.NamespaceUri) ? null : element.Name.NamespaceUri;
        var node = new AstTreeNodeViewModel($"<{element.Name.LocalName}>", details, element);

        foreach (var attribute in element.Attributes)
        {
            var attrNode = new AstTreeNodeViewModel($"@{attribute.Name.LocalName}", attribute.GetValueText(), attribute);
            node.AddChild(attrNode);
            nodeCount++;
        }

        foreach (var child in element.Children)
        {
            switch (child)
            {
                case SvgAstElement childElement:
                    var childNode = CreateNode(childElement, ref nodeCount);
                    node.AddChild(childNode);
                    break;
                case SvgAstText textNode:
                    node.AddChild(new AstTreeNodeViewModel("Text", $"'{Sanitize(textNode.ToString())}'", textNode));
                    nodeCount++;
                    break;
                case SvgAstComment comment:
                    node.AddChild(new AstTreeNodeViewModel("Comment", comment.ToString(), comment));
                    nodeCount++;
                    break;
                case SvgAstProcessingInstruction instruction:
                    node.AddChild(new AstTreeNodeViewModel("Processing Instruction", $"{instruction.Target} {instruction.GetValueText()}", instruction));
                    nodeCount++;
                    break;
                case SvgAstCData cdata:
                    node.AddChild(new AstTreeNodeViewModel("CDATA", cdata.ToString(), cdata));
                    nodeCount++;
                    break;
            }
        }

        return node;
    }

    private static string Sanitize(string value)
    {
        return value
            .Replace("\r", "\\r", StringComparison.Ordinal)
            .Replace("\n", "\\n", StringComparison.Ordinal);
    }

    private static void ReplaceCollection<T>(ObservableCollection<T> collection, IEnumerable<T> items)
    {
        collection.Clear();
        foreach (var item in items)
        {
            collection.Add(item);
        }
    }

    private AstTreeNodeViewModel? FindNodeForPosition(int position)
    {
        if (_flatAstNodes.Count == 0)
        {
            return null;
        }

        var clamped = Math.Clamp(position, 0, Math.Max(0, _svgText?.Length ?? 0));
        AstTreeNodeViewModel? best = null;

        foreach (var candidate in _flatAstNodes)
        {
            var astNode = candidate.Node;
            if (astNode is null)
            {
                continue;
            }

            var start = astNode.Start;
            var endExclusive = astNode.Start + Math.Max(astNode.Length, 1);
            if (clamped < start || clamped >= endExclusive)
            {
                continue;
            }

            if (best is null || astNode.Length < (best.Node?.Length ?? int.MaxValue))
            {
                best = candidate;
            }
        }

        return best;
    }

    private void FlattenAstNodes(IEnumerable<AstTreeNodeViewModel> nodes)
    {
        foreach (var node in nodes)
        {
            if (node.Node is not null)
            {
                _flatAstNodes.Add(node);
            }

            if (node.Children.Count > 0)
            {
                FlattenAstNodes(node.Children);
            }
        }
    }

    private bool SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return false;
        }

        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
