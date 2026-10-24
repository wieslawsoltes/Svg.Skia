using Microsoft.UI.Xaml;
using Svg.Editor.Skia.Uno;
using Svg.Skia;

namespace UnoDraw;

public sealed partial class App : Application
{
    private Window? _window;
    private SvgEditorWorkspace? _workspace;
    private bool _allowClose;
    internal static string? StartupFile { get; set; }
    internal Task SaveRecoveryCheckpointAsync() => _workspace?.SaveRecoveryCheckpointAsync() ?? Task.CompletedTask;
    partial void OnPlatformWindowReady();

    public App()
    {
        InitializeComponent();
        SKSvgJavaScriptRuntime.Register();
        Suspending += async (_, args) =>
        {
            var deferral = args.SuspendingOperation.GetDeferral();
            try
            { if (_workspace is { } workspace) await workspace.SaveRecoveryCheckpointAsync(); }
            finally { deferral.Complete(); }
        };
    }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        _window = new Window { Title = "UnoDraw" };
        var workspace = _workspace = new SvgEditorWorkspace();
        _window.Content = workspace;
        workspace.HostWindow = _window;
        var desktop = !OperatingSystem.IsBrowser() && !OperatingSystem.IsAndroid() && !OperatingSystem.IsIOS();
        if (desktop)
            _window.AppWindow.Resize(new Windows.Graphics.SizeInt32 { Width = 1440, Height = 960 });
        workspace.WorkspaceTitleChanged += (_, title) => _window.Title = title;
        if (desktop)
            _window.AppWindow.Closing += async (_, closing) =>
        {
            if (_allowClose)
                return;
            closing.Cancel = true;
            if (await workspace.ConfirmCloseAsync())
            {
                _allowClose = true;
                workspace.Dispose();
                _window.Close();
            }
        };
        workspace.Loaded += async (_, _) =>
        {
            if (workspace.Documents.Count > 0)
                return;
            if (!string.IsNullOrWhiteSpace(StartupFile) && File.Exists(StartupFile))
                await workspace.OpenPathAsync(StartupFile);
            else
            {
                using var source = typeof(App).Assembly.GetManifestResourceStream("UnoDraw.Welcome.svg");
                if (source is not null)
                {
                    using var reader = new StreamReader(source);
                    await workspace.OpenSvgSourceAsync(await reader.ReadToEndAsync(), "Welcome.svg");
                }
                else
                    workspace.NewDocument();
            }
            await workspace.OfferRecoveryAsync();
        };
        _window.Activate();
        OnPlatformWindowReady();
    }
}
