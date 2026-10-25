using System;
using System.Globalization;
using System.Linq;
using System.Xml;
using System.Xml.Linq;
using Xunit;

namespace Svg.Editor.Skia.UnitTests;

public class SvgWorkspaceLibraryTests
{
    [Fact]
    public void SnapshotsAndRestoredLayoutsAreDetached()
    {
        var layout = new SvgWorkspaceLayout();
        var empty = new SvgWorkspaceLibrary();
        var library = empty.Add("Design", layout);
        var entry = Assert.Single(library.Entries);
        layout.Move("layers", SvgPanelDock.Left);
        Assert.Empty(empty.Entries);
        Assert.Equal(SvgPanelDock.RightBottom, entry.Restore().Find("layers").Dock);
        entry.Restore().Move("layers", SvgPanelDock.Hidden);
        Assert.Equal(SvgPanelDock.RightBottom, entry.Restore().Find("layers").Dock);
    }

    [Fact]
    public void UpdatePreservesIdentityAndRenameDoesNotChangeSnapshot()
    {
        var library = new SvgWorkspaceLibrary().Add("Original", new SvgWorkspaceLayout());
        var original = library.Entries[0];
        var changed = original.Restore();
        changed.Move("layers", SvgPanelDock.Left);
        var updated = library.Update(original.Id, changed).Rename(original.Id, "Revised");
        var entry = Assert.Single(updated.Entries);
        Assert.Equal(original.Id, entry.Id);
        Assert.Equal("Revised", entry.Name);
        Assert.Equal(changed.Serialize(), entry.LayoutXml);
        Assert.Equal("Original", library.Entries[0].Name);
        Assert.NotEqual(original.LayoutXml, entry.LayoutXml);
    }

    [Fact]
    public void RoundTripPreservesNamesIdentityOrderAndEveryLayoutPreference()
    {
        var layout = new SvgWorkspaceLayout();
        layout.Move("layers", SvgPanelDock.Floating);
        layout.SetFloatingBounds("layers", 330, 140, 410, 580, 16384, 16384);
        layout.Resize(SvgPanelDock.RightTop, 420);
        layout.SetRightSplit(0.725);
        layout.ShowRulers = false;
        layout.TouchDensity = true;
        var library = new SvgWorkspaceLibrary().Add("Żółć & vectors", layout).Add("Type", new SvgWorkspaceLayout());
        Assert.Equal(library.Serialize(), SvgWorkspaceLibrary.Parse(library.Serialize()).Serialize());
    }

    [Fact]
    public void DuplicateNamesAreNormalizedAndCaseInsensitive()
    {
        var library = new SvgWorkspaceLibrary().Add("  Café  ", new SvgWorkspaceLayout());
        Assert.Equal("Café", library.Entries[0].Name);
        Assert.Throws<ArgumentException>(() => library.Add("CAFE\u0301", new SvgWorkspaceLayout()));
        var renamed = library.Rename(library.Entries[0].Id, "CAFÉ");
        Assert.Equal("CAFÉ", renamed.Entries[0].Name);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("Unsafe\nName")]
    [InlineData("Unsafe\0Name")]
    public void InvalidNamesDoNotChangeOriginalLibrary(string name)
    {
        var library = new SvgWorkspaceLibrary();
        Assert.Throws<ArgumentException>(() => library.Add(name, new SvgWorkspaceLayout()));
        Assert.Empty(library.Entries);
    }

    [Fact]
    public void OversizedNamesAndLibrariesAreRejected()
    {
        var layout = new SvgWorkspaceLayout();
        var library = new SvgWorkspaceLibrary();
        Assert.Throws<ArgumentException>(() => library.Add(new string('x', 65), layout));
        for (var index = 0; index < SvgWorkspaceLibrary.MaximumEntries; index++) library = library.Add("Workspace " + index, layout);
        var before = library.Serialize();
        Assert.Throws<InvalidOperationException>(() => library.Add("Excess", layout));
        Assert.Equal(before, library.Serialize());
        Assert.Throws<ArgumentException>(() => SvgWorkspaceLibrary.Parse(new string('x', SvgWorkspaceLibrary.MaximumCharacters + 1)));
    }

