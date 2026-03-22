using Svg;

namespace Svg.Editor.Skia.Uno.Models;

public sealed class SampleLibraryAssetDefinition
{
    public SampleLibraryAssetDefinition(
        SvgSymbol symbol,
        string name,
        string sectionName,
        string searchKeywords = "")
    {
        Symbol = symbol;
        Name = name;
        SectionName = sectionName;
        SearchKeywords = searchKeywords;
    }

    public SvgSymbol Symbol { get; }

    public string Name { get; }

    public string SectionName { get; }

    public string SearchKeywords { get; }
}
