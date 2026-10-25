using System.Linq;
using System.Xml.Linq;
using Xunit;

namespace Svg.Editor.Skia.UnitTests;

public class SvgEditorEffectRegressionTests
{
    [Fact]
    public void CloningRootLevelFilterDoesNotShiftAnonymousSelectionKeys()
    {
        using var editor = new SvgEditorController();
        editor.LoadSvg("<svg xmlns='http://www.w3.org/2000/svg' width='100' height='80'><filter id='f'><feOffset dx='1'/></filter><rect width='20' height='20' filter='url(#f)'/><circle id='other' cx='80' cy='40' r='10'/></svg>");
        editor.Select(editor.Document!.Children.OfType<SvgRectangle>().Single());
        var edit = editor.BeginFilterEdit();
        edit.Graph.SetAttribute(0, "dx", "4");
        editor.ApplyFilterEdit(edit);
        var rectangle = Assert.IsType<SvgRectangle>(Assert.Single(editor.SelectedElements));
        Assert.True(string.IsNullOrEmpty(rectangle.ID));
        Assert.Contains(edit.ResourceId, rectangle.Filter!.OriginalString);
        Assert.NotNull(editor.Document.GetElementById<SvgCircle>("other"));
    }

    [Fact]
    public void ReleasingTwoConsumersOfTheSameMaskAllocatesDistinctSubtreeIds()
    {
        using var editor = new SvgEditorController();
        editor.LoadSvg("""
            <svg xmlns="http://www.w3.org/2000/svg" xmlns:xlink="http://www.w3.org/1999/xlink" width="100" height="80">
              <defs><mask id="mask" maskContentUnits="userSpaceOnUse">
                <g id="source"><rect id="shape" width="20" height="20" fill="white"/><use id="instance" xlink:href="#shape" x="20"/></g>
              </mask></defs>
              <g id="a" mask="url(#mask)" data-unodraw-mask-resource="mask"><rect width="40" height="20"/></g>
              <g id="b" transform="translate(0 40)" mask="url(#mask)" data-unodraw-mask-resource="mask"><rect width="40" height="20"/></g>
            </svg>
            """);
        editor.Select(editor.Document!.GetElementById<SvgGroup>("a"));
        editor.Select(editor.Document.GetElementById<SvgGroup>("b"), true);
        editor.ReleaseOpacityMasks();
        var tree = XDocument.Parse(editor.GetSvg());
        var ids = tree.Descendants().Attributes("id").Select(id => id.Value).ToArray();
        Assert.Equal(ids.Length, ids.Distinct().Count());
        Assert.Contains("shape-copy", ids);
        Assert.Contains("shape-copy2", ids);
        var uses = tree.Descendants().Where(element => element.Name.LocalName == "use").ToArray();
        Assert.Contains(uses, element => element.Attributes().Any(attribute => attribute.Name.LocalName == "href" && attribute.Value == "#shape-copy"));
        Assert.Contains(uses, element => element.Attributes().Any(attribute => attribute.Name.LocalName == "href" && attribute.Value == "#shape-copy2"));
        Assert.Equal(1, editor.Session.UndoCount);
    }

    [Fact]
    public void FilterCopyRetainsAnimatedChildReferencesAndDoesNotShareTheirIds()
    {
        using var editor = new SvgEditorController();
        editor.LoadSvg("""
            <svg xmlns="http://www.w3.org/2000/svg" width="100" height="80">
              <defs><filter id="f"><feOffset id="shift" dx="2">
                <animate id="motion" attributeName="dx" begin="0s;motion.end" values="2;4" dur="1s"/>
              </feOffset></filter></defs>
              <rect id="shape" width="20" height="20" filter="url(#f)"/>
            </svg>
            """);
        editor.Select(editor.Document!.GetElementById<SvgRectangle>("shape"));
        var edit = editor.BeginFilterEdit();
        editor.ApplyFilterEdit(edit);
        var tree = XDocument.Parse(editor.GetSvg());
        var animation = tree.Descendants().Single(element => (string?)element.Attribute("id") == "motion-copy");
        Assert.Equal("0s;motion-copy.end", (string?)animation.Attribute("begin"));
        Assert.Equal("2;4", (string?)animation.Attribute("values"));
        Assert.NotNull(editor.Document.GetElementById("motion"));
    }
}
