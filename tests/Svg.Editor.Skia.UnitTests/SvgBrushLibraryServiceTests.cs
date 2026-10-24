using System;
using System.Linq;
using SkiaSharp;
using Svg;
using Svg.Editor.Skia;
using Xunit;

namespace Svg.Editor.Skia.UnitTests;

public class SvgBrushLibraryServiceTests
{
    [Fact]
    public void LibraryRoundTripRetainsFullBrushParameters()
    {
        var brush = Definition(SvgBrushKind.Scatter);
        brush.Name = "Stars & shapes";
        brush.Seed = 42;
        brush.Color = new SKColor(12, 34, 56, 78);
        brush.Cap = SKStrokeCap.Square;
        brush.Rotation = 33;
        brush.FollowTangent = false;
        var xml = SvgBrushLibraryService.Export(new[] { brush });
        var restored = Assert.Single(SvgBrushLibraryService.Import(xml));
        Assert.Equal(brush.Name, restored.Name);
        Assert.Equal(brush.Color, restored.Color);
        Assert.Equal(42, restored.Seed);
        Assert.Equal(brush.StampPath, restored.StampPath);
        Assert.Equal(33, restored.Rotation);
        Assert.False(restored.FollowTangent);
        Assert.Equal(SKStrokeCap.Square, restored.Cap);
        Assert.Equal(brush.Profile.ToString(), restored.Profile.ToString());
    }

    [Fact]
    public void PatternBrushCreatesVectorMotifsAtMeasuredIntervals()
    {
        using var line = SKPath.ParseSvgPathData("M0 0 L100 0");
        var brush = Definition(SvgBrushKind.Pattern);
        brush.Width = 10;
        brush.Spacing = 2;
        brush.StampPath = "M-5 -5 L5 -5 L5 5 L-5 5 Z";
        using var result = SvgBrushLibraryService.CreateOutline(line, brush);
        Assert.Equal(new SKRect(-5, -5, 105, 5), result.Bounds);
        Assert.True(result.Contains(20, 0));
        Assert.False(result.Contains(10, 0));
    }

    [Fact]
    public void ScatterIsDeterministicForSeedAndChangesForAnotherSeed()
    {
        using var line = SKPath.ParseSvgPathData("M0 0 C100 100 200 -100 300 0");
        var brush = Definition(SvgBrushKind.Scatter);
        using var first = SvgBrushLibraryService.CreateOutline(line, brush);
        using var second = SvgBrushLibraryService.CreateOutline(line, brush);
        Assert.Equal(PathService.ToSvgPathData(first), PathService.ToSvgPathData(second));
        brush.Seed++;
        using var different = SvgBrushLibraryService.CreateOutline(line, brush);
        Assert.NotEqual(PathService.ToSvgPathData(first), PathService.ToSvgPathData(different));
    }

    [Fact]
    public void SaveReloadAndLinkedUpdatePreserveCenterlineIdentityAndOpacity()
    {
        using var editor = Load();
        var brush = Definition(SvgBrushKind.WidthProfile);
        brush.Width = 10;
        brush.Color = new SKColor(255, 0, 0, 128);
        var path = editor.Document!.GetElementById<SvgPath>("path");
        editor.Execute("Brush", () => { SvgBrushLibraryService.Save(editor.Document, brush); SvgBrushLibraryService.Apply(path, brush); });
        var centerline = path.CustomAttributes[SvgVariableStrokeService.CenterlineAttribute];
        var initialOpacity = path.FillOpacity;
        using var reopened = new SvgEditorController();
        reopened.LoadSvg(editor.GetSvg());
        var restored = Assert.Single(SvgBrushLibraryService.Load(reopened.Document!));
        restored.Width = 20;
        reopened.Execute("Update brush", () => SvgBrushLibraryService.Save(reopened.Document!, restored));
        path = reopened.Document!.GetElementById<SvgPath>("path");
        Assert.Equal(initialOpacity, path.FillOpacity);
        Assert.Equal(centerline, path.CustomAttributes[SvgVariableStrokeService.CenterlineAttribute]);
        using var geometry = PathService.ElementToPath(path);
        Assert.Equal(20, geometry!.Bounds.Height);
        Assert.Equal("keep", path.CustomAttributes["data-note"]);
    }

    [Fact]
    public void LinkedUpdateStagesAllPathsBeforeMutatingAnyResourceOrArtwork()
    {
        using var editor = Load();
        var brush = Definition(SvgBrushKind.Pattern);
        var first = editor.Document!.GetElementById<SvgPath>("path");
        SvgBrushLibraryService.Save(editor.Document, brush);
        SvgBrushLibraryService.Apply(first, brush);
        var second = (SvgPath)first.DeepCopy();
        second.ID = "second";
        editor.Document.Children.Add(second);
        second.CustomAttributes["data-unodraw-locked"] = "true";
        var before = editor.Document.GetXML();
        brush.Width = 50;
        Assert.Throws<InvalidOperationException>(() => SvgBrushLibraryService.Save(editor.Document, brush));
        Assert.Equal(before, editor.Document.GetXML());
    }

