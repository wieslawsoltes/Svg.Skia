using Microsoft.UI.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Svg.Editor.Core;
using Windows.ApplicationModel.DataTransfer;
using Windows.System;
using Windows.UI.Core;

namespace Svg.Editor.Skia.Uno;

public sealed partial class SvgEditorWorkspace
{
    private string? _pendingCompactMenuCommand;
    private bool _spacePreviousPanMode;
    private bool _spacePreviousZoomMode;
    private bool _spacePreviousZoomOutMode;

    private void BuildMenus() => BuildEditorMenus();

    private async void Command_Click(object sender, RoutedEventArgs args)
    { if (sender is FrameworkElement { Tag: string id }) await RunCommandAsync(id); }

    private async Task RunCommandAsync(string id)
    {
        EditorInputTrace.Write($"command-request id={id} modal={_dialogVisible} closing={_closing}");
        if (_dialogVisible || _closing)
            return;
        try
        {
            var command = _commands.FirstOrDefault(candidate => candidate.Id == id);
            if (command is null || command.CanExecute?.Invoke() == false)
                return;
            CancelRulerGuide();
            RestoreEditorNavigation();
            CommitInspectorEdits();
            if (id != "finishpath")
                Canvas.CancelGesture();
            await command.Execute();
        }
        catch (Exception exception) { await ShowErrorAsync(exception.Message); }
        finally
        {
            if (!_disposed)
                RefreshEditorMenus();
            EditorInputTrace.Write("command-completed id=" + id);
        }
    }

    private Task CopyAsync(bool cut)
    {
        if (Controller is not { } controller || controller.SelectedElements.Count == 0)
            return Task.CompletedTask;
        var svg = controller.CopySelection();
        var package = new DataPackage { RequestedOperation = cut ? DataPackageOperation.Move : DataPackageOperation.Copy };
        package.SetText(svg);
        package.SetData("image/svg+xml", svg);
        Clipboard.SetContent(package);
        if (cut)
            controller.DeleteSelection();
        return Task.CompletedTask;
    }

    private async Task PasteAsync()
    {
        if (Controller is not { } controller)
            return;
        var target = new global::Svg.Editor.Skia.SvgEditorSelectionContext(controller);
        var content = Clipboard.GetContent();
        if (content.Contains("image/svg+xml") && await content.GetDataAsync("image/svg+xml") is string svg)
        {
            if (!_disposed && !_closing && target.IsCurrent(Controller))
                controller.Paste(svg);
        }
        else if (content.Contains(StandardDataFormats.Text))
        {
            var text = await content.GetTextAsync();
            // A clipboard provider can complete after a document switch, close or
            // selection change. Never apply delayed content to a different target.
            if (_disposed || _closing || !target.IsCurrent(Controller))
                return;
            if (text.TrimStart().StartsWith('<'))
                controller.Paste(text);
            else if (controller.SelectedElements.OfType<SvgTextBase>().Any())
                controller.SetText(text);
        }
        else if (!_disposed && !_closing && target.IsCurrent(Controller))
            controller.Paste();
    }

    private void ToggleAnimation()
    {
        if (_animationClock.IsRunning)
        { _animationTimer.Stop(); _animationClock.Stop(); return; }
        _animationClock.Start();
        _animationTimer.Start();
    }

    private static bool StudioContainsFocus(DependencyObject? focused, DependencyObject ancestor)
    {
        for (var item = focused; item is not null; item = VisualTreeHelper.GetParent(item))
            if (ReferenceEquals(item, ancestor))
                return true;
        return false;
    }

    private static bool StudioIsTextInput(DependencyObject? focused)
    {
        for (var item = focused; item is not null; item = VisualTreeHelper.GetParent(item))
            if (item is TextBox or PasswordBox or NumberBox or RichEditBox or ComboBox or AutoSuggestBox)
                return true;
        return false;
    }

    private SvgEditorShortcutContext EditorShortcutContext(DependencyObject? focused, DependencyObject? originalSource = null)
    {
        if (_closing || _dialogVisible || _disposed || focused is not null && !StudioContainsFocus(focused, this))
            return SvgEditorShortcutContext.Blocked;
        if (StudioIsTextInput(focused) || StudioIsTextInput(originalSource))
            return SvgEditorShortcutContext.TextInput;
        if (focused is null || ReferenceEquals(focused, this) || StudioContainsFocus(focused, Canvas))
            return SvgEditorShortcutContext.Canvas;
        if (StudioContainsFocus(focused, DocumentOutline))
            return SvgEditorShortcutContext.DocumentTree;
        return SvgEditorShortcutContext.OtherControl;
    }

    private bool HasEditorCommand(string id) => _commands.Any(command => command.Id == id);

