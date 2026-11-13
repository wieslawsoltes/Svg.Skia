using System;
using System.Collections.Generic;
using System.Numerics;
using System.Threading.Tasks;
using Silk.NET.WebGPU;
using Uno.UI.Composition.Drawing;
using UnoDraw.Rendering;
using Windows.Foundation;
using Windows.UI;
using Xunit;
using Color = Windows.UI.Color;

namespace Uno.Svg.Skia.UnitTests;

public sealed class SingleCanvasGraphicsObservationTests
{
    [Fact]
    public void RegistrationDoesNotClaimAFactoryOrChangePreferredContextOrder()
    {
        var observation = new SingleCanvasGraphicsObservation();
        var gpu = new NativeWebGpuProvider();
        var skia = new NativeSkiaProvider();

        Assert.Same(gpu.PreferredContexts, observation.ObserveWebGpu(gpu).PreferredContexts);
        Assert.Same(skia.PreferredContexts, observation.ObserveSkia(skia).PreferredContexts);
        Assert.Equal(0, gpu.Calls);
        Assert.Equal(0, skia.Calls);
        Assert.False(observation.UsesWebGpu);
        Assert.Null(observation.DescribeSelection());
    }

    [Fact]
    public void WebGpuForwardsTheSameContextAndNativeFactoryWithoutOwningEither()
    {
        var observation = new SingleCanvasGraphicsObservation();
        var provider = new NativeWebGpuProvider();
        var context = new WebGpuContext();
        var decorated = (IGraphicsProvider<IWebGpuDeviceContext>)observation.ObserveWebGpu(provider);

        var result = decorated.CreateGraphics(context);

        Assert.Same(provider.Factory, result);
        Assert.Same(context, provider.LastContext);
        Assert.Equal(1, provider.Calls);
        Assert.True(observation.UsesWebGpu);
        Assert.False(context.Disposed);
        Assert.False(((Factory)result).Disposed);
        Assert.Equal("UnoDraw single-canvas graphics observation: provider='NativeWebGpuProvider'; "
            + "factory='WebGpuFactory'; context='WebGpuContext'; kind='WebGpu'; topology='single-canvas'.",
            observation.DescribeSelection());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ACompatibleSkiaFallbackReplacesAnEarlierWebGpuObservation(bool software)
    {
        var observation = SelectedWebGpu();
        var provider = new NativeSkiaProvider
        {
            BeforeReturn = () => Assert.False(observation.UsesWebGpu)
        };
        var decorated = observation.ObserveSkia(provider);
        var context = software ? new Context(GraphicsContextKind.Software) : new GlContext();

        var result = software
            ? ((IGraphicsProvider<IGraphicsContext>)decorated).CreateGraphics(context)
            : ((IGraphicsProvider<IGLDeviceContext>)decorated).CreateGraphics((GlContext)context);

        Assert.Same(provider.Factory, result);
        Assert.Same(context, provider.LastContext);
        Assert.Equal(software ? "software" : "gl", provider.LastOverload);
        Assert.Equal(1, provider.Calls);
        Assert.False(observation.UsesWebGpu);
        Assert.False(context.Disposed);
        Assert.False(((Factory)result).Disposed);
        Assert.Contains("provider='NativeSkiaProvider'; factory='SkiaFactory'", observation.DescribeSelection());
        Assert.Contains(software ? "kind='Software'" : "kind='WebGL'", observation.DescribeSelection());
        Assert.EndsWith("topology='retained'.", observation.DescribeSelection());
    }

    [Fact]
    public void AFailedWebGpuAttemptClearsSelectionAndPropagatesTheOriginalException()
    {
        var observation = SelectedWebGpu();
        var failure = new InvalidOperationException("native factory failure");
        var provider = new NativeWebGpuProvider { Failure = failure };
        var context = new WebGpuContext();
        var decorated = (IGraphicsProvider<IWebGpuDeviceContext>)observation.ObserveWebGpu(provider);

        Assert.Same(failure, Assert.Throws<InvalidOperationException>(() => decorated.CreateGraphics(context)));
        Assert.False(observation.UsesWebGpu);
        Assert.Null(observation.DescribeSelection());
        Assert.False(context.Disposed);
        Assert.False(((Factory)provider.Factory!).Disposed);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void AFailedFallbackCannotLeaveAnEarlierWebGpuSelection(bool software)
    {
        var observation = SelectedWebGpu();
        var failure = new InvalidOperationException("native fallback failure");
        var provider = new NativeSkiaProvider { Failure = failure };
        var decorated = observation.ObserveSkia(provider);
        var context = software ? new Context(GraphicsContextKind.Software) : new GlContext();

        var actual = Assert.Throws<InvalidOperationException>(() =>
        {
            if (software)
                ((IGraphicsProvider<IGraphicsContext>)decorated).CreateGraphics(context);
            else
                ((IGraphicsProvider<IGLDeviceContext>)decorated).CreateGraphics((GlContext)context);
        });

        Assert.Same(failure, actual);
        Assert.False(observation.UsesWebGpu);
        Assert.Null(observation.DescribeSelection());
        Assert.False(context.Disposed);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void NullOrIncompatibleFactoriesAreReturnedForFrameworkRejection(bool nullFactory)
    {
        var observation = SelectedWebGpu();
        var factory = nullFactory ? null : new Factory();
        var provider = new NativeWebGpuProvider { Factory = factory };
        var context = new WebGpuContext();
        var decorated = (IGraphicsProvider<IWebGpuDeviceContext>)observation.ObserveWebGpu(provider);

        Assert.Same(factory, decorated.CreateGraphics(context));
        Assert.False(observation.UsesWebGpu);
        Assert.Null(observation.DescribeSelection());
        Assert.False(context.Disposed);
        Assert.False(factory?.Disposed ?? false);
    }

    [Fact]
    public void AThrowAfterAReentrantObservationStillClearsTheFailedOuterAttempt()
    {
        var observation = new SingleCanvasGraphicsObservation();
        var nested = (IGraphicsProvider<IWebGpuDeviceContext>)observation.ObserveWebGpu(new NativeWebGpuProvider());
        var failure = new InvalidOperationException("outer creation failed");
        var outer = new NativeSkiaProvider
        {
            BeforeReturn = () => nested.CreateGraphics(new WebGpuContext()),
            Failure = failure
        };
        var decorated = (IGraphicsProvider<IGLDeviceContext>)observation.ObserveSkia(outer);

        Assert.Same(failure, Assert.Throws<InvalidOperationException>(() => decorated.CreateGraphics(new GlContext())));
        Assert.False(observation.UsesWebGpu);
        Assert.Null(observation.DescribeSelection());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ADeclinedOuterReturnClearsAReentrantCompatibleObservation(bool nullFactory)
    {
        var observation = new SingleCanvasGraphicsObservation();
        var nested = (IGraphicsProvider<IWebGpuDeviceContext>)observation.ObserveWebGpu(new NativeWebGpuProvider());
        var factory = nullFactory ? null : new Factory();
        var outer = new NativeWebGpuProvider
        {
            Factory = factory,
            BeforeReturn = () => nested.CreateGraphics(new WebGpuContext())
        };
        var decorated = (IGraphicsProvider<IWebGpuDeviceContext>)observation.ObserveWebGpu(outer);

        Assert.Same(factory, decorated.CreateGraphics(new WebGpuContext()));
        Assert.False(observation.UsesWebGpu);
        Assert.Null(observation.DescribeSelection());
        Assert.False(factory?.Disposed ?? false);
    }

    private static SingleCanvasGraphicsObservation SelectedWebGpu()
    {
        var observation = new SingleCanvasGraphicsObservation();
        ((IGraphicsProvider<IWebGpuDeviceContext>)observation.ObserveWebGpu(new NativeWebGpuProvider()))
            .CreateGraphics(new WebGpuContext());
        Assert.True(observation.UsesWebGpu);
        return observation;
    }

    private sealed class NativeWebGpuProvider : IGraphicsProvider<IWebGpuDeviceContext>
    {
        public IReadOnlyList<GraphicsContextKind> PreferredContexts { get; } = [GraphicsContextKind.WebGpu];
        internal IDrawingFactory? Factory { get; set; } = new WebGpuFactory();
        internal Exception? Failure { get; set; }
        internal Action? BeforeReturn { get; set; }
        internal int Calls { get; private set; }
        internal IGraphicsContext? LastContext { get; private set; }

        public IDrawingFactory CreateGraphics(IWebGpuDeviceContext context)
        {
            Calls++;
            LastContext = context;
            BeforeReturn?.Invoke();
            if (Failure is { } failure)
                throw failure;
            return Factory!;
        }
    }

    private sealed class NativeSkiaProvider : IGraphicsProvider<IGLDeviceContext>, IGraphicsProvider<IGraphicsContext>
    {
        public IReadOnlyList<GraphicsContextKind> PreferredContexts { get; } = [GraphicsContextKind.WebGL, GraphicsContextKind.Software];
        internal IDrawingFactory Factory { get; } = new SkiaFactory();
        internal Exception? Failure { get; set; }
        internal Action? BeforeReturn { get; set; }
        internal int Calls { get; private set; }
        internal IGraphicsContext? LastContext { get; private set; }
        internal string? LastOverload { get; private set; }

        public IDrawingFactory CreateGraphics(IGLDeviceContext context) => Create(context, "gl");
        public IDrawingFactory CreateGraphics(IGraphicsContext context) => Create(context, "software");

        private IDrawingFactory Create(IGraphicsContext context, string overload)
        {
            Calls++;
            LastContext = context;
            LastOverload = overload;
            BeforeReturn?.Invoke();
            if (Failure is { } failure)
                throw failure;
            return Factory;
        }
    }

    private class Context(GraphicsContextKind kind) : IGraphicsContext
    {
        public GraphicsContextKind Kind => kind;
        internal bool Disposed { get; private set; }
        public void Dispose() => Disposed = true;
    }

    private sealed class GlContext() : Context(GraphicsContextKind.WebGL), IGLDeviceContext
    {
        public Func<string, nint> GetProcAddress => throw new NotSupportedException("Observation must not read native APIs.");
    }

    private sealed unsafe class WebGpuContext() : Context(GraphicsContextKind.WebGpu), IWebGpuDeviceContext
    {
        public WebGPU Api => throw new NotSupportedException();
        public Instance* Instance => throw new NotSupportedException();
        public Adapter* Adapter => throw new NotSupportedException();
        public Device* Device => throw new NotSupportedException();
        public Queue* Queue => throw new NotSupportedException();
        public TextureFormat ColorFormat => throw new NotSupportedException();
    }

    private class Factory : IDrawingFactory, IDisposable
    {
        internal bool Disposed { get; private set; }
        public void Dispose() => Disposed = true;
        public ITexture RenderOffscreen(int width, int height, Action<IDrawingSession> render) => throw new NotSupportedException();
        public Task<IImage> SnapshotAsync(ITexture texture) => throw new NotSupportedException();
        public ITexture CreateTexture(IImage image) => throw new NotSupportedException();
        public ITexture CreateTexture(int width, int height, ReadOnlySpan<byte> pixels) => throw new NotSupportedException();
        public IShader CreateLinearGradientShader(Vector2 start, Vector2 end, Color[] colors, float[] stops,
            GradientTileMode mode, Matrix3x2 matrix) => throw new NotSupportedException();
        public IShader CreateRadialGradientShader(Vector2 center, Vector2 origin, float radiusX, float radiusY,
            Color[] colors, float[] stops, GradientTileMode mode, Matrix3x2 matrix) => throw new NotSupportedException();
        public IColorFilter CreateTintColorFilter(Color color) => throw new NotSupportedException();
        public IColorFilter CreateColorMatrixColorFilter(float[] matrix) => throw new NotSupportedException();
        public IEffectFilter? CreateEffectFilter(EffectNode tree, Rect bounds) => throw new NotSupportedException();
        public IEffectFilter CreateDropShadowFilter(float dx, float dy, float sigmaX, float sigmaY, Color color) => throw new NotSupportedException();
        public ICommandRecorder CreateRecording() => throw new NotSupportedException();
    }

    private sealed class WebGpuFactory : Factory, IDrawingFactory<IWebGpuRenderTarget>
    {
        public IPresentSession BeginPresent(IWebGpuRenderTarget target, ReadOnlySpan<Rect> damage) => throw new NotSupportedException();
    }

    private sealed class SkiaFactory : Factory, IDrawingFactory<IGLRenderTarget>, IDrawingFactory<ISoftwareRenderTarget>
    {
        public IPresentSession BeginPresent(IGLRenderTarget target, ReadOnlySpan<Rect> damage) => throw new NotSupportedException();
        public IPresentSession BeginPresent(ISoftwareRenderTarget target, ReadOnlySpan<Rect> damage) => throw new NotSupportedException();
    }
}
