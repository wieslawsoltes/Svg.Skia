using System.Runtime.InteropServices.JavaScript;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Input;
using Svg.Editor.Core;
using Windows.System;

namespace Svg.Editor.Skia.Uno;

public sealed partial class SvgEditorWorkspace
{
    private readonly HashSet<SvgEditorShortcutKey> _heldEditorShortcutKeys = new();
    private readonly List<KeyboardAccelerator> _editorKeyboardAccelerators = new();
    private readonly bool _editorUsesCommandKey = DetectCommandKeyPlatform();
    private Window? _shortcutHostWindow;
    private bool _editorWasActive;
    private bool _editorKeyboardInitialized;
    private event EventHandler? EditorShortcutsChanged;

    private string EditorShortcutText(string commandId)
        => _editorShortcuts.GetShortcutText(commandId, _editorUsesCommandKey);

    private void ApplyEditorShortcutMetadata()
    {
        // Clear legacy labels as well as applying defaults. An action without a
        // registered gesture must never advertise a shortcut owned by another action.
        foreach (var command in _commands)
            command.Shortcut = EditorShortcutText(command.Id);
        RefreshEditorShellShortcuts();
        if (_editorKeyboardInitialized)
            RebuildEditorAccelerators();
        EditorShortcutsChanged?.Invoke(this, EventArgs.Empty);
    }

    private void InitializeStudioAppAccelerators()
    {
        InitializeStudioCommandCenter();
        RegisterEditorNudgeCommands();
        LoadEditorShortcutProfile();
        Canvas.KeyboardShortcutsManagedExternally = true;
        KeyboardAcceleratorPlacementMode = KeyboardAcceleratorPlacementMode.Hidden;

        // Key releases must be observed even when a child handled them. Otherwise
        // a native text editor or popup can leave temporary navigation latched.
        AddHandler(KeyUpEvent, new KeyEventHandler(Workspace_KeyUp), true);
        GotFocus += (_, args) =>
        {
            _editorWasActive = true;
            EditorInputTrace.Focus("workspace-focus", XamlRoot, args.OriginalSource as DependencyObject);
        };
        if (EditorInputTrace.Enabled)
        {
            AddHandler(PointerPressedEvent, new PointerEventHandler((_, args) =>
                EditorInputTrace.Pointer("workspace-pointer-down", args, XamlRoot)), true);
            AddHandler(PointerReleasedEvent, new PointerEventHandler((_, args) =>
                EditorInputTrace.Pointer("workspace-pointer-up", args, XamlRoot)), true);
        }
        LostFocus += (_, _) => DispatcherQueue.TryEnqueue(() =>
        {
            var focused = XamlRoot is { } root ? FocusManager.GetFocusedElement(root) as DependencyObject : null;
            if (EditorShortcutContext(focused) != SvgEditorShortcutContext.Canvas)
                RestoreEditorNavigation();
            if (focused is not null && !StudioContainsFocus(focused, this))
            {
                _editorWasActive = false;
                _heldEditorShortcutKeys.Clear();
            }
        });
        Loaded += (_, _) =>
        {
            if (ReferenceEquals(_shortcutHostWindow, HostWindow))
                return;
            if (_shortcutHostWindow is not null)
                _shortcutHostWindow.Activated -= EditorWindow_Activated;
            _shortcutHostWindow = HostWindow;
            if (_shortcutHostWindow is not null)
                _shortcutHostWindow.Activated += EditorWindow_Activated;
        };
        Unloaded += (_, _) =>
        {
            CancelRulerGuide();
            RestoreEditorNavigation();
            _heldEditorShortcutKeys.Clear();
            _editorWasActive = false;
            if (_shortcutHostWindow is not null)
                _shortcutHostWindow.Activated -= EditorWindow_Activated;
            _shortcutHostWindow = null;
        };
        _editorKeyboardInitialized = true;
        ApplyEditorShortcutMetadata();
    }

    private void RebuildEditorAccelerators()
    {
        foreach (var accelerator in _editorKeyboardAccelerators)
            KeyboardAccelerators.Remove(accelerator);
        _editorKeyboardAccelerators.Clear();
        _heldEditorShortcutKeys.Clear();
        // Application and modified document shortcuts must work after dismissing
        // popup menus. Register from the same table used by bubbling key dispatch;
        // never register bare tools, arrows, Tab, or editing keys on native controls.
        foreach (var shortcut in _editorShortcuts.Bindings)
        {
            if (shortcut.Scope is SvgEditorShortcutScope.Canvas or SvgEditorShortcutScope.CanvasOrDocumentTree)
                continue;
            var accelerator = new KeyboardAccelerator
            {
                Key = (VirtualKey)shortcut.Key,
                Modifiers = EditorAcceleratorModifiers(shortcut.Modifiers, shortcut.Key)
            };
            accelerator.Invoked += async (_, args) =>
            {
                EditorInputTrace.Key("accelerator-" + shortcut.CommandId, (VirtualKey)shortcut.Key, args.Handled,
                    _dialogVisible, XamlRoot, _editorUsesCommandKey);
                if (_disposed || !IsLoaded || !IsEnabled || Visibility != Visibility.Visible ||
                    _closing || _dialogVisible || XamlRoot is not { } root)
                    return;
                var focused = FocusManager.GetFocusedElement(root) as DependencyObject;
                if (focused is null && !_editorWasActive)
                    return;
                if (!TryGetEditorModifiers(out var modifiers, (VirtualKey)shortcut.Key))
                    return;
                var match = _editorShortcuts.Resolve(shortcut.Key, modifiers, EditorShortcutContext(focused));
                if (!ReferenceEquals(match, shortcut) || !HasEditorCommand(shortcut.CommandId))
                    return;
                args.Handled = true;
                if (!_heldEditorShortcutKeys.Add(shortcut.Key) && !shortcut.Repeats)
                    return;
                await RunCommandAsync(shortcut.CommandId);
            };
            KeyboardAccelerators.Add(accelerator);
            _editorKeyboardAccelerators.Add(accelerator);
        }
    }

