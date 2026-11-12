using System.Runtime.InteropServices.JavaScript;
using SkiaSharp;
using Uno.WinUI.Graphics3DWebGpu;

namespace UnoDraw.Rendering;

/// <summary>
/// Shares one Graphite context per Uno device. Each canvas owns a recorder and
/// retires it asynchronously; a non-yielding Dawn context must never be destroyed
/// while its native submission futures are outstanding.
/// </summary>
internal sealed class WebGpuGraphiteContext
{
    private static readonly Dictionary<nint, WebGpuGraphiteContext> s_devices = [];
    private static readonly HashSet<Retirement> s_retirements = [];
    private static bool s_disabled;
    private readonly List<Retirement> _quarantined = [];
    private readonly nint _device;
    private readonly nint _instance;
    private readonly nint _queue;
    private int _leases;

    private unsafe WebGpuGraphiteContext(WebGpuCanvasContext source)
    {
        _device = (nint)source.Device;
        _instance = (nint)source.Instance;
        _queue = (nint)source.Queue;
        if (_instance == 0 || _device == 0 || _queue == 0)
            throw new NotSupportedException("The shared WebGPU instance, device and queue are required.");
        var internalMultisampleCount = WebGpuGraphiteInterop.GetInternalMultisampleCount();
        using var backend = new SKGraphiteDawnBackendContext
        {
            WgpuInstance = _instance,
            WgpuDevice = _device,
            WgpuQueue = _queue
        };
        Context = SKGraphiteContext.CreateDawn(backend, new SKGraphiteContextOptions
        {
            GpuBudgetInBytes = 256L * 1024 * 1024,
            // Zero retains Skia's default. In pinned non-Vello WASM Skia,
            // requesting one selects antialiased raster path/clip atlases through
            // RendererProvider; specialized analytic renderers remain unchanged.
            InternalMultisampleCount = internalMultisampleCount,
            // A rejected document can discard its recorder snapshot without
            // creating a required sequence gap in later supported frames.
            RequireOrderedRecordings = false
        })
            ?? throw new NotSupportedException("Skia could not create a Graphite context on the shared WebGPU device.");
        var coverage = internalMultisampleCount == 1 ? "raster" : "default";
        Console.WriteLine($"UnoDraw Graphite coverage initialized: {coverage} (InternalMultisampleCount={internalMultisampleCount}).");
    }

    private SKGraphiteContext Context { get; }

    internal static unsafe Lease Acquire(WebGpuCanvasContext source)
    {
        ThrowIfDisabled();
        var key = (nint)source.Device;
        if (!s_devices.TryGetValue(key, out var entry))
        {
            entry = new WebGpuGraphiteContext(source);
            s_devices.Add(key, entry);
        }
        if (entry._instance != (nint)source.Instance || entry._queue != (nint)source.Queue)
            throw new InvalidOperationException("The shared WebGPU instance or queue changed unexpectedly.");
        entry._leases++;
        return new Lease(entry);
    }

    private static void ThrowIfDisabled()
    {
        if (s_disabled)
            throw new NotSupportedException("Graphite is disabled until page reload after a failed submission or incomplete native retirement.");
    }

    private void ReleaseDrainedLease()
    {
        _leases--;
        if (_leases != 0 || _quarantined.Count != 0)
            return;
        Context.CheckAsyncWorkCompletion();
        Context.Dispose();
        s_devices.Remove(_device);
    }

    internal sealed class Lease(WebGpuGraphiteContext owner)
    {
        private GraphiteImageProvider? _images;
        private SKGraphiteRecorder? _recorder;
        private bool _retired;

        internal SKGraphiteContext Context
        {
            get
            {
                ThrowIfDisabled();
                if (_retired || owner.Context.IsDeviceLost)
                    throw new InvalidOperationException("The Graphite device lease is unavailable.");
                return owner.Context;
            }
        }

