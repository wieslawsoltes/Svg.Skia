namespace SvgML.Uno.Demo;

public sealed partial class App : Application
{
    private Window? _mainWindow;

    public App()
    {
        InitializeComponent();
    }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        _mainWindow = new Window();

        if (_mainWindow.Content is not Frame rootFrame)
        {
            rootFrame = new Frame();
            rootFrame.NavigationFailed += OnNavigationFailed;
            _mainWindow.Content = rootFrame;
        }

        if (rootFrame.Content is null)
        {
            rootFrame.Navigate(typeof(MainPage), args.Arguments);
        }

        _mainWindow.Activate();
    }

    private static void OnNavigationFailed(object sender, NavigationFailedEventArgs e)
    {
        throw new InvalidOperationException($"Failed to load {e.SourcePageType.FullName}: {e.Exception}");
    }
}
