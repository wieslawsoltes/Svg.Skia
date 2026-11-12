using Uno.Foundation;

namespace UnoDraw;

/// <summary>Closes the pinned IDBFS flush snapshot gap around recovery-file renames.</summary>
internal static class BrowserStorageSynchronization
{
    internal static void Initialize()
    {
        using var stream = typeof(BrowserStorageSynchronization).Assembly.GetManifestResourceStream(
            "UnoDraw.BrowserStorageSynchronization.source")
            ?? throw new InvalidOperationException("The browser storage initialization resource is missing.");
        using var reader = new StreamReader(stream);
        var result = WebAssemblyRuntime.InvokeJS(reader.ReadToEnd());
        Console.WriteLine(result is "installed" or "already-installed"
            ? "UnoDraw browser storage initialized: ordered persistence snapshot."
            : "UnoDraw browser storage: using the runtime's original synchronization.");
    }
}
