using Uno.Resizetizer;
using Uno.UI.Xaml;
using Windows.UI.ViewManagement;
#if __MACOS__
using AppKit;
#endif

namespace UnoSvgEditorSample;

public sealed partial class App : Application
{
    private static readonly Thickness DefaultShellTopBarMargin = new(14, 10, 14, 4);

    public App()
    {
        Uno.UI.FeatureConfiguration.Font.DefaultTextFontFamily = "ms-appx:///Uno.Fonts.OpenSans/Fonts/OpenSans.ttf#Open Sans";
#if __SKIA__
        ApplicationView.PreferredLaunchViewSize = new Windows.Foundation.Size(1720, 1120);
#endif
        InitializeComponent();
    }

    private Window? MainWindow { get; set; }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        MainWindow = new Window();

        if (MainWindow.Content is not Frame rootFrame)
        {
            rootFrame = new Frame();
            MainWindow.Content = rootFrame;
            rootFrame.NavigationFailed += OnNavigationFailed;
        }

        MainWindow.SetWindowIcon();
        MainWindow.Activate();
        ConfigureWindowChrome(MainWindow);

        if (rootFrame.Content is null)
        {
            rootFrame.Navigate(typeof(MainPage), args.Arguments);
        }
    }

    private static void OnNavigationFailed(object sender, NavigationFailedEventArgs e)
    {
        throw new InvalidOperationException($"Failed to load {e.SourcePageType.FullName}: {e.Exception}");
    }

    private void ConfigureWindowChrome(Window window)
    {
        Resources["ShellTopBarMargin"] = DefaultShellTopBarMargin;

#if __MACOS__
        if (!OperatingSystem.IsMacOS())
        {
            return;
        }

        if (window.GetNativeWindow() is not NSWindow nativeWindow)
        {
            return;
        }

        nativeWindow.StyleMask |= NSWindowStyle.FullSizeContentView;
        nativeWindow.TitleVisibility = NSWindowTitleVisibility.Hidden;
        nativeWindow.TitlebarAppearsTransparent = true;
        nativeWindow.MovableByWindowBackground = true;

        Resources["ShellTopBarMargin"] = CreateMacShellTopBarMargin(nativeWindow);
#endif
    }

#if __MACOS__
    private static Thickness CreateMacShellTopBarMargin(NSWindow nativeWindow)
    {
        const double fallbackLeadingInset = 86;
        const double leadingPadding = 18;
        const double topInset = 8;
        const double rightInset = 14;
        const double bottomInset = 4;

        var leadingInset = fallbackLeadingInset;
        if (nativeWindow.StandardWindowButton(NSWindowButton.CloseButton) is NSButton closeButton)
        {
            leadingInset = Math.Max(
                fallbackLeadingInset,
                Math.Ceiling(closeButton.Frame.X + closeButton.Frame.Width + leadingPadding));
        }

        return new Thickness(leadingInset, topInset, rightInset, bottomInset);
    }
#endif
}
