using Windows.UI.ViewManagement;

namespace UnoColorPickerSample;

public sealed partial class App : Application
{
    private Window? MainWindow { get; set; }

    public App()
    {
#if __SKIA__
        ApplicationView.PreferredLaunchViewSize = new Windows.Foundation.Size(1280, 920);
#endif
        InitializeComponent();
    }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        MainWindow = new Window();

        if (MainWindow.Content is not Frame rootFrame)
        {
            rootFrame = new Frame();
            MainWindow.Content = rootFrame;
            rootFrame.NavigationFailed += OnNavigationFailed;
        }

        MainWindow.Activate();

        if (rootFrame.Content is null)
        {
            rootFrame.Navigate(typeof(MainPage), args.Arguments);
        }
    }

    private static void OnNavigationFailed(object sender, NavigationFailedEventArgs e)
    {
        throw new InvalidOperationException($"Failed to load {e.SourcePageType.FullName}: {e.Exception}");
    }
}
