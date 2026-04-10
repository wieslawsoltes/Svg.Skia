using System.Collections.Generic;
using Svg;
using Svg.Skia;
using ShimPoint = ShimSkiaSharp.SKPoint;
using SkiaPicture = SkiaSharp.SKPicture;

namespace TestApp.ViewModels;

public interface ITestAppSvgViewAdapter
{
    SkiaPicture? Picture { get; }

    SKSvg? SkSvg { get; }

    double AnimationPlaybackRate { get; set; }

    SvgAnimationHostBackend ActualAnimationBackend { get; }

    string? AnimationBackendFallbackReason { get; }

    bool TryGetPicturePoint(double x, double y, out ShimPoint picturePoint);

    IEnumerable<SvgElement> HitTestElements(double x, double y);

    void InvalidateView();
}
