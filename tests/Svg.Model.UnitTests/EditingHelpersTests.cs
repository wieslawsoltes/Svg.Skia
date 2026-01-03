using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using ShimSkiaSharp;
using ShimSkiaSharp.Editing;
using Svg;
using Svg.Model;
using Svg.Model.Drawables;
using Svg.Model.Drawables.Elements;
using Svg.Model.Editing;
using Svg.Model.Services;
using Xunit;

namespace Svg.Model.UnitTests;

public class EditingHelpersTests
{
    private static readonly ISvgAssetLoader s_assetLoader = new TestAssetLoader();

    [Fact]
    public void DrawableWalker_Traverse_ReturnsAllNodes()
    {
        var root = new TestContainer(s_assetLoader, DrawableCloneTestData.CreateReferences());
        var child = new TestDrawable(s_assetLoader, DrawableCloneTestData.CreateReferences());
        root.ChildrenDrawables.Add(child);

        var useDrawable = CreateDrawable<UseDrawable>();
        useDrawable.ReferencedDrawable = new TestDrawable(s_assetLoader, DrawableCloneTestData.CreateReferences());
        root.ChildrenDrawables.Add(useDrawable);

        var switchDrawable = CreateDrawable<SwitchDrawable>();
        switchDrawable.FirstChild = new TestDrawable(s_assetLoader, DrawableCloneTestData.CreateReferences());
        root.ChildrenDrawables.Add(switchDrawable);

        var markerDrawable = CreateDrawable<MarkerDrawable>();
        markerDrawable.MarkerElementDrawable = new TestDrawable(s_assetLoader, DrawableCloneTestData.CreateReferences());
        root.ChildrenDrawables.Add(markerDrawable);

        var pathDrawable = CreateDrawable<PathDrawable>();
        var pathMarker = new TestDrawable(s_assetLoader, DrawableCloneTestData.CreateReferences());
        pathDrawable.MarkerDrawables = new List<DrawableBase> { pathMarker };
        root.ChildrenDrawables.Add(pathDrawable);

        var maskDrawable = CreateDrawable<MaskDrawable>();
        maskDrawable.ChildrenDrawables.Add(new TestDrawable(s_assetLoader, DrawableCloneTestData.CreateReferences()));
        root.MaskDrawable = maskDrawable;

        var nodes = DrawableWalker.Traverse(root).ToList();

        Assert.Equal(12, nodes.Count);
        Assert.Contains(root, nodes);
        Assert.Contains(child, nodes);
        Assert.Contains(useDrawable, nodes);
        Assert.Contains(useDrawable.ReferencedDrawable!, nodes);
        Assert.Contains(switchDrawable, nodes);
        Assert.Contains(switchDrawable.FirstChild!, nodes);
        Assert.Contains(markerDrawable, nodes);
        Assert.Contains(markerDrawable.MarkerElementDrawable!, nodes);
        Assert.Contains(pathDrawable, nodes);
        Assert.Contains(pathMarker, nodes);
        Assert.Contains(maskDrawable, nodes);
        Assert.Contains(maskDrawable.ChildrenDrawables[0], nodes);
    }

    [Fact]
    public void UpdateFills_CloneOnWrite_ClonesSharedPaint()
    {
        var root = new TestContainer(s_assetLoader, DrawableCloneTestData.CreateReferences());
        var sharedPaint = DrawableCloneTestData.CreatePaint(200);
        var childA = new TestDrawable(s_assetLoader, DrawableCloneTestData.CreateReferences()) { Fill = sharedPaint };
        var childB = new TestDrawable(s_assetLoader, DrawableCloneTestData.CreateReferences()) { Fill = sharedPaint };
        root.ChildrenDrawables.Add(childA);
        root.ChildrenDrawables.Add(childB);

        var updated = root.UpdateFills(
            paint => paint.Color is { },
            paint => paint.Color = new SKColor(1, 1, 1, 1),
            EditMode.CloneOnWrite);

        Assert.Equal(1, updated);
        Assert.NotSame(sharedPaint, childA.Fill);
        Assert.Same(childA.Fill, childB.Fill);
        Assert.Equal(new SKColor(10, 20, 30, 200), sharedPaint.Color);
    }

