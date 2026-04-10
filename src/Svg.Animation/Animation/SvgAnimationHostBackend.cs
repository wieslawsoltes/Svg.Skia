using System;

namespace Svg.Skia;

public enum SvgAnimationHostBackend
{
    Default,
    Manual,
    DispatcherTimer,
    RenderLoop,
    NativeComposition
}

public sealed class SvgAnimationHostBackendCapabilities
{
    public SvgAnimationHostBackendCapabilities(
        bool isHostReady,
        bool supportsDispatcherTimer,
        bool supportsRenderLoop,
        bool supportsNativeComposition)
    {
        IsHostReady = isHostReady;
        SupportsDispatcherTimer = supportsDispatcherTimer;
        SupportsRenderLoop = supportsRenderLoop;
        SupportsNativeComposition = supportsNativeComposition;
    }

    public bool IsHostReady { get; }

    public bool SupportsDispatcherTimer { get; }

    public bool SupportsRenderLoop { get; }

    public bool SupportsNativeComposition { get; }
}

public sealed class SvgAnimationHostBackendResolution
{
    public SvgAnimationHostBackendResolution(
        SvgAnimationHostBackend requestedBackend,
        SvgAnimationHostBackend actualBackend,
        string? fallbackReason)
    {
        RequestedBackend = requestedBackend;
        ActualBackend = actualBackend;
        FallbackReason = fallbackReason;
    }

    public SvgAnimationHostBackend RequestedBackend { get; }

    public SvgAnimationHostBackend ActualBackend { get; }

    public string? FallbackReason { get; }

    public bool IsFallback =>
        !string.IsNullOrWhiteSpace(FallbackReason) &&
        RequestedBackend != ActualBackend;
}

public static class SvgAnimationHostBackendResolver
{
    public static SvgAnimationHostBackendResolution Resolve(
        SvgAnimationHostBackend requestedBackend,
        SvgAnimationHostBackendCapabilities capabilities,
        bool hasAnimations)
    {
        if (requestedBackend == SvgAnimationHostBackend.Manual)
        {
            return new SvgAnimationHostBackendResolution(
                requestedBackend,
                SvgAnimationHostBackend.Manual,
                null);
        }

        if (!hasAnimations)
        {
            return new SvgAnimationHostBackendResolution(
                requestedBackend,
                SvgAnimationHostBackend.Manual,
                "SVG source does not contain animation elements.");
        }

        if (!capabilities.IsHostReady)
        {
            return new SvgAnimationHostBackendResolution(
                requestedBackend,
                SvgAnimationHostBackend.Manual,
                "Animation playback requires an attached UI host.");
        }

        switch (requestedBackend)
        {
            case SvgAnimationHostBackend.DispatcherTimer:
                {
                    return capabilities.SupportsDispatcherTimer
                        ? new SvgAnimationHostBackendResolution(
                            requestedBackend,
                            SvgAnimationHostBackend.DispatcherTimer,
                            null)
                        : new SvgAnimationHostBackendResolution(
                            requestedBackend,
                            SvgAnimationHostBackend.Manual,
                            "Dispatcher timer animation playback is unavailable.");
                }
            case SvgAnimationHostBackend.NativeComposition:
                {
                    if (capabilities.SupportsNativeComposition && SupportsAutomaticTicks(capabilities))
                    {
                        return new SvgAnimationHostBackendResolution(
                            requestedBackend,
                            SvgAnimationHostBackend.NativeComposition,
                            null);
                    }

                    return ResolveNativeCompositionFallback(requestedBackend, capabilities);
                }
            case SvgAnimationHostBackend.RenderLoop:
                {
                    if (capabilities.SupportsRenderLoop)
                    {
                        return new SvgAnimationHostBackendResolution(
                            requestedBackend,
                            SvgAnimationHostBackend.RenderLoop,
                            null);
                    }

                    if (capabilities.SupportsDispatcherTimer)
                    {
                        return new SvgAnimationHostBackendResolution(
                            requestedBackend,
                            SvgAnimationHostBackend.DispatcherTimer,
                            "Render-loop animation playback is unavailable; falling back to dispatcher timer.");
                    }

                    return new SvgAnimationHostBackendResolution(
                        requestedBackend,
                        SvgAnimationHostBackend.Manual,
                        "Render-loop animation playback is unavailable.");
                }
            case SvgAnimationHostBackend.Default:
            default:
                {
                    if (capabilities.SupportsNativeComposition && SupportsAutomaticTicks(capabilities))
                    {
                        return new SvgAnimationHostBackendResolution(
                            requestedBackend,
                            SvgAnimationHostBackend.NativeComposition,
                            null);
                    }

                    if (capabilities.SupportsRenderLoop)
                    {
                        return new SvgAnimationHostBackendResolution(
                            requestedBackend,
                            SvgAnimationHostBackend.RenderLoop,
                            null);
                    }

                    if (capabilities.SupportsDispatcherTimer)
                    {
                        return new SvgAnimationHostBackendResolution(
                            requestedBackend,
                            SvgAnimationHostBackend.DispatcherTimer,
                            null);
                    }

                    return new SvgAnimationHostBackendResolution(
                        requestedBackend,
                        SvgAnimationHostBackend.Manual,
                        "Automatic animation playback backends are unavailable.");
                }
        }
    }

    private static bool SupportsAutomaticTicks(SvgAnimationHostBackendCapabilities capabilities)
    {
        return capabilities.SupportsRenderLoop || capabilities.SupportsDispatcherTimer;
    }

    private static SvgAnimationHostBackendResolution ResolveNativeCompositionFallback(
        SvgAnimationHostBackend requestedBackend,
        SvgAnimationHostBackendCapabilities capabilities)
    {
        if (capabilities.SupportsRenderLoop)
        {
            return new SvgAnimationHostBackendResolution(
                requestedBackend,
                SvgAnimationHostBackend.RenderLoop,
                "Native composition animation playback is unavailable; falling back to render loop.");
        }

        if (capabilities.SupportsDispatcherTimer)
        {
            return new SvgAnimationHostBackendResolution(
                requestedBackend,
                SvgAnimationHostBackend.DispatcherTimer,
                "Native composition animation playback is unavailable; falling back to dispatcher timer.");
        }

        return new SvgAnimationHostBackendResolution(
            requestedBackend,
            SvgAnimationHostBackend.Manual,
            "Native composition animation playback is unavailable.");
    }
}
