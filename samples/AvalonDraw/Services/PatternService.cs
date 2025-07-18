using System;
using System.Collections.ObjectModel;
using System.Linq;
using Svg;

namespace AvalonDraw.Services;

public class PatternService
{
    public class PatternEntry
    {
        public SvgPatternServer Pattern { get; }
        public string Name { get; }

        public PatternEntry(SvgPatternServer pattern, string name)
        {
            Pattern = pattern;
            Name = name;
        }

        public override string ToString() => Name;
    }

    public ObservableCollection<PatternEntry> Patterns { get; } = new();

    public void Load(SvgDocument? document)
    {
        Patterns.Clear();
        if (document is null)
            return;
        int index = 1;
        foreach (var p in document.Descendants().OfType<SvgPatternServer>())
        {
            var name = string.IsNullOrEmpty(p.ID) ? $"Pattern {index++}" : p.ID!;
            Patterns.Add(new PatternEntry(p, name));
        }
    }

    public void AddPattern(SvgDocument document, SvgPatternServer pattern)
    {
        document.Children.Add(pattern);
        var name = string.IsNullOrEmpty(pattern.ID) ? $"Pattern {Patterns.Count + 1}" : pattern.ID!;
        Patterns.Add(new PatternEntry(pattern, name));
    }
}
