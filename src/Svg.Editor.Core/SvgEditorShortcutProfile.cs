using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Xml;
using System.Xml.Linq;

namespace Svg.Editor.Core;

/// <summary>
/// Editable shortcut overrides over the standard catalog. Hosts own command
/// execution; a profile cannot change an action's permissions or keyboard scope.
/// </summary>
public sealed class SvgEditorShortcutProfile
{
    public const int MaximumCharacters = 65536;
    public const int MaximumOverrides = 512;
    private sealed class Assignment
    {
        public Assignment(string commandId, SvgEditorShortcutKey? key, SvgEditorShortcutModifiers modifiers)
        { CommandId = commandId; Key = key; Modifiers = modifiers; }
        public string CommandId { get; }
        public SvgEditorShortcutKey? Key { get; }
        public SvgEditorShortcutModifiers Modifiers { get; }
    }

    private Dictionary<string, Assignment> _overrides = new(StringComparer.Ordinal);
    private Dictionary<int, SvgEditorShortcut> _lookup = new();
    private ReadOnlyCollection<SvgEditorShortcut> _bindings = Array.AsReadOnly(Array.Empty<SvgEditorShortcut>());
    private string _name = "Custom";

    public SvgEditorShortcutProfile() => Rebuild(_overrides);

    public string Name
    {
        get => _name;
        set
        {
            if (string.IsNullOrWhiteSpace(value) || value.Length > 64 || value.Any(char.IsControl))
                throw new ArgumentException("A shortcut profile name must contain 1–64 printable characters.", nameof(value));
            _name = value.Trim();
        }
    }

    public IReadOnlyList<SvgEditorShortcut> Bindings => _bindings;
    public int OverrideCount => _overrides.Count;

    public SvgEditorShortcut? Resolve(SvgEditorShortcutKey key, SvgEditorShortcutModifiers modifiers, SvgEditorShortcutContext context)
        => FindShortcut(key, modifiers) is { } shortcut && shortcut.Allows(context) ? shortcut : null;

    public SvgEditorShortcut? FindShortcut(SvgEditorShortcutKey key, SvgEditorShortcutModifiers modifiers)
        => _lookup.TryGetValue(GestureKey(key, modifiers), out var shortcut) ? shortcut : null;

    public string GetShortcutText(string commandId, bool usesCommandKey = false)
        => _bindings.FirstOrDefault(shortcut => shortcut.CommandId == commandId && shortcut.IsPreferred)?.GetDisplayText(usesCommandKey) ?? string.Empty;

    public bool IsCustomized(string commandId) => _overrides.ContainsKey(commandId);

    public SvgEditorShortcutProfile Clone()
    {
        var copy = new SvgEditorShortcutProfile { Name = Name };
        copy.Rebuild(new Dictionary<string, Assignment>(_overrides, StringComparer.Ordinal));
        return copy;
    }

    public bool TryAssign(string commandId, SvgEditorShortcutKey key, SvgEditorShortcutModifiers modifiers, out string? error)
    {
        error = Validate(commandId, key, modifiers);
        if (error is not null) return false;
        var conflicting = FindShortcut(key, modifiers);
        if (conflicting is not null && conflicting.CommandId != commandId)
        {
            error = "The shortcut is already assigned to " + conflicting.CommandId + ". Clear that binding before reassigning it.";
            return false;
        }
        var candidate = new Dictionary<string, Assignment>(_overrides, StringComparer.Ordinal)
        { [commandId] = new Assignment(commandId, key, modifiers) };
        return TryRebuild(candidate, out error);
    }

    public void Clear(string commandId)
    {
        RequireCommand(commandId);
        var candidate = new Dictionary<string, Assignment>(_overrides, StringComparer.Ordinal)
        { [commandId] = new Assignment(commandId, null, SvgEditorShortcutModifiers.None) };
        Rebuild(candidate);
    }

    public bool TryResetCommand(string commandId, out string? error)
    {
        RequireCommand(commandId);
        var candidate = new Dictionary<string, Assignment>(_overrides, StringComparer.Ordinal);
        candidate.Remove(commandId);
        return TryRebuild(candidate, out error);
    }

    public void ResetDefaults() => Rebuild(new Dictionary<string, Assignment>(StringComparer.Ordinal));

