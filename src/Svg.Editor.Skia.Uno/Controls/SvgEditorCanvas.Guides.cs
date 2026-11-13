using Microsoft.UI.Xaml;
using SkiaSharp;
using Svg.Editor.Core;
using Svg.Editor.Skia;

namespace Svg.Editor.Skia.Uno;

public sealed partial class SvgEditorCanvas
{
    private bool _guidesVisible = true, _guidesLocked = true, _snapToGuides = true, _smartGuides = true;
    private readonly SvgEditorGuideCache _guideCache = new();
    private IReadOnlyList<SvgEditorGuide> _guides = Array.Empty<SvgEditorGuide>();
    private SvgEditorGuideIndex? _snapGuideIndex;
    private SvgEditorGuide? _guidePreview;
    private string? _movingGuideId, _snapGuideId, _snapSecondGuideId;
    private SKPoint _guideOriginalOrigin;

    public bool GuidesVisible { get => _guidesVisible; set { _guidesVisible = value; InvalidateInteraction(); } }
    public bool GuidesLocked { get => _guidesLocked; set { _guidesLocked = value; CancelGuidePreview(); InvalidateInteraction(); } }
    public bool SnapToGuides { get => _snapToGuides; set => _snapToGuides = value; }
    public bool SmartGuides { get => _smartGuides; set { _smartGuides = value; _snapGuideId = _snapSecondGuideId = null; InvalidateInteraction(); } }

    public void PreviewRulerGuide(SKPoint? picturePoint, bool vertical)
    {
        _guidePreview = picturePoint is { } point ? new SvgEditorGuide(string.Empty, point, vertical ? new SKPoint(0, 1) : new SKPoint(1, 0)) : null;
        InvalidateInteraction();
    }

    private void RefreshGuideCache() => _guides = _guideCache.GetGuides(Controller);

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
        InvalidateInteraction();
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
        _guideCache.Clear();
        _guides = Array.Empty<SvgEditorGuide>();
        _snapGuideIndex = null;
    }

    private SKPoint SnapEditorPoint(SKPoint point)
    {
        _snapGuideId = _snapSecondGuideId = null;
        if (PanMode || ZoomMode || Controller is not { } controller
            || _gesture is Gesture.Pan or Gesture.Pinch or Gesture.Marquee or Gesture.Zoom
            || InteractionMode is SvgEditorInteractionMode.Eyedropper or SvgEditorInteractionMode.Lasso or SvgEditorInteractionMode.MagicWand
                or SvgEditorInteractionMode.Scissors or SvgEditorInteractionMode.Eraser or SvgEditorInteractionMode.Knife)
            return point;
        _snapGuideIndex = _guideCache.GetSnapIndex(controller, SnapToGuides && GuidesVisible, SmartGuides, _movingGuideId);
        var tolerance = (float)(5 / Zoom);
        if (_gesture == Gesture.Move && !_startBounds.IsEmpty)
        {
            var delta = Subtract(point, _startWorld);
            var bestDistance = float.PositiveInfinity;
            var adjustment = default(SKPoint);
            for (var column = 0; column < 3; column++)
                for (var row = 0; row < 3; row++)
                {
                    var x = column == 0 ? _startBounds.Left : column == 1 ? _startBounds.MidX : _startBounds.Right;
                    var y = row == 0 ? _startBounds.Top : row == 1 ? _startBounds.MidY : _startBounds.Bottom;
                    var anchor = new SKPoint(x + delta.X, y + delta.Y);
                    var snapped = _snapGuideIndex.Snap(anchor, tolerance, out var first, out var second);
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
        return _snapGuideIndex.Snap(point, tolerance, out _snapGuideId, out _snapSecondGuideId);
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
        if (_activePointer is not null && _moved && (_snapGuideId is not null || _snapSecondGuideId is not null)
            && _snapGuideIndex is { } snapIndex)
        {
            resources.Stroke.Color = resources.SmartGuide;
            for (var index = 0; index < snapIndex.Guides.Count; index++)
            {
                var guide = snapIndex.Guides[index];
                if (guide.Id == _snapGuideId || guide.Id == _snapSecondGuideId)
                    DrawLine(guide);
            }
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
