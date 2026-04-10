using Xunit;

namespace Svg.Skia.UnitTests;

public class SvgAnimationHostBackendResolverTests
{
    [Fact]
    public void Resolve_DefaultPrefersRenderLoop()
    {
        var capabilities = new SvgAnimationHostBackendCapabilities(
            isHostReady: true,
            supportsDispatcherTimer: true,
            supportsRenderLoop: true,
            supportsNativeComposition: false);

        var resolution = SvgAnimationHostBackendResolver.Resolve(
            SvgAnimationHostBackend.Default,
            capabilities,
            hasAnimations: true);

        Assert.Equal(SvgAnimationHostBackend.RenderLoop, resolution.ActualBackend);
        Assert.Null(resolution.FallbackReason);
        Assert.False(resolution.IsFallback);
    }

    [Fact]
    public void Resolve_RenderLoopFallsBackToDispatcherTimer()
    {
        var capabilities = new SvgAnimationHostBackendCapabilities(
            isHostReady: true,
            supportsDispatcherTimer: true,
            supportsRenderLoop: false,
            supportsNativeComposition: false);

        var resolution = SvgAnimationHostBackendResolver.Resolve(
            SvgAnimationHostBackend.RenderLoop,
            capabilities,
            hasAnimations: true);

        Assert.Equal(SvgAnimationHostBackend.DispatcherTimer, resolution.ActualBackend);
        Assert.Equal(
            "Render-loop animation playback is unavailable; falling back to dispatcher timer.",
            resolution.FallbackReason);
        Assert.True(resolution.IsFallback);
    }

    [Fact]
    public void Resolve_DefaultFallsBackToManualWhenHostIsNotReady()
    {
        var capabilities = new SvgAnimationHostBackendCapabilities(
            isHostReady: false,
            supportsDispatcherTimer: false,
            supportsRenderLoop: false,
            supportsNativeComposition: false);

        var resolution = SvgAnimationHostBackendResolver.Resolve(
            SvgAnimationHostBackend.Default,
            capabilities,
            hasAnimations: true);

        Assert.Equal(SvgAnimationHostBackend.Manual, resolution.ActualBackend);
        Assert.Equal("Animation playback requires an attached UI host.", resolution.FallbackReason);
        Assert.True(resolution.IsFallback);
    }

    [Fact]
    public void Resolve_NoAnimationsFallsBackToManual()
    {
        var capabilities = new SvgAnimationHostBackendCapabilities(
            isHostReady: true,
            supportsDispatcherTimer: true,
            supportsRenderLoop: true,
            supportsNativeComposition: false);

        var resolution = SvgAnimationHostBackendResolver.Resolve(
            SvgAnimationHostBackend.DispatcherTimer,
            capabilities,
            hasAnimations: false);

        Assert.Equal(SvgAnimationHostBackend.Manual, resolution.ActualBackend);
        Assert.Equal("SVG source does not contain animation elements.", resolution.FallbackReason);
        Assert.True(resolution.IsFallback);
    }

    [Fact]
    public void Resolve_ManualRemainsManualWithoutFallback()
    {
        var capabilities = new SvgAnimationHostBackendCapabilities(
            isHostReady: false,
            supportsDispatcherTimer: false,
            supportsRenderLoop: false,
            supportsNativeComposition: false);

        var resolution = SvgAnimationHostBackendResolver.Resolve(
            SvgAnimationHostBackend.Manual,
            capabilities,
            hasAnimations: false);

        Assert.Equal(SvgAnimationHostBackend.Manual, resolution.ActualBackend);
        Assert.Null(resolution.FallbackReason);
        Assert.False(resolution.IsFallback);
    }

    [Fact]
    public void Resolve_DefaultPrefersNativeCompositionWhenSupported()
    {
        var capabilities = new SvgAnimationHostBackendCapabilities(
            isHostReady: true,
            supportsDispatcherTimer: true,
            supportsRenderLoop: true,
            supportsNativeComposition: true);

        var resolution = SvgAnimationHostBackendResolver.Resolve(
            SvgAnimationHostBackend.Default,
            capabilities,
            hasAnimations: true);

        Assert.Equal(SvgAnimationHostBackend.NativeComposition, resolution.ActualBackend);
        Assert.Null(resolution.FallbackReason);
        Assert.False(resolution.IsFallback);
    }

    [Fact]
    public void Resolve_NativeCompositionFallsBackToRenderLoop()
    {
        var capabilities = new SvgAnimationHostBackendCapabilities(
            isHostReady: true,
            supportsDispatcherTimer: true,
            supportsRenderLoop: true,
            supportsNativeComposition: false);

        var resolution = SvgAnimationHostBackendResolver.Resolve(
            SvgAnimationHostBackend.NativeComposition,
            capabilities,
            hasAnimations: true);

        Assert.Equal(SvgAnimationHostBackend.RenderLoop, resolution.ActualBackend);
        Assert.Equal(
            "Native composition animation playback is unavailable; falling back to render loop.",
            resolution.FallbackReason);
        Assert.True(resolution.IsFallback);
    }
}
