using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using SkiaSharp;
using Xunit;

namespace Svg.Editor.Skia.UnitTests;

public class SvgEditorGuideIndexTests
{
    [Fact]
    public void IndexedSnappingMatchesOrderedLegacyQueries()
    {
        var random = new Random(17431);
        for (var collection = 0; collection < 16; collection++)
        {
            var guides = new List<SvgEditorGuide>();
            for (var index = 0; index < 180; index++)
            {
                var origin = new SKPoint(random.Next(-80, 81), random.Next(-80, 81));
                var direction = index % 13 == 0 ? new SKPoint(3, 4)
                    : index % 11 == 0 ? new SKPoint(-4, 3)
                    : index % 2 == 0 ? new SKPoint(0, index % 4 == 0 ? -1 : 1)
                    : new SKPoint(index % 3 == 0 ? -1 : 1, 0);
                guides.Add(new SvgEditorGuide("guide-" + index, origin, direction));
            }
            var indexed = new SvgEditorGuideIndex(guides);
            for (var query = 0; query < 120; query++)
            {
                var point = new SKPoint((float)(random.NextDouble() * 200 - 100), (float)(random.NextDouble() * 200 - 100));
                var tolerance = query % 10 == 0 ? 0 : query % 9 == 0 ? 400 : (float)(random.NextDouble() * 8);
                AssertSameSnap(guides, indexed, point, tolerance);
            }
        }
    }

    [Fact]
    public void IndexedSnappingPreservesTiesDuplicatesAndIntersections()
    {
        var guides = Enumerable.Range(0, 200).Select(index =>
            new SvgEditorGuide("far-" + index, new SKPoint(1000 + index * 10, 1000), new SKPoint(0, 1))).ToList();
        guides.Insert(2, new SvgEditorGuide("left", new SKPoint(-5, 0), new SKPoint(0, 1)));
        guides.Insert(13, new SvgEditorGuide("above", new SKPoint(0, -5), new SKPoint(1, 0)));
        guides.Insert(47, new SvgEditorGuide("right", new SKPoint(5, 0), new SKPoint(0, 1)));
        guides.Insert(73, new SvgEditorGuide("below", new SKPoint(0, 5), new SKPoint(1, 0)));
        guides.Insert(101, new SvgEditorGuide("diagonal", default, new SKPoint(1, 1)));
        guides.Insert(125, new SvgEditorGuide("same-id", new SKPoint(5, 0), new SKPoint(0, -1)));
        guides.Insert(167, new SvgEditorGuide("same-id", new SKPoint(5, 0), new SKPoint(0, 1)));
        var indexed = new SvgEditorGuideIndex(guides);
        foreach (var x in new[] { -5f, -2.5f, 0f, 2.5f, 5f, 10f })
            foreach (var y in new[] { -5f, 0f, 5f, 7.5f })
                foreach (var tolerance in new[] { 0f, 2.5f, 5f, 7.5f })
                    AssertSameSnap(guides, indexed, new SKPoint(x, y), tolerance);
    }

