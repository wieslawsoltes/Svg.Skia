using Microsoft.UI.Xaml;
using Svg.Editor.Skia.Uno;

namespace UnoDraw;

public sealed partial class App : Application
{
    private Window? _window;
    private bool _allowClose;

    public App()
    {
        InitializeComponent();
    }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        var workspace = new SvgEditorWorkspace();
        _window = new Window { Title = "UnoDraw", Content = workspace };
        workspace.HostWindow = _window;
        _window.AppWindow.Resize(new Windows.Graphics.SizeInt32 { Width = 1440, Height = 960 });
        workspace.WorkspaceTitleChanged += (_, title) => _window.Title = title;
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
            var path = Program.StartupFile;
            if (string.IsNullOrWhiteSpace(path))
                path = Path.Combine(AppContext.BaseDirectory, "Assets", "Welcome.svg");
            if (File.Exists(path))
                await workspace.OpenPathAsync(path, isExample: Program.StartupFile is null);
            else
                workspace.NewDocument();
            await workspace.OfferRecoveryAsync();
        };
        _window.Activate();
    }
}
