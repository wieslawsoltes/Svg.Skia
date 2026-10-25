using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml;
using System.Xml.Linq;

namespace Svg.Editor.Skia;

/// <summary>A named, detached workspace snapshot. Restoring returns a new layout instance.</summary>
public sealed class SvgNamedWorkspace
{
    internal SvgNamedWorkspace(string id, string name, string layoutXml)
    { Id = id; Name = name; LayoutXml = layoutXml; }
    public string Id { get; }
    public string Name { get; }
    public string LayoutXml { get; }
    public SvgWorkspaceLayout Restore() => SvgWorkspaceLayout.Parse(LayoutXml);
    public override string ToString() => Name;
}

/// <summary>
/// Versioned workspace library independent of documents, renderers and UI frameworks.
/// Mutations return a new library; validation or storage failure can never partially
/// change the caller's existing library. Import always keeps existing snapshots.
/// </summary>
public sealed class SvgWorkspaceLibrary
{
    public const int MaximumEntries = 32;
    public const int MaximumNameLength = 64;
    public const int MaximumCharacters = 524288;
    private readonly ReadOnlyCollection<SvgNamedWorkspace> _entries;
    public IReadOnlyList<SvgNamedWorkspace> Entries => _entries;
    public SvgWorkspaceLibrary() : this(Array.Empty<SvgNamedWorkspace>()) { }
    private SvgWorkspaceLibrary(IEnumerable<SvgNamedWorkspace> entries)
        => _entries = Array.AsReadOnly(entries.ToArray());

    public SvgNamedWorkspace? Find(string id) => _entries.FirstOrDefault(entry => entry.Id == id);
    public SvgNamedWorkspace? FindByName(string name)
    {
        var normalized = NormalizeName(name);
        return _entries.FirstOrDefault(entry => StringComparer.OrdinalIgnoreCase.Equals(entry.Name, normalized));
    }

    public SvgWorkspaceLibrary Add(string name, SvgWorkspaceLayout layout)
    {
        if (layout is null) throw new ArgumentNullException(nameof(layout));
        if (_entries.Count >= MaximumEntries) throw new InvalidOperationException($"Keep at most {MaximumEntries} named workspaces.");
        name = NormalizeName(name);
        EnsureAvailableName(name);
        var snapshot = ValidateLayout(layout.Serialize());
        return CreateValidated(_entries.Concat(new[] { new SvgNamedWorkspace(Guid.NewGuid().ToString("N"), name, snapshot) }));
    }

    public SvgWorkspaceLibrary Update(string id, SvgWorkspaceLayout layout)
    {
        if (layout is null) throw new ArgumentNullException(nameof(layout));
        var entry = Require(id);
        var replacement = new SvgNamedWorkspace(entry.Id, entry.Name, ValidateLayout(layout.Serialize()));
        return CreateValidated(_entries.Select(item => item.Id == id ? replacement : item));
    }

    public SvgWorkspaceLibrary Rename(string id, string name)
    {
        var entry = Require(id);
        name = NormalizeName(name);
        EnsureAvailableName(name, id);
        var replacement = new SvgNamedWorkspace(entry.Id, name, entry.LayoutXml);
        return CreateValidated(_entries.Select(item => item.Id == id ? replacement : item));
    }

    public SvgWorkspaceLibrary Remove(string id)
    {
        Require(id);
        return new SvgWorkspaceLibrary(_entries.Where(item => item.Id != id));
    }

    /// <summary>
    /// Appends incoming snapshots with fresh identities. Name collisions receive a
    /// numeric suffix; neither IDs nor names can cause an existing snapshot overwrite.
    /// All incoming data and the final library are validated before returning.
    /// </summary>
    public SvgWorkspaceLibrary Import(string xml)
    {
        var incoming = Parse(xml);
        if (_entries.Count + incoming.Entries.Count > MaximumEntries)
            throw new InvalidOperationException($"Import would exceed {MaximumEntries} named workspaces.");
        var names = new HashSet<string>(_entries.Select(entry => entry.Name), StringComparer.OrdinalIgnoreCase);
        var ids = new HashSet<string>(_entries.Select(entry => entry.Id), StringComparer.Ordinal);
        var entries = _entries.ToList();
        foreach (var entry in incoming.Entries)
        {
            var name = entry.Name;
            for (var suffix = 2; !names.Add(name); suffix++)
            {
                var ending = " (" + suffix.ToString(CultureInfo.InvariantCulture) + ")";
                name = TruncateName(entry.Name, MaximumNameLength - ending.Length) + ending;
            }
            string id;
            do { id = Guid.NewGuid().ToString("N"); } while (!ids.Add(id));
            entries.Add(new SvgNamedWorkspace(id, name, entry.LayoutXml));
        }
        return CreateValidated(entries);
    }

