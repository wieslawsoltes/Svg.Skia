using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Svg.Editor.Skia.Uno;
using Uno.Foundation;
using Uno.Foundation.Extensibility;
using Uno.Graphics;
using Windows.Foundation;

namespace UnoDraw.Rendering;

/// <summary>
/// Connects the editor's Skia painters to a retained WebGPU texture through Uno's
/// public island extension. An opt-in document renderer can use an app-owned
/// WebGL2 context; persistent CPU upload remains the default and its fallback.
/// </summary>
internal static class WebGpuSkiaCanvasBridge
{
    private static int s_reportedInitialization;
    private static int s_reportedWebGlInitialization;
    private static int s_reportedCompositeInitialization;

    internal static void Register()
    {
        var preference = WebAssemblyRuntime.InvokeJS(
            "new URLSearchParams(globalThis.location.search).get('canvasBridge') || ''");
        if (preference == "legacy")
        {
            Console.WriteLine("UnoDraw canvas bridge preference: legacy raster texture upload.");
            return;
        }

        // SKCanvasElement already routes non-Skia backends through this extension.
        // It suppresses the parent's software upload while an island is present.
        // The original renderer is used only to request Uno's supported fallback;
        // its GL-specific Init/Render/Destroy methods are never called.
        var preferWebGl = preference == "webgl";
        var preferComposite = preference == "composite";
        ApiExtensibility.Register<IGLIslandRenderer>(typeof(IGLIsland),
            renderer => new RasterCanvasIsland(renderer, preferWebGl, preferComposite));
        Console.WriteLine(preferWebGl
            ? "UnoDraw canvas bridge preference: WebGL document rasterization."
            : preferComposite
                ? "UnoDraw canvas bridge preference: cached document composition."
                : "UnoDraw canvas bridge preference: persistent RGBA upload.");
    }

    internal static void ReportInitialization()
    {
        if (Interlocked.Exchange(ref s_reportedInitialization, 1) == 0)
            Console.WriteLine("UnoDraw canvas bridge initialized: persistent RGBA upload.");
    }

    internal static void ReportWebGlInitialization()
    {
        if (Interlocked.Exchange(ref s_reportedWebGlInitialization, 1) == 0)
            Console.WriteLine("UnoDraw canvas bridge initialized: WebGL document rasterization.");
    }

    internal static void ReportCompositeInitialization()
    {
        if (Interlocked.Exchange(ref s_reportedCompositeInitialization, 1) == 0)
            Console.WriteLine("UnoDraw canvas bridge initialized: cached document composition.");
    }

    private sealed class RasterCanvasIsland : Grid, IGLIsland
    {
        private readonly IGLIslandRenderer _fallback;
        private readonly Canvas _viewport = new() { IsHitTestVisible = false };
        private readonly WebGpuRasterCanvas _canvas;
        private readonly bool _preferComposite;
        private readonly RectangleGeometry _clip = new();
        private readonly List<(UIElement Element, DependencyProperty Property, long Token)> _transformSubscriptions = [];
        private XamlRoot? _observedRoot;
        private ISkiaCanvasLayers? _layers;
        private bool _failed;

        public RasterCanvasIsland(IGLIslandRenderer fallback, bool preferWebGl, bool preferComposite)
        {
            _fallback = fallback;
            _preferComposite = preferComposite;
            _canvas = new WebGpuRasterCanvas(RequestFallback, preferWebGl);
            IsHitTestVisible = false;
            Clip = _clip;

            // An SKCanvasElement's own content precedes its children. Uno appends
            // islands after existing children, so restore that original order.
            Canvas.SetZIndex(this, -1);
            Children.Add(_viewport);
            Loaded += OnLoaded;
            Unloaded += OnUnloaded;
            SizeChanged += OnSizeChanged;
        }

        public FrameworkElement Element => this;

        public void Invalidate() => _canvas.InvalidateDocument();

        private void OnLoaded(object sender, RoutedEventArgs args)
        {
            if (_failed)
                return;
            if (Parent is not FrameworkElement source || source is not ISkiaCanvasRenderer renderer)
            {
                // Returning null from the extension factory would leave Uno's
                // fallback unresolved. Explicitly report unsupported painters.
                RequestFallback(null);
                return;
            }

            _canvas.Renderer = renderer;
            ObserveRoot(XamlRoot);
            ObserveTransforms(source);
            if (_failed)
                return;
            if (_preferComposite && renderer is ISkiaCanvasLayerSource layerSource)
            {
                _layers = layerSource.TryAcquireLayers(_canvas.Invalidate);
                if (_layers is null)
                    Console.WriteLine("UnoDraw canvas bridge composite fallback: the canvas layers are already owned or unavailable.");
                else
                    _canvas.SetCompositeLayers(_layers);
            }
            UpdateSize(RenderSize);
        }

        private void OnUnloaded(object sender, RoutedEventArgs args)
        {
            ObserveRoot(null);
            StopObservingTransforms();
            ReleaseLayers();
            _canvas.ReleaseSurfaces();
            _canvas.Renderer = null;
        }