        internal SKGraphiteRecorder Recorder
        {
            get
            {
                _ = Context;
                if (_recorder is not null)
                    return _recorder;
                _images = new GraphiteImageProvider();
                var images = _images;
                // The native image-provider protocol accepts a null handle on
                // failure. SkiaSharp's delegate annotation is non-nullable; our
                // provider records the failure and the frame is rejected below.
                _recorder = owner.Context.CreateRecorder(64L * 1024 * 1024,
                    (recorder, image, mipmapped) => images.FindOrCreate(recorder, image, mipmapped)!, images.Dispose)
                    ?? throw new NotSupportedException("Skia could not create the Graphite document recorder.");
                return _recorder;
            }
        }

        internal void DiscardRecording()
        {
            // Unsupported document semantics may leave commands in the recorder.
            // They must not be inserted after the CPU fallback overwrites Target.
            try
            {
                using var discarded = _recorder?.Snap();
            }
            finally
            {
                // Skia d1717dc Recorder::snap moves root image uploads into the
                // recording. Disposing it drops those uploads. Its unordered
                // recording path already resets device storage caches/atlases;
                // our independent image-provider cache must be reset as well.
                _images?.InvalidateAfterDiscard();
            }
        }

        internal void ThrowIfImageUploadFailed()
        {
            if (_images?.Failure is { } failure)
                throw new InvalidOperationException($"Graphite image upload failed: {failure}");
        }

        internal void RejectSubmission()
        {
            // InsertRecording/Submit can fail after adding partial commands to
            // this shared context. No other recorder may later submit them after
            // the CPU fallback has overwritten its target. Retire normally when
            // the existing queue drains, but prohibit every future submission.
            s_disabled = true;
        }

        internal void Retire(JSObject state, SKSurface? surface, SKGraphiteBackendTexture? texture)
        {
            if (_retired)
                throw new InvalidOperationException("The Graphite lease was already retired.");
            _retired = true;
            var retirement = new Retirement(owner, this, state, surface, texture);
            // Keep native wrappers rooted even if a driver never resolves its
            // queue promise. Their finalizers cannot safely destroy pending work.
            s_retirements.Add(retirement);
            retirement.Start();
        }

        internal void DisposeDrainedResources()
        {
            if (_recorder is not null)
            {
                // The recorder invokes the registered image-cache disposer while
                // it is still alive, before destroying its native resource owner.
                _recorder.Dispose();
                _recorder = null;
                _images = null;
            }
            else
            {
                _images?.Dispose();
                _images = null;
            }
        }
    }

    private sealed class Retirement(WebGpuGraphiteContext owner, Lease lease,
        JSObject state, SKSurface? surface, SKGraphiteBackendTexture? texture)
    {
        internal void Start() => _ = CompleteAsync();

        private async Task CompleteAsync()
        {
            try
            {
                var error = await WebGpuGraphiteInterop.WaitForQueueAsync(state);
                if (!string.IsNullOrEmpty(error))
                    throw new InvalidOperationException(error);
                // The shared queue promise follows all work from this recorder.
                // Poll native futures only after the event loop made them ready.
                owner.Context.CheckAsyncWorkCompletion();
                surface?.Dispose();
                texture?.Dispose();
                lease.DisposeDrainedResources();
                owner.ReleaseDrainedLease();
            }
            catch (Exception error)
            {
                // Pinned emdawn registers only a fulfillment handler for native
                // queue futures. On rejection no public SkiaSharp API can prove
                // those futures drained; the non-yielding destructor would abort.
                // Fail closed for the entire session, so device recreation cannot
                // accumulate more undestroyable contexts. CPU documents/text tiles
                // are released separately and never enter this quarantine.
                s_disabled = true;
                owner._quarantined.Add(this);
                Console.WriteLine($"UnoDraw Graphite canvas fallback: native retirement is incomplete; Graphite is disabled until page reload ({error.Message}).");
            }
            finally
            {
                s_retirements.Remove(this);
                try
                {
                    WebGpuGraphiteInterop.ReleaseState(state);
                }
                catch (Exception error)
                {
                    Console.WriteLine($"UnoDraw Graphite canvas fallback cleanup: {error.Message}.");
                }
            }
        }
    }
}
