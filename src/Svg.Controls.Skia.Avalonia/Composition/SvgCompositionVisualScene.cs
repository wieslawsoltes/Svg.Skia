using System;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Media;
using Avalonia.Platform;
using Avalonia.Rendering.Composition;
using Avalonia.Skia;
using ShimSkiaSharp;
using Svg.Skia;
using NativeSkPicture = SkiaSharp.SKPicture;

namespace Avalonia.Svg.Skia;

internal sealed class SvgCompositionVisualScene : IDisposable
{
    private sealed class LayerMessage
    {
        public LayerMessage(NativeSkPicture? picture)
        {
            Picture = picture;
        }

        public NativeSkPicture? Picture { get; }
    }

    private sealed class LayerHandler : CompositionCustomVisualHandler
    {
        private readonly Action<string> _onRenderUnavailable;
        private NativeSkPicture? _picture;
        private bool _reportedRenderUnavailable;

        public LayerHandler(NativeSkPicture? picture, Action<string> onRenderUnavailable)
        {
            _picture = picture;
            _onRenderUnavailable = onRenderUnavailable;
        }

        public override void OnMessage(object message)
        {
            if (message is not LayerMessage layerMessage)
            {
                return;
            }

            if (!ReferenceEquals(_picture, layerMessage.Picture))
            {
                _picture?.Dispose();
            }

            _picture = layerMessage.Picture;
            Invalidate();
        }

        public override void OnRender(ImmediateDrawingContext drawingContext)
        {
            if (_picture is null)
            {
                return;
            }

            var leaseFeature = drawingContext.TryGetFeature<ISkiaSharpApiLeaseFeature>();
            if (leaseFeature is null)
            {
                ReportRenderUnavailable("Avalonia compositor custom visuals did not provide a Skia drawing lease.");
                return;
            }

            using var lease = leaseFeature.Lease();
            var canvas = lease?.SkCanvas;
            if (canvas is null)
            {
                ReportRenderUnavailable("Avalonia compositor custom visuals did not provide a Skia canvas.");
                return;
            }

            canvas.Save();
            canvas.DrawPicture(_picture);
            canvas.Restore();
        }

        private void ReportRenderUnavailable(string reason)
        {
            if (_reportedRenderUnavailable)
            {
                return;
            }

            _reportedRenderUnavailable = true;
            _onRenderUnavailable(reason);
        }
    }

    private sealed class LayerVisual
    {
        private SvgNativeCompositionLayer? _layer;
        private SKPicture? _sourcePicture;

        public LayerVisual(CompositionCustomVisual visual)
        {
            Visual = visual;
        }

        public CompositionCustomVisual Visual { get; }

        public void Initialize(SvgNativeCompositionLayer layer)
        {
            _layer = layer;
            _sourcePicture = layer.Picture;
            ApplyVisualState(layer);
        }

        public void Update(SvgNativeCompositionLayer layer, bool wireframe)
        {
            var sourcePictureChanged = !ReferenceEquals(_sourcePicture, layer.Picture);
            _layer = layer;
            _sourcePicture = layer.Picture;
            ApplyVisualState(layer);

            if (sourcePictureChanged)
            {
                Visual.SendHandlerMessage(new LayerMessage(CreateRenderPicture(_sourcePicture, wireframe)));
            }
        }

        public void UpdateWireframe(bool wireframe)
        {
            Visual.SendHandlerMessage(new LayerMessage(CreateRenderPicture(_sourcePicture, wireframe)));
        }

        public void Activate(bool wireframe)
        {
            if (_layer is { } layer)
            {
                ApplyVisualState(layer);
            }

            Visual.SendHandlerMessage(new LayerMessage(CreateRenderPicture(_sourcePicture, wireframe)));
        }

        public void Dispose()
        {
            Visual.SendHandlerMessage(new LayerMessage(null));
            _layer = null;
            _sourcePicture = null;
        }

        private void ApplyVisualState(SvgNativeCompositionLayer layer)
        {
            Visual.Visible = layer.IsVisible && layer.Picture?.Commands is { Count: > 0 };
            Visual.Opacity = ClampOpacity(layer.Opacity);
            Visual.Offset = new Vector3D(layer.Offset.X, layer.Offset.Y, 0d);
            Visual.Size = new Vector(layer.Size.Width, layer.Size.Height);
        }

        private static NativeSkPicture? CreateRenderPicture(SKPicture? picture, bool wireframe)
        {
            if (picture?.Commands is not { Count: > 0 })
            {
                return null;
            }

            return wireframe
                ? SvgSource.s_skiaModel.ToWireframePicture(picture)
                : SvgSource.s_skiaModel.ToSKPicture(picture);
        }

        private static float ClampOpacity(float opacity)
        {
            if (opacity <= 0f)
            {
                return 0f;
            }

            return opacity >= 1f ? 1f : opacity;
        }
    }

    private readonly Svg _owner;
    private readonly CompositionContainerVisual _rootVisual;
    private readonly Dictionary<int, LayerVisual> _layers;
    private SKRect _sourceBounds;
    private bool _disposed;