    [Fact]
    public void IndexedSnappingMatchesFloatBoundariesAndExtremeCoordinates()
    {
        var positions = new[]
        {
            -float.MaxValue, -float.MaxValue / 4, -1e20f, -1000000f, -1f,
            -1e-20f, -float.Epsilon, -0f, 0f, float.Epsilon, 1e-20f,
            MathF.BitDecrement(1), 1f, MathF.BitIncrement(1), 1000000f,
            1e20f, float.MaxValue / 4, float.MaxValue
        };
        var guides = new List<SvgEditorGuide>();
        foreach (var position in positions)
        {
            guides.Add(new SvgEditorGuide("x-" + guides.Count, new SKPoint(position, position), new SKPoint(0, 1)));
            guides.Add(new SvgEditorGuide("y-" + guides.Count, new SKPoint(position, position), new SKPoint(-1, 0)));
        }
        guides.Add(new SvgEditorGuide("invalid-origin", new SKPoint(float.NaN, 0), new SKPoint(0, 1)));
        guides.Add(new SvgEditorGuide("invalid-direction", default, new SKPoint(float.PositiveInfinity, 0)));
        guides.Add(new SvgEditorGuide("zero-direction", default, default));
        var indexed = new SvgEditorGuideIndex(guides);
        foreach (var position in positions)
            foreach (var tolerance in new[] { 0f, float.Epsilon, 1e-20f, MathF.BitDecrement(1), 1f, MathF.BitIncrement(1), 5f, 1e20f, float.MaxValue })
            {
                AssertSameSnap(guides, indexed, new SKPoint(position, 0), tolerance);
                AssertSameSnap(guides, indexed, new SKPoint(1, position), tolerance);
            }
        var random = new Random(19281);
        for (var query = 0; query < 2000; query++)
        {
            var x = BitConverter.Int32BitsToSingle((int)random.NextInt64(int.MinValue, (long)int.MaxValue + 1));
            var y = BitConverter.Int32BitsToSingle((int)random.NextInt64(int.MinValue, (long)int.MaxValue + 1));
            var tolerance = BitConverter.Int32BitsToSingle(random.Next());
            if (float.IsFinite(x) && float.IsFinite(y) && float.IsFinite(tolerance))
                AssertSameSnap(guides, indexed, new SKPoint(x, y), tolerance);
        }
    }

    [Fact]
    public void IndexOwnsItsSnapshotAndKeepsValidation()
    {
        var guides = new List<SvgEditorGuide> { new("first", new SKPoint(10, 0), new SKPoint(0, 1)) };
        var indexed = new SvgEditorGuideIndex(guides);
        guides.Clear();
        Assert.Equal(new SKPoint(10, 4), indexed.Snap(new SKPoint(11, 4), 2, out var first, out _));
        Assert.Equal("first", first);
        Assert.Throws<NotSupportedException>(() => ((IList<SvgEditorGuide>)indexed.Guides).Clear());
        Assert.Throws<ArgumentNullException>(() => new SvgEditorGuideIndex(null!));
        Assert.Throws<ArgumentOutOfRangeException>(() => indexed.Snap(new SKPoint(float.NaN, 0), 5, out _, out _));
        Assert.Throws<ArgumentOutOfRangeException>(() => indexed.Snap(default, -1, out _, out _));
        Assert.Throws<ArgumentOutOfRangeException>(() => indexed.Snap(default, float.PositiveInfinity, out _, out _));
    }

    [Fact]
    public void CachePreservesCandidateOrderExclusionsAndExistingLimit()
    {
        using var editor = new SvgEditorController();
        var source = new StringBuilder("<svg xmlns='http://www.w3.org/2000/svg' width='1200' height='800' viewBox='10 20 600 400'>")
            .Append("<defs><rect id='definition' x='7' y='8' width='10' height='10'/></defs>")
            .Append("<g id='selected' transform='translate(7 8)'><rect x='1' y='2' width='3' height='4'/></g>")
            .Append("<g display='none'><rect x='11' y='12' width='13' height='14'/></g>")
            .Append("<g data-unodraw-locked='true'><rect x='21' y='22' width='23' height='24'/></g>")
            .Append("<rect visibility='hidden' x='31' y='32' width='33' height='34'/>");
        for (var index = 0; index < 1100; index++)
            source.Append("<rect x='").Append(index).Append("' y='").Append(index % 80).Append("' width='1' height='2'/>");
        editor.LoadSvg(source.Append("</svg>").ToString());
        var excluded = editor.AddGuide(new SKPoint(41, 32), new SKPoint(3, 4));
        editor.AddGuide(new SKPoint(52, 63), new SKPoint(0, 1));
        editor.Select(editor.Document!.GetElementById<SvgGroup>("selected"));
        var cache = new SvgEditorGuideCache();
        var indexed = cache.GetSnapIndex(editor, true, true, excluded);
        var expected = LegacyCandidates(editor, true, true, excluded);
        Assert.Equal(SvgEditorGuideCache.MaximumSmartGuides + 1, indexed.Guides.Count);
        Assert.Equal(expected, indexed.Guides);
        Assert.DoesNotContain(indexed.Guides, guide => guide.Id == excluded);
        foreach (var point in new[] { new SKPoint(3, 5), new SKPoint(52, 65), new SKPoint(500, 100) })
            AssertSameSnap(expected, indexed, point, 5);
        Assert.Same(indexed, cache.GetSnapIndex(editor, true, true, excluded));
        Assert.Equal(LegacyCandidates(editor, false, true, null), cache.GetSnapIndex(editor, false, true).Guides);
        Assert.Equal(editor.GetGuides(), cache.GetSnapIndex(editor, true, false).Guides);
        Assert.Empty(cache.GetSnapIndex(editor, false, false).Guides);
    }