    [Fact]
    public void ImportNeverOverwritesAndAssignsFreshIdentities()
    {
        var original = new SvgWorkspaceLibrary().Add("Design", new SvgWorkspaceLayout()).Add("Design (2)", new SvgWorkspaceLayout());
        var imported = original.Import(original.Serialize());
        Assert.Equal(new[] { "Design", "Design (2)", "Design (3)", "Design (2) (2)" }, imported.Entries.Select(entry => entry.Name));
        Assert.Equal(4, imported.Entries.Select(entry => entry.Id).Distinct().Count());
        Assert.Same(original.Entries[0], imported.Entries[0]);
        Assert.Equal(2, original.Entries.Count);
    }

    [Fact]
    public void ImportedLongNamesReceiveBoundedSuffixes()
    {
        var original = new SvgWorkspaceLibrary().Add(new string('x', 64), new SvgWorkspaceLayout());
        var imported = original.Import(original.Serialize());
        Assert.Equal(64, imported.Entries[1].Name.Length);
        Assert.EndsWith(" (2)", imported.Entries[1].Name);
    }

    [Fact]
    public void FailedImportDoesNotPartiallyChangeExistingLibrary()
    {
        var library = new SvgWorkspaceLibrary().Add("Saved", new SvgWorkspaceLayout());
        var incoming = new SvgWorkspaceLibrary().Add("Good", new SvgWorkspaceLayout()).Add("Bad", new SvgWorkspaceLayout());
        var xml = XElement.Parse(incoming.Serialize());
        xml.Elements("entry").Last().Element("workspace")!.SetAttributeValue("version", 999);
        var before = library.Serialize();
        Assert.Throws<ArgumentException>(() => library.Import(xml.ToString()));
        Assert.Equal(before, library.Serialize());
    }

    [Fact]
    public void DuplicateIdentitiesOrNamesInFilesAreRejected()
    {
        var library = new SvgWorkspaceLibrary().Add("One", new SvgWorkspaceLayout()).Add("Two", new SvgWorkspaceLayout());
        var xml = XElement.Parse(library.Serialize());
        var entries = xml.Elements("entry").ToArray();
        entries[1].SetAttributeValue("id", entries[0].Attribute("id")!.Value);
        Assert.Throws<ArgumentException>(() => SvgWorkspaceLibrary.Parse(xml.ToString()));
        xml = XElement.Parse(library.Serialize());
        xml.Elements("entry").Last().SetAttributeValue("name", "ONE");
        Assert.Throws<ArgumentException>(() => SvgWorkspaceLibrary.Parse(xml.ToString()));
    }

    [Fact]
    public void UnknownVersionsAndExternalEntitiesAreRejected()
    {
        Assert.Throws<ArgumentException>(() => SvgWorkspaceLibrary.Parse("<unodraw-workspaces version='2'/>"));
        Assert.Throws<XmlException>(() => SvgWorkspaceLibrary.Parse("<!DOCTYPE x [<!ENTITY ext SYSTEM 'file:///etc/passwd'>]><unodraw-workspaces version='1'>&ext;</unodraw-workspaces>"));
    }

    [Fact]
    public void RemovedSnapshotsRemainUsableButCannotBeUpdatedInNewLibrary()
    {
        var library = new SvgWorkspaceLibrary().Add("Keep a copy", new SvgWorkspaceLayout());
        var entry = library.Entries[0];
        var removed = library.Remove(entry.Id);
        Assert.Empty(removed.Entries);
        Assert.NotNull(entry.Restore());
        Assert.Throws<ArgumentException>(() => removed.Update(entry.Id, entry.Restore()));
        Assert.Throws<ArgumentException>(() => removed.Remove(entry.Id));
    }

    [Fact]
    public void SerializationIsCultureInvariantAndNeverTouchesSvgHistory()
    {
        var culture = CultureInfo.CurrentCulture;
        using var controller = new SvgEditorController();
        controller.NewDocument();
        var before = controller.GetSvg();
        try
        {
            CultureInfo.CurrentCulture = new CultureInfo("pl-PL");
            var layout = new SvgWorkspaceLayout(); layout.Resize(SvgPanelDock.Left, 275.5);
            var library = new SvgWorkspaceLibrary().Add("My desktop", layout);
            Assert.Equal(275.5, SvgWorkspaceLibrary.Parse(library.Serialize()).Entries[0].Restore().LeftWidth);
        }
        finally { CultureInfo.CurrentCulture = culture; }
        Assert.Equal(before, controller.GetSvg());
        Assert.False(controller.CanUndo);
    }
}
