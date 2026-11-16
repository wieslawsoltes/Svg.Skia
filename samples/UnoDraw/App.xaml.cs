using Microsoft.UI.Xaml;
using Svg.Editor.Skia.Uno;
using Svg.Skia;

namespace UnoDraw;

public sealed partial class App : Application
{
    private Window? _window;
    private SvgEditorWorkspace? _workspace;
    private UnoDrawMcpConnection? _mcpConnection;
    internal UnoDrawMcpConnection? McpConnection => _mcpConnection;
    private bool _allowClose;
    private bool _closeInProgress;
    internal static string? StartupFile { get; set; }
    internal Task SaveRecoveryCheckpointAsync() => _workspace?.SaveRecoveryCheckpointAsync() ?? Task.CompletedTask;
    partial void OnPlatformWindowReady();
    partial void ConfigurePlatformWorkspace(SvgEditorWorkspace workspace);
    partial void ConfigureMcpConnection(UnoDrawMcpConnection connection);

    public App()
    {
        InitializeComponent();
        SKSvgJavaScriptRuntime.Register();
        Windows.ApplicationModel.Core.CoreApplication.Suspending += async (_, args) =>
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
        ConfigurePlatformWorkspace(workspace);
        _window.Content = workspace;
        workspace.HostWindow = _window;
        var desktop = !OperatingSystem.IsBrowser() && !OperatingSystem.IsAndroid() && !OperatingSystem.IsIOS();
        if (desktop || OperatingSystem.IsBrowser())
        {
            var connection = _mcpConnection = new UnoDrawMcpConnection(workspace.CreateMcpTarget);
            ConfigureMcpConnection(connection);
            workspace.McpConnection = connection;
            _window.Closed += async (_, _) => await connection.DisposeAsync();
        }
        if (desktop)
            _window.AppWindow.Resize(new Windows.Graphics.SizeInt32 { Width = 1440, Height = 960 });
        workspace.WorkspaceTitleChanged += (_, title) => _window.Title = title;
        if (desktop)
            _window.AppWindow.Closing += async (_, closing) =>
        {
            if (_allowClose)
                return;
            closing.Cancel = true;
            if (_closeInProgress)
                return;
            _closeInProgress = true;
            try
            {
                // Unwind MCP-owned dialogs and pickers before save confirmation.
                // A canceled close keeps the connection controller reconnectable.
                if (_mcpConnection is { } activeConnection)
                {
                    try
                    { await activeConnection.StopAsync(); }
                    catch (Exception error)
                    {
                        Console.Error.WriteLine("UnoDraw MCP could not stop: " + error.Message);
                        return;
                    }
                }
                if (!await workspace.ConfirmCloseAsync())
                    return;
                if (_mcpConnection is { } connection)
                {
                    try
                    { await connection.DisposeAsync(); }
                    catch (Exception error) { Console.Error.WriteLine("UnoDraw MCP shutdown: " + error.Message); }
                }
                _allowClose = true;
                workspace.Dispose();
                _window.Close();
            }
            finally { _closeInProgress = false; }
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
            if (_mcpConnection is { } connection)
                await connection.StartOnLaunchAsync();
        };
        _window.Activate();
        OnPlatformWindowReady();
    }
}
