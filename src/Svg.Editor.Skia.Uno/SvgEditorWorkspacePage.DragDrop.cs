using System.Collections.Generic;
using System.IO;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using Svg;
using Svg.Editor.Skia.Uno.Controls;
using Svg.Editor.Skia.Uno.Models;
using Svg.Model.Drawables;
using Windows.ApplicationModel.DataTransfer;
using Windows.Foundation;
using Windows.Storage;
using Windows.UI;
using Shim = ShimSkiaSharp;
using SK = SkiaSharp;

namespace Svg.Editor.Skia.Uno;

public partial class SvgEditorWorkspacePage
{
    private const float MaxDroppedAssetDimension = 420f;
    private const float MinDroppedAssetDimension = 32f;
    private const float DroppedAssetCascadeOffset = 28f;

    private static readonly HashSet<string> SupportedImageDropExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".png",
        ".jpg",
        ".jpeg",
        ".webp",
        ".bmp",
        ".gif"
    };

    private static readonly HashSet<string> SupportedSvgDropExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".svg"
    };

    private enum CanvasDropKind
    {
        None,
        Component,
        SvgFile,
        ImageFile,
        Unsupported
    }

    private sealed class CanvasDropItem
    {
        public required StorageFile File { get; init; }

        public required CanvasDropKind Kind { get; init; }

        public required float Width { get; init; }

        public required float Height { get; init; }

        public required string Title { get; init; }

        public required string Subtitle { get; init; }

        public required string Href { get; init; }

        public string? SvgSource { get; init; }
    }

    private sealed class CanvasDropPayload
    {
        public CanvasDropKind Kind { get; set; }

        public EditorComponentItem? Component { get; init; }

        public List<CanvasDropItem> Items { get; init; } = [];
    }

    private Canvas? _canvasDropPreviewLayer;
    private Border? _canvasDropPreviewHost;
    private Border? _canvasDropTargetHighlight;
    private Border? _canvasDropHintBadge;
    private TextBlock? _canvasDropHintText;
    private SvgSymbolPreview? _canvasDropComponentPreview;
    private global::Uno.Svg.Skia.Svg? _canvasDropSvgPreview;
    private Image? _canvasDropImagePreview;
    private StackPanel? _canvasDropGenericPreview;
    private TextBlock? _canvasDropGenericTitle;
    private TextBlock? _canvasDropGenericSubtitle;
    private CanvasDropPayload? _canvasDropPayload;
    private SvgUse? _canvasDropSwapTarget;
    private int _canvasDropSessionId;

    private void InitializeCanvasDropPreview()
    {
        if (_canvasDropPreviewLayer is not null)
        {
            return;
        }

        _canvasDropTargetHighlight = new Border
        {
            Visibility = Visibility.Collapsed,
            IsHitTestVisible = false,
            CornerRadius = new CornerRadius(18),
            BorderBrush = new SolidColorBrush(Color.FromArgb(255, 13, 153, 255)),
            BorderThickness = new Thickness(2),
            Background = new SolidColorBrush(Color.FromArgb(22, 13, 153, 255))
        };

        _canvasDropComponentPreview = new SvgSymbolPreview
        {
            Visibility = Visibility.Collapsed,
            IsHitTestVisible = false,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
            PreviewBackground = new SolidColorBrush(Color.FromArgb(0, 0, 0, 0)),
            PreviewCornerRadius = new CornerRadius(0)
        };

        _canvasDropSvgPreview = new global::Uno.Svg.Skia.Svg
        {
            Visibility = Visibility.Collapsed,
            IsHitTestVisible = false,
            Stretch = Stretch.Fill
        };

        _canvasDropImagePreview = new Image
        {
            Visibility = Visibility.Collapsed,
            IsHitTestVisible = false,
            Stretch = Stretch.Fill
        };

        _canvasDropGenericTitle = new TextBlock
        {
            Style = (Style)Application.Current.Resources["ShellTitleStyle"],
            FontSize = 14,
            TextWrapping = TextWrapping.WrapWholeWords
        };

        _canvasDropGenericSubtitle = new TextBlock
        {
            Style = (Style)Application.Current.Resources["SectionCaptionStyle"],
            FontSize = 12,
            TextWrapping = TextWrapping.WrapWholeWords
        };

        _canvasDropGenericPreview = new StackPanel
        {
            Visibility = Visibility.Collapsed,
            IsHitTestVisible = false,
            Spacing = 4,
            Padding = new Thickness(16, 14, 16, 14),
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            Children =
            {
                _canvasDropGenericTitle,
                _canvasDropGenericSubtitle
            }
        };

        var previewGrid = new Grid();
        previewGrid.Children.Add(_canvasDropComponentPreview);
        previewGrid.Children.Add(_canvasDropSvgPreview);
        previewGrid.Children.Add(_canvasDropImagePreview);
        previewGrid.Children.Add(_canvasDropGenericPreview);

        _canvasDropPreviewHost = new Border
        {
            Visibility = Visibility.Collapsed,
            IsHitTestVisible = false,
            CornerRadius = new CornerRadius(16),
            BorderBrush = new SolidColorBrush(Color.FromArgb(255, 13, 153, 255)),
            BorderThickness = new Thickness(1.5),
            Background = new SolidColorBrush(Color.FromArgb(184, 251, 252, 255)),
            Child = previewGrid
        };

        _canvasDropHintText = new TextBlock
        {
            Style = (Style)Application.Current.Resources["ShellTitleStyle"],
            FontSize = 11.5,
            Foreground = new SolidColorBrush(Color.FromArgb(255, 255, 255, 255)),
            Text = string.Empty
        };

        _canvasDropHintBadge = new Border
        {
            Visibility = Visibility.Collapsed,
            IsHitTestVisible = false,
            CornerRadius = new CornerRadius(12),
            Background = new SolidColorBrush(Color.FromArgb(255, 13, 153, 255)),
            Padding = new Thickness(10, 5, 10, 5),
            Child = _canvasDropHintText
        };

        _canvasDropPreviewLayer = new Canvas
        {
            Visibility = Visibility.Collapsed,
            IsHitTestVisible = false,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch
        };
        _canvasDropPreviewLayer.Children.Add(_canvasDropTargetHighlight);
        _canvasDropPreviewLayer.Children.Add(_canvasDropPreviewHost);
        _canvasDropPreviewLayer.Children.Add(_canvasDropHintBadge);

        EditorSurfaceHost.Children.Add(_canvasDropPreviewLayer);
    }

    protected async void OnCanvasDragEnter(object sender, DragEventArgs e)
    {
        InitializeCanvasDropPreview();
        var sessionId = ++_canvasDropSessionId;
        await PrepareCanvasDropPayloadAsync(e.DataView, sessionId);
        UpdateCanvasDropPreview(e);
        e.Handled = true;
    }

    protected void OnCanvasDragOver(object sender, DragEventArgs e)
    {
        UpdateCanvasDropPreview(e);
        e.Handled = true;
    }

    protected void OnCanvasDragLeave(object sender, DragEventArgs e)
    {
        ClearCanvasDropPreview();
        e.Handled = true;
    }

    protected async void OnCanvasDrop(object sender, DragEventArgs e)
    {
        InitializeCanvasDropPreview();
        if (_canvasDropPayload is null)
        {
            await PrepareCanvasDropPayloadAsync(e.DataView, ++_canvasDropSessionId);
        }

        if (_canvasDropPayload is null || !TryGetCanvasDropPicturePoint(e, out var picturePoint, out _))
        {
            ClearCanvasDropPreview();
            e.AcceptedOperation = DataPackageOperation.None;
            e.Handled = true;
            return;
        }

        switch (_canvasDropPayload.Kind)
        {
            case CanvasDropKind.Component:
                HandleDroppedComponent(picturePoint);
                e.AcceptedOperation = DataPackageOperation.Copy;
                break;
            case CanvasDropKind.SvgFile:
            case CanvasDropKind.ImageFile:
                HandleDroppedFiles(_canvasDropPayload.Items, picturePoint);
                e.AcceptedOperation = DataPackageOperation.Copy;
                break;
            default:
                e.AcceptedOperation = DataPackageOperation.None;
                break;
        }

        ClearCanvasDropPreview();
        e.Handled = true;
    }

    private async Task PrepareCanvasDropPayloadAsync(DataPackageView dataView, int sessionId)
    {
        CanvasDropPayload? payload = null;

        if (TryGetDraggedComponent(dataView, out var component))
        {
            payload = new CanvasDropPayload
            {
                Kind = CanvasDropKind.Component,
                Component = component
            };
        }
        else if (dataView.Contains(StandardDataFormats.Text))
        {
            var text = await dataView.GetTextAsync();
            if (TryGetDraggedComponent(text, out component))
            {
                payload = new CanvasDropPayload
                {
                    Kind = CanvasDropKind.Component,
                    Component = component
                };
            }
        }
        else if (dataView.Contains(StandardDataFormats.StorageItems))
        {
            var items = await dataView.GetStorageItemsAsync();
            var files = items.OfType<StorageFile>().ToList();
            if (files.Count > 0)
            {
                payload = await CreateCanvasDropPayloadAsync(files);
            }
        }

        if (sessionId != _canvasDropSessionId)
        {
            return;
        }

        _canvasDropPayload = payload;
    }

    private async Task<CanvasDropPayload> CreateCanvasDropPayloadAsync(IReadOnlyList<StorageFile> files)
    {
        var payload = new CanvasDropPayload();
        foreach (var file in files)
        {
            var item = await CreateCanvasDropItemAsync(file);
            if (item is not null)
            {
                payload.Items.Add(item);
            }
        }

        payload.Kind = payload.Items.Count == 0
            ? CanvasDropKind.Unsupported
            : payload.Items[0].Kind;

        return payload;
    }

    private async Task<CanvasDropItem?> CreateCanvasDropItemAsync(StorageFile file)
    {
        var extension = Path.GetExtension(file.Name);
        if (SupportedSvgDropExtensions.Contains(extension))
        {
            var svg = await FileIO.ReadTextAsync(file);
            var document = _documentService.FromSvg(svg);
            var (width, height) = GetDroppedSvgSize(document);
            return new CanvasDropItem
            {
                File = file,
                Kind = CanvasDropKind.SvgFile,
                Width = width,
                Height = height,
                Title = file.DisplayName,
                Subtitle = "SVG file",
                Href = BuildStorageFileHref(file),
                SvgSource = svg
            };
        }

        if (SupportedImageDropExtensions.Contains(extension))
        {
            var (width, height) = GetDroppedRasterSize(file.Path);
            return new CanvasDropItem
            {
                File = file,
                Kind = CanvasDropKind.ImageFile,
                Width = width,
                Height = height,
                Title = file.DisplayName,
                Subtitle = $"{extension.TrimStart('.').ToUpperInvariant()} image",
                Href = BuildStorageFileHref(file)
            };
        }

        return null;
    }

    private void UpdateCanvasDropPreview(DragEventArgs e)
    {
        if (_canvasDropPayload is null || !TryGetCanvasDropPicturePoint(e, out var picturePoint, out var viewPoint))
        {
            HideCanvasDropPreviewVisuals();
            e.AcceptedOperation = DataPackageOperation.None;
            return;
        }

        if (_canvasDropPayload.Kind == CanvasDropKind.Unsupported)
        {
            HideCanvasDropPreviewVisuals();
            e.AcceptedOperation = DataPackageOperation.None;
            return;
        }

        e.AcceptedOperation = DataPackageOperation.Copy;
        switch (_canvasDropPayload.Kind)
        {
            case CanvasDropKind.Component:
                UpdateComponentDropPreview(_canvasDropPayload.Component, picturePoint, viewPoint);
                break;
            case CanvasDropKind.SvgFile:
            case CanvasDropKind.ImageFile:
                UpdateFileDropPreview(_canvasDropPayload.Items, picturePoint, viewPoint);
                break;
            default:
                HideCanvasDropPreviewVisuals();
                break;
        }
    }

    private void UpdateComponentDropPreview(EditorComponentItem? component, Shim.SKPoint picturePoint, Point viewPoint)
    {
        if (component is null)
        {
            HideCanvasDropPreviewVisuals();
            return;
        }

        if (GetVisualHit(viewPoint) is SvgUse swapTarget)
        {
            _canvasDropSwapTarget = swapTarget;
            if (TryGetElementViewBounds(swapTarget, out var swapRect))
            {
                ShowCanvasDropPreview(
                    swapRect,
                    swapRect,
                    "Drop to swap instance",
                    component.Symbol,
                    svgSource: null,
                    imageHref: null,
                    title: component.Name,
                    subtitle: component.SourceSubtitle);
                return;
            }
        }

        _canvasDropSwapTarget = null;
        var parent = GetCreationParent(picturePoint);
        if (!TryMapPointToElementLocal(parent, picturePoint, out var localCenter))
        {
            localCenter = picturePoint;
        }

        var (width, height) = GetComponentSize(component.Symbol);
        var localRect = SK.SKRect.Create(localCenter.X - (width / 2f), localCenter.Y - (height / 2f), width, height);
        if (!TryMapLocalRectToView(parent, localRect, out var previewRect))
        {
            previewRect = new Rect(
                viewPoint.X - (width / 2f),
                viewPoint.Y - (height / 2f),
                width,
                height);
        }

        Rect? highlightRect = null;
        if (TryGetFrameHighlightBounds(parent, out var frameRect))
        {
            highlightRect = frameRect;
        }

        ShowCanvasDropPreview(
            previewRect,
            highlightRect,
            "Drop to place component",
            component.Symbol,
            svgSource: null,
            imageHref: null,
            title: component.Name,
            subtitle: component.SourceSubtitle);
    }

    private void UpdateFileDropPreview(IReadOnlyList<CanvasDropItem> items, Shim.SKPoint picturePoint, Point viewPoint)
    {
        if (items.Count == 0)
        {
            HideCanvasDropPreviewVisuals();
            return;
        }

        var item = items[0];
        var parent = GetCreationParent(picturePoint);
        if (!TryMapPointToElementLocal(parent, picturePoint, out var localCenter))
        {
            localCenter = picturePoint;
        }

        var localRect = SK.SKRect.Create(localCenter.X - (item.Width / 2f), localCenter.Y - (item.Height / 2f), item.Width, item.Height);
        if (!TryMapLocalRectToView(parent, localRect, out var previewRect))
        {
            previewRect = new Rect(
                viewPoint.X - (item.Width / 2f),
                viewPoint.Y - (item.Height / 2f),
                item.Width,
                item.Height);
        }

        Rect? highlightRect = null;
        if (TryGetFrameHighlightBounds(parent, out var frameRect))
        {
            highlightRect = frameRect;
        }

        var hint = items.Count == 1 ? "Drop to import asset" : $"Drop to import {items.Count} assets";
        var subtitle = items.Count == 1 ? item.Subtitle : $"{item.Subtitle} + {items.Count - 1} more";

        ShowCanvasDropPreview(
            previewRect,
            highlightRect,
            hint,
            symbol: null,
            svgSource: item.Kind == CanvasDropKind.SvgFile ? item.SvgSource : null,
            imageHref: item.Kind == CanvasDropKind.ImageFile ? item.Href : null,
            title: item.Title,
            subtitle: subtitle);
    }

    private void ShowCanvasDropPreview(
        Rect previewRect,
        Rect? highlightRect,
        string hint,
        SvgSymbol? symbol,
        string? svgSource,
        string? imageHref,
        string title,
        string subtitle)
    {
        if (_canvasDropPreviewLayer is null
            || _canvasDropPreviewHost is null
            || _canvasDropTargetHighlight is null
            || _canvasDropHintBadge is null
            || _canvasDropHintText is null
            || _canvasDropComponentPreview is null
            || _canvasDropSvgPreview is null
            || _canvasDropImagePreview is null
            || _canvasDropGenericPreview is null
            || _canvasDropGenericTitle is null
            || _canvasDropGenericSubtitle is null)
        {
            return;
        }

        _canvasDropPreviewLayer.Visibility = Visibility.Visible;
        _canvasDropPreviewHost.Visibility = Visibility.Visible;
        _canvasDropHintBadge.Visibility = Visibility.Visible;
        _canvasDropHintText.Text = hint;

        Canvas.SetLeft(_canvasDropPreviewHost, previewRect.X);
        Canvas.SetTop(_canvasDropPreviewHost, previewRect.Y);
        _canvasDropPreviewHost.Width = Math.Max(previewRect.Width, 1);
        _canvasDropPreviewHost.Height = Math.Max(previewRect.Height, 1);

        var badgeLeft = previewRect.X;
        var badgeTop = Math.Max(8, previewRect.Y - 32);
        Canvas.SetLeft(_canvasDropHintBadge, badgeLeft);
        Canvas.SetTop(_canvasDropHintBadge, badgeTop);

        if (highlightRect is { } targetRect)
        {
            _canvasDropTargetHighlight.Visibility = Visibility.Visible;
            Canvas.SetLeft(_canvasDropTargetHighlight, targetRect.X);
            Canvas.SetTop(_canvasDropTargetHighlight, targetRect.Y);
            _canvasDropTargetHighlight.Width = Math.Max(targetRect.Width, 1);
            _canvasDropTargetHighlight.Height = Math.Max(targetRect.Height, 1);
        }
        else
        {
            _canvasDropTargetHighlight.Visibility = Visibility.Collapsed;
        }

        _canvasDropComponentPreview.Visibility = symbol is null ? Visibility.Collapsed : Visibility.Visible;
        _canvasDropSvgPreview.Visibility = svgSource is null ? Visibility.Collapsed : Visibility.Visible;
        _canvasDropImagePreview.Visibility = imageHref is null ? Visibility.Collapsed : Visibility.Visible;
        _canvasDropGenericPreview.Visibility = symbol is null && svgSource is null && imageHref is null
            ? Visibility.Visible
            : Visibility.Collapsed;

        _canvasDropComponentPreview.Symbol = symbol;
        _canvasDropSvgPreview.Source = svgSource;
        _canvasDropImagePreview.Source = imageHref is null ? null : CreateBitmapImage(imageHref);
        _canvasDropGenericTitle.Text = title;
        _canvasDropGenericSubtitle.Text = subtitle;
    }

    private void HideCanvasDropPreviewVisuals()
    {
        if (_canvasDropPreviewLayer is null
            || _canvasDropPreviewHost is null
            || _canvasDropTargetHighlight is null
            || _canvasDropHintBadge is null)
        {
            return;
        }

        _canvasDropPreviewLayer.Visibility = Visibility.Collapsed;
        _canvasDropPreviewHost.Visibility = Visibility.Collapsed;
        _canvasDropTargetHighlight.Visibility = Visibility.Collapsed;
        _canvasDropHintBadge.Visibility = Visibility.Collapsed;
    }

    private void ClearCanvasDropPreview()
    {
        HideCanvasDropPreviewVisuals();
        _canvasDropPayload = null;
        _canvasDropSwapTarget = null;
        _canvasDropSessionId++;
    }

    private void HandleDroppedComponent(Shim.SKPoint picturePoint)
    {
        if (_document is null || _canvasDropPayload?.Component is null)
        {
            return;
        }

        if (!EnsureComponentAssetImported(_canvasDropPayload.Component, out var asset))
        {
            CanvasStatus = "The dragged component asset isn't available in this file.";
            return;
        }

        SelectComponentAsset(asset, activateSymbolTool: false);

        if (_canvasDropSwapTarget is { } swapTarget)
        {
            ApplyComponentAssetToInstance(swapTarget, asset);
            RefreshDocumentVisual(rebuildOutline: true, reloadProperties: true);
            ApplySelection([swapTarget], swapTarget);
            CanvasStatus = $"Swapped the instance to {asset.Name}.";
            return;
        }

        var parent = GetCreationParent(picturePoint);
        if (!TryMapPointToElementLocal(parent, picturePoint, out var localCenter))
        {
            localCenter = picturePoint;
        }

        var (width, height) = GetComponentSize(asset.Symbol);
        var instance = CreateComponentInstance(
            asset.Symbol,
            localCenter.X - (width / 2f),
            localCenter.Y - (height / 2f),
            width,
            height);

        parent.Children.Add(instance);
        RefreshDocumentVisual(rebuildOutline: true, reloadProperties: true);
        ApplySelection([instance], instance);
        CanvasStatus = $"Inserted a new {asset.Name} instance.";
    }

    private void HandleDroppedFiles(IReadOnlyList<CanvasDropItem> items, Shim.SKPoint picturePoint)
    {
        if (_document is null)
        {
            return;
        }

        var inserted = new List<SvgVisualElement>();
        var skipped = 0;

        for (var index = 0; index < items.Count; index++)
        {
            var item = items[index];
            if (item.Kind is not (CanvasDropKind.SvgFile or CanvasDropKind.ImageFile))
            {
                skipped++;
                continue;
            }

            var targetPoint = new Shim.SKPoint(
                picturePoint.X + (index * DroppedAssetCascadeOffset),
                picturePoint.Y + (index * DroppedAssetCascadeOffset));
            var parent = GetCreationParent(targetPoint);
            if (!TryMapPointToElementLocal(parent, targetPoint, out var localCenter))
            {
                localCenter = targetPoint;
            }

            var image = new SvgImage
            {
                ID = CreateUniqueId(Path.GetFileNameWithoutExtension(item.File.Name)),
                X = new SvgUnit(SvgUnitType.User, localCenter.X - (item.Width / 2f)),
                Y = new SvgUnit(SvgUnitType.User, localCenter.Y - (item.Height / 2f)),
                Width = new SvgUnit(SvgUnitType.User, item.Width),
                Height = new SvgUnit(SvgUnitType.User, item.Height),
                Href = item.Href
            };

            parent.Children.Add(image);
            inserted.Add(image);
        }

        if (inserted.Count == 0)
        {
            CanvasStatus = skipped == 0
                ? "No supported files were dropped."
                : "The dropped files aren't supported yet.";
            return;
        }

        RefreshDocumentVisual(rebuildOutline: true, reloadProperties: true);
        ApplySelection(inserted, inserted.LastOrDefault());
        CanvasStatus = inserted.Count == 1
            ? $"Imported {inserted[0].ID ?? "asset"}."
            : $"Imported {inserted.Count} assets{(skipped > 0 ? $" and skipped {skipped} unsupported file(s)." : ".")}";
    }

    private void ApplyComponentAssetToInstance(SvgUse use, EditorComponentItem asset)
    {
        use.ReferencedElement = new Uri($"#{asset.DocumentSymbolId}", UriKind.Relative);

        if (!asset.SymbolEntry.Apply(use) && string.IsNullOrWhiteSpace(asset.DocumentSymbolId))
        {
            return;
        }

        if (use.Width.Value <= 0f || use.Height.Value <= 0f)
        {
            var (width, height) = GetComponentSize(asset.Symbol);
            use.Width = new SvgUnit(use.Width.Type, width);
            use.Height = new SvgUnit(use.Height.Type, height);
        }
    }

    private bool TryGetDraggedComponent(DataPackageView dataView, out EditorComponentItem? component)
    {
        component = null;
        if (!dataView.Properties.TryGetValue(EditorDragDropData.KindKey, out var kind)
            || !string.Equals(kind as string, EditorDragDropData.ComponentKind, StringComparison.Ordinal))
        {
            return false;
        }

        if (!dataView.Properties.TryGetValue(EditorDragDropData.ComponentAssetKey, out var assetKey)
            || assetKey is not string key)
        {
            return false;
        }

        component = ComponentAssets.FirstOrDefault(item => string.Equals(item.AssetKey, key, StringComparison.Ordinal));
        return component is not null;
    }

    private bool TryGetDraggedComponent(string? text, out EditorComponentItem? component)
    {
        component = null;
        if (string.IsNullOrWhiteSpace(text)
            || !text.StartsWith(EditorDragDropData.ComponentTextPrefix, StringComparison.Ordinal))
        {
            return false;
        }

        var assetKey = text[EditorDragDropData.ComponentTextPrefix.Length..];
        component = ComponentAssets.FirstOrDefault(item => string.Equals(item.AssetKey, assetKey, StringComparison.Ordinal));
        return component is not null;
    }

    private bool TryGetCanvasDropPicturePoint(DragEventArgs e, out Shim.SKPoint picturePoint, out Point viewPoint)
    {
        picturePoint = default;
        viewPoint = e.GetPosition(EditorSurfaceHost);
        if (!EditorSvg.TryGetPicturePoint(viewPoint, out var mapped))
        {
            return false;
        }

        picturePoint = new Shim.SKPoint(mapped.X, mapped.Y);
        return true;
    }

    private bool TryGetElementViewBounds(SvgVisualElement element, out Rect viewRect)
    {
        viewRect = default;
        if (GetDrawableBounds(element) is not { } pictureRect)
        {
            return false;
        }

        return TryMapPictureRectToView(pictureRect, out viewRect);
    }

    private bool TryGetFrameHighlightBounds(SvgElement parent, out Rect viewRect)
    {
        viewRect = default;
        if (ResolveFrameHighlightTarget(parent) is not SvgGroup frame)
        {
            return false;
        }

        if (TryGetFrameBackground(frame, out var background))
        {
            return TryMapPictureRectToView(
                SK.SKRect.Create(background.X.Value, background.Y.Value, background.Width.Value, background.Height.Value),
                out viewRect);
        }

        return TryGetElementViewBounds(frame, out viewRect);
    }

    private SvgGroup? ResolveFrameHighlightTarget(SvgElement parent)
    {
        return parent switch
        {
            SvgGroup group when IsFrameGroup(group) => group,
            SvgElement element => element.Parents.OfType<SvgGroup>().FirstOrDefault(IsFrameGroup),
            _ => null
        };
    }

    private bool TryMapLocalRectToView(SvgElement parent, SK.SKRect localRect, out Rect viewRect)
    {
        viewRect = default;
        var corners = new[]
        {
            new Shim.SKPoint(localRect.Left, localRect.Top),
            new Shim.SKPoint(localRect.Right, localRect.Top),
            new Shim.SKPoint(localRect.Right, localRect.Bottom),
            new Shim.SKPoint(localRect.Left, localRect.Bottom)
        };

        var hasPoint = false;
        var minX = 0.0;
        var minY = 0.0;
        var maxX = 0.0;
        var maxY = 0.0;

        foreach (var corner in corners)
        {
            if (!TryMapPointFromElementLocal(parent, corner, out var pictureCorner)
                || !EditorSvg.TryGetViewPoint(pictureCorner, out var viewCorner))
            {
                return false;
            }

            if (!hasPoint)
            {
                minX = maxX = viewCorner.X;
                minY = maxY = viewCorner.Y;
                hasPoint = true;
                continue;
            }

            minX = Math.Min(minX, viewCorner.X);
            minY = Math.Min(minY, viewCorner.Y);
            maxX = Math.Max(maxX, viewCorner.X);
            maxY = Math.Max(maxY, viewCorner.Y);
        }

        if (!hasPoint)
        {
            return false;
        }

        viewRect = new Rect(minX, minY, Math.Max(maxX - minX, 1.0), Math.Max(maxY - minY, 1.0));
        return true;
    }

    private bool TryMapPointFromElementLocal(SvgElement element, Shim.SKPoint localPoint, out Shim.SKPoint picturePoint)
    {
        picturePoint = localPoint;
        if (_document is null || ReferenceEquals(element, _document))
        {
            return true;
        }

        if (EditorSvg.SkSvg?.Drawable is not DrawableBase root)
        {
            return false;
        }

        var drawable = FindDrawable(root, element);
        if (drawable is null)
        {
            return false;
        }

        picturePoint = drawable.TotalTransform.MapPoint(localPoint);
        return true;
    }

    private bool TryMapPictureRectToView(SK.SKRect pictureRect, out Rect viewRect)
    {
        viewRect = default;
        if (!EditorSvg.TryGetViewPoint(new Shim.SKPoint(pictureRect.Left, pictureRect.Top), out var topLeft)
            || !EditorSvg.TryGetViewPoint(new Shim.SKPoint(pictureRect.Right, pictureRect.Bottom), out var bottomRight))
        {
            return false;
        }

        viewRect = new Rect(
            Math.Min(topLeft.X, bottomRight.X),
            Math.Min(topLeft.Y, bottomRight.Y),
            Math.Max(Math.Abs(bottomRight.X - topLeft.X), 1.0),
            Math.Max(Math.Abs(bottomRight.Y - topLeft.Y), 1.0));
        return true;
    }

    private static (float Width, float Height) GetDroppedSvgSize(SvgDocument? document)
    {
        if (document is null)
        {
            return (240f, 180f);
        }

        var width = document.ViewBox.Width > 0f ? document.ViewBox.Width : document.Width.Value;
        var height = document.ViewBox.Height > 0f ? document.ViewBox.Height : document.Height.Value;
        return ClampDroppedAssetSize(width, height);
    }

    private static (float Width, float Height) GetDroppedRasterSize(string path)
    {
        try
        {
            using var codec = SK.SKCodec.Create(path);
            if (codec is null)
            {
                return (320f, 240f);
            }

            return ClampDroppedAssetSize(codec.Info.Width, codec.Info.Height);
        }
        catch
        {
            return (320f, 240f);
        }
    }

    private static (float Width, float Height) ClampDroppedAssetSize(float width, float height)
    {
        if (width <= 0f && height <= 0f)
        {
            return (240f, 180f);
        }

        if (width <= 0f)
        {
            width = height;
        }

        if (height <= 0f)
        {
            height = width;
        }

        var scale = Math.Min(1f, MaxDroppedAssetDimension / Math.Max(width, height));
        width *= scale;
        height *= scale;

        return (Math.Max(width, MinDroppedAssetDimension), Math.Max(height, MinDroppedAssetDimension));
    }

    private static string BuildStorageFileHref(StorageFile file)
    {
        return new Uri(file.Path, UriKind.Absolute).AbsoluteUri;
    }

    private static BitmapImage CreateBitmapImage(string href)
    {
        var image = new BitmapImage();
        image.UriSource = new Uri(href, UriKind.Absolute);
        return image;
    }
}
