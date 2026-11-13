using System;
using System.Collections.Generic;
using Uno.UI.Composition.Drawing;

namespace UnoDraw.Rendering;

/// <summary>
/// Observes the browser host's native provider calls without owning a context or
/// wrapping its drawing factory. The host finishes negotiation before creating App.
/// </summary>
internal sealed class SingleCanvasGraphicsObservation
{
    private Selection? _selection;

    internal bool UsesWebGpu => _selection?.Kind == GraphicsContextKind.WebGpu;

    internal IGraphicsProvider ObserveWebGpu(IGraphicsProvider provider) =>
        provider is IGraphicsProvider<IWebGpuDeviceContext> typed
            ? new SingleCanvasWebGpuProvider(provider, typed, this)
            : provider;

    internal IGraphicsProvider ObserveSkia(IGraphicsProvider provider) =>
        provider is IGraphicsProvider<IGLDeviceContext> gl
            && provider is IGraphicsProvider<IGraphicsContext> software
            ? new SingleCanvasSkiaProvider(provider, gl, software, this)
            : provider;

    internal string? DescribeSelection()
    {
        var selection = _selection;
        if (selection is null)
            return null;
        var topology = selection.Kind == GraphicsContextKind.WebGpu ? "single-canvas" : "retained";
        return $"UnoDraw single-canvas graphics observation: provider='{selection.Provider}'; "
            + $"factory='{selection.Factory}'; context='{selection.Context}'; kind='{selection.Kind}'; topology='{topology}'.";
    }

    private IDrawingFactory CreateGraphics<TContext>(IGraphicsProvider provider,
        IGraphicsProvider<TContext> typed, TContext context) where TContext : IGraphicsContext
    {
        // A failed or declined later attempt must not retain an earlier WebGPU
        // observation. Both native providers share this startup-only state.
        _selection = null;
        try
        {
            var factory = typed.CreateGraphics(context);
            // The outer return replaces any reentrant observation, including
            // when this provider declines or returns an incompatible factory.
            // Retain only strings, never a native resource beyond its lifetime.
            _selection = factory is not null && CanPresent(context.Kind, factory)
                ? new Selection(context.Kind, provider.GetType().Name,
                    factory.GetType().Name, context.GetType().Name)
                : null;
            return factory!;
        }
        catch
        {
            _selection = null;
            throw;
        }
    }

    private static bool CanPresent(GraphicsContextKind kind, IDrawingFactory factory) => kind switch
    {
        GraphicsContextKind.WebGpu => factory is IDrawingFactory<IWebGpuRenderTarget>,
        GraphicsContextKind.WebGL => factory is IDrawingFactory<IGLRenderTarget>,
        GraphicsContextKind.Software => factory is IDrawingFactory<ISoftwareRenderTarget>,
        _ => false
    };

    private sealed record Selection(GraphicsContextKind Kind, string Provider, string Factory, string Context);

    private sealed class SingleCanvasWebGpuProvider : IGraphicsProvider<IWebGpuDeviceContext>
    {
        private readonly IGraphicsProvider _provider;
        private readonly IGraphicsProvider<IWebGpuDeviceContext> _typed;
        private readonly SingleCanvasGraphicsObservation _observation;

        internal SingleCanvasWebGpuProvider(IGraphicsProvider provider,
            IGraphicsProvider<IWebGpuDeviceContext> typed, SingleCanvasGraphicsObservation observation)
        {
            _provider = provider;
            _typed = typed;
            _observation = observation;
        }

        public IReadOnlyList<GraphicsContextKind> PreferredContexts => _provider.PreferredContexts;

        public IDrawingFactory CreateGraphics(IWebGpuDeviceContext context) =>
            _observation.CreateGraphics(_provider, _typed, context);
    }

    private sealed class SingleCanvasSkiaProvider : IGraphicsProvider<IGLDeviceContext>, IGraphicsProvider<IGraphicsContext>
    {
        private readonly IGraphicsProvider _provider;
        private readonly IGraphicsProvider<IGLDeviceContext> _gl;
        private readonly IGraphicsProvider<IGraphicsContext> _software;
        private readonly SingleCanvasGraphicsObservation _observation;

        internal SingleCanvasSkiaProvider(IGraphicsProvider provider, IGraphicsProvider<IGLDeviceContext> gl,
            IGraphicsProvider<IGraphicsContext> software, SingleCanvasGraphicsObservation observation)
        {
            _provider = provider;
            _gl = gl;
            _software = software;
            _observation = observation;
        }

        public IReadOnlyList<GraphicsContextKind> PreferredContexts => _provider.PreferredContexts;

        public IDrawingFactory CreateGraphics(IGLDeviceContext context) =>
            _observation.CreateGraphics(_provider, _gl, context);

        public IDrawingFactory CreateGraphics(IGraphicsContext context) =>
            _observation.CreateGraphics(_provider, _software, context);
    }
}
