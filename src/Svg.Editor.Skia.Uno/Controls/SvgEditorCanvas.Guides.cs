using Microsoft.UI.Xaml;
using SkiaSharp;
using Svg.Editor.Core;
using Svg.Editor.Skia;

namespace Svg.Editor.Skia.Uno;

public sealed partial class SvgEditorCanvas
{
    private bool _guidesVisible = true, _guidesLocked = true, _snapToGuides = true, _smartGuides = true;
    private SvgEditorController? _guideController;
    private long _guideRevision = -1;
    private IReadOnlyList<SvgEditorGuide> _guides = Array.Empty<SvgEditorGuide>();
    private readonly List<SvgEditorGuide> _smartGuideCandidates = new();
    private readonly List<SvgEditorGuide> _snapCandidates = new();
    private SvgEditorGuide? _guidePreview;
    private string? _movingGuideId, _snapGuideId, _snapSecondGuideId;
    private SKPoint _guideOriginalOrigin;
    private long _smartGuideRevision = -1;
    private SvgVisualElement[] _smartGuideSelection = Array.Empty<SvgVisualElement>();

    public bool GuidesVisible { get => _guidesVisible; set { _guidesVisible = value; Invalidate(); } }
    public bool GuidesLocked { get => _guidesLocked; set { _guidesLocked = value; CancelGuidePreview(); Invalidate(); } }
    public bool SnapToGuides { get => _snapToGuides; set => _snapToGuides = value; }
    public bool SmartGuides { get => _smartGuides; set { _smartGuides = value; _snapGuideId = _snapSecondGuideId = null; Invalidate(); } }

    public void PreviewRulerGuide(SKPoint? picturePoint, bool vertical)
    {
        _guidePreview = picturePoint is { } point ? new SvgEditorGuide(string.Empty, point, vertical ? new SKPoint(0, 1) : new SKPoint(1, 0)) : null;
        Invalidate();
    }

    private void RefreshGuideCache()
    {
        if (ReferenceEquals(_guideController, Controller) && _guideRevision == Controller?.DocumentRevision)
            return;
        _guideController = Controller;
        _guideRevision = Controller?.DocumentRevision ?? -1;
        _guides = Controller?.GetGuides() ?? Array.Empty<SvgEditorGuide>();
        _smartGuideRevision = -1;
    }

    private bool TryBeginGuideDrag(SKPoint view)
    {
        if (!GuidesVisible || GuidesLocked || ZoomMode || InteractionMode != SvgEditorInteractionMode.None || Tool != SvgEditorToolKind.Select)
            return false;
        RefreshGuideCache();
        var point = Viewport.ViewToWorld(view);
        var guide = _guides.Select(item => (Guide: item, Distance: SKPoint.Distance(point, SvgEditorGuideService.Project(point, item))))
            .Where(item => item.Distance * Zoom <= 5).OrderBy(item => item.Distance).FirstOrDefault();
        if (guide.Guide.Id is null)
            return false;
        _movingGuideId = guide.Guide.Id;
        _guideOriginalOrigin = guide.Guide.Origin;
        _guidePreview = guide.Guide;
        _gesture = Gesture.None;
        return true;
    }

    private bool TryUpdateGuideDrag()
    {
        if (_movingGuideId is null || _guidePreview is not { } guide)
            return false;
        var origin = Add(_guideOriginalOrigin, Subtract(_currentWorld, _startWorld));
        _guidePreview = new SvgEditorGuide(guide.Id, origin, guide.Direction);
        Invalidate();
        return true;
    }

    private bool CompleteGuideDrag()
    {
        if (_movingGuideId is not { } id)
            return false;
        var preview = _guidePreview;
        _movingGuideId = null;
        _guidePreview = null;
        if (_moved && Controller is { } controller && preview is { } guide)
        {
            if (_lastView.X < 0 || _lastView.Y < 0 || _lastView.X > Viewport.Width || _lastView.Y > Viewport.Height)
                controller.RemoveGuide(id);
            else
                controller.MoveGuide(id, guide.Origin);
        }
        return true;
    }

    private void CancelGuidePreview()
    {
        _guidePreview = null;
        _movingGuideId = _snapGuideId = _snapSecondGuideId = null;
    }

