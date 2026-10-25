using System;
using System.Threading;
using Foundation;
using UIKit;
using Uno.UI.Runtime.Skia.AppleUIKit;

namespace UnoDraw.iOS;

[Register("UnoDrawApplicationDelegate")]
public sealed class UnoDrawApplicationDelegate : UnoUIApplicationDelegate
{
    private nint _recoveryTask = UIApplication.BackgroundTaskInvalid;
    private bool _savingRecovery;

    public override async void DidEnterBackground(UIApplication application)
    {
        base.DidEnterBackground(application);
        if (_savingRecovery || Microsoft.UI.Xaml.Application.Current is not App app)
            return;

        // Uno 6.5 observes UIScene notifications on iOS 13+, while this head uses
        // the legacy UIWindow lifecycle. Save directly for its application event.
        // A background task lets the atomic recovery write finish after this
        // callback returns; the shared recovery gate coalesces repeated saves.
        _savingRecovery = true;
        _recoveryTask = application.BeginBackgroundTask("Save UnoDraw recovery", () => EndRecoveryTask(application));
        try
        {
            await app.SaveRecoveryCheckpointAsync();
        }
        catch (Exception exception)
        {
            // Exceptions cannot escape an asynchronous native lifecycle callback.
            // The workspace reports storage failures; this also covers editor
            // validation that may run before the checkpoint starts.
            Console.Error.WriteLine($"UnoDraw recovery checkpoint failed: {exception}");
        }
        finally
        {
            _savingRecovery = false;
            EndRecoveryTask(application);
        }
    }

    private void EndRecoveryTask(UIApplication application)
    {
        var task = Interlocked.Exchange(ref _recoveryTask, UIApplication.BackgroundTaskInvalid);
        if (task != UIApplication.BackgroundTaskInvalid)
            application.EndBackgroundTask(task);
    }
}