    [Fact]
    public void CacheReusesCameraAndSelectionIndependentStateButRefreshesGeometry()
    {
        using var editor = new SvgEditorController();
        editor.LoadSvg("<svg xmlns='http://www.w3.org/2000/svg' width='200' height='100' viewBox='10 20 100 50'><rect id='r' x='20' y='25' width='10' height='10'/></svg>");
        editor.AddGuide(new SKPoint(40, 30), new SKPoint(1, 0));
        var cache = new SvgEditorGuideCache();
        var guides = cache.GetGuides(editor);
        var original = cache.GetSnapIndex(editor, true, true);
        var viewport = new SvgEditorViewport();
        viewport.Resize(800, 600);
        viewport.Pan(42, 37);
        viewport.ZoomAt(2, new SKPoint(100, 200));
        Assert.Same(original, cache.GetSnapIndex(editor, true, true));
        var rectangle = editor.Document!.GetElementById<SvgRectangle>("r");
        editor.Select(rectangle);
        Assert.Same(guides, cache.GetGuides(editor));
        var selected = cache.GetSnapIndex(editor, true, true);
        Assert.NotSame(original, selected);
        Assert.Equal(LegacyCandidates(editor, true, true, null), selected.Guides);
        var manual = cache.GetSnapIndex(editor, true, false);
        editor.ClearSelection();
        Assert.Same(manual, cache.GetSnapIndex(editor, true, false));

        var revision = editor.DocumentRevision;
        rectangle.X = 45;
        editor.Refresh();
        Assert.Equal(revision, editor.DocumentRevision);
        var preview = cache.GetSnapIndex(editor, true, true);
        Assert.NotSame(selected, preview);
        Assert.Equal(LegacyCandidates(editor, true, true, null), preview.Guides);
        Assert.NotSame(guides, cache.GetGuides(editor));

        editor.Select(rectangle);
        editor.MoveSelection(10, 0);
        var moved = cache.GetSnapIndex(editor, true, true);
        Assert.NotSame(preview, moved);
        editor.Undo();
        Assert.NotSame(moved, cache.GetSnapIndex(editor, true, true));
        Assert.Equal(LegacyCandidates(editor, true, true, null), cache.GetSnapIndex(editor, true, true).Guides);
        cache.Clear();
        Assert.NotSame(manual, cache.GetSnapIndex(editor, true, false));
        editor.NewDocument();
        Assert.Empty(cache.GetGuides(editor));
        Assert.Equal(LegacyCandidates(editor, true, true, null), cache.GetSnapIndex(editor, true, true).Guides);
        Assert.Empty(cache.GetGuides(null));
    }

    [Fact]
    public void CacheTracksRendererChangesWithoutControllerRevision()
    {
        using var editor = new SvgEditorController();
        editor.LoadSvg("<svg xmlns='http://www.w3.org/2000/svg' width='200' height='100'><rect id='r' x='20' y='25' width='10' height='10'><animate attributeName='x' from='20' to='80' dur='1s' fill='freeze'/></rect></svg>");
        var cache = new SvgEditorGuideCache();
        var before = cache.GetSnapIndex(editor, false, true);
        var revision = editor.RenderRevision;
        editor.Renderer.SetAnimationTime(TimeSpan.FromMilliseconds(750));
        Assert.Equal(revision, editor.RenderRevision);
        var after = cache.GetSnapIndex(editor, false, true);
        Assert.NotSame(before, after);
        Assert.Equal(LegacyCandidates(editor, false, true, null), after.Guides);
    }