    private SvgCompositionVisualScene(
        Svg owner,
        CompositionContainerVisual rootVisual,
        Dictionary<int, LayerVisual> layers,
        SKRect sourceBounds)
    {
        _owner = owner;
        _rootVisual = rootVisual;
        _layers = layers;
        _sourceBounds = sourceBounds;
    }

    public static bool TryCreate(
        Svg owner,
        SvgNativeCompositionScene scene,
        bool wireframe,
        out SvgCompositionVisualScene? compositionScene)
    {
        compositionScene = null;

        if (scene.Layers.Count == 0)
        {
            return false;
        }

        var elementVisual = ElementComposition.GetElementVisual(owner);
        if (elementVisual is null)
        {
            return false;
        }

        var compositor = elementVisual.Compositor;
        var rootVisual = compositor.CreateContainerVisual();
        rootVisual.ClipToBounds = true;

        var layers = new Dictionary<int, LayerVisual>(scene.Layers.Count);
        foreach (var layer in scene.Layers)
        {
            var visual = compositor.CreateCustomVisual(new LayerHandler(picture: null, owner.OnNativeCompositionRenderUnavailable));
            var layerVisual = new LayerVisual(visual);
            layerVisual.Initialize(layer);
            rootVisual.Children.Add(visual);
            layers[layer.DocumentChildIndex] = layerVisual;
        }

        ElementComposition.SetElementChildVisual(owner, rootVisual);

        compositionScene = new SvgCompositionVisualScene(owner, rootVisual, layers, scene.SourceBounds);
        compositionScene.RefreshLayout();
        foreach (var layerVisual in layers.Values)
        {
            layerVisual.Activate(wireframe);
        }
        return true;
    }

    public void RefreshLayout()
    {
        if (_disposed)
        {
            return;
        }

        if (!TryGetRootTransform(out var scale, out var offset))
        {
            _rootVisual.Visible = false;
            return;
        }

        _rootVisual.Visible = true;
        _rootVisual.ClipToBounds = true;
        _rootVisual.Size = new Vector(_owner.Bounds.Width, _owner.Bounds.Height);
        _rootVisual.Scale = scale;
        _rootVisual.Offset = offset;
    }

    public void UpdateFrame(SvgNativeCompositionFrame frame, bool wireframe)
    {
        if (_disposed)
        {
            return;
        }

        _sourceBounds = frame.SourceBounds;
        foreach (var layer in frame.Layers)
        {
            if (_layers.TryGetValue(layer.DocumentChildIndex, out var layerVisual))
            {
                layerVisual.Update(layer, wireframe);
            }
        }

        RefreshLayout();
    }

    public void UpdateWireframe(bool wireframe)
    {
        if (_disposed)
        {
            return;
        }

        foreach (var layer in _layers.Values)
        {
            layer.UpdateWireframe(wireframe);
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        foreach (var layer in _layers.Values)
        {
            layer.Dispose();
        }
        _rootVisual.Children.RemoveAll();
        ElementComposition.SetElementChildVisual(_owner, null);
    }

    private bool TryGetRootTransform(out Vector3D scale, out Vector3D offset)
    {
        scale = new Vector3D(1d, 1d, 1d);
        offset = new Vector3D(0d, 0d, 0d);

        var bounds = _sourceBounds;
        if (_owner.Bounds.Width <= 0 || _owner.Bounds.Height <= 0 || bounds.Width <= 0 || bounds.Height <= 0)
        {
            return false;
        }

        var viewPort = new Rect(_owner.Bounds.Size);
        var sourceSize = new Size(bounds.Width, bounds.Height);
        var scaleVector = _owner.Stretch.CalculateScaling(_owner.Bounds.Size, sourceSize, _owner.StretchDirection);
        var scaledSize = sourceSize * scaleVector;
        var destinationRect = viewPort
            .CenterRect(new Rect(scaledSize))
            .Intersect(viewPort);

        if (destinationRect.Width <= 0 || destinationRect.Height <= 0)
        {
            return false;
        }

        var sourceRect = new Rect(sourceSize)
            .CenterRect(new Rect(destinationRect.Size / scaleVector));

        if (sourceRect.Width <= 0 || sourceRect.Height <= 0)
        {
            return false;
        }

        var scaleMatrix = Matrix.CreateScale(
            destinationRect.Width / sourceRect.Width,
            destinationRect.Height / sourceRect.Height);
        var translateMatrix = Matrix.CreateTranslation(
            -sourceRect.X + destinationRect.X - bounds.Left,
            -sourceRect.Y + destinationRect.Y - bounds.Top);
        var userMatrix = Matrix.CreateScale(_owner.Zoom, _owner.Zoom) * Matrix.CreateTranslation(_owner.PanX, _owner.PanY);
        var matrix = scaleMatrix * translateMatrix * userMatrix;

        scale = new Vector3D(matrix.M11, matrix.M22, 1d);
        offset = new Vector3D(matrix.M31, matrix.M32, 0d);
        return true;
    }
}
