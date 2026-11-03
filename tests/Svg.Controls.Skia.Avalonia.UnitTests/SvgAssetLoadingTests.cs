using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Headless.XUnit;
using Avalonia.Platform;
using Avalonia.Threading;
using ShimSkiaSharp;
using ShimSkiaSharp.Editing;
using Xunit;

namespace Avalonia.Svg.Skia.UnitTests;

[CollectionDefinition(nameof(SvgAssetLoadingTests), DisableParallelization = true)]
public sealed class SvgAssetLoadingTestCollection
{
}

[Collection(nameof(SvgAssetLoadingTests))]
public class SvgAssetLoadingTests
{
    private const string SampleSvg =
        "<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"10\" height=\"12\"><rect width=\"10\" height=\"12\" fill=\"red\" /></svg>";

    private static readonly Uri s_baseUri = new("avares://Svg.Controls.Skia.Avalonia.UnitTests/Views/Main.axaml");

    [AvaloniaTheory]
    [InlineData("/Assets/Icon.svg")]
    [InlineData("../Assets/Icon.svg")]
    [InlineData("avares://Svg.Controls.Skia.Avalonia.UnitTests/Assets/Icon.svg")]
    public async Task Path_OpensAssetOnUiThread_AndReadsStreamInBackground(string path)
    {
        using var scope = AvaloniaLocator.EnterScope();
        var loader = new RecordingAssetLoader();
        AvaloniaLocator.CurrentMutable.Bind<IAssetLoader>().ToConstant(loader);
        var uiThreadId = Environment.CurrentManagedThreadId;
        var svg = new Svg(s_baseUri);

        try
        {
            svg.Path = path;

            await WaitForLoadAsync(svg);

            var opened = Assert.Single(loader.Opened);
            Assert.Equal(uiThreadId, opened.ThreadId);
            Assert.Equal(new Uri(s_baseUri, "/Assets/Icon.svg"), opened.Uri);
            Assert.NotEmpty(opened.Stream.ReadThreads);
            Assert.All(opened.Stream.ReadThreads, threadId => Assert.NotEqual(uiThreadId, threadId));
            Assert.True(opened.Stream.IsDisposed);
            Assert.NotNull(svg.Picture);
            Assert.Equal(10, svg.Picture!.CullRect.Width);
            Assert.Equal(12, svg.Picture.CullRect.Height);
        }
        finally
        {
            svg.Path = null;
            await WaitForLoadAsync(svg);
        }
    }

    [AvaloniaFact]
    public async Task Path_MultipleControlsAndCssReloads_KeepAssetLoaderOnUiThread()
    {
        using var scope = AvaloniaLocator.EnterScope();
        var loader = new RecordingAssetLoader();
        AvaloniaLocator.CurrentMutable.Bind<IAssetLoader>().ToConstant(loader);
        var uiThreadId = Environment.CurrentManagedThreadId;
        var controls = new List<Svg>();

        try
        {
            for (var i = 0; i < 24; i++)
            {
                var svg = new Svg(s_baseUri);
                controls.Add(svg);
                svg.Path = $"/Assets/Icon{i}.svg";
                Svg.SetCss(svg, "rect { fill: blue; }");

                // Ordinary Avalonia assets can be opened while the SVGs are still loading.
                using var otherAsset = AssetLoader.Open(new Uri(s_baseUri, "/Assets/Other.svg"));
            }

            await Task.WhenAll(controls.Select(WaitForLoadAsync));
            await Task.WhenAll(loader.Opened.Select(asset => asset.Stream.Disposed.Task))
                .WaitAsync(TimeSpan.FromSeconds(5));

            Assert.All(loader.Opened, asset => Assert.Equal(uiThreadId, asset.ThreadId));
            Assert.All(controls, svg => AssertFillColor(svg, new SKColor(0, 0, 255, 255)));
        }
        finally
        {
            foreach (var svg in controls)
            {
                svg.Path = null;
            }
        }
    }

