using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace Svg.Editor.Core;

/// <summary>Logical modifiers. Primary is Command on Apple platforms and Control elsewhere.</summary>
[Flags]
public enum SvgEditorShortcutModifiers
{
    None = 0,
    Primary = 1,
    Shift = 2,
    Alt = 4
}

/// <summary>
/// Portable virtual keys used by editor shortcuts. Values match the virtual-key
/// contract used by XAML; hosts using another input API translate at their boundary.
/// </summary>
public enum SvgEditorShortcutKey
{
    Backspace = 8, Tab = 9, Enter = 13, Escape = 27, Space = 32,
    Left = 37, Up = 38, Right = 39, Down = 40, Delete = 46,
    Number0 = 48, Number1 = 49, Number2 = 50, Number3 = 51, Number4 = 52,
    Number5 = 53, Number6 = 54, Number7 = 55, Number8 = 56, Number9 = 57,
    A = 65, B = 66, C = 67, D = 68, E = 69, F = 70, G = 71, H = 72, I = 73,
    J = 74, K = 75, L = 76, M = 77, N = 78, O = 79, P = 80, Q = 81, R = 82,
    S = 83, T = 84, U = 85, V = 86, W = 87, X = 88, Y = 89, Z = 90,
    Add = 107, Subtract = 109,
    F1 = 112, F2 = 113, F3 = 114, F4 = 115, F5 = 116, F6 = 117,
    F7 = 118, F8 = 119, F9 = 120, F10 = 121, F11 = 122, F12 = 123,
    Semicolon = 186, Equals = 187, Comma = 188, Minus = 189, Period = 190,
    Slash = 191, Backquote = 192, OpenBracket = 219, Backslash = 220,
    CloseBracket = 221, Quote = 222
}

public enum SvgEditorShortcutContext
{
    Canvas,
    DocumentTree,
    OtherControl,
    TextInput,
    Blocked
}

public enum SvgEditorShortcutScope
{
    Application,
    Document,
    Canvas,
    CanvasOrDocumentTree
}

public enum SvgEditorNavigationShortcut
{
    None,
    Hand,
    ZoomIn,
    ZoomOut
}

/// <summary>One exact gesture. Matching never discards additional modifiers.</summary>
public sealed class SvgEditorShortcut
{
    internal SvgEditorShortcut(string commandId, SvgEditorShortcutKey key, SvgEditorShortcutModifiers modifiers,
        SvgEditorShortcutScope scope, bool repeats, bool preferred)
    {
        CommandId = commandId;
        Key = key;
        Modifiers = modifiers;
        Scope = scope;
        Repeats = repeats;
        IsPreferred = preferred;
    }

    public string CommandId { get; }
    public SvgEditorShortcutKey Key { get; }
    public SvgEditorShortcutModifiers Modifiers { get; }
    public SvgEditorShortcutScope Scope { get; }
    public bool Repeats { get; }
    public bool IsPreferred { get; }

    public bool Allows(SvgEditorShortcutContext context)
    {
        if (context == SvgEditorShortcutContext.Blocked) return false;
        switch (Scope)
        {
            case SvgEditorShortcutScope.Application:
                // Some hosts report AltGr as Ctrl+Alt without exposing its right-key
                // state. Never let a global Alt chord interrupt text composition.
                return context != SvgEditorShortcutContext.TextInput || (Modifiers & SvgEditorShortcutModifiers.Alt) == 0;
            case SvgEditorShortcutScope.Document:
                return context != SvgEditorShortcutContext.TextInput;
            case SvgEditorShortcutScope.Canvas:
                return context == SvgEditorShortcutContext.Canvas;
            case SvgEditorShortcutScope.CanvasOrDocumentTree:
                return context == SvgEditorShortcutContext.Canvas || context == SvgEditorShortcutContext.DocumentTree;
            default:
                return false;
        }
    }

    public string GetDisplayText(bool usesCommandKey = false)
    {
        var text = string.Empty;
        if ((Modifiers & SvgEditorShortcutModifiers.Primary) != 0) text += usesCommandKey ? "Cmd+" : "Ctrl+";
        if ((Modifiers & SvgEditorShortcutModifiers.Alt) != 0) text += usesCommandKey ? "Option+" : "Alt+";
        if ((Modifiers & SvgEditorShortcutModifiers.Shift) != 0) text += "Shift+";
        return text + KeyText(Key);
    }