    [Fact]
    public void EditedGeneratedNodesRejectLinkedUpdatesWithoutLosingEdits()
    {
        using var editor = Load();
        var brush = Definition(SvgBrushKind.Pattern);
        var path = editor.Document!.GetElementById<SvgPath>("path");
        SvgBrushLibraryService.Save(editor.Document, brush);
        SvgBrushLibraryService.Apply(path, brush);
        path.PathData[0].End = new System.Drawing.PointF(123, 456);
        var before = editor.Document.GetXML();
        Assert.Throws<InvalidOperationException>(() => SvgBrushLibraryService.Save(editor.Document, brush));
        Assert.Equal(before, editor.Document.GetXML());
    }

    [Fact]
    public void RemovingBrushKeepsPortableArtworkAndDetachesTheLibraryLink()
    {
        using var editor = Load();
        var brush = Definition(SvgBrushKind.Pattern);
        var path = editor.Document!.GetElementById<SvgPath>("path");
        SvgBrushLibraryService.Save(editor.Document, brush);
        SvgBrushLibraryService.Apply(path, brush);
        var geometry = path.PathData.ToString();
        SvgBrushLibraryService.Remove(editor.Document, brush.Id);
        Assert.Equal(geometry, path.PathData.ToString());
        Assert.False(path.CustomAttributes.ContainsKey(SvgBrushLibraryService.BrushIdAttribute));
        Assert.True(path.CustomAttributes.ContainsKey(SvgBrushLibraryService.SettingsAttribute));
        Assert.Empty(SvgBrushLibraryService.Load(editor.Document));
    }

    [Fact]
    public void ExcessiveStampsMalformedProfilesAndEntitiesAreRejected()
    {
        using var line = SKPath.ParseSvgPathData("M0 0 L100000 0");
        var brush = Definition(SvgBrushKind.Pattern);
        brush.Width = 0.1;
        Assert.Throws<InvalidOperationException>(() => SvgBrushLibraryService.CreateOutline(line, brush));
        Assert.Throws<FormatException>(() => SvgBrushLibraryService.ParseProfile("0,1;bad"));
        Assert.Throws<System.Xml.XmlException>(() => SvgBrushLibraryService.Import("<!DOCTYPE x [<!ENTITY y 'z'>]><unodraw-brush-library version='1'>&y;</unodraw-brush-library>"));
        Assert.Throws<ArgumentException>(() => SvgBrushLibraryService.Export(new[] { brush, brush }));
    }

    [Fact]
    public void BrushApplyIsOneUndoStepAndSavedGeometryRendersWithoutItsDefinition()
    {
        using var editor = Load();
        var path = editor.Document!.GetElementById<SvgPath>("path");
        var before = editor.GetSvg();
        var brush = Definition(SvgBrushKind.WidthProfile);
        brush.Width = 10;
        brush.Color = SKColors.Red;
        editor.Execute("Brush", () => SvgBrushLibraryService.Apply(path, brush));
        Assert.Equal(1, editor.Session.UndoCount);
        using var reopened = new SvgEditorController();
        reopened.LoadSvg(editor.GetSvg());
        using var bitmap = new SKBitmap(140, 100);
        using var canvas = new SKCanvas(bitmap);
        canvas.Clear(SKColors.Transparent);
        canvas.DrawPicture(reopened.Renderer.Picture!);
        Assert.Equal(SKColors.Red, bitmap.GetPixel(60, 50));
        editor.Undo();
        Assert.Equal(before, editor.GetSvg());
    }

    [Fact]
    public void ClipboardRemapsBrushResourcesAndKeepsTheirArtworkEditable()
    {
        using var editor = Load();
        var brush = Definition(SvgBrushKind.Pattern);
        var path = editor.Document!.GetElementById<SvgPath>("path");
        SvgBrushLibraryService.Save(editor.Document, brush);
        editor.Execute("Brush", () => SvgBrushLibraryService.Apply(path, brush));
        editor.Select(path);
        var clipboard = editor.CopySelection();
        editor.Paste(clipboard, 0, 0);
        var pasted = Assert.IsType<SvgPath>(Assert.Single(editor.SelectedElements));
        var linkedId = SvgBrushLibraryService.GetBrushId(pasted);
        Assert.NotEqual(brush.Id, linkedId);
        Assert.Contains(SvgBrushLibraryService.Load(editor.Document), item => item.Id == linkedId);
        using var centerline = SvgVariableStrokeService.GetCenterline(pasted);
        Assert.NotNull(centerline);
    }

    private static SvgBrushDefinition Definition(SvgBrushKind kind) => new() { Id = "brush", Name = "Brush", Kind = kind };
    private static SvgEditorController Load()
    {
        var editor = new SvgEditorController();
        editor.LoadSvg("<svg xmlns='http://www.w3.org/2000/svg' width='140' height='100'><path id='path' d='M10 50 L110 50' fill='none' stroke='red' stroke-width='10' data-note='keep'/></svg>");
        return editor;
    }
}
