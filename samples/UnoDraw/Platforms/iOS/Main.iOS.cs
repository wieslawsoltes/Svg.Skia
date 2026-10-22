using Uno.Extensions.Storage.Pickers;
using Uno.Foundation.Extensibility;
using Uno.UI.Hosting;

namespace UnoDraw.iOS;

public static class EntryPoint
{
    public static void Main(string[] args)
    {
        ApiExtensibility.Register(typeof(IFileSavePickerExtension), owner => new IosFileSavePickerExtension(owner));
        var host = UnoPlatformHostBuilder.Create()
            .App(() => new App())
            .UseAppleUIKit(builder => builder.UseUIApplicationDelegate<UnoDrawApplicationDelegate>())
            .Build();
        host.Run();
    }
}
