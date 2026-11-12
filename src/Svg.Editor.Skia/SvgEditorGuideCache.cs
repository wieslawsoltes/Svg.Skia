using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using SkiaSharp;
using Shim = ShimSkiaSharp;

namespace Svg.Editor.Skia;

/// <summary>Retains construction guides and indexed smart guides between pointer events.</summary>
public sealed class SvgEditorGuideCache
{
    /// <summary>The existing editor limit, including the picture's six bounds guides.</summary>
    public const int MaximumSmartGuides = 6000;
    private SvgEditorController? _controller;
    private SvgDocument? _document;
    private SKPicture? _picture;
    private long _documentRevision;
    private long _renderRevision;
    private Shim.SKMatrix _rootTransform;
    private SKRect _pictureBounds;
    private IReadOnlyList<SvgEditorGuide> _guides = Array.Empty<SvgEditorGuide>();
    private SvgVisualElement[] _selection = Array.Empty<SvgVisualElement>();
    private SvgEditorGuideIndex? _index;
    private bool _snapToGuides;
    private bool _smartGuides;
    private string? _excludedGuideId;

    /// <summary>Gets guides in picture coordinates, without depending on camera or selection.</summary>
    public IReadOnlyList<SvgEditorGuide> GetGuides(SvgEditorController? controller)
    {
        Refresh(controller);
        return _guides;
    }

    /// <summary>
    /// Reuses an index until rendered geometry, selection exclusions, or guide
    /// options change. Mutations use the controller's transaction or Refresh API.
    /// </summary>
    public SvgEditorGuideIndex GetSnapIndex(SvgEditorController controller, bool snapToGuides,
        bool smartGuides, string? excludedGuideId = null)
    {
        if (controller is null)
            throw new ArgumentNullException(nameof(controller));
        Refresh(controller);
        if (_index is not null && _snapToGuides == snapToGuides && _smartGuides == smartGuides
            && _excludedGuideId == excludedGuideId && (!smartGuides || SelectionMatches(controller.SelectedElements)))
            return _index;

        var candidates = new List<SvgEditorGuide>();
        if (snapToGuides)
            for (var index = 0; index < _guides.Count; index++)
                if (_guides[index].Id != excludedGuideId)
                    candidates.Add(_guides[index]);
        if (smartGuides && controller.Document is { } document)
        {
            var selected = new HashSet<SvgVisualElement>(controller.SelectedElements);
            var smartCount = 0;
            if (controller.Renderer.Picture is { } picture)
                AddBounds(picture.CullRect, "artboard");
            foreach (var element in document.Descendants().OfType<SvgVisualElement>())
            {
                if (smartCount >= MaximumSmartGuides)
                    break;
                if (element is SvgGroup || SvgEditorController.IsEditLocked(element) || IsSelected(element) || !IsVisibleArtwork(element))
                    continue;
                if (controller.GetSceneNode(element) is not { } node || node.TransformedBounds.IsEmpty)
                    continue;
                var bounds = node.TransformedBounds;
                AddBounds(new SKRect(bounds.Left, bounds.Top, bounds.Right, bounds.Bottom), "object-" + smartCount);
            }

            bool IsSelected(SvgElement element)
            {
                for (var item = element; item is not null; item = item.Parent)
                    if (item is SvgVisualElement visual && selected.Contains(visual))
                        return true;
                return false;
            }

            void AddBounds(SKRect bounds, string id)
            {
                if (!Finite(bounds.Left) || !Finite(bounds.Top) || !Finite(bounds.Right) || !Finite(bounds.Bottom))
                    return;
                foreach (var x in new[] { bounds.Left, bounds.MidX, bounds.Right })
                    candidates.Add(new SvgEditorGuide("smart-" + id + "-x-" + x, new SKPoint(x, bounds.Top), new SKPoint(0, 1)));
                foreach (var y in new[] { bounds.Top, bounds.MidY, bounds.Bottom })
                    candidates.Add(new SvgEditorGuide("smart-" + id + "-y-" + y, new SKPoint(bounds.Left, y), new SKPoint(1, 0)));
                smartCount += 6;
            }
        }
        _index = new SvgEditorGuideIndex(candidates);
        _selection = smartGuides ? controller.SelectedElements.ToArray() : Array.Empty<SvgVisualElement>();
        _snapToGuides = snapToGuides;
        _smartGuides = smartGuides;
        _excludedGuideId = excludedGuideId;
        return _index;
    }

    /// <summary>Releases borrowed document and picture references when the view unloads.</summary>
    public void Clear()
    {
        _controller = null;
        _document = null;
        _picture = null;
        _guides = Array.Empty<SvgEditorGuide>();
        _selection = Array.Empty<SvgVisualElement>();
        _index = null;
    }

    private void Refresh(SvgEditorController? controller)
    {
        if (controller?.Document is not { } document)
        {
            Clear();
            return;
        }
        var picture = controller.Renderer.Picture;
        var root = controller.GetSceneNode(document) ?? controller.Renderer.RetainedSceneGraph?.Root;
        var rootTransform = root?.TotalTransform ?? Shim.SKMatrix.Identity;
        var pictureBounds = picture?.CullRect ?? SKRect.Empty;
        if (ReferenceEquals(_controller, controller) && ReferenceEquals(_document, document)
            && ReferenceEquals(_picture, picture) && _documentRevision == controller.DocumentRevision
            && _renderRevision == controller.RenderRevision && _rootTransform == rootTransform
            && _pictureBounds == pictureBounds)
            return;

        var guides = controller.GetGuides();
        _guides = new ReadOnlyCollection<SvgEditorGuide>(guides as IList<SvgEditorGuide> ?? new List<SvgEditorGuide>(guides));
        _controller = controller;
        _document = document;
        _picture = picture;
        _documentRevision = controller.DocumentRevision;
        _renderRevision = controller.RenderRevision;
        _rootTransform = rootTransform;
        _pictureBounds = pictureBounds;
        _index = null;
    }

    private bool SelectionMatches(IReadOnlyList<SvgVisualElement> selection)
    {
        if (_selection.Length != selection.Count)
            return false;
        for (var index = 0; index < _selection.Length; index++)
            if (!ReferenceEquals(_selection[index], selection[index]))
                return false;
        return true;
    }

    private static bool IsVisibleArtwork(SvgElement element)
    {
        for (var item = element; item is not null; item = item.Parent)
        {
            if (item is SvgDefinitionList or SvgSymbol or SvgClipPath or SvgMask or SvgPatternServer or SvgMarker)
                return false;
            if (item is SvgVisualElement visual && (visual.Display == "none" || visual.Visibility is "hidden" or "collapse"))
                return false;
        }
        return true;
    }

    private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
}