    public string Serialize()
    {
        var root = new XElement("editor-shortcuts", new XAttribute("version", 1), new XAttribute("name", Name));
        foreach (var assignment in _overrides.Values.OrderBy(item => item.CommandId, StringComparer.Ordinal))
        {
            var element = new XElement("binding", new XAttribute("command", assignment.CommandId));
            if (assignment.Key is { } key)
            {
                element.Add(new XAttribute("key", (int)key));
                element.Add(new XAttribute("modifiers", (int)assignment.Modifiers));
            }
            else element.Add(new XAttribute("disabled", true));
            root.Add(element);
        }
        var xml = root.ToString(SaveOptions.DisableFormatting);
        if (xml.Length > MaximumCharacters) throw new InvalidOperationException("The shortcut profile exceeds the supported size.");
        return xml;
    }

    public static SvgEditorShortcutProfile Parse(string xml)
    {
        if (xml is null) throw new ArgumentNullException(nameof(xml));
        if (xml.Length > MaximumCharacters) throw new FormatException("The shortcut profile exceeds the supported size.");
        using var input = new StringReader(xml);
        using var reader = XmlReader.Create(input, new XmlReaderSettings
        {
            DtdProcessing = DtdProcessing.Prohibit,
            XmlResolver = null,
            MaxCharactersInDocument = MaximumCharacters,
            IgnoreComments = true
        });
        var root = XDocument.Load(reader).Root;
        if (root?.Name != "editor-shortcuts" || (string?)root.Attribute("version") != "1")
            throw new FormatException("This is not a supported EditorTheme shortcut profile.");
        var profile = new SvgEditorShortcutProfile { Name = (string?)root.Attribute("name") ?? "Custom" };
        var assignments = new Dictionary<string, Assignment>(StringComparer.Ordinal);
        foreach (var element in root.Elements())
        {
            if (element.Name != "binding" || element.HasElements || assignments.Count >= MaximumOverrides)
                throw new FormatException("The shortcut profile contains invalid or excessive bindings.");
            var id = (string?)element.Attribute("command") ?? string.Empty;
            RequireCommand(id);
            if (assignments.ContainsKey(id)) throw new FormatException("A command can have only one shortcut override.");
            if ((string?)element.Attribute("disabled") == "true")
            {
                if (element.Attribute("key") is not null || element.Attribute("modifiers") is not null)
                    throw new FormatException("A disabled binding cannot also assign a gesture.");
                assignments.Add(id, new Assignment(id, null, SvgEditorShortcutModifiers.None));
                continue;
            }
            if (element.Attribute("disabled") is not null ||
                !int.TryParse((string?)element.Attribute("key"), NumberStyles.None, CultureInfo.InvariantCulture, out var key) ||
                !int.TryParse((string?)element.Attribute("modifiers"), NumberStyles.None, CultureInfo.InvariantCulture, out var modifiers))
                throw new FormatException("A shortcut requires a valid key and modifiers.");
            var problem = Validate(id, (SvgEditorShortcutKey)key, (SvgEditorShortcutModifiers)modifiers);
            if (problem is not null) throw new FormatException(problem);
            assignments.Add(id, new Assignment(id, (SvgEditorShortcutKey)key, (SvgEditorShortcutModifiers)modifiers));
        }
        // Validate the final map in one pass so exchanging two shortcuts round-trips
        // independently of the order of entries in the XML document.
        profile.Rebuild(assignments);
        return profile;
    }

    private bool TryRebuild(Dictionary<string, Assignment> candidate, out string? error)
    {
        try { Rebuild(candidate); error = null; return true; }
        catch (ArgumentException exception) { error = exception.Message; return false; }
    }

    private void Rebuild(Dictionary<string, Assignment> assignments)
    {
        if (assignments.Count > MaximumOverrides) throw new ArgumentException("The shortcut profile has too many overrides.");
        var bindings = SvgEditorShortcutCatalog.Bindings.Where(shortcut => !assignments.ContainsKey(shortcut.CommandId)).ToList();
        foreach (var assignment in assignments.Values)
        {
            if (assignment.Key is not { } key) continue;
            var standard = SvgEditorShortcutCatalog.Bindings.FirstOrDefault(shortcut => shortcut.CommandId == assignment.CommandId && shortcut.IsPreferred);
            var scope = SafeScope(key, assignment.Modifiers, standard?.Scope ?? SvgEditorShortcutScope.Document);
            bindings.Add(new SvgEditorShortcut(assignment.CommandId, key, assignment.Modifiers, scope, standard?.Repeats == true, true));
        }
        var lookup = new Dictionary<int, SvgEditorShortcut>();
        foreach (var shortcut in bindings)
        {
            var gesture = GestureKey(shortcut.Key, shortcut.Modifiers);
            if (lookup.TryGetValue(gesture, out var conflict))
                throw new ArgumentException("Shortcut conflict between " + conflict.CommandId + " and " + shortcut.CommandId + ".");
            lookup.Add(gesture, shortcut);
        }
        _overrides = assignments;
        _bindings = bindings.AsReadOnly();
        _lookup = lookup;
    }

