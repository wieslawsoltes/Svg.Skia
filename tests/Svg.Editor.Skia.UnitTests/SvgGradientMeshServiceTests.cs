using System;
using System.Linq;
using SkiaSharp;
using Svg;
using Svg.Editor.Skia;
using Xunit;

namespace Svg.Editor.Skia.UnitTests;

public class SvgGradientMeshServiceTests
{
    [Fact]
    public void GridTessellationHasExactSharedEdgesAndExpectedBounds()
    {
        var mesh = SvgGradientMeshService.CreateGrid(new SKRect(10, 20, 210, 120), 3, 3);
        mesh.Subdivisions = 4;
        var triangles = SvgGradientMeshService.Tessellate(mesh);
        Assert.Equal(128, triangles.Count);
        var points = triangles.SelectMany(triangle => new[] { triangle.A, triangle.B, triangle.C }).ToArray();
        Assert.Equal(10, points.Min(point => point.X));
        Assert.Equal(210, points.Max(point => point.X));
        Assert.Equal(20, points.Min(point => point.Y));
        Assert.Equal(120, points.Max(point => point.Y));
        Assert.Contains(points, point => point.X == 110 && point.Y == 70);
    }

    [Fact]
    public void CurvedBoundaryTangentsMoveTheSurfaceAndColorsRemainInterpolated()
    {
        var mesh = SvgGradientMeshService.CreateGrid(SKRect.Create(100, 100));
        mesh.Nodes[0].Horizontal = new SKPoint(100, -100);
        mesh.Nodes[1].Horizontal = new SKPoint(100, 100);
        mesh.Nodes[0].Color = SKColors.Red;
        mesh.Nodes[1].Color = SKColors.Blue;
        var triangles = SvgGradientMeshService.Tessellate(mesh);
        Assert.True(triangles.Min(triangle => Math.Min(triangle.A.Y, Math.Min(triangle.B.Y, triangle.C.Y))) < -20);
        Assert.Contains(triangles, triangle => triangle.Color.Red > 70 && triangle.Color.Blue > 70);
    }

    [Fact]
    public void TransparentCornerInterpolationDoesNotIntroduceBlackFringes()
    {
        var mesh = SvgGradientMeshService.CreateGrid(SKRect.Create(100, 100));
        mesh.Nodes[0].Color = SKColors.Transparent;
        foreach (var node in mesh.Nodes.Skip(1))
            node.Color = SKColors.Red;
        var triangles = SvgGradientMeshService.Tessellate(mesh);
        Assert.All(triangles, triangle => { Assert.Equal((byte)255, triangle.Color.Red); Assert.Equal((byte)0, triangle.Color.Green); });
        Assert.Contains(triangles, triangle => triangle.Color.Alpha < 100);
    }

    [Fact]
    public void MeshRoundTripPreservesEditableNetworkAndRendersOrdinarySvg()
    {
        using var editor = new SvgEditorController();
        editor.NewDocument(100, 100);
        var mesh = SvgGradientMeshService.CreateGrid(SKRect.Create(100, 100));
        foreach (var node in mesh.Nodes)
            node.Color = SKColors.Coral;
        var group = SvgGradientMeshService.Create(mesh, "mesh");
        editor.Execute("Mesh", () => editor.Document!.Children.Add(group));
        using var reloaded = new SvgEditorController();
        reloaded.LoadSvg(editor.GetSvg());
        var restored = reloaded.Document!.GetElementById<SvgGroup>("mesh");
        var network = SvgGradientMeshService.GetMesh(restored);
        Assert.Equal(mesh.Nodes[2].Position, network.Nodes[2].Position);
        Assert.Equal(mesh.Nodes[0].Color, network.Nodes[0].Color);
        Assert.All(restored.Children, child => Assert.IsType<SvgPolygon>(child));
        using var bitmap = new SKBitmap(100, 100);
        using var canvas = new SKCanvas(bitmap);
        canvas.Clear(SKColors.Transparent);
        canvas.DrawPicture(reloaded.Renderer.Picture!);
        Assert.Equal(SKColors.Coral, bitmap.GetPixel(45, 47));
        Assert.Equal((byte)255, bitmap.GetPixel(50, 50).Alpha);
    }

    [Fact]
    public void EditedCellGeometryCannotBeOverwrittenByReopeningTheMesh()
    {
        var mesh = SvgGradientMeshService.CreateGrid(SKRect.Create(100, 100));
        var group = SvgGradientMeshService.Create(mesh);
        ((SvgPolygon)group.Children[0]).Points[0] = 15;
        var before = group.GetXML();
        Assert.Throws<InvalidOperationException>(() => SvgGradientMeshService.GetMesh(group));
        Assert.Throws<InvalidOperationException>(() => SvgGradientMeshService.Apply(group, mesh));
        Assert.Equal(before, group.GetXML());
    }

    [Fact]
    public void InvalidOrLockedMeshLeavesExistingGeometryUnchanged()
    {
        var mesh = SvgGradientMeshService.CreateGrid(SKRect.Create(100, 100));
        var group = SvgGradientMeshService.Create(mesh);
        var before = group.GetXML();
        mesh.Nodes[0].Position = new SKPoint(float.NaN, 0);
        Assert.Throws<ArgumentException>(() => SvgGradientMeshService.Apply(group, mesh));
        Assert.Equal(before, group.GetXML());
        mesh.Nodes[0].Position = default;
        group.CustomAttributes["data-unodraw-locked"] = "true";
        Assert.Throws<InvalidOperationException>(() => SvgGradientMeshService.Apply(group, mesh));
    }

    [Fact]
    public void MeshQualityLimitAndXmlEntityExpansionAreRejected()
    {
        var mesh = SvgGradientMeshService.CreateGrid(SKRect.Create(100, 100), 16, 16);
        Assert.NotEmpty(SvgGradientMeshService.Tessellate(mesh));
        mesh.Subdivisions = 128;
        Assert.Throws<ArgumentException>(() => SvgGradientMeshService.Tessellate(mesh));
        Assert.Throws<System.Xml.XmlException>(() => SvgGradientMeshService.Deserialize("<!DOCTYPE mesh [<!ENTITY x 'x'>]><unodraw-mesh version='1'>&x;</unodraw-mesh>"));
    }

    [Fact]
    public void UpdatingMeshPreservesGroupIdentityEffectsAndOneStepUndo()
    {
        using var editor = new SvgEditorController();
        editor.NewDocument();
        var mesh = SvgGradientMeshService.CreateGrid(SKRect.Create(100, 100));
        var group = SvgGradientMeshService.Create(mesh, "mesh");
        group.Opacity = 0.6f;
        group.CustomAttributes["data-note"] = "keep";
        editor.Execute("Mesh", () => editor.Document!.Children.Add(group));
        var before = editor.GetSvg();
        mesh.Nodes[0].Color = SKColors.Gold;
        editor.Execute("Edit mesh", () => SvgGradientMeshService.Apply(group, mesh));
        Assert.Same(group, editor.Document!.GetElementById("mesh"));
        Assert.Equal(0.6f, group.Opacity);
        Assert.Equal("keep", group.CustomAttributes["data-note"]);
        editor.Undo();
        Assert.Equal(before, editor.GetSvg());
    }
}
