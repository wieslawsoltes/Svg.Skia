using System;
using System.Linq;
using System.Xml.Linq;
using Xunit;

namespace Svg.Editor.Skia.UnitTests;

public class SvgFilterResourceIsolationTests
{
    [Fact]
    public void SharedEditCannotStealAnArtworkId()
    {
        using var editor = Create();
        var edit = editor.BeginFilterEdit();
        edit.Graph = SvgFilterGraph.Parse("<filter xmlns='http://www.w3.org/2000/svg'><feOffset id='art' dx='2'/></filter>");
        var before = editor.GetSvg();
        Assert.Throws<InvalidOperationException>(() => editor.ApplyFilterEdit(edit, true));
        Assert.Equal(before, editor.GetSvg());
        Assert.False(editor.CanUndo);
        Assert.False(editor.IsDirty);
        editor.ApplyFilterEdit(edit, false);
        Assert.NotNull(editor.Document!.GetElementById<SvgRectangle>("art"));
        Assert.NotNull(editor.Document.GetElementById("art-copy"));
        var ids = XDocument.Parse(editor.GetSvg()).Descendants().Attributes("id").Select(id => id.Value).ToArray();
        Assert.Equal(ids.Length, ids.Distinct().Count());
    }

    [Fact]
    public void SharedEditMayRetainItsOwnNestedIds()
    {
        using var editor = Create();
        var edit = editor.BeginFilterEdit();
        edit.Graph.SetAttribute(0, "dx", "5");
        editor.ApplyFilterEdit(edit, true);
        Assert.NotNull(editor.Document!.GetElementById("effect-node"));
        Assert.Single(XDocument.Parse(editor.GetSvg()).Descendants(SvgFilterCatalog.Namespace + "filter"));
    }

    [Fact]
    public void DuplicateIdsInsideImportedFilterFailBeforeApply()
    {
        using var editor = Create();
        var edit = editor.BeginFilterEdit();
        edit.Graph = SvgFilterGraph.Parse("<filter xmlns='http://www.w3.org/2000/svg'><feOffset id='same'/><feGaussianBlur id='same'/></filter>");
        var before = editor.GetSvg();
        Assert.Throws<InvalidOperationException>(() => editor.ApplyFilterEdit(edit));
        Assert.Equal(before, editor.GetSvg());
        Assert.False(editor.CanUndo);
    }

    private static SvgEditorController Create()
    {
        var editor = new SvgEditorController();
        editor.LoadSvg("<svg xmlns='http://www.w3.org/2000/svg' width='100' height='80'><defs><filter id='f'><feOffset id='effect-node' dx='1'/></filter></defs><rect id='art' width='20' height='20' filter='url(#f)'/></svg>");
        editor.Select(editor.Document!.GetElementById<SvgRectangle>("art"));
        return editor;
    }
}
