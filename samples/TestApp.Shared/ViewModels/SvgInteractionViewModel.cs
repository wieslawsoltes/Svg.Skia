using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Svg.Skia;
using ShimPoint = ShimSkiaSharp.SKPoint;
using SkiaColors = SkiaSharp.SKColors;
using SkiaPaint = SkiaSharp.SKPaint;
using SkiaPaintStyle = SkiaSharp.SKPaintStyle;

namespace TestApp.ViewModels;

public sealed class SvgInteractionViewModel : ViewModelBase, IDisposable
{
    private readonly ObservableCollection<string> _hitResults = new();
    private readonly ReadOnlyObservableCollection<string> _readOnlyHitResults;
    private readonly IList<ShimPoint> _hitTestPoints = new List<ShimPoint>();
    private ITestAppSvgViewAdapter? _view;
    private SKSvg? _currentSkSvg;
    private bool _showHitBounds;
    private string _animationStatusText = "No animation";
    private string _animationClockText = "00:00.000";
    private string _animationBackendInfoText = "Default";
    private string? _animationBackendFallbackReason;
    private bool _canPlay;
    private bool _canPause;
    private bool _canRestart;
    private double _resumeAnimationPlaybackRate = 1.0;

    public SvgInteractionViewModel()
    {
        _readOnlyHitResults = new ReadOnlyObservableCollection<string>(_hitResults);
    }

    public ReadOnlyObservableCollection<string> HitResults => _readOnlyHitResults;

    public bool ShowHitBounds
    {
        get => _showHitBounds;
        set
        {
            if (SetProperty(ref _showHitBounds, value))
            {
                SubscribeOnDraw();
                _view?.InvalidateView();
            }
        }
    }

    public string AnimationStatusText
    {
        get => _animationStatusText;
        private set => SetProperty(ref _animationStatusText, value);
    }

    public string AnimationClockText
    {
        get => _animationClockText;
        private set => SetProperty(ref _animationClockText, value);
    }

    public string AnimationBackendInfoText
    {
        get => _animationBackendInfoText;
        private set => SetProperty(ref _animationBackendInfoText, value);
    }

    public string? AnimationBackendFallbackReason
    {
        get => _animationBackendFallbackReason;
        private set => SetProperty(ref _animationBackendFallbackReason, value);
    }

    public bool CanPlay
    {
        get => _canPlay;
        private set => SetProperty(ref _canPlay, value);
    }

    public bool CanPause
    {
        get => _canPause;
        private set => SetProperty(ref _canPause, value);
    }

    public bool CanRestart
    {
        get => _canRestart;
        private set => SetProperty(ref _canRestart, value);
    }

    public void Attach(ITestAppSvgViewAdapter view)
    {
        _view = view;
        SubscribeOnDraw();
        UpdateAnimationUi();
    }

    public void Detach()
    {
        if (_currentSkSvg is not null)
        {
            _currentSkSvg.OnDraw -= SkSvgOnDraw;
        }

        _currentSkSvg = null;
        _view = null;
    }

    public void NotifySelectionChanged()
    {
        _hitResults.Clear();
        _hitTestPoints.Clear();
        SubscribeOnDraw();
        _view?.InvalidateView();
        UpdateAnimationUi();
    }

    public void HandlePointerPressed(double x, double y)
    {
        _hitResults.Clear();
        _hitTestPoints.Clear();

        if (_view?.SkSvg is null)
        {
            return;
        }

        if (_view.TryGetPicturePoint(x, y, out var picturePoint))
        {
            _hitTestPoints.Add(picturePoint);
            var element = _view.HitTestElements(x, y).FirstOrDefault();
            if (element is not null)
            {
                _hitResults.Add(element.ID ?? element.GetType().Name);
            }
        }

        SubscribeOnDraw();
        _view.InvalidateView();
    }

    public void Tick()
    {
        if (!ReferenceEquals(_currentSkSvg, _view?.SkSvg))
        {
            SubscribeOnDraw();
        }

        if (_view?.AnimationPlaybackRate > 0)
        {
            _resumeAnimationPlaybackRate = _view.AnimationPlaybackRate;
        }

        UpdateAnimationUi();
    }

    public void PlayAnimation()
    {
        if (_view?.SkSvg?.HasAnimations != true)
        {
            UpdateAnimationUi();
            return;
        }

        if (_view.AnimationPlaybackRate <= 0)
        {
            _view.AnimationPlaybackRate = _resumeAnimationPlaybackRate > 0 ? _resumeAnimationPlaybackRate : 1.0;
        }

        UpdateAnimationUi();
    }

    public void PauseAnimation()
    {
        if (_view is null)
        {
            return;
        }

        if (_view.AnimationPlaybackRate > 0)
        {
            _resumeAnimationPlaybackRate = _view.AnimationPlaybackRate;
        }

        _view.AnimationPlaybackRate = 0;
        UpdateAnimationUi();
    }

    public void RestartAnimation()
    {
        _view?.SkSvg?.ResetAnimation();
        AutoStartAnimationIfNeeded();
        _view?.InvalidateView();
        UpdateAnimationUi();
    }

    public void Dispose()
    {
        Detach();
    }

    private void SubscribeOnDraw()
    {
        if (_currentSkSvg is not null)
        {
            _currentSkSvg.OnDraw -= SkSvgOnDraw;
        }

        _currentSkSvg = _view?.SkSvg;

        if (_currentSkSvg is not null)
        {
            _currentSkSvg.OnDraw += SkSvgOnDraw;
        }

        AutoStartAnimationIfNeeded();
        UpdateAnimationUi();
    }

    private void AutoStartAnimationIfNeeded()
    {
        if (_view?.SkSvg?.HasAnimations != true || _view.AnimationPlaybackRate > 0)
        {
            return;
        }

        _view.AnimationPlaybackRate = _resumeAnimationPlaybackRate > 0 ? _resumeAnimationPlaybackRate : 1.0;
    }

    private void UpdateAnimationUi()
    {
        var skSvg = _view?.SkSvg;
        var hasAnimations = skSvg?.HasAnimations == true;
        var animationTime = skSvg?.AnimationTime ?? TimeSpan.Zero;
        var isPaused = (_view?.AnimationPlaybackRate ?? 0) <= 0;

        CanPlay = hasAnimations && isPaused;
        CanPause = hasAnimations && !isPaused;
        CanRestart = hasAnimations;

        AnimationStatusText = !hasAnimations
            ? "No animation"
            : isPaused
                ? "Paused"
                : "Playing";

        AnimationClockText = animationTime.ToString(@"mm\:ss\.fff");
        AnimationBackendInfoText = _view?.ActualAnimationBackend.ToString() ?? "Default";
        AnimationBackendFallbackReason = _view?.AnimationBackendFallbackReason;
    }

    private void SkSvgOnDraw(object? sender, SKSvgDrawEventArgs e)
    {
        if (!_showHitBounds || sender is not SKSvg skSvg)
        {
            return;
        }

        var hits = new HashSet<SvgSceneNode>();

        foreach (var point in _hitTestPoints)
        {
            foreach (var node in skSvg.HitTestSceneNodes(point))
            {
                hits.Add(node);
            }
        }

        using var paint = new SkiaPaint
        {
            IsAntialias = true,
            Style = SkiaPaintStyle.Stroke,
            Color = SkiaColors.Cyan
        };

        foreach (var hit in hits.Take(1))
        {
            e.Canvas.DrawRect(skSvg.SkiaModel.ToSKRect(hit.TransformedBounds), paint);
        }
    }
}