    private async void Workspace_KeyDown(object sender, KeyRoutedEventArgs args)
    {
        EditorInputTrace.Key("workspace-down", args.Key, args.Handled, _dialogVisible, XamlRoot, _editorUsesCommandKey);
        if (_closing)
        { args.Handled = true; return; }
        if (_disposed || _dialogVisible || args.Handled)
            return;
        var focused = XamlRoot is { } root ? FocusManager.GetFocusedElement(root) as DependencyObject : null;
        var context = EditorShortcutContext(focused, args.OriginalSource as DependencyObject);
        if (context == SvgEditorShortcutContext.Blocked || !TryGetEditorModifiers(out var modifiers, args.Key))
            return;
        if (context == SvgEditorShortcutContext.Canvas && Canvas.ContextFlyout is { } contextMenu &&
            (args.Key == VirtualKey.Application && modifiers == SvgEditorShortcutModifiers.None ||
             args.Key == VirtualKey.F10 && modifiers == SvgEditorShortcutModifiers.Shift))
        {
            // The workspace owns drawing focus, so native context-menu keys need
            // an explicit canvas target. Controls and text fields keep their menus.
            args.Handled = true;
            if (args.KeyStatus.WasKeyDown)
                return;
            RestoreEditorNavigation();
            Canvas.CancelGesture();
            var point = Controller?.SelectionBounds is { } bounds
                ? Canvas.Viewport.WorldToView(new SkiaSharp.SKPoint(bounds.MidX, bounds.MidY))
                : new SkiaSharp.SKPoint((float)Canvas.ActualWidth / 2, (float)Canvas.ActualHeight / 2);
            contextMenu.ShowAt(Canvas, new FlyoutShowOptions
            {
                Position = new Windows.Foundation.Point(Math.Max(0, Math.Min(Canvas.ActualWidth, point.X)),
                    Math.Max(0, Math.Min(Canvas.ActualHeight, point.Y)))
            });
            return;
        }
        if (_spacePanning && IsEditorModifierKey(args.Key))
        {
            ApplyEditorNavigation(SvgEditorShortcutCatalog.Navigation(modifiers, context));
            return;
        }
        if (args.Key == VirtualKey.Space)
        {
            var navigation = SvgEditorShortcutCatalog.Navigation(modifiers, context);
            if (navigation == SvgEditorNavigationShortcut.None)
                return;
            ApplyEditorNavigation(navigation);
            args.Handled = true;
            return;
        }
        if (args.Key == VirtualKey.Escape && modifiers == SvgEditorShortcutModifiers.None &&
            context != SvgEditorShortcutContext.TextInput && _temporaryStudioPanel is not null)
        {
            RestoreEditorNavigation();
            ApplySafely(() => { CommitInspectorEdits(); _temporaryStudioPanel = null; RebuildStudioDocks(); ApplyStudioMetrics(); });
            args.Handled = true;
            return;
        }

        var key = (SvgEditorShortcutKey)args.Key;
        var shortcut = _editorShortcuts.Resolve(key, modifiers, context);
        if (shortcut is null)
        {
            if (TryActivateEditorMenu(args.Key, modifiers))
                args.Handled = true;
            return;
        }
        if (!HasEditorCommand(shortcut.CommandId))
            return;
        args.Handled = true;
        var firstPress = _heldEditorShortcutKeys.Add(key);
        if ((!firstPress || args.KeyStatus.WasKeyDown) && !shortcut.Repeats)
            return;
        await RunCommandAsync(shortcut.CommandId);
    }

    private static bool IsDown(VirtualKey key) => (InputKeyboardSource.GetKeyStateForCurrentThread(key) & CoreVirtualKeyStates.Down) != 0;

    private static bool IsEditorModifierKey(VirtualKey key)
        => key is VirtualKey.Control or VirtualKey.LeftControl or VirtualKey.RightControl
            or VirtualKey.Shift or VirtualKey.LeftShift or VirtualKey.RightShift
            or VirtualKey.Menu or VirtualKey.LeftMenu or VirtualKey.RightMenu
            or VirtualKey.LeftWindows or VirtualKey.RightWindows;

    private void ApplyEditorNavigation(SvgEditorNavigationShortcut navigation)
    {
        if (navigation == SvgEditorNavigationShortcut.None)
        { RestoreEditorNavigation(); return; }
        if (!_spacePanning)
        {
            _spacePanning = true;
            _spacePreviousPanMode = Canvas.PanMode;
            _spacePreviousZoomMode = Canvas.ZoomMode;
            _spacePreviousZoomOutMode = Canvas.ZoomOutMode;
        }
        Canvas.PanMode = navigation == SvgEditorNavigationShortcut.Hand;
        Canvas.ZoomMode = navigation is SvgEditorNavigationShortcut.ZoomIn or SvgEditorNavigationShortcut.ZoomOut;
        Canvas.ZoomOutMode = navigation == SvgEditorNavigationShortcut.ZoomOut;
        RefreshEditorToolState();
    }

    private void RestoreEditorNavigation()
    {
        if (!_spacePanning)
            return;
        _spacePanning = false;
        Canvas.PanMode = _spacePreviousPanMode;
        Canvas.ZoomMode = _spacePreviousZoomMode;
        Canvas.ZoomOutMode = _spacePreviousZoomOutMode;
        RefreshEditorToolState();
    }

    private void Workspace_KeyUp(object sender, KeyRoutedEventArgs args)
    {
        EditorInputTrace.Key("workspace-up", args.Key, args.Handled, _dialogVisible, XamlRoot, _editorUsesCommandKey);
        _heldEditorShortcutKeys.Remove((SvgEditorShortcutKey)args.Key);
        if (!_spacePanning)
            return;
        if (args.Key == VirtualKey.Space)
        {
            RestoreEditorNavigation();
            args.Handled = true;
        }
        else if (IsEditorModifierKey(args.Key))
        {
            if (!TryGetEditorModifiers(out var modifiers))
            { RestoreEditorNavigation(); return; }
            var focused = XamlRoot is { } root ? FocusManager.GetFocusedElement(root) as DependencyObject : null;
            ApplyEditorNavigation(SvgEditorShortcutCatalog.Navigation(modifiers, EditorShortcutContext(focused)));
        }
    }
}