    [Fact]
    public void UpdateStrokes_InPlace_UpdatesUniquePaints()
    {
        var root = new TestContainer(s_assetLoader, DrawableCloneTestData.CreateReferences());
        var paint = DrawableCloneTestData.CreatePaint(100);
        var childA = new TestDrawable(s_assetLoader, DrawableCloneTestData.CreateReferences()) { Stroke = paint };
        var childB = new TestDrawable(s_assetLoader, DrawableCloneTestData.CreateReferences()) { Stroke = paint };
        root.ChildrenDrawables.Add(childA);
        root.ChildrenDrawables.Add(childB);

        var updated = root.UpdateStrokes(
            p => p.Color is { },
            p => p.Color = new SKColor(2, 2, 2, 2));

        Assert.Equal(1, updated);
        Assert.Equal(new SKColor(2, 2, 2, 2), paint.Color);
    }

    [Fact]
    public void UpdateOpacity_CloneOnWrite_UpdatesTargetOnly()
    {
        var root = new TestContainer(s_assetLoader, DrawableCloneTestData.CreateReferences());
        var paintA = DrawableCloneTestData.CreatePaint(10);
        var paintB = DrawableCloneTestData.CreatePaint(20);
        var childA = new TestDrawable(s_assetLoader, DrawableCloneTestData.CreateReferences()) { Opacity = paintA };
        var childB = new TestDrawable(s_assetLoader, DrawableCloneTestData.CreateReferences()) { Opacity = paintB };
        root.ChildrenDrawables.Add(childA);
        root.ChildrenDrawables.Add(childB);

        var updated = root.UpdateOpacity(
            p => ReferenceEquals(p, paintA),
            p => p.Color = new SKColor(9, 9, 9, 9),
            EditMode.CloneOnWrite);

        Assert.Equal(1, updated);
        Assert.NotSame(paintA, childA.Opacity);
        Assert.Same(paintB, childB.Opacity);
    }

    [Fact]
    public void SvgDocumentTraverseElements_ReturnsAllNodes()
    {
        var svg = "<svg><g id=\"group\"><rect id=\"rect\" /></g><circle id=\"circle\" /></svg>";
        var document = SvgService.FromSvg(svg);
        Assert.NotNull(document);

        var elements = document!.TraverseElements().ToList();

        Assert.Equal(4, elements.Count);
        Assert.Contains(elements, element => element.ID == "group");
        Assert.Contains(elements, element => element.ID == "rect");
        Assert.Contains(elements, element => element.ID == "circle");
    }

    [Fact]
    public void UpdateStyleAttributes_UpdatesMatchingElements()
    {
        var svg = "<svg><rect id=\"a\" /><rect id=\"b\" /></svg>";
        var document = SvgService.FromSvg(svg);
        Assert.NotNull(document);

        var updated = document!.UpdateStyleAttributes(
            element => element.ID == "b",
            element => element.Visibility = "hidden");

        Assert.Equal(1, updated);
        var target = document.Children.OfType<SvgVisualElement>().First(e => e.ID == "b");
        Assert.Equal("hidden", target.Visibility);
    }

    private static T CreateDrawable<T>() where T : DrawableBase
    {
        var type = typeof(T);
        var ctor = type.GetConstructor(
            BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public,
            binder: null,
            new[] { typeof(ISvgAssetLoader), typeof(HashSet<Uri>) },
            modifiers: null);
        Assert.NotNull(ctor);
        return (T)ctor!.Invoke(new object?[] { s_assetLoader, DrawableCloneTestData.CreateReferences() });
    }

    private sealed class TestDrawable : DrawableBase
    {
        public TestDrawable(ISvgAssetLoader assetLoader, HashSet<Uri>? references)
            : base(assetLoader, references)
        {
        }

        public override void OnDraw(SKCanvas canvas, DrawAttributes ignoreAttributes, DrawableBase? until)
        {
        }

        public override SKDrawable Clone()
        {
            var clone = new TestDrawable(AssetLoader, CloneReferences(References));
            CopyTo(clone, Parent);
            return clone;
        }
    }

    private sealed class TestContainer : DrawableContainer
    {
        public TestContainer(ISvgAssetLoader assetLoader, HashSet<Uri>? references)
            : base(assetLoader, references)
        {
        }

        public override SKDrawable Clone()
        {
            var clone = new TestContainer(AssetLoader, CloneReferences(References));
            CopyTo(clone, Parent);
            return clone;
        }
    }
}
