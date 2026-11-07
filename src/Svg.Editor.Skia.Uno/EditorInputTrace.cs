using System.Threading;
using Microsoft.UI.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
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
    private const int MaximumEntries = 65536;

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
        var valid = SvgEditorShortcutCatalog.TryNormalizeModifiersForKey((SvgEditorShortcutKey)key, usesCommandKey, control, shift, alt, meta,
            !usesCommandKey && control && rightAlt && !leftAlt, out var modifiers);
        var focused = root is null ? null : FocusManager.GetFocusedElement(root);
        Write($"{stage} key={key} handled={handled} modal={modal} focus={focused?.GetType().Name ?? "none"} " +
            $"ctrl={control} shift={shift} alt={alt} leftAlt={leftAlt} rightAlt={rightAlt} meta={meta} normalized={(valid ? modifiers.ToString() : "rejected")}");
    }

    internal static void Focus(string stage, XamlRoot? root, DependencyObject? source = null)
    {
        if (!Enabled)
            return;
        var focused = root is null ? null : FocusManager.GetFocusedElement(root) as DependencyObject;
        Write($"{stage} focus={focused?.GetType().Name ?? "none"} target={DiagnosticTarget(focused)} " +
            $"source={source?.GetType().Name ?? "none"} sourceTarget={DiagnosticTarget(source)}");
    }

    internal static void Pointer(string stage, PointerRoutedEventArgs args, XamlRoot? root)
    {
        if (!Enabled)
            return;
        Write($"{stage} handled={args.Handled} source={args.OriginalSource?.GetType().Name ?? "none"}");
        Focus(stage + "-focus", root, args.OriginalSource as DependencyObject);
    }

    private static string DiagnosticTarget(DependencyObject? element)
    {
        if (element is null)
            return "none";
        // Only fixed editor control IDs are permitted. Never log arbitrary names,
        // layer IDs, field contents, accessible labels or user-provided metadata.
        var id = AutomationProperties.GetAutomationId(element);
        if (id is "StudioCommandSearchBox" or "StudioCommandSearchResults")
            return id;
        const string prefix = "StudioReferencePoint";
        return id is not null && id.Length == prefix.Length + 1 && id.StartsWith(prefix, StringComparison.Ordinal)
            && id[^1] is >= '0' and <= '8' ? id : "other";
    }

    private static bool Down(VirtualKey key)
        => (InputKeyboardSource.GetKeyStateForCurrentThread(key) & CoreVirtualKeyStates.Down) != 0;
}