        private void OnSizeChanged(object sender, SizeChangedEventArgs args)
        {
            if (IsLoaded)
                UpdateSize(args.NewSize);
        }

        private void ObserveRoot(XamlRoot? root)
        {
            if (ReferenceEquals(root, _observedRoot))
                return;
            if (_observedRoot is not null)
                _observedRoot.Changed -= OnRootChanged;
            _observedRoot = root;
            if (root is not null)
                root.Changed += OnRootChanged;
        }

        private void OnRootChanged(XamlRoot sender, XamlRootChangedEventArgs args)
        {
            ObserveRoot(XamlRoot);
            UpdateSize(RenderSize);
        }

        private void UpdateSize(Size size)
        {
            if (_failed || _canvas.Renderer is null)
                return;

            if (!double.IsFinite(size.Width) || !double.IsFinite(size.Height))
            {
                RequestFallback("the canvas layout has no finite pixel dimensions");
                return;
            }

            _clip.Rect = new Rect(0, 0, Math.Max(0, size.Width), Math.Max(0, size.Height));
            if (size.Width <= 0 || size.Height <= 0)
            {
                // Removing the child lets WebGpuCanvasElement release its target
                // with the compositor's retained-frame lifetime rules. No zero-
                // sized texture or GL framebuffer is created or destroyed here.
                _viewport.Children.Remove(_canvas);
                _canvas.ReleaseSurfaces();
                return;
            }

            var scale = XamlRoot?.RasterizationScale ?? 1;
            if (!CanvasPixelSize.TryCreate(size, scale, out var pixels))
            {
                RequestFallback("the canvas dimensions exceed the supported pixel buffer size");
                return;
            }

            if (!_canvas.Configure(pixels))
                return;
            if (!_viewport.Children.Contains(_canvas))
                _viewport.Children.Add(_canvas);
        }

        private void ObserveTransforms(FrameworkElement source)
        {
            StopObservingTransforms();
            for (DependencyObject? current = source; current is not null; current = VisualTreeHelper.GetParent(current))
            {
                if (current is not UIElement element)
                    continue;
                Subscribe(element, UIElement.RenderTransformProperty);
                Subscribe(element, UIElement.ProjectionProperty);
                if (!IsUnscaled(element))
                {
                    RequestFallback("an ancestor transform requires the legacy texture's mip filtering");
                    return;
                }
            }
        }

        private void Subscribe(UIElement element, DependencyProperty property)
        {
            var token = element.RegisterPropertyChangedCallback(property, (sender, _) =>
            {
                if (sender is UIElement changed && !IsUnscaled(changed))
                    RequestFallback("an ancestor transform requires the legacy texture's mip filtering");
            });
            _transformSubscriptions.Add((element, property, token));
        }

        // Reject even an identity ScaleTransform/TransformGroup: its properties
        // can subsequently animate without replacing RenderTransform. Translation
        // cannot minify the screen-resolution texture and needs no mip chain.
        private static bool IsUnscaled(UIElement element)
            => element.Projection is null && (element.RenderTransform is null or TranslateTransform);

        private void StopObservingTransforms()
        {
            foreach (var (element, property, token) in _transformSubscriptions)
                element.UnregisterPropertyChangedCallback(property, token);
            _transformSubscriptions.Clear();
        }

        private void RequestFallback(string? reason)
        {
            if (_failed)
                return;
            _failed = true;
            ObserveRoot(null);
            StopObservingTransforms();
            if (_layers is not null)
                Console.WriteLine($"UnoDraw canvas bridge composite fallback: {reason ?? "the active canvas bridge became unavailable"}.");
            ReleaseLayers();
            if (reason is not null)
                Console.WriteLine($"UnoDraw canvas bridge fallback: {reason}.");
            // Uno queues removal after the current paint/Loaded callback and
            // invalidates the original painter through its normal software path.
            _fallback.OnUnavailable();
        }

        private void ReleaseLayers()
        {
            var layers = _layers;
            _layers = null;
            _canvas.SetCompositeLayers(null);
            layers?.Dispose();
        }
    }
}

internal readonly record struct CanvasPixelSize(Size LogicalSize, double Scale, int Width, int Height)
{
    internal static bool TryCreate(Size size, double scale, out CanvasPixelSize pixels)
    {
        pixels = default;
        if (!double.IsFinite(size.Width) || !double.IsFinite(size.Height) ||
            !double.IsFinite(scale) || size.Width <= 0 || size.Height <= 0 || scale <= 0)
            return false;

        var width = Math.Ceiling(size.Width * scale);
        var height = Math.Ceiling(size.Height * scale);
        if (width < 1 || height < 1 || width > int.MaxValue || height > int.MaxValue ||
            width * height > int.MaxValue / 4d)
            return false;

        pixels = new CanvasPixelSize(size, scale, (int)width, (int)height);
        return true;
    }
}