    private static SvgEditorShortcutScope SafeScope(SvgEditorShortcutKey key, SvgEditorShortcutModifiers modifiers, SvgEditorShortcutScope requested)
    {
        // A custom single-letter shortcut must not become an application-global
        // accelerator just because the action normally has a global Ctrl/Cmd chord.
        var modified = (modifiers & (SvgEditorShortcutModifiers.Primary | SvgEditorShortcutModifiers.Alt)) != 0;
        var function = key >= SvgEditorShortcutKey.F1 && key <= SvgEditorShortcutKey.F12;
        if (!modified && !function) return requested == SvgEditorShortcutScope.CanvasOrDocumentTree ? requested : SvgEditorShortcutScope.Canvas;
        // Even a remapped app action cannot take copy, select-all or undo away from
        // native editors. The same protection applies to word and caret navigation.
        if (modifiers == SvgEditorShortcutModifiers.Primary || modifiers == (SvgEditorShortcutModifiers.Primary | SvgEditorShortcutModifiers.Shift))
            if (key is SvgEditorShortcutKey.A or SvgEditorShortcutKey.C or SvgEditorShortcutKey.X or SvgEditorShortcutKey.V
                or SvgEditorShortcutKey.Z or SvgEditorShortcutKey.Y)
                return SvgEditorShortcutScope.Document;
        return requested;
    }

    private static string? Validate(string commandId, SvgEditorShortcutKey key, SvgEditorShortcutModifiers modifiers)
    {
        try { RequireCommand(commandId); }
        catch (ArgumentException exception) { return exception.Message; }
        if (!Enum.IsDefined(typeof(SvgEditorShortcutKey), key) || (modifiers & ~(SvgEditorShortcutModifiers.Primary | SvgEditorShortcutModifiers.Shift | SvgEditorShortcutModifiers.Alt)) != 0)
            return "Choose a supported key and modifiers.";
        if (key == SvgEditorShortcutKey.Space) return "Space is reserved for temporary canvas navigation.";
        if ((modifiers & SvgEditorShortcutModifiers.Alt) != 0 && (modifiers & SvgEditorShortcutModifiers.Primary) == 0)
            return "Use Ctrl/Cmd with Alt/Option to preserve menu access and text entry.";
        if (key == SvgEditorShortcutKey.F10 && modifiers is SvgEditorShortcutModifiers.None or SvgEditorShortcutModifiers.Shift)
            return "F10 and Shift+F10 are reserved for menu navigation and context menus.";
        if (key is SvgEditorShortcutKey.Tab or SvgEditorShortcutKey.Escape or SvgEditorShortcutKey.Enter
            or SvgEditorShortcutKey.Backspace or SvgEditorShortcutKey.Delete or SvgEditorShortcutKey.Left
            or SvgEditorShortcutKey.Up or SvgEditorShortcutKey.Right or SvgEditorShortcutKey.Down)
        {
            var original = SvgEditorShortcutCatalog.Bindings.Any(shortcut => shortcut.CommandId == commandId && shortcut.Key == key && shortcut.Modifiers == modifiers);
            if (!original) return "Navigation and editing keys retain their standard action. Choose a letter or function key.";
        }
        return null;
    }

    private static void RequireCommand(string id)
    {
        if (string.IsNullOrEmpty(id) || id.Length > 128 || id.Any(character => !char.IsLetterOrDigit(character) && character != '-' && character != '_' && character != '.'))
            throw new ArgumentException("A shortcut requires a valid command identifier.", nameof(id));
    }

    private static int GestureKey(SvgEditorShortcutKey key, SvgEditorShortcutModifiers modifiers)
        => ((int)modifiers << 16) | (int)key;
}
