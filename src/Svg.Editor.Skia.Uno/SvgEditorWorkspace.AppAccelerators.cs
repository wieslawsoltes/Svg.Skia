using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Input;
using Windows.System;

namespace Svg.Editor.Skia.Uno;

public sealed partial class SvgEditorWorkspace
{
    private void InitializeStudioAppAccelerators()
    {
        // App commands must not rely solely on the focused leaf's bubbling KeyDown.
        // Native popup dismissal and disabling scroll chrome can temporarily leave
        // no routable focused leaf. Do not register text-editing or canvas-only keys.
        void Add(VirtualKey key, VirtualKeyModifiers modifiers, string id)
        {
            var accelerator = new KeyboardAccelerator { Key = key, Modifiers = modifiers };
            accelerator.Invoked += async (_, args) =>
            {
                if (_disposed || !IsLoaded || !IsEnabled || Visibility != Visibility.Visible ||
                    _closing || _dialogVisible || XamlRoot is not { } root) return;
                var focused = FocusManager.GetFocusedElement(root) as DependencyObject;
                // Embedded editors must not intercept a different workspace's input.
                if (focused is not null && !StudioContainsFocus(focused, this)) return;
                args.Handled = true; // Set before await to prevent duplicate dispatch.
                await RunCommandAsync(id);
            };
            KeyboardAccelerators.Add(accelerator);
        }
        Add(VirtualKey.K, VirtualKeyModifiers.Control, "commands");
        Add(VirtualKey.N, VirtualKeyModifiers.Control, "new");
        Add(VirtualKey.O, VirtualKeyModifiers.Control, "open");
        Add(VirtualKey.S, VirtualKeyModifiers.Control, "save");
        Add(VirtualKey.S, VirtualKeyModifiers.Control | VirtualKeyModifiers.Shift, "saveas");
        Add(VirtualKey.E, VirtualKeyModifiers.Control, "export");
        Add(VirtualKey.P, VirtualKeyModifiers.Control, "print");
    }
}
