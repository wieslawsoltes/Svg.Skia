using System.Threading;
using Microsoft.UI.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Input;
using Svg.Editor.Core;
using Windows.System;
using Windows.UI.Core;

namespace Svg.Editor.Skia.Uno;

/// <summary>Opt-in bounded input diagnostics. Never records field text, labels or document content.</summary>
internal static class EditorInputTrace
{
    internal static bool Enabled { get; } = Environment.GetEnvironmentVariable("UNODRAW_EDITOR_INPUT_TRACE") == "1";
    private static int _sequence;
    private const int MaximumEntries = 16384;

    internal static void Write(string message)
    {
        if (!Enabled)
            return;
        var sequence = Interlocked.Increment(ref _sequence);
        if (sequence > MaximumEntries)
            return;
        Console.Error.WriteLine($"[EditorInput {sequence} {Environment.TickCount64}] {message}");
    }

    internal static void Key(string stage, VirtualKey key, bool handled, bool modal, XamlRoot? root, bool usesCommandKey)
    {
        if (!Enabled)
            return;
        var control = Down(VirtualKey.Control);
        var shift = Down(VirtualKey.Shift);
        var alt = Down(VirtualKey.Menu);
        var leftAlt = Down(VirtualKey.LeftMenu);
        var rightAlt = Down(VirtualKey.RightMenu);
        var meta = Down(VirtualKey.LeftWindows) || Down(VirtualKey.RightWindows);
        var valid = SvgEditorShortcutCatalog.TryNormalizeModifiers(usesCommandKey, control, shift, alt, meta,
            !usesCommandKey && control && rightAlt && !leftAlt, out var modifiers);
        var focused = root is null ? null : FocusManager.GetFocusedElement(root);
        Write($"{stage} key={key} handled={handled} modal={modal} focus={focused?.GetType().Name ?? "none"} " +
            $"ctrl={control} shift={shift} alt={alt} leftAlt={leftAlt} rightAlt={rightAlt} meta={meta} normalized={(valid ? modifiers.ToString() : "rejected")}");
    }

    private static bool Down(VirtualKey key)
        => (InputKeyboardSource.GetKeyStateForCurrentThread(key) & CoreVirtualKeyStates.Down) != 0;
}