    public string Serialize()
    {
        var root = new XElement("unodraw-workspaces", new XAttribute("version", 1));
        foreach (var entry in _entries)
            root.Add(new XElement("entry", new XAttribute("id", entry.Id), new XAttribute("name", entry.Name),
                ReadXml(entry.LayoutXml, 32768)));
        var xml = root.ToString(SaveOptions.DisableFormatting);
        if (xml.Length > MaximumCharacters) throw new InvalidOperationException("The workspace library exceeds the 512 KiB character budget.");
        return xml;
    }

    public static SvgWorkspaceLibrary Parse(string xml)
    {
        if (xml is null) throw new ArgumentNullException(nameof(xml));
        var root = ReadXml(xml, MaximumCharacters);
        if (root.Name != "unodraw-workspaces" || (string?)root.Attribute("version") != "1")
            throw new ArgumentException("Unsupported workspace-library format or version.", nameof(xml));
        var entries = new List<SvgNamedWorkspace>();
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var ids = new HashSet<string>(StringComparer.Ordinal);
        foreach (var item in root.Elements())
        {
            if (item.Name != "entry" || entries.Count >= MaximumEntries)
                throw new ArgumentException("The workspace library contains unsupported or excessive entries.", nameof(xml));
            var rawId = (string?)item.Attribute("id");
            if (!Guid.TryParseExact(rawId, "N", out var parsedId))
                throw new ArgumentException("A workspace identity is missing or invalid.", nameof(xml));
            var id = parsedId.ToString("N");
            var name = NormalizeName((string?)item.Attribute("name") ?? string.Empty);
            if (!ids.Add(id) || !names.Add(name))
                throw new ArgumentException("The workspace library contains duplicate identities or names.", nameof(xml));
            var children = item.Elements().Take(2).ToArray();
            if (children.Length != 1) throw new ArgumentException("Each named workspace requires one layout snapshot.", nameof(xml));
            entries.Add(new SvgNamedWorkspace(id, name, ValidateLayout(children[0].ToString(SaveOptions.DisableFormatting))));
        }
        return CreateValidated(entries);
    }

    public static string NormalizeName(string name)
    {
        if (name is null) throw new ArgumentNullException(nameof(name));
        var normalized = name.Trim().Normalize(NormalizationForm.FormC);
        if (normalized.Length == 0 || normalized.Length > MaximumNameLength
            || normalized.Any(character => char.IsControl(character)))
            throw new ArgumentException($"Use a workspace name of 1–{MaximumNameLength} characters without control characters.", nameof(name));
        XmlConvert.VerifyXmlChars(normalized);
        return normalized;
    }
    private SvgNamedWorkspace Require(string id)
        => Find(id) ?? throw new ArgumentException("The named workspace no longer exists.", nameof(id));
    private void EnsureAvailableName(string name, string? exceptId = null)
    {
        if (_entries.Any(entry => entry.Id != exceptId && StringComparer.OrdinalIgnoreCase.Equals(entry.Name, name)))
            throw new ArgumentException("A workspace already has that name. Choose another name or explicitly update its snapshot.", nameof(name));
    }
    private static SvgWorkspaceLibrary CreateValidated(IEnumerable<SvgNamedWorkspace> entries)
    {
        var library = new SvgWorkspaceLibrary(entries);
        _ = library.Serialize();
        return library;
    }
    private static string ValidateLayout(string xml)
    {
        var root = ReadXml(xml, 32768);
        // SvgWorkspaceLayout.Parse intentionally falls back for host preferences. A
        // portable library must reject future formats instead of silently losing them.
        if (root.Name != "workspace" || (string?)root.Attribute("version") != "1")
            throw new ArgumentException("Unsupported workspace snapshot format or version.", nameof(xml));
        return SvgWorkspaceLayout.Parse(xml).Serialize();
    }
    private static XElement ReadXml(string xml, int limit)
    {
        if (xml.Length > limit) throw new ArgumentException("Workspace XML exceeds its size budget.", nameof(xml));
        using var reader = XmlReader.Create(new StringReader(xml), new XmlReaderSettings
        { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null, MaxCharactersInDocument = limit });
        return XElement.Load(reader);
    }
    private static string TruncateName(string name, int maximum)
    {
        if (name.Length <= maximum) return name;
        if (char.IsHighSurrogate(name[maximum - 1])) maximum--;
        return name.Substring(0, maximum).TrimEnd();
    }
}
