using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using ShimSkiaSharp;
using Svg;
using Svg.Model;
using Svg.Model.Drawables;
using Svg.Model.Drawables.Elements;
using Xunit;

namespace Svg.Model.UnitTests;

public class DrawableCloneTests
{
    private static readonly ISvgAssetLoader s_assetLoader = new TestAssetLoader();

    [Fact]
    public void DrawableBase_Clone_CopiesBaseState()
    {
        var drawable = new TestDrawable(s_assetLoader, DrawableCloneTestData.CreateReferences());
        PopulateBase(drawable);
        drawable.MaskDrawable = CreateMaskDrawable();
        drawable.MaskDrawable.Parent = drawable;

        var clone = (TestDrawable)drawable.Clone();

        Assert.NotSame(drawable, clone);
        Assert.NotSame(drawable.References, clone.References);
        Assert.Equal(drawable.References, clone.References);
        Assert.Same(drawable.Element, clone.Element);
        Assert.Equal(drawable.GeometryBounds, clone.GeometryBounds);
        Assert.Equal(drawable.TransformedBounds, clone.TransformedBounds);
        Assert.Equal(drawable.Transform, clone.Transform);
        Assert.Equal(drawable.TotalTransform, clone.TotalTransform);
        Assert.Equal(drawable.Overflow, clone.Overflow);
        Assert.Equal(drawable.Clip, clone.Clip);
        Assert.Equal(drawable.FilterClip, clone.FilterClip);

        Assert.NotSame(drawable.ClipPath, clone.ClipPath);
        Assert.NotSame(drawable.MaskDrawable, clone.MaskDrawable);
        Assert.Same(clone, clone.MaskDrawable!.Parent);
        Assert.NotSame(drawable.Mask, clone.Mask);
        Assert.NotSame(drawable.MaskDstIn, clone.MaskDstIn);
        Assert.NotSame(drawable.Opacity, clone.Opacity);
        Assert.NotSame(drawable.Filter, clone.Filter);
        Assert.NotSame(drawable.Fill, clone.Fill);
        Assert.NotSame(drawable.Stroke, clone.Stroke);
    }

    [Fact]
    public void DrawablePath_Clone_CopiesPathAndMarkers()
    {
        var drawable = CreateDrawable<LineDrawable>();
        PopulateBase(drawable);
        drawable.Path = DrawableCloneTestData.CreatePath();

        var marker = CreateDrawable<MarkerDrawable>();
        PopulateBase(marker);
        marker.Parent = drawable;
        drawable.MarkerDrawables = new List<DrawableBase> { marker };

        var clone = (LineDrawable)drawable.Clone();

        Assert.NotSame(drawable.Path, clone.Path);
        Assert.NotSame(drawable.MarkerDrawables, clone.MarkerDrawables);
        Assert.Single(clone.MarkerDrawables!);
        Assert.NotSame(marker, clone.MarkerDrawables![0]);
        Assert.Same(clone, clone.MarkerDrawables[0].Parent);
    }

    [Theory]
    [MemberData(nameof(PathDerivedTypes))]
    public void PathDerived_Clone_ReturnsSameType(Type type)
    {
        var drawable = (DrawablePath)CreateDrawable(type);
        PopulateBase(drawable);
        drawable.Path = DrawableCloneTestData.CreatePath();

        var clone = (DrawablePath)drawable.Clone();

        Assert.IsType(type, clone);
        Assert.NotSame(drawable.Path, clone.Path);
        Assert.Equal(drawable.Path!.Bounds, clone.Path!.Bounds);
    }

    [Theory]
    [MemberData(nameof(ContainerDerivedTypes))]
    public void ContainerDerived_Clone_ClonesChildren(Type type)
    {
        var container = (DrawableContainer)CreateDrawable(type);
        PopulateBase(container);

        var child = CreateDrawable<CircleDrawable>();
        PopulateBase(child);
        child.Path = DrawableCloneTestData.CreatePath();
        child.Parent = container;
        container.ChildrenDrawables.Add(child);

        var clone = (DrawableContainer)container.Clone();

        Assert.IsType(type, clone);
        Assert.Single(clone.ChildrenDrawables);
        Assert.NotSame(child, clone.ChildrenDrawables[0]);
        Assert.Same(clone, clone.ChildrenDrawables[0].Parent);
        var clonedChild = Assert.IsAssignableFrom<DrawablePath>(clone.ChildrenDrawables[0]);
        Assert.NotSame(child.Path, clonedChild.Path);
    }

    [Fact]
    public void ImageDrawable_Clone_CopiesImageAndFragment()
    {
        var drawable = CreateDrawable<ImageDrawable>();
        PopulateBase(drawable);
        drawable.Image = DrawableCloneTestData.CreateImage();
        drawable.SrcRect = SKRect.Create(1, 2, 3, 4);
        drawable.DestRect = SKRect.Create(5, 6, 7, 8);
        drawable.FragmentTransform = SKMatrix.CreateScale(2, 3);

        var fragment = CreateDrawable<FragmentDrawable>();
        PopulateBase(fragment);
        fragment.Parent = drawable;
        drawable.FragmentDrawable = fragment;

        var clone = (ImageDrawable)drawable.Clone();

        Assert.NotSame(drawable.Image, clone.Image);
        Assert.Equal(drawable.SrcRect, clone.SrcRect);
        Assert.Equal(drawable.DestRect, clone.DestRect);
        Assert.Equal(drawable.FragmentTransform, clone.FragmentTransform);
        Assert.NotSame(fragment, clone.FragmentDrawable);
        Assert.Same(clone, clone.FragmentDrawable!.Parent);
    }

