using System;
using System.Runtime.CompilerServices;
using Svg;
using Svg.Editor.Skia;
using Xunit;

namespace Svg.Editor.Skia.UnitTests;

public class SvgEditorHierarchyPresentationTests
{
    private const string Source = "<svg xmlns='http://www.w3.org/2000/svg' width='400' height='300'>"
        + "<defs id='resources'><linearGradient id='paint'><stop id='stop' offset='0' stop-color='red'/></linearGradient>"
        + "<pattern id='pattern' width='10' height='10'/></defs>"
        + "<g id='layer'><rect id='shape' width='20' height='15' fill='url(#paint)'/>"
        + "<rect id='sibling' x='30' width='20' height='15'/></g><g id='target'/></svg>";

    [Fact]
    public void CommittedNudgesAndSelectionKeepTheExistingPresentationCurrent()
    {
        using var editor = new SvgEditorController();
        editor.LoadSvg(Source);
        var document = editor.Document!;
        var outline = SvgEditorHierarchyPresentation.Capture(document, " shape ");
        var resources = SvgEditorResourcePresentation.Capture(document);
        var revision = editor.DocumentRevision;
        editor.Select(document.GetElementById<SvgRectangle>("shape"));
        for (var index = 0; index < 120; index++)
        {
            editor.MoveSelection(1, -0.25f);
            Assert.True(outline.IsCurrent(editor.Document, "shape"));
            Assert.True(resources.IsCurrent(editor.Document));
        }
        Assert.Equal(revision + 120, editor.DocumentRevision);
        editor.ClearSelection();
        Assert.True(outline.IsCurrent(editor.Document, "\tshape\r\n"));
        Assert.True(resources.IsCurrent(editor.Document));
        editor.Undo();
        Assert.False(outline.IsCurrent(editor.Document, "shape"));
        Assert.False(resources.IsCurrent(editor.Document));
    }

    [Theory]
    [InlineData("id")]
    [InlineData("display")]
    [InlineData("visibility")]
    [InlineData("lock")]
    [InlineData("legacy-lock")]
    [InlineData("root-lock")]
    [InlineData("descendant-lock")]
    public void EveryOutlinePresentationInputInvalidatesTheSnapshot(string change)
    {
        var document = Parse();
        var shape = document.GetElementById("shape");
        var outline = SvgEditorHierarchyPresentation.Capture(document, null);
        switch (change)
        {
            case "id":
                shape.ID = "renamed";
                break;
            case "display":
                shape.Display = "none";
                break;
            case "visibility":
                shape.Visibility = "hidden";
                break;
            case "lock":
                document.GetElementById("layer").CustomAttributes["data-unodraw-locked"] = "true";
                break;
            case "legacy-lock":
                shape.CustomAttributes["data-lock"] = "true";
                break;
            case "root-lock":
                document.CustomAttributes["data-unodraw-locked"] = "true";
                break;
            case "descendant-lock":
                document.GetElementById("stop").CustomAttributes["data-unodraw-locked"] = "true";
                break;
        }
        Assert.False(outline.IsCurrent(document, string.Empty));
    }

    [Theory]
    [InlineData("reorder")]
    [InlineData("reparent")]
    [InlineData("replace")]
    [InlineData("remove")]
    [InlineData("add")]
    public void StructureAndObjectIdentityAreComparedExactly(string change)
    {
        var document = Parse();
        var layer = document.GetElementById("layer");
        var shape = document.GetElementById("shape");
        var outline = SvgEditorHierarchyPresentation.Capture(document, null);
        if (change == "reorder")
        {
            layer.Children.Remove(shape);
            layer.Children.Add(shape);
        }
        else if (change == "reparent")
        {
            // The flattened element sequence stays the same: child counts must
            // still detect that the last rectangle became the group's sibling.
            var sibling = document.GetElementById("sibling");
            layer.Children.Remove(sibling);
            document.Children.Insert(document.Children.IndexOf(layer) + 1, sibling);
        }
        else if (change == "replace")
        {
            var replacement = new SvgRectangle { ID = shape.ID, Width = 20, Height = 15 };
            var index = layer.Children.IndexOf(shape);
            layer.Children.Remove(shape);
            layer.Children.Insert(index, replacement);
        }
        else if (change == "remove")
            layer.Children.Remove(shape);
        else
            layer.Children.Add(new SvgRectangle { ID = "new" });
        Assert.False(outline.IsCurrent(document, null));
    }

    [Fact]
    public void DocumentAndNormalizedSearchArePartOfOutlineIdentity()
    {
        var document = Parse();
        var outline = SvgEditorHierarchyPresentation.Capture(document, "\u2003shape\t");
        Assert.True(outline.IsCurrent(document, "shape\r\n"));
        Assert.False(outline.IsCurrent(document, "sibling"));
        Assert.False(outline.IsCurrent(Parse(), "shape"));
        Assert.False(outline.IsCurrent(null, "shape"));
        var empty = SvgEditorHierarchyPresentation.Capture(null, null);
        Assert.True(empty.IsCurrent(null, "  "));
        Assert.False(empty.IsCurrent(document, null));
        Assert.True(SvgEditorResourcePresentation.Capture(null).IsCurrent(null));
        Assert.False(SvgEditorResourcePresentation.Capture(null).IsCurrent(document));
    }