    [AvaloniaFact]
    public async Task SvgSource_UnloadedPathAndCssReload_OpenAssetsOnUiThread()
    {
        using var scope = AvaloniaLocator.EnterScope();
        var loader = new RecordingAssetLoader();
        AvaloniaLocator.CurrentMutable.Bind<IAssetLoader>().ToConstant(loader);
        var uiThreadId = Environment.CurrentManagedThreadId;
        using var source = new SvgSource(s_baseUri) { Path = "../Assets/Icon.svg" };
        var svg = new Svg(s_baseUri);

        try
        {
            svg.SvgSource = source;
            await WaitForLoadAsync(svg);

            Svg.SetCss(svg, "rect { fill: blue; }");
            await WaitForLoadAsync(svg);

            Assert.Equal(2, loader.Opened.Count);
            Assert.All(loader.Opened, asset =>
            {
                Assert.Equal(uiThreadId, asset.ThreadId);
                Assert.NotEmpty(asset.Stream.ReadThreads);
                Assert.All(asset.Stream.ReadThreads, threadId => Assert.NotEqual(uiThreadId, threadId));
                Assert.True(asset.Stream.IsDisposed);
            });
            AssertFillColor(svg, new SKColor(0, 0, 255, 255));
            Assert.Null(source.Svg);
            Assert.Null(source.Parameters);
        }
        finally
        {
            svg.SvgSource = null;
        }
    }

    [AvaloniaFact]
    public async Task Path_CacheHit_DoesNotReopenAsset()
    {
        using var scope = AvaloniaLocator.EnterScope();
        var loader = new RecordingAssetLoader();
        AvaloniaLocator.CurrentMutable.Bind<IAssetLoader>().ToConstant(loader);
        var svg = new Svg(s_baseUri) { EnableCache = true };

        try
        {
            svg.Path = "/Assets/First.svg";
            await WaitForLoadAsync(svg);
            var firstPicture = svg.Picture;

            svg.Path = "/Assets/Second.svg";
            await WaitForLoadAsync(svg);

            svg.Path = "/Assets/First.svg";
            await WaitForLoadAsync(svg);

            Assert.Equal(2, loader.Opened.Count);
            Assert.NotNull(firstPicture);
            Assert.Same(firstPicture, svg.Picture);
            Assert.All(loader.Opened, asset => Assert.True(asset.Stream.IsDisposed));
        }
        finally
        {
            svg.Path = null;
        }
    }

    [AvaloniaFact]
    public async Task LoadAsync_OpensAssetOnCallingThread_AndReadsStreamInBackground()
    {
        using var scope = AvaloniaLocator.EnterScope();
        var loader = new RecordingAssetLoader();
        AvaloniaLocator.CurrentMutable.Bind<IAssetLoader>().ToConstant(loader);
        var callingThreadId = Environment.CurrentManagedThreadId;

        using var source = await SvgSource.LoadAsync("../Assets/Icon.svg", s_baseUri);

        var opened = Assert.Single(loader.Opened);
        Assert.Equal(callingThreadId, opened.ThreadId);
        Assert.NotEmpty(opened.Stream.ReadThreads);
        Assert.All(opened.Stream.ReadThreads, threadId => Assert.NotEqual(callingThreadId, threadId));
        Assert.True(opened.Stream.IsDisposed);
        Assert.NotNull(source.Picture);
    }

    [AvaloniaFact]
    public async Task LoadAsync_CancelledAfterOpeningAsset_DisposesStreamWithoutReading()
    {
        using var scope = AvaloniaLocator.EnterScope();
        using var cancellation = new CancellationTokenSource();
        var loader = new RecordingAssetLoader { OnOpen = _ => cancellation.Cancel() };
        AvaloniaLocator.CurrentMutable.Bind<IAssetLoader>().ToConstant(loader);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            SvgSource.LoadAsync("/Assets/Icon.svg", s_baseUri, cancellationToken: cancellation.Token));

