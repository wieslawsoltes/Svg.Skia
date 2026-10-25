using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Xunit;

namespace Svg.Editor.Skia.UnitTests;

public class SvgCommandSearchIndexTests
{
    private static SvgCommandCatalogEntry[] Catalog() => new[]
    {
        new SvgCommandCatalogEntry("saveas", "Save as…", "File", "Ctrl+Shift+S"),
        new SvgCommandCatalogEntry("save", "Save", "File", "Ctrl+S"),
        new SvgCommandCatalogEntry("workspace-save", "Save workspace as…", "Window"),
        new SvgCommandCatalogEntry("gradient", "Gradient editor", "Object"),
        new SvgCommandCatalogEntry("gradient-reverse", "Reverse gradient draft", "Gradient"),
        new SvgCommandCatalogEntry("layers", "Layers panel", "Window"),
        new SvgCommandCatalogEntry("align", "Align left", "Arrange")
    };

    [Theory]
    [InlineData("save", "save")]
    [InlineData("Save as", "saveas")]
    [InlineData(" SAVE WORKSPACE AS… ", "workspace-save")]
    [InlineData("Ctrl+S", "save")]
    [InlineData("ctrl shift s", "saveas")]
    [InlineData("editor gradient", "gradient")]
    [InlineData("window layers", "layers")]
    public void ExactLabelsShortcutsAndUnorderedTokensFindIntendedCommand(string query, string expected)
    {
        var index = new SvgCommandSearchIndex(Catalog());
        Assert.Equal(expected, index.Search(query)[0].Id);
    }

    [Fact]
    public void ExplicitQueryIsNeverDisplacedByRecentChoices()
    {
        var index = new SvgCommandSearchIndex(Catalog());
        var result = index.Search("save", recentIds: new[] { "workspace-save", "saveas", "layers" });
        Assert.Equal("save", result[0].Id);
        Assert.Equal("saveas", result[1].Id);
        Assert.Equal("workspace-save", result[2].Id);
    }

    [Fact]
    public void EmptyQueryPromotesValidRecentsAndRetainsStableCatalogOrder()
    {
        var index = new SvgCommandSearchIndex(Catalog());
        var result = index.Search("", recentIds: new[] { "missing", "layers", "layers", "save" });
        Assert.Equal(new[] { "layers", "save", "saveas", "workspace-save", "gradient", "gradient-reverse", "align" }, result.Select(entry => entry.Id));
        Assert.Equal(Catalog().Select(entry => entry.Id), index.Search(" ").Select(entry => entry.Id));
    }

    [Fact]
    public void NoMatchCannotReturnThePreviousSearchResult()
    {
        var index = new SvgCommandSearchIndex(Catalog());
        Assert.NotEmpty(index.Search("save"));
        Assert.Empty(index.Search("zzzz missing command"));
        Assert.Empty(index.Search("gradient layers"));
    }

    [Fact]
    public void RankingAndNormalizationDoNotDependOnCurrentCulture()
    {
        var previous = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("tr-TR");
            var index = new SvgCommandSearchIndex(Catalog());
            Assert.Equal("align", index.Search("ALIGN LEFT")[0].Id);
            Assert.Equal("save", index.Search("FILE SAVE")[0].Id);
        }
        finally { CultureInfo.CurrentCulture = previous; }
    }

    [Fact]
    public void CatalogAndResultsAreDetachedFromCallerCollections()
    {
        var input = Catalog().ToList();
        var index = new SvgCommandSearchIndex(input);
        var expected = input.Count;
        input.Clear();
        Assert.Equal(expected, index.Count);
        var result = index.Search("");
        Assert.Throws<NotSupportedException>(() => ((IList<SvgCommandCatalogEntry>)result).Clear());
        Assert.Equal(expected, index.Search("").Count);
    }

    [Fact]
    public void DuplicateIdsFailButDuplicateLabelsRemainSeparatelyAddressable()
    {
        var entry = new SvgCommandCatalogEntry("one", "Gradient editor", "Object");
        Assert.Throws<ArgumentException>(() => new SvgCommandSearchIndex(new[] { entry, entry }));
        var index = new SvgCommandSearchIndex(new[] { entry, new SvgCommandCatalogEntry("two", "Gradient editor", "Window") });
        Assert.Equal(new[] { "one", "two" }, index.Search("gradient").Select(item => item.Id));
    }

    [Fact]
    public void SearchLimitsBoundResultsWithoutDroppingTheBestMatch()
    {
        var index = new SvgCommandSearchIndex(Catalog());
        Assert.Equal("save", Assert.Single(index.Search("save", 1)).Id);
        Assert.Equal(3, index.Search("", 3).Count);
        Assert.Throws<ArgumentOutOfRangeException>(() => index.Search("", 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => index.Search("", 257));
        Assert.Throws<ArgumentException>(() => index.Search(new string('a', 257)));
    }

    [Fact]
    public void UnboundedInputEnumerationStopsAtCatalogBudget()
    {
        var enumerated = 0;
        IEnumerable<SvgCommandCatalogEntry> Forever()
        {
            while (true) { enumerated++; yield return new SvgCommandCatalogEntry("id" + enumerated, "Action", "Test"); }
        }
        Assert.Throws<ArgumentException>(() => new SvgCommandSearchIndex(Forever()));
        Assert.Equal(SvgCommandSearchIndex.MaximumEntries + 1, enumerated);
    }

    [Fact]
    public void InvalidMetadataAndEmptyCatalogsAreHandledExplicitly()
    {
        Assert.Empty(new SvgCommandSearchIndex(Array.Empty<SvgCommandCatalogEntry>()).Search("test"));
        Assert.Throws<ArgumentNullException>(() => new SvgCommandSearchIndex(null!));
        Assert.Throws<ArgumentException>(() => new SvgCommandSearchIndex(new SvgCommandCatalogEntry[] { null! }));
        Assert.Throws<ArgumentException>(() => new SvgCommandCatalogEntry("", "Save", "File"));
        Assert.Throws<ArgumentException>(() => new SvgCommandCatalogEntry("save", new string('x', 257), "File"));
        Assert.Throws<ArgumentException>(() => new SvgCommandCatalogEntry("save", "Save", "File", new string('x', 129)));
    }
}