    private static void AssertSameSnap(IReadOnlyList<SvgEditorGuide> guides, SvgEditorGuideIndex indexed, SKPoint point, float tolerance)
    {
        var expected = SvgEditorGuideService.Snap(point, tolerance, guides, out var expectedFirst, out var expectedSecond);
        var actual = indexed.Snap(point, tolerance, out var actualFirst, out var actualSecond);
        Assert.Equal(BitConverter.SingleToInt32Bits(expected.X), BitConverter.SingleToInt32Bits(actual.X));
        Assert.Equal(BitConverter.SingleToInt32Bits(expected.Y), BitConverter.SingleToInt32Bits(actual.Y));
        Assert.Equal(expectedFirst, actualFirst);
        Assert.Equal(expectedSecond, actualSecond);
    }

    // The pre-index canvas candidate construction is retained as an independent
    // behavioral oracle for exclusions, original ordering, IDs, and the cap.
    private static IReadOnlyList<SvgEditorGuide> LegacyCandidates(SvgEditorController controller,
        bool snapToGuides, bool smartGuides, string? excludedGuideId)
    {
        var result = new List<SvgEditorGuide>();
        if (snapToGuides)
            result.AddRange(controller.GetGuides().Where(guide => guide.Id != excludedGuideId));
        if (!smartGuides)
            return result;
        var smart = new List<SvgEditorGuide>();
        var selected = controller.SelectedElements.ToArray();
        if (controller.Renderer.Picture is { } picture)
            AddBounds(picture.CullRect, "artboard");
        foreach (var element in controller.Document!.Descendants().OfType<SvgVisualElement>())
        {
            if (smart.Count >= 6000)
                break;
            if (element is SvgGroup || SvgEditorController.IsEditLocked(element) || IsSelected(element) || !IsVisible(element))
                continue;
            if (controller.GetSceneNode(element) is not { } node || node.TransformedBounds.IsEmpty)
                continue;
            var bounds = node.TransformedBounds;
            AddBounds(new SKRect(bounds.Left, bounds.Top, bounds.Right, bounds.Bottom), "object-" + smart.Count);
        }
        result.AddRange(smart);
        return result;

        bool IsSelected(SvgElement element)
        {
            for (var item = element; item is not null; item = item.Parent)
                if (item is SvgVisualElement visual && selected.Contains(visual))
                    return true;
            return false;
        }
        static bool IsVisible(SvgElement element)
        {
            for (var item = element; item is not null; item = item.Parent)
            {
                if (item is SvgDefinitionList or SvgSymbol or SvgClipPath or SvgMask or SvgPatternServer or SvgMarker)
                    return false;
                if (item is SvgVisualElement visual && (visual.Display == "none" || visual.Visibility is "hidden" or "collapse"))
                    return false;
            }
            return true;
        }
        void AddBounds(SKRect bounds, string id)
        {
            if (!float.IsFinite(bounds.Left) || !float.IsFinite(bounds.Top) || !float.IsFinite(bounds.Right) || !float.IsFinite(bounds.Bottom))
                return;
            foreach (var x in new[] { bounds.Left, bounds.MidX, bounds.Right })
                smart.Add(new SvgEditorGuide("smart-" + id + "-x-" + x, new SKPoint(x, bounds.Top), new SKPoint(0, 1)));
            foreach (var y in new[] { bounds.Top, bounds.MidY, bounds.Bottom })
                smart.Add(new SvgEditorGuide("smart-" + id + "-y-" + y, new SKPoint(bounds.Left, y), new SKPoint(1, 0)));
        }
    }
}
