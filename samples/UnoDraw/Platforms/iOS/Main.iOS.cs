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
            .App(() =>
            {
                // Preserve shortcuts and workspaces from the pre-7 settings store
                // before constructing the editor. Existing migrated values win.
                Uno.Storage.ApplicationDataMigrator.MigrateSettings();
                return new App();
            })
            .UseAppleUIKit(builder => builder.UseUIApplicationDelegate<UnoDrawApplicationDelegate>())
            .Build();
        host.Run();
    }
}