    [Fact]
    public void UseDrawable_Clone_CopiesReferencedDrawable()
    {
        var drawable = CreateDrawable<UseDrawable>();
        PopulateBase(drawable);

        var referenced = CreateDrawable<GroupDrawable>();
        PopulateBase(referenced);
        referenced.Parent = drawable;
        drawable.ReferencedDrawable = referenced;

        var clone = (UseDrawable)drawable.Clone();

        Assert.NotSame(referenced, clone.ReferencedDrawable);
        Assert.Same(clone, clone.ReferencedDrawable!.Parent);
    }

    [Fact]
    public void SwitchDrawable_Clone_CopiesFirstChild()
    {
        var drawable = CreateDrawable<SwitchDrawable>();
        PopulateBase(drawable);

        var child = CreateDrawable<GroupDrawable>();
        PopulateBase(child);
        child.Parent = drawable;
        drawable.FirstChild = child;

        var clone = (SwitchDrawable)drawable.Clone();

        Assert.NotSame(child, clone.FirstChild);
        Assert.Same(clone, clone.FirstChild!.Parent);
    }

    [Fact]
    public void MarkerDrawable_Clone_CopiesMarkerElementDrawable()
    {
        var drawable = CreateDrawable<MarkerDrawable>();
        PopulateBase(drawable);
        drawable.MarkerClipRect = SKRect.Create(1, 2, 3, 4);

        var markerElement = CreateDrawable<GroupDrawable>();
        PopulateBase(markerElement);
        markerElement.Parent = drawable;
        drawable.MarkerElementDrawable = markerElement;

        var clone = (MarkerDrawable)drawable.Clone();

        Assert.NotSame(markerElement, clone.MarkerElementDrawable);
        Assert.Equal(drawable.MarkerClipRect, clone.MarkerClipRect);
        Assert.Same(clone, clone.MarkerElementDrawable!.Parent);
    }

    [Fact]
    public void TextDrawable_Clone_CopiesTextAndPath()
    {
        var drawable = CreateDrawable<TextDrawable>();
        PopulateBase(drawable);
        drawable.Text = new SvgText();
        drawable.OwnerBounds = SKRect.Create(1, 2, 3, 4);
        SetTextPath(drawable, DrawableCloneTestData.CreatePath());

        var clone = (TextDrawable)drawable.Clone();

        Assert.Same(drawable.Text, clone.Text);
        Assert.Equal(drawable.OwnerBounds, clone.OwnerBounds);
        Assert.NotSame(drawable.Path, clone.Path);
        Assert.Equal(drawable.Path!.Bounds, clone.Path!.Bounds);
    }

    public static IEnumerable<object[]> PathDerivedTypes()
    {
        yield return new object[] { typeof(CircleDrawable) };
        yield return new object[] { typeof(EllipseDrawable) };
        yield return new object[] { typeof(LineDrawable) };
        yield return new object[] { typeof(PathDrawable) };
        yield return new object[] { typeof(PolygonDrawable) };
        yield return new object[] { typeof(PolylineDrawable) };
        yield return new object[] { typeof(RectangleDrawable) };
    }

    public static IEnumerable<object[]> ContainerDerivedTypes()
    {
        yield return new object[] { typeof(AnchorDrawable) };
        yield return new object[] { typeof(FragmentDrawable) };
        yield return new object[] { typeof(GroupDrawable) };
        yield return new object[] { typeof(MaskDrawable) };
        yield return new object[] { typeof(SymbolDrawable) };
    }

    private static T CreateDrawable<T>() where T : DrawableBase
        => (T)CreateDrawable(typeof(T));

    private static DrawableBase CreateDrawable(Type type)
    {
        var ctor = type.GetConstructor(
            BindingFlags.Instance | BindingFlags.NonPublic,
            binder: null,
            types: new[] { typeof(ISvgAssetLoader), typeof(HashSet<Uri>) },
            modifiers: null);

        Assert.NotNull(ctor);

        return (DrawableBase)ctor.Invoke(new object?[] { s_assetLoader, DrawableCloneTestData.CreateReferences() });
    }

    private static void PopulateBase(DrawableBase drawable)
    {
        drawable.Element = new SvgGroup();
        drawable.IsDrawable = true;
        drawable.IgnoreAttributes = DrawAttributes.ClipPath | DrawAttributes.Filter;
        drawable.IsAntialias = true;
        drawable.GeometryBounds = SKRect.Create(1, 2, 3, 4);
        drawable.TransformedBounds = SKRect.Create(2, 3, 4, 5);
        drawable.Transform = SKMatrix.CreateTranslation(1, 2);
        drawable.TotalTransform = SKMatrix.CreateScale(2, 3);
        drawable.Overflow = SKRect.Create(5, 6, 7, 8);
        drawable.Clip = SKRect.Create(9, 10, 11, 12);
        drawable.ClipPath = DrawableCloneTestData.CreateClipPath();
        drawable.Mask = DrawableCloneTestData.CreatePaint(1);
        drawable.MaskDstIn = DrawableCloneTestData.CreatePaint(2);
        drawable.Opacity = DrawableCloneTestData.CreatePaint(3);
        drawable.Filter = DrawableCloneTestData.CreatePaint(4);
        drawable.FilterClip = SKRect.Create(2, 2, 3, 3);
        drawable.Fill = DrawableCloneTestData.CreatePaint(5);
        drawable.Stroke = DrawableCloneTestData.CreatePaint(6);
    }

    private static MaskDrawable CreateMaskDrawable()
    {
        var mask = CreateDrawable<MaskDrawable>();
        PopulateBase(mask);
        mask.Element = new SvgMask();
        return mask;
    }

    private static void SetTextPath(TextDrawable drawable, SKPath path)
    {
        var property = typeof(TextDrawable).GetProperty("Path", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
        Assert.NotNull(property);
        property.SetValue(drawable, path);
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
}
