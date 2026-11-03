using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Headless.XUnit;
using Avalonia.Platform;
using Avalonia.Threading;
using Xunit;

namespace Avalonia.Svg.Skia.UnitTests;

[CollectionDefinition(nameof(SvgAssetLoadingTests), DisableParallelization = true)]
public sealed class SvgAssetLoadingTestCollection;

[Collection(nameof(SvgAssetLoadingTests))]
public class SvgAssetLoadingTests
{
    private const string SampleSvg =
        "<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"10\" height=\"12\"><rect width=\"10\" height=\"12\" fill=\"red\" /></svg>";

    private static readonly Uri s_baseUri = new("avares://Svg.Controls.Skia.Avalonia.UnitTests/Views/Main.axaml");

    [AvaloniaFact]
    public async Task Path_OpensAssetOnUiThread_AndReadsStreamInBackground()
    {
        using var scope = AvaloniaLocator.EnterScope();
        var loader = new RecordingAssetLoader();
        AvaloniaLocator.CurrentMutable.Bind<IAssetLoader>().ToConstant(loader);
        var uiThreadId = Environment.CurrentManagedThreadId;
        var svg = new Svg(s_baseUri);

        try
        {
            svg.Path = "/Assets/Icon.svg";

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

        public Stream Open(Uri uri, Uri? baseUri = null)
        {
            var stream = new RecordingStream(Encoding.UTF8.GetBytes(SampleSvg));
            Opened.Enqueue(new OpenedAsset(uri.IsAbsoluteUri || baseUri is null ? uri : new Uri(baseUri, uri), Environment.CurrentManagedThreadId, stream));
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
            }

            base.Dispose(disposing);
        }
    }
}
