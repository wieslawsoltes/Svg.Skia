using System.Runtime.InteropServices.JavaScript;
using Uno.Foundation;
using Uno.UI.Composition.WebGpu;

namespace UnoDraw.Rendering;

/// <summary>
/// Observes the shared browser device without replacing Uno's native callbacks or
/// polling every frame. One pending promise is retained per host device; unloaded
/// canvases are removed and never kept alive by that promise.
/// </summary>
internal static partial class WebGpuDeviceLifetime
{
    private static readonly Dictionary<int, DeviceWatch> s_devices = [];
    private static bool s_initialized;

    internal static IDisposable Subscribe(nint deviceHandle, WebGpuRasterCanvas canvas)
    {
        if (!s_initialized)
        {
            WebAssemblyRuntime.InvokeJS("""
                globalThis.UnoDrawCanvasBridge = {
                    waitForDeviceLoss: async device => {
                        const info = await device.lost;
                        return String(info.reason || 'unknown');
                    }
                };
                """);
            s_initialized = true;
        }

        var handle = unchecked((int)deviceHandle);
        var created = !s_devices.TryGetValue(handle, out var watch);
        if (created)
        {
            watch = new DeviceWatch(handle, WebGpuJsInterop.GetJsObject(handle));
            s_devices.Add(handle, watch);
        }

        var subscription = watch!.Add(canvas);
        if (created)
            watch.Start();
        return subscription;
    }

    [JSImport("globalThis.UnoDrawCanvasBridge.waitForDeviceLoss")]
    private static partial Task<string> WaitForDeviceLossAsync(JSObject device);

    // The proxy returned by Uno is borrowed. The JS marshaller may share it with
    // the host or another consumer, so this observer must never dispose it.
    private sealed class DeviceWatch(int handle, JSObject device)
    {
        private readonly List<Subscription> _subscriptions = [];

        internal Subscription Add(WebGpuRasterCanvas canvas)
        {
            var subscription = new Subscription(this, canvas);
            _subscriptions.Add(subscription);
            return subscription;
        }

        internal void Remove(Subscription subscription) => _subscriptions.Remove(subscription);

        internal void Start() => _ = ObserveAsync();

        private async Task ObserveAsync()
        {
            string reason;
            try
            {
                reason = await WaitForDeviceLossAsync(device);
            }
            catch (Exception error)
            {
                reason = $"device lifetime monitoring failed: {error.Message}";
            }
            s_devices.Remove(handle);
            var subscriptions = _subscriptions.ToArray();
            _subscriptions.Clear();
            foreach (var subscription in subscriptions)
                subscription.Notify(reason);
        }
    }

    private sealed class Subscription(DeviceWatch watch, WebGpuRasterCanvas canvas) : IDisposable
    {
        private readonly WeakReference<WebGpuRasterCanvas> _canvas = new(canvas);
        private bool _disposed;

        internal void Notify(string reason)
        {
            if (!_disposed && _canvas.TryGetTarget(out var canvas))
                canvas.OnDeviceLost(reason);
            _disposed = true;
        }

        public void Dispose()
        {
            if (_disposed)
                return;
            _disposed = true;
            watch.Remove(this);
        }
    }
}
