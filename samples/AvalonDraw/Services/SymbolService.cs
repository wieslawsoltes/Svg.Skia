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

    private SvgDocument? _document;

    public SvgDocument? Document => _document;

    public ObservableCollection<SymbolEntry> Symbols { get; } = new();

    public void Load(SvgDocument? document)
    {
        _document = document;
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
        _document = document;
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

    public void ReplaceSymbol(SvgSymbol oldSymbol, SvgSymbol newSymbol)
    {
        if (_document is null)
            return;
        var defs = _document.Children.OfType<SvgDefinitionList>().FirstOrDefault();
        if (defs is null)
            return;
        var idx = defs.Children.IndexOf(oldSymbol);
        if (idx >= 0)
            defs.Children[idx] = newSymbol;
        var entry = Symbols.FirstOrDefault(s => s.Symbol == oldSymbol);
        if (entry is not null)
        {
            var name = string.IsNullOrEmpty(newSymbol.ID) ? entry.Name : newSymbol.ID!;
            var index = Symbols.IndexOf(entry);
            Symbols[index] = new SymbolEntry(newSymbol, name);
        }
    }
}
