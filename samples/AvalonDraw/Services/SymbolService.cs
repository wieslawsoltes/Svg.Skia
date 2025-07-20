using System;
using System.Collections.ObjectModel;
using System.Linq;
using Svg;

namespace AvalonDraw.Services;

public class SymbolService
{
    public class SymbolEntry
    {
        public SvgSymbol Symbol { get; }
        public string Name { get; }

        public SymbolEntry(SvgSymbol symbol, string name)
        {
            Symbol = symbol;
            Name = name;
        }

        public override string ToString() => Name;
    }

    public ObservableCollection<SymbolEntry> Symbols { get; } = new();
    public SvgDocument? Document { get; private set; }

    public void Load(SvgDocument? document)
    {
        Document = document;
        Symbols.Clear();
        if (document is null)
            return;
        var defs = document.Children.OfType<SvgDefinitionList>().FirstOrDefault();
        if (defs is null)
            return;
        int index = 1;
        foreach (var s in defs.Children.OfType<SvgSymbol>())
        {
            var name = string.IsNullOrEmpty(s.ID) ? $"Symbol {index++}" : s.ID!;
            Symbols.Add(new SymbolEntry(s, name));
        }
    }

    public void AddSymbol(SvgDocument document, SvgSymbol symbol)
    {
        Document = document;
        var defs = document.Children.OfType<SvgDefinitionList>().FirstOrDefault();
        if (defs is null)
        {
            defs = new SvgDefinitionList();
            document.Children.Add(defs);
        }
        defs.Children.Add(symbol);
        var name = string.IsNullOrEmpty(symbol.ID) ? $"Symbol {Symbols.Count + 1}" : symbol.ID!;
        Symbols.Add(new SymbolEntry(symbol, name));
    }

    public SvgSymbol? Find(string id)
    {
        return Document?.Descendants().OfType<SvgSymbol>()
            .FirstOrDefault(s => string.Equals(s.ID, id, StringComparison.Ordinal));
    }

    public void ReplaceSymbol(SvgSymbol oldSymbol, SvgSymbol newSymbol)
    {
        if (Document is null)
            return;
        if (oldSymbol.Parent is not SvgElement parent)
            return;
        var idx = parent.Children.IndexOf(oldSymbol);
        if (idx >= 0)
        {
            parent.Children.RemoveAt(idx);
            parent.Children.Insert(idx, newSymbol);
        }
        Load(Document);
    }
}