    private static string KeyText(SvgEditorShortcutKey key)
    {
        var value = (int)key;
        if (value >= (int)SvgEditorShortcutKey.A && value <= (int)SvgEditorShortcutKey.Z)
            return ((char)value).ToString();
        if (value >= (int)SvgEditorShortcutKey.Number0 && value <= 57)
            return ((char)value).ToString();
        if (value >= (int)SvgEditorShortcutKey.F1 && value <= (int)SvgEditorShortcutKey.F12)
            return "F" + (value - (int)SvgEditorShortcutKey.F1 + 1);
        switch (key)
        {
            case SvgEditorShortcutKey.Escape: return "Esc";
            case SvgEditorShortcutKey.Equals: return "=";
            case SvgEditorShortcutKey.Minus: return "−";
            case SvgEditorShortcutKey.Add: return "Num +";
            case SvgEditorShortcutKey.Subtract: return "Num −";
            case SvgEditorShortcutKey.OpenBracket: return "[";
            case SvgEditorShortcutKey.CloseBracket: return "]";
            case SvgEditorShortcutKey.Backslash: return "\\";
            case SvgEditorShortcutKey.Slash: return "/";
            case SvgEditorShortcutKey.Quote: return "'";
            case SvgEditorShortcutKey.Semicolon: return ";";
            case SvgEditorShortcutKey.Backquote: return "`";
            case SvgEditorShortcutKey.Comma: return ",";
            case SvgEditorShortcutKey.Period: return ".";
            default: return key.ToString();
        }
    }
}

/// <summary>
/// Default EditorTheme gestures shared by dispatch, menus, tooltips, command search
/// and help. Availability is deliberately left to the host's command catalog.
/// </summary>
public static class SvgEditorShortcutCatalog
{
    private static readonly ReadOnlyCollection<SvgEditorShortcut> s_bindings = Build();
    private static readonly Dictionary<int, SvgEditorShortcut> s_byGesture = IndexGestures();
    private static readonly Dictionary<string, SvgEditorShortcut> s_preferred = IndexPreferred();

    public static IReadOnlyList<SvgEditorShortcut> Bindings => s_bindings;

    public static SvgEditorShortcut? Resolve(SvgEditorShortcutKey key, SvgEditorShortcutModifiers modifiers,
        SvgEditorShortcutContext context)
        => s_byGesture.TryGetValue(GestureKey(key, modifiers), out var shortcut) && shortcut.Allows(context) ? shortcut : null;

    public static string GetShortcutText(string commandId, bool usesCommandKey = false)
        => s_preferred.TryGetValue(commandId, out var shortcut) ? shortcut.GetDisplayText(usesCommandKey) : string.Empty;

    public static string FormatGesture(SvgEditorShortcutKey key, SvgEditorShortcutModifiers modifiers, bool usesCommandKey = false)
        => new SvgEditorShortcut(string.Empty, key, modifiers, SvgEditorShortcutScope.Canvas, false, false).GetDisplayText(usesCommandKey);

    /// <summary>
    /// Rejects AltGraph and the other platform's system modifier instead of treating
    /// them as ordinary shortcuts. This prevents text composition and OS chords from
    /// invoking editor actions. A host should pass AltGraph when its input API exposes it.
    /// </summary>
    public static bool TryNormalizeModifiers(bool usesCommandKey, bool control, bool shift, bool alt,
        bool meta, bool altGraph, out SvgEditorShortcutModifiers modifiers)
    {
        modifiers = SvgEditorShortcutModifiers.None;
        if (altGraph || (usesCommandKey ? control : meta)) return false;
        if (usesCommandKey ? meta : control) modifiers |= SvgEditorShortcutModifiers.Primary;
        if (shift) modifiers |= SvgEditorShortcutModifiers.Shift;
        if (alt) modifiers |= SvgEditorShortcutModifiers.Alt;
        return true;
    }

    public static SvgEditorNavigationShortcut Navigation(SvgEditorShortcutModifiers modifiers, SvgEditorShortcutContext context)
    {
        if (context != SvgEditorShortcutContext.Canvas) return SvgEditorNavigationShortcut.None;
        switch (modifiers)
        {
            case SvgEditorShortcutModifiers.None:
            case SvgEditorShortcutModifiers.Shift:
                return SvgEditorNavigationShortcut.Hand;
            case SvgEditorShortcutModifiers.Primary:
                return SvgEditorNavigationShortcut.ZoomIn;
            case SvgEditorShortcutModifiers.Primary | SvgEditorShortcutModifiers.Alt:
                return SvgEditorNavigationShortcut.ZoomOut;
            default:
                return SvgEditorNavigationShortcut.None;
        }
    }

    private static int GestureKey(SvgEditorShortcutKey key, SvgEditorShortcutModifiers modifiers)
        => ((int)modifiers << 16) | (int)key;

    private static Dictionary<int, SvgEditorShortcut> IndexGestures()
    {
        var result = new Dictionary<int, SvgEditorShortcut>();
        foreach (var shortcut in s_bindings) result.Add(GestureKey(shortcut.Key, shortcut.Modifiers), shortcut);
        return result;
    }

