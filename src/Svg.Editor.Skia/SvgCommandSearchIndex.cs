using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace Svg.Editor.Skia;

/// <summary>Presentation metadata only: command execution and permissions remain with the host.</summary>
public sealed class SvgCommandCatalogEntry
{
    public SvgCommandCatalogEntry(string id, string label, string category, string shortcut = "")
    {
        Id = Require(id, nameof(id), 128);
        Label = Require(label, nameof(label), 256);
        Category = Require(category, nameof(category), 128);
        Shortcut = shortcut ?? throw new ArgumentNullException(nameof(shortcut));
        if (shortcut.Length > 128) throw new ArgumentException("Shortcut text exceeds 128 characters.", nameof(shortcut));
    }
    public string Id { get; }
    public string Label { get; }
    public string Category { get; }
    public string Shortcut { get; }
    private static string Require(string value, string parameter, int maximum)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length > maximum)
            throw new ArgumentException("Command metadata must be nonempty and within its length budget.", parameter);
        return value;
    }
}

/// <summary>
/// Immutable, culture-invariant index for small command catalogs. Exact labels and
/// shortcuts outrank prefixes and all-token matches. Recent IDs affect only empty
/// searches, never override an explicit query, and never enable command execution.
/// </summary>
public sealed class SvgCommandSearchIndex
{
    public const int MaximumEntries = 4096;
    public const int MaximumQueryLength = 256;
    private sealed class Indexed
    {
        public Indexed(SvgCommandCatalogEntry entry, int order)
        {
            Entry = entry; Order = order;
            Label = Normalize(entry.Label); Category = Normalize(entry.Category);
            Shortcut = Normalize(entry.Shortcut); Id = Normalize(entry.Id);
        }
        public SvgCommandCatalogEntry Entry { get; }
        public int Order { get; }
        public string Label { get; }
        public string Category { get; }
        public string Shortcut { get; }
        public string Id { get; }
    }
    private readonly Indexed[] _entries;
    public int Count => _entries.Length;
    public SvgCommandSearchIndex(IEnumerable<SvgCommandCatalogEntry> entries)
    {
        if (entries is null) throw new ArgumentNullException(nameof(entries));
        var snapshot = entries.Take(MaximumEntries + 1).ToArray();
        if (snapshot.Length > MaximumEntries) throw new ArgumentException("The command catalog exceeds 4,096 entries.", nameof(entries));
        var ids = new HashSet<string>(StringComparer.Ordinal);
        foreach (var entry in snapshot)
            if (entry is null || !ids.Add(entry.Id)) throw new ArgumentException("Command entries require unique IDs.", nameof(entries));
        _entries = snapshot.Select((entry, index) => new Indexed(entry, index)).ToArray();
    }

    public IReadOnlyList<SvgCommandCatalogEntry> Search(string? query, int maximumResults = 100, IReadOnlyList<string>? recentIds = null)
    {
        if (maximumResults < 1 || maximumResults > 256) throw new ArgumentOutOfRangeException(nameof(maximumResults));
        if (query?.Length > MaximumQueryLength) throw new ArgumentException("Search text exceeds 256 characters.", nameof(query));
        var text = Normalize(query ?? string.Empty);
        if (text.Length == 0)
        {
            var order = new Dictionary<string, int>(StringComparer.Ordinal);
            if (recentIds is not null)
                for (var i = 0; i < Math.Min(32, recentIds.Count); i++)
                    if (recentIds[i] is { } id && !order.ContainsKey(id)) order.Add(id, i);
            return Array.AsReadOnly(_entries.OrderBy(item => order.TryGetValue(item.Entry.Id, out var rank) ? rank : int.MaxValue)
                .ThenBy(item => item.Order).Take(maximumResults).Select(item => item.Entry).ToArray());
        }
        var tokens = text.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
        return Array.AsReadOnly(_entries.Select(item => (Item: item, Rank: Score(item, text, tokens)))
            .Where(item => item.Rank >= 0).OrderBy(item => item.Rank).ThenBy(item => item.Item.Order)
            .Take(maximumResults).Select(item => item.Item.Entry).ToArray());
    }

    private static int Score(Indexed item, string query, string[] tokens)
    {
        if (item.Label == query) return 0;
        if (item.Shortcut.Length != 0 && item.Shortcut == query) return 1;
        if (item.Label.StartsWith(query, StringComparison.Ordinal)) return 10;
        if (item.Label.IndexOf(query, StringComparison.Ordinal) >= 0) return 20;
        var score = 0;
        foreach (var token in tokens)
        {
            if (HasWordPrefix(item.Label, token)) continue;
            if (item.Label.IndexOf(token, StringComparison.Ordinal) >= 0) { score += 1; continue; }
            if (item.Category.IndexOf(token, StringComparison.Ordinal) >= 0) { score += 3; continue; }
            if (item.Shortcut.IndexOf(token, StringComparison.Ordinal) >= 0) { score += 4; continue; }
            if (item.Id.IndexOf(token, StringComparison.Ordinal) >= 0) { score += 6; continue; }
            return -1;
        }
        // A category-qualified query such as "File Save" prefers "Save" over
        // "Save as", without allowing label length to overrule match quality.
        return 30 + score * 32 + Math.Min(31, item.Label.Length);
    }
    private static bool HasWordPrefix(string text, string token)
        => text.StartsWith(token, StringComparison.Ordinal) || text.IndexOf(" " + token, StringComparison.Ordinal) >= 0;
    private static string Normalize(string value)
    {
        var result = new StringBuilder(value.Length);
        var separator = false;
        foreach (var character in value)
        {
            if (char.IsLetterOrDigit(character))
            {
                if (separator && result.Length > 0) result.Append(' ');
                result.Append(char.ToLower(character, CultureInfo.InvariantCulture)); separator = false;
            }
            else separator = true;
        }
        return result.ToString();
    }
}