    private void DisposeEditorKeyboard()
    {
        CancelRulerGuide();
        RestoreEditorNavigation();
        if (_shortcutHostWindow is not null)
            _shortcutHostWindow.Activated -= EditorWindow_Activated;
        _shortcutHostWindow = null;
        foreach (var accelerator in _editorKeyboardAccelerators)
            KeyboardAccelerators.Remove(accelerator);
        _editorKeyboardAccelerators.Clear();
        _heldEditorShortcutKeys.Clear();
        _editorKeyboardInitialized = false;
        RemoveHandler(KeyUpEvent, new KeyEventHandler(Workspace_KeyUp));
    }

    private void RegisterEditorNudgeCommands()
    {
        void Add(string direction, double dx, double dy)
        {
            foreach (var large in new[] { false, true })
            {
                var id = "nudge-" + direction + (large ? "-large" : string.Empty);
                _commands.Add(new EditorCommand(id, "Nudge " + direction + (large ? " by 10 increments" : string.Empty), "Object", string.Empty, () =>
                {
                    var step = EditorKeyboardIncrement * (large ? 10 : 1);
                    Controller?.MoveSelection((float)(dx * step), (float)(dy * step));
                    return Task.CompletedTask;
                })
                {
                    CanExecute = () => Controller is { } controller && controller.SelectedElements.Count > 0
                    && !controller.SelectedElements.Any(global::Svg.Editor.Skia.SvgEditorController.IsEditLocked)
                });
            }
        }
        Add("left", -1, 0);
        Add("right", 1, 0);
        Add("up", 0, -1);
        Add("down", 0, 1);
    }

    private void EditorWindow_Activated(object sender, WindowActivatedEventArgs args)
    {
        if (args.WindowActivationState != Windows.UI.Core.CoreWindowActivationState.Deactivated)
            return;
        CancelRulerGuide();
        RestoreEditorNavigation();
        _editorWasActive = false;
        _heldEditorShortcutKeys.Clear();
    }

    private VirtualKeyModifiers EditorAcceleratorModifiers(SvgEditorShortcutModifiers modifiers, SvgEditorShortcutKey key)
    {
        var result = VirtualKeyModifiers.None;
        if ((modifiers & SvgEditorShortcutModifiers.Primary) != 0)
            result |= _editorUsesCommandKey && !SvgEditorShortcutCatalog.UsesPhysicalControl(key, modifiers)
                ? VirtualKeyModifiers.Windows : VirtualKeyModifiers.Control;
        if ((modifiers & SvgEditorShortcutModifiers.Shift) != 0)
            result |= VirtualKeyModifiers.Shift;
        if ((modifiers & SvgEditorShortcutModifiers.Alt) != 0)
            result |= VirtualKeyModifiers.Menu;
        return result;
    }

    private bool TryGetEditorModifiers(out SvgEditorShortcutModifiers modifiers, VirtualKey? key = null)
    {
        var control = IsDown(VirtualKey.Control);
        var alt = IsDown(VirtualKey.Menu);
        // AltGr commonly arrives as synthetic Control plus right Alt. Preserve it
        // for international text input; never reduce it to a Control shortcut.
        var altGraph = !_editorUsesCommandKey && control && IsDown(VirtualKey.RightMenu) && !IsDown(VirtualKey.LeftMenu);
        return SvgEditorShortcutCatalog.TryNormalizeModifiersForKey((SvgEditorShortcutKey)(key ?? VirtualKey.None), _editorUsesCommandKey, control, IsDown(VirtualKey.Shift), alt,
            IsDown(VirtualKey.LeftWindows) || IsDown(VirtualKey.RightWindows), altGraph, out modifiers);
    }

    private static bool DetectCommandKeyPlatform()
    {
        if (OperatingSystem.IsMacOS() || OperatingSystem.IsIOS() || OperatingSystem.IsMacCatalyst())
            return true;
        if (!OperatingSystem.IsBrowser())
            return false;
        // Browser .NET reports "browser", not its host OS. Read the platform with
        // standard JS interop so a Mac browser gets Command labels and dispatch.
        using var navigator = JSHost.GlobalThis.GetPropertyAsJSObject("navigator");
        var platform = navigator?.GetPropertyAsString("platform") ?? string.Empty;
        return platform.Contains("Mac", StringComparison.OrdinalIgnoreCase)
            || platform.Contains("iPad", StringComparison.OrdinalIgnoreCase)
            || platform.Contains("iPhone", StringComparison.OrdinalIgnoreCase)
            || platform.Contains("iPod", StringComparison.OrdinalIgnoreCase);
    }
}