        var opened = Assert.Single(loader.Opened);
        Assert.True(opened.Stream.IsDisposed);
        Assert.Empty(opened.Stream.ReadThreads);
    }

    [AvaloniaFact]
    public async Task Path_StreamReadFailure_DisposesStream_AndCanRecover()
    {
        using var scope = AvaloniaLocator.EnterScope();
        var loader = new RecordingAssetLoader
        {
            OnOpen = asset =>
            {
                if (asset.Uri.AbsolutePath.EndsWith("/Broken.svg", StringComparison.Ordinal))
                {
                    asset.Stream.ReadException = new IOException("Simulated asset read failure.");
                }
            }
        };
        AvaloniaLocator.CurrentMutable.Bind<IAssetLoader>().ToConstant(loader);
        var svg = new Svg(s_baseUri);

        try
        {
            svg.Path = "/Assets/Broken.svg";
            await WaitForLoadAsync(svg);

            var failed = Assert.Single(loader.Opened);
            Assert.True(failed.Stream.IsDisposed);
            Assert.Null(svg.Picture);

            svg.Path = "/Assets/Icon.svg";
            await WaitForLoadAsync(svg);

            Assert.Equal(2, loader.Opened.Count);
            Assert.All(loader.Opened, asset => Assert.True(asset.Stream.IsDisposed));
            Assert.NotNull(svg.Picture);
        }
        finally
        {
            svg.Path = null;
        }
    }

    private static void AssertFillColor(Svg svg, SKColor expectedColor)
    {
        var command = svg.SkSvg?.Model?
            .FindCommands<DrawPathCanvasCommand>()
            .FirstOrDefault(command => command.Paint?.Style == SKPaintStyle.Fill);

        Assert.NotNull(command?.Paint?.Color);
        Assert.Equal(expectedColor, command.Paint.Color.Value);
    }

    private static async Task WaitForLoadAsync(Svg svg)
    {
        var pendingLoadField = typeof(Svg).GetField("_pendingLoadCts", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(pendingLoadField);
        var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(5);

        while (pendingLoadField.GetValue(svg) is not null)
        {
            Assert.True(DateTime.UtcNow < deadline, "The SVG load did not finish.");
            await Task.Delay(10);
            await Dispatcher.UIThread.InvokeAsync(() => { }, DispatcherPriority.Background);
        }
    }

    private sealed class RecordingAssetLoader : IAssetLoader
    {
        public ConcurrentQueue<OpenedAsset> Opened { get; } = new();

        public Action<OpenedAsset>? OnOpen { get; init; }

        public Stream Open(Uri uri, Uri? baseUri = null)
        {
            var stream = new RecordingStream(Encoding.UTF8.GetBytes(SampleSvg));
            var asset = new OpenedAsset(uri.IsAbsoluteUri || baseUri is null ? uri : new Uri(baseUri, uri), Environment.CurrentManagedThreadId, stream);
            Opened.Enqueue(asset);
            OnOpen?.Invoke(asset);
            return stream;
        }

        public bool Exists(Uri uri, Uri? baseUri = null) => true;

        public (Stream stream, Assembly assembly) OpenAndGetAssembly(Uri uri, Uri? baseUri = null) =>
            (Open(uri, baseUri), typeof(SvgAssetLoadingTests).Assembly);

        public Assembly GetAssembly(Uri uri, Uri? baseUri = null) => typeof(SvgAssetLoadingTests).Assembly;

        public IEnumerable<Uri> GetAssets(Uri uri, Uri? baseUri) => Array.Empty<Uri>();

        public void SetDefaultAssembly(Assembly assembly)
        {
        }

        public void InvalidateAssemblyCache(string name)
        {
        }

        public void InvalidateAssemblyCache()
        {
        }
    }

    private sealed record OpenedAsset(Uri Uri, int ThreadId, RecordingStream Stream);

    private sealed class RecordingStream(byte[] bytes) : Stream
    {
        private readonly MemoryStream _inner = new(bytes, writable: false);
        private int _disposed;

        public ConcurrentQueue<int> ReadThreads { get; } = new();

        public TaskCompletionSource Disposed { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public Exception? ReadException { get; set; }

        public bool IsDisposed => Volatile.Read(ref _disposed) != 0;

        public override bool CanRead => _inner.CanRead;

        public override bool CanSeek => _inner.CanSeek;

        public override bool CanWrite => false;

        public override long Length => _inner.Length;

        public override long Position
        {
            get => _inner.Position;
            set => _inner.Position = value;
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            ReadThreads.Enqueue(Environment.CurrentManagedThreadId);
            if (ReadException is { } exception)
            {
                throw exception;
            }

            return _inner.Read(buffer, offset, count);
        }

        public override long Seek(long offset, SeekOrigin origin) => _inner.Seek(offset, origin);

        public override void Flush() => _inner.Flush();

        public override void SetLength(long value) => throw new NotSupportedException();

        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _inner.Dispose();
                Interlocked.Exchange(ref _disposed, 1);
                Disposed.TrySetResult();
            }

            base.Dispose(disposing);
        }
    }
}