    [Fact]
    public void ResourceContentsAndUnrelatedRowsDoNotReplacePickerItems()
    {
        var document = Parse();
        var resources = SvgEditorResourcePresentation.Capture(document);
        var gradient = document.GetElementById<SvgLinearGradientServer>("paint");
        gradient.Children.Add(new SvgGradientStop { ID = "second-stop", Offset = new SvgUnit(0.75f) });
        var shape = document.GetElementById("shape");
        shape.ID = "renamed-shape";
        shape.Display = "none";
        shape.CustomAttributes["data-unodraw-locked"] = "true";
        Assert.True(resources.IsCurrent(document));
        Assert.False(resources.IsCurrent(Parse()));
    }

    [Fact]
    public void ResourceCategoriesUseSvgTypesRatherThanSimpleNames()
    {
        Assert.True(SvgEditorResourcePresentation.IsResource(new global::Svg.FilterEffects.SvgFilter()));
        Assert.True(SvgEditorResourcePresentation.IsResource(new DerivedFilter()));
        Assert.False(SvgEditorResourcePresentation.IsResource(new SvgFilter()));
        Assert.False(SvgEditorResourcePresentation.IsResource(new SvgGroup()));
    }

    [Theory]
    [InlineData("rename")]
    [InlineData("hide")]
    [InlineData("inherited-lock")]
    [InlineData("root-lock")]
    [InlineData("legacy-lock")]
    [InlineData("reorder")]
    [InlineData("remove")]
    [InlineData("add")]
    [InlineData("replace")]
    public void ResourceMembershipOrderIdentityAndLabelsInvalidateThePicker(string change)
    {
        var document = Parse();
        var definitions = document.GetElementById("resources");
        var gradient = document.GetElementById("paint");
        var resources = SvgEditorResourcePresentation.Capture(document);
        switch (change)
        {
            case "rename":
                gradient.ID = "renamed";
                break;
            case "hide":
                gradient.Visibility = "hidden";
                break;
            case "inherited-lock":
                definitions.CustomAttributes["data-unodraw-locked"] = "true";
                break;
            case "root-lock":
                document.CustomAttributes["data-unodraw-locked"] = "true";
                break;
            case "legacy-lock":
                gradient.CustomAttributes["data-lock"] = "true";
                break;
            case "reorder":
                definitions.Children.Remove(gradient);
                definitions.Children.Add(gradient);
                break;
            case "remove":
                definitions.Children.Remove(gradient);
                break;
            case "add":
                definitions.Children.Add(new SvgSymbol { ID = "new-symbol" });
                break;
            case "replace":
                definitions.Children.Remove(gradient);
                definitions.Children.Insert(0, new SvgLinearGradientServer { ID = "paint" });
                break;
        }
        Assert.False(resources.IsCurrent(document));
    }

    [Fact]
    public void UnchangedValidationDoesNotAllocateCollectionsOrQueryStrings()
    {
        var document = Parse();
        var outline = SvgEditorHierarchyPresentation.Capture(document, "shape");
        var resources = SvgEditorResourcePresentation.Capture(document);
        // Warm the exact measurement method, including its counter and return
        // paths. A separate warmup loop does not warm those measurement call
        // sites and can include JIT/runtime initialization in the byte count.
        for (var sample = 0; sample < 16; sample++)
        {
            var warmup = MeasureValidation(document, outline, resources);
            Assert.True(warmup.AllCurrent);
        }

        var measured = MeasureValidation(document, outline, resources);
        Assert.True(measured.AllCurrent);
        Assert.Equal(0, measured.AllocatedBytes);

        // Reflection type-name caches are weakly held and may be reclaimed even
        // after warmup. Validation must remain allocation-free after collection.
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        var afterCollection = MeasureValidation(document, outline, resources);
        Assert.True(afterCollection.AllCurrent);
        Assert.Equal(0, afterCollection.AllocatedBytes);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static (bool AllCurrent, long AllocatedBytes) MeasureValidation(SvgDocument document,
        SvgEditorHierarchyPresentation outline, SvgEditorResourcePresentation resources)
    {
        var before = GC.GetAllocatedBytesForCurrentThread();
        var allCurrent = true;
        for (var index = 0; index < 1000; index++)
            allCurrent &= outline.IsCurrent(document, "\tshape\n") && resources.IsCurrent(document);
        return (allCurrent, GC.GetAllocatedBytesForCurrentThread() - before);
    }

    private static SvgDocument Parse() => SvgDocument.FromSvg<SvgDocument>(Source);

    private sealed class DerivedFilter : global::Svg.FilterEffects.SvgFilter
    {
    }

    private sealed class SvgFilter : SvgGroup
    {
    }
}
