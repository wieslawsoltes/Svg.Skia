using System.Runtime.InteropServices;
using System.Runtime.InteropServices.JavaScript;
using Uno.Foundation;

namespace UnoDraw.Rendering;

/// <summary>Public Emscripten context operations and browser canvas transfer.</summary>
internal static partial class WebGlSkiaCanvasInterop
{
    private static bool s_initialized;

    internal static JSObject CreateCanvas(Action<string> unavailable, Action initialized)
    {
        if (!s_initialized)
        {
            using var source = typeof(WebGlSkiaCanvasInterop).Assembly.GetManifestResourceStream(
                "UnoDraw.WebGlSkiaCanvas.source")
                ?? throw new InvalidOperationException("The WebGL canvas transfer module is missing.");
            using var reader = new StreamReader(source);
            WebAssemblyRuntime.InvokeJS(reader.ReadToEnd());
            s_initialized = true;
        }
        return CreateCanvasCore(unavailable, initialized);
    }

    [JSImport("globalThis.UnoDrawWebGlSkia.createCanvas")]
    private static partial JSObject CreateCanvasCore(
        [JSMarshalAs<JSType.Function<JSType.String>>] Action<string> unavailable,
        [JSMarshalAs<JSType.Function>] Action initialized);

    [JSImport("globalThis.UnoDrawWebGlSkia.prepareCanvas")]
    internal static partial void PrepareCanvas(JSObject state);

    [JSImport("globalThis.UnoDrawWebGlSkia.copyCanvas")]
    internal static partial void CopyCanvas(JSObject state, JSObject device, JSObject queue,
        JSObject texture, int width, int height);

    [JSImport("globalThis.UnoDrawWebGlSkia.disposeCanvas")]
    internal static partial void DisposeCanvas(JSObject state);

    [DllImport("uno_draw_gl", EntryPoint = "uno_draw_gl_create", CallingConvention = CallingConvention.Cdecl)]
    internal static extern int CreateContext([MarshalAs(UnmanagedType.LPUTF8Str)] string selector);

    [DllImport("uno_draw_gl", EntryPoint = "uno_draw_gl_current", CallingConvention = CallingConvention.Cdecl)]
    internal static extern int GetCurrentContext();

    [DllImport("uno_draw_gl", EntryPoint = "uno_draw_gl_make_current", CallingConvention = CallingConvention.Cdecl)]
    internal static extern int MakeCurrent(int context);

    [DllImport("uno_draw_gl", EntryPoint = "uno_draw_gl_is_lost", CallingConvention = CallingConvention.Cdecl)]
    internal static extern int IsContextLost(int context);

    [DllImport("uno_draw_gl", EntryPoint = "uno_draw_gl_destroy", CallingConvention = CallingConvention.Cdecl)]
    internal static extern void DestroyContext(int context);

    [DllImport("uno_draw_gl", EntryPoint = "uno_draw_gl_resize", CallingConvention = CallingConvention.Cdecl)]
    internal static extern int Resize([MarshalAs(UnmanagedType.LPUTF8Str)] string selector, int width, int height);

    [DllImport("uno_draw_gl", EntryPoint = "uno_draw_gl_buffer_size", CallingConvention = CallingConvention.Cdecl)]
    internal static extern int GetBufferSize(int context, out int width, out int height);

    [DllImport("uno_draw_gl", EntryPoint = "uno_draw_gl_framebuffer", CallingConvention = CallingConvention.Cdecl)]
    internal static extern int GetFramebuffer(out int stencil, out int samples, out int maximumWidth, out int maximumHeight);

    [DllImport("uno_draw_gl", EntryPoint = "uno_draw_gl_error", CallingConvention = CallingConvention.Cdecl)]
    internal static extern int GetError();

    [DllImport("uno_draw_gl", EntryPoint = "uno_draw_gl_resolve", CallingConvention = CallingConvention.Cdecl)]
    internal static extern nint GetProcAddress([MarshalAs(UnmanagedType.LPUTF8Str)] string name);
}