    private SKPoint SnapEditorPoint(SKPoint point)
    {
        _snapGuideId = _snapSecondGuideId = null;
        if (PanMode || ZoomMode || Controller is not { } controller
            || _gesture is Gesture.Pan or Gesture.Pinch or Gesture.Marquee or Gesture.Zoom
            || InteractionMode is SvgEditorInteractionMode.Eyedropper or SvgEditorInteractionMode.Lasso or SvgEditorInteractionMode.MagicWand
                or SvgEditorInteractionMode.Scissors or SvgEditorInteractionMode.Eraser or SvgEditorInteractionMode.Knife)
            return point;
        RefreshGuideCache();
        _snapCandidates.Clear();
        if (SnapToGuides && GuidesVisible)
            _snapCandidates.AddRange(_guides.Where(guide => guide.Id != _movingGuideId));
        if (SmartGuides)
        {
            if (_smartGuideRevision != controller.DocumentRevision || !_smartGuideSelection.SequenceEqual(controller.SelectedElements))
            {
                _smartGuideRevision = controller.DocumentRevision;
                _smartGuideSelection = controller.SelectedElements.ToArray();
                _smartGuideCandidates.Clear();
                if (controller.Renderer.Picture is { } picture)
                    AddBounds(picture.CullRect, "artboard");
                foreach (var element in controller.Document!.Descendants().OfType<SvgVisualElement>())
                {
                    if (_smartGuideCandidates.Count >= 6000)
                        break;
                    if (element is SvgGroup || SvgEditorController.IsEditLocked(element) || IsSelected(element) || !IsVisibleArtwork(element))
                        continue;
                    if (controller.GetSceneNode(element) is not { } node || node.TransformedBounds.IsEmpty)
                        continue;
                    var b = node.TransformedBounds;
                    AddBounds(new SKRect(b.Left, b.Top, b.Right, b.Bottom), "object-" + _smartGuideCandidates.Count);
                }
            }
            _snapCandidates.AddRange(_smartGuideCandidates);
        }
        var tolerance = (float)(5 / Zoom);
        if (_gesture == Gesture.Move && !_startBounds.IsEmpty)
        {
            var delta = Subtract(point, _startWorld);
            var bestDistance = float.PositiveInfinity;
            var adjustment = default(SKPoint);
            foreach (var x in new[] { _startBounds.Left, _startBounds.MidX, _startBounds.Right })
                foreach (var y in new[] { _startBounds.Top, _startBounds.MidY, _startBounds.Bottom })
                {
                    var anchor = new SKPoint(x + delta.X, y + delta.Y);
                    var snapped = SvgEditorGuideService.Snap(anchor, tolerance, _snapCandidates, out var first, out var second);
                    if (first is null)
                        continue;
                    var distance = SKPoint.Distance(anchor, snapped);
                    if (distance >= bestDistance)
                        continue;
                    bestDistance = distance;
                    adjustment = Subtract(snapped, anchor);
                    _snapGuideId = first;
                    _snapSecondGuideId = second;
                }
            return Add(point, adjustment);
        }
        return SvgEditorGuideService.Snap(point, tolerance, _snapCandidates, out _snapGuideId, out _snapSecondGuideId);

        bool IsSelected(SvgElement element)
        {
            for (var item = element; item is not null; item = item.Parent)
                if (item is SvgVisualElement visual && _smartGuideSelection.Contains(visual))
                    return true;
            return false;
        }
        static bool IsVisibleArtwork(SvgElement element)
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
        void AddBounds(SKRect bounds, string id)
        {
            if (!float.IsFinite(bounds.Left) || !float.IsFinite(bounds.Top) || !float.IsFinite(bounds.Right) || !float.IsFinite(bounds.Bottom))
                return;
            foreach (var x in new[] { bounds.Left, bounds.MidX, bounds.Right })
                _smartGuideCandidates.Add(new SvgEditorGuide("smart-" + id + "-x-" + x, new SKPoint(x, bounds.Top), new SKPoint(0, 1)));
            foreach (var y in new[] { bounds.Top, bounds.MidY, bounds.Bottom })
                _smartGuideCandidates.Add(new SvgEditorGuide("smart-" + id + "-y-" + y, new SKPoint(bounds.Left, y), new SKPoint(1, 0)));
        }
    }

    private void DrawEditorGuides(SKCanvas canvas, CanvasResources resources)
    {
        RefreshGuideCache();
        resources.Stroke.StrokeWidth = 1;
        resources.Stroke.Color = resources.Guide;
        if (GuidesVisible)
            foreach (var guide in _guides)
                if (guide.Id != _movingGuideId)
                    DrawLine(guide);
        if (_guidePreview is { } preview)
        {
            resources.Stroke.Color = resources.GuidePreview;
            DrawLine(preview);
        }
        if (_activePointer is not null && _moved)
        {
            resources.Stroke.Color = resources.SmartGuide;
            foreach (var guide in _snapCandidates)
                if (guide.Id == _snapGuideId || guide.Id == _snapSecondGuideId)
                    DrawLine(guide);
        }
        void DrawLine(SvgEditorGuide guide)
        {
            var center = Viewport.ViewToWorld(new SKPoint((float)Viewport.Width / 2, (float)Viewport.Height / 2));
            var origin = SvgEditorGuideService.Project(center, guide);
            var extent = (float)(Math.Sqrt(Viewport.Width * Viewport.Width + Viewport.Height * Viewport.Height) / Zoom + 2);
            var start = Viewport.WorldToView(new SKPoint(origin.X - guide.Direction.X * extent, origin.Y - guide.Direction.Y * extent));
            var end = Viewport.WorldToView(new SKPoint(origin.X + guide.Direction.X * extent, origin.Y + guide.Direction.Y * extent));
            canvas.DrawLine(start, end, resources.Stroke);
        }
    }
}