    private static Dictionary<string, SvgEditorShortcut> IndexPreferred()
    {
        var result = new Dictionary<string, SvgEditorShortcut>(StringComparer.Ordinal);
        foreach (var shortcut in s_bindings)
            if (shortcut.IsPreferred) result.Add(shortcut.CommandId, shortcut);
        return result;
    }

    private static ReadOnlyCollection<SvgEditorShortcut> Build()
    {
        const SvgEditorShortcutModifiers primary = SvgEditorShortcutModifiers.Primary;
        const SvgEditorShortcutModifiers shift = SvgEditorShortcutModifiers.Shift;
        const SvgEditorShortcutModifiers alt = SvgEditorShortcutModifiers.Alt;
        const SvgEditorShortcutScope app = SvgEditorShortcutScope.Application;
        const SvgEditorShortcutScope canvas = SvgEditorShortcutScope.Canvas;
        var result = new List<SvgEditorShortcut>();
        void Add(string id, SvgEditorShortcutKey key, SvgEditorShortcutModifiers modifiers = SvgEditorShortcutModifiers.None,
            SvgEditorShortcutScope scope = SvgEditorShortcutScope.Document, bool repeats = false, bool preferred = true)
            => result.Add(new SvgEditorShortcut(id, key, modifiers, scope, repeats, preferred));

        Add("new", SvgEditorShortcutKey.N, primary, app);
        Add("open", SvgEditorShortcutKey.O, primary, app);
        Add("save", SvgEditorShortcutKey.S, primary, app);
        Add("saveas", SvgEditorShortcutKey.S, primary | shift, app);
        Add("close", SvgEditorShortcutKey.W, primary, app);
        Add("image", SvgEditorShortcutKey.P, primary | shift, app);
        Add("export", SvgEditorShortcutKey.E, primary | alt, app);
        Add("print", SvgEditorShortcutKey.P, primary, app);
        Add("preferences", SvgEditorShortcutKey.K, primary, app);
        Add("shortcutsettings", SvgEditorShortcutKey.K, primary | alt | shift, app);
        Add("commands", SvgEditorShortcutKey.F12, primary | shift, app);
        Add("help", SvgEditorShortcutKey.F1, scope: app);

        Add("undo", SvgEditorShortcutKey.Z, primary);
        Add("redo", SvgEditorShortcutKey.Z, primary | shift);
        Add("cut", SvgEditorShortcutKey.X, primary);
        Add("copy", SvgEditorShortcutKey.C, primary);
        Add("paste", SvgEditorShortcutKey.V, primary);
        Add("pasteinfront", SvgEditorShortcutKey.F, primary);
        Add("pastebehind", SvgEditorShortcutKey.B, primary);
        Add("pasteinplace", SvgEditorShortcutKey.V, primary | shift);
        Add("delete", SvgEditorShortcutKey.Delete, scope: SvgEditorShortcutScope.CanvasOrDocumentTree);
        Add("delete", SvgEditorShortcutKey.Backspace, scope: canvas, preferred: false);
        Add("selectall", SvgEditorShortcutKey.A, primary);
        Add("deselect", SvgEditorShortcutKey.A, primary | shift);
        Add("cancel-action", SvgEditorShortcutKey.Escape, scope: canvas);
        Add("group", SvgEditorShortcutKey.G, primary);
        Add("ungroup", SvgEditorShortcutKey.G, primary | shift);
        Add("lock", SvgEditorShortcutKey.Number2, primary);
        Add("unlockall", SvgEditorShortcutKey.Number2, primary | alt);
        Add("hide", SvgEditorShortcutKey.Number3, primary);
        Add("showall", SvgEditorShortcutKey.Number3, primary | alt);
        Add("forward", SvgEditorShortcutKey.CloseBracket, primary);
        Add("front", SvgEditorShortcutKey.CloseBracket, primary | shift);
        Add("backward", SvgEditorShortcutKey.OpenBracket, primary);
        Add("back", SvgEditorShortcutKey.OpenBracket, primary | shift);
        Add("transformagain", SvgEditorShortcutKey.D, primary);
        Add("transform", SvgEditorShortcutKey.M, primary | shift);
        Add("clip", SvgEditorShortcutKey.Number7, primary);
        Add("unclip", SvgEditorShortcutKey.Number7, primary | alt);
        Add("blend", SvgEditorShortcutKey.B, primary | alt);
        Add("layer", SvgEditorShortcutKey.L, primary);
        Add("convertpath", SvgEditorShortcutKey.O, primary | shift);

        Add("fit", SvgEditorShortcutKey.Number0, primary);
        Add("fitall", SvgEditorShortcutKey.Number0, primary | alt);
        Add("zoom100", SvgEditorShortcutKey.Number1, primary);
        Add("zoomin", SvgEditorShortcutKey.Equals, primary, repeats: true);
        Add("zoomin", SvgEditorShortcutKey.Equals, primary | shift, repeats: true, preferred: false);
        Add("zoomin", SvgEditorShortcutKey.Add, primary, repeats: true, preferred: false);
        Add("zoomout", SvgEditorShortcutKey.Minus, primary, repeats: true);
        Add("zoomout", SvgEditorShortcutKey.Subtract, primary, repeats: true, preferred: false);
        Add("wireframe", SvgEditorShortcutKey.Y, primary);
        Add("grid", SvgEditorShortcutKey.Quote, primary);
        Add("snapgrid", SvgEditorShortcutKey.Quote, primary | shift);
        Add("transparency", SvgEditorShortcutKey.D, primary | shift);
        Add("workspace-rulers", SvgEditorShortcutKey.R, primary);
        Add("zen", SvgEditorShortcutKey.Tab, scope: canvas);
        Add("workspace-hidepanels", SvgEditorShortcutKey.Tab, shift, canvas);
        Add("screenmode", SvgEditorShortcutKey.F, scope: canvas);

        Add("panel-brushes", SvgEditorShortcutKey.F5);
        Add("panel-color", SvgEditorShortcutKey.F6);
        Add("panel-layers", SvgEditorShortcutKey.F7);
        Add("panel-appearance", SvgEditorShortcutKey.F6, shift);
        Add("panel-align", SvgEditorShortcutKey.F7, shift);
        Add("panel-transform", SvgEditorShortcutKey.F8, shift);
        Add("panel-gradient", SvgEditorShortcutKey.F9, primary);
        Add("panel-stroke", SvgEditorShortcutKey.F10, primary);
        Add("panel-attributes", SvgEditorShortcutKey.F11, primary);
        Add("panel-geometry", SvgEditorShortcutKey.F9, primary | shift);
        Add("panel-transparency", SvgEditorShortcutKey.F10, primary | shift);
        Add("panel-typography", SvgEditorShortcutKey.T, primary);
        Add("panel-paragraph", SvgEditorShortcutKey.T, primary | alt);

        void Tool(string id, SvgEditorShortcutKey key, SvgEditorShortcutModifiers modifiers = SvgEditorShortcutModifiers.None)
            => Add("tool-" + id, key, modifiers, canvas);
        Tool("select", SvgEditorShortcutKey.V);
        Tool("direct", SvgEditorShortcutKey.A);
        Tool("magicwand", SvgEditorShortcutKey.Y);
        Tool("lasso", SvgEditorShortcutKey.Q);
        Tool("pen", SvgEditorShortcutKey.P);
        Tool("pencil", SvgEditorShortcutKey.N);
        Tool("brush", SvgEditorShortcutKey.B);
        Tool("rectangle", SvgEditorShortcutKey.M);
        Tool("ellipse", SvgEditorShortcutKey.L);
        Tool("line", SvgEditorShortcutKey.Backslash);
        Tool("type", SvgEditorShortcutKey.T);
        Tool("rotate", SvgEditorShortcutKey.R);
        Tool("reflect", SvgEditorShortcutKey.O);
        Tool("scale", SvgEditorShortcutKey.S);
        Tool("free-transform", SvgEditorShortcutKey.E);
        Tool("scissors", SvgEditorShortcutKey.C);
        Tool("eraser", SvgEditorShortcutKey.E, shift);
        Tool("gradient", SvgEditorShortcutKey.G);
        Tool("eyedropper", SvgEditorShortcutKey.I);
        Tool("hand", SvgEditorShortcutKey.H);
        Tool("zoom", SvgEditorShortcutKey.Z);
        Tool("artboard", SvgEditorShortcutKey.O, shift);
        Tool("width", SvgEditorShortcutKey.W, shift);
        Tool("blend", SvgEditorShortcutKey.W);
        Tool("mesh", SvgEditorShortcutKey.U);
        Add("activepaint", SvgEditorShortcutKey.X, scope: canvas);
        Add("swappaints", SvgEditorShortcutKey.X, shift, canvas);
        Add("defaultpaints", SvgEditorShortcutKey.D, scope: canvas);
        Add("nonepaint", SvgEditorShortcutKey.Slash, scope: canvas);
        Add("finishpath", SvgEditorShortcutKey.Enter, scope: canvas);

        void Nudge(string id, SvgEditorShortcutKey key)
        {
            Add("nudge-" + id, key, scope: canvas, repeats: true);
            Add("nudge-" + id + "-large", key, shift, canvas, repeats: true);
        }
        Nudge("left", SvgEditorShortcutKey.Left);
        Nudge("right", SvgEditorShortcutKey.Right);
        Nudge("up", SvgEditorShortcutKey.Up);
        Nudge("down", SvgEditorShortcutKey.Down);
        return result.AsReadOnly();
    }
}
