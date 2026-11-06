using System;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using Svg;
using Svg.Editor.Skia;
using Svg.Skia;
using Svg.Transforms;
using Xunit;

namespace Svg.Editor.Skia.UnitTests;

public class SvgTimelineServiceTests
{
    private const string Drawing = """
        <svg xmlns="http://www.w3.org/2000/svg" width="200" height="100">
          <defs><path id="route" d="M0 0 L100 0"/></defs>
          <rect id="shape" x="0" y="10" width="20" height="20" fill="red"/>
        </svg>
        """;

    [Fact]
    public void KeyframeTrackRoundTripsAndEvaluatesThroughExistingRuntime()
    {
        using var editor = Editor();
        var shape = editor.Document!.GetElementById<SvgRectangle>("shape");
        var track = SvgTimelineService.CreateTrack(shape, "x", "0", "100");
        track.Duration = "4s";
        SvgTimelineService.SetKey(track, .25, "50");
        editor.Execute("Animate x", () => SvgTimelineService.Apply(editor.Document!, track));
        Assert.Equal(1, editor.Session.UndoCount);
        using var restored = new SvgEditorController();
        restored.LoadSvg(editor.GetSvg());
        var loaded = Assert.Single(SvgTimelineService.ReadTracks(restored.Document!));
        Assert.Equal(new[] { 0, .25, 1 }, loaded.Keys.Select(key => key.Offset));
        using var animator = new SvgAnimationController(restored.Document!);
        var frame = animator.CreateAnimatedDocument(TimeSpan.FromSeconds(2));
        Assert.Equal(66.66667f, frame.GetElementById<SvgRectangle>("shape").X.Value, 3);
        Assert.Equal(0, shape.X.Value);
        editor.Undo();
        Assert.Empty(SvgTimelineService.ReadTracks(editor.Document!));
    }

    [Fact]
    public void FirstAuthoredAnimationRefreshesLiveRendererAndRemovalUndoRedo()
    {
        using var editor = Editor();
        Assert.False(editor.Renderer.HasAnimations);
        var track = SvgTimelineService.CreateTrack(editor.Document!.GetElementById("shape"), "x", "0", "100");
        editor.Execute("Add animation", () => SvgTimelineService.Apply(editor.Document!, track));
        Assert.True(editor.Renderer.HasAnimations);
        editor.Renderer.SetAnimationTime(TimeSpan.FromSeconds(1));
        Assert.Equal("shape", editor.Renderer.HitTestTopmostElement(new ShimSkiaSharp.SKPoint(55, 15))?.ID);
        Assert.Null(editor.Renderer.HitTestTopmostElement(new ShimSkiaSharp.SKPoint(5, 15)));
        track.Keys[1].Value = "200";
        editor.Execute("Change animation", () => SvgTimelineService.Apply(editor.Document!, track));
        Assert.Equal(TimeSpan.FromSeconds(1), editor.Renderer.AnimationTime);
        Assert.Equal("shape", editor.Renderer.HitTestTopmostElement(new ShimSkiaSharp.SKPoint(105, 15))?.ID);
        editor.Execute("Remove animation", () => SvgTimelineService.Delete(editor.Document!, track));
        Assert.False(editor.Renderer.HasAnimations);
        Assert.Null(editor.Renderer.AnimationController);
        Assert.Equal("shape", editor.Renderer.HitTestTopmostElement(new ShimSkiaSharp.SKPoint(5, 15))?.ID);
        editor.Undo();
        Assert.True(editor.Renderer.HasAnimations);
        editor.Renderer.SetAnimationTime(TimeSpan.FromSeconds(1));
        Assert.Equal("shape", editor.Renderer.HitTestTopmostElement(new ShimSkiaSharp.SKPoint(105, 15))?.ID);
        editor.Redo();
        Assert.False(editor.Renderer.HasAnimations);
    }

    [Fact]
    public void RefreshRetainsScheduledEventsWhileRebindingEditedTiming()
    {
        using var editor = Editor();
        var track = SvgTimelineService.CreateTrack(editor.Document!.GetElementById("shape"), "x", "0", "100");
        track.Begin = "indefinite";
        editor.Execute("Add event animation", () => SvgTimelineService.Apply(editor.Document!, track));
        var animator = editor.Renderer.AnimationController;
        editor.Renderer.SetAnimationTime(TimeSpan.FromSeconds(3));
        Assert.True(editor.Renderer.BeginAnimationElement(track.Source!.ID));
        editor.Renderer.SetAnimationTime(TimeSpan.FromSeconds(4));
        Assert.Equal("shape", editor.Renderer.HitTestTopmostElement(new ShimSkiaSharp.SKPoint(55, 15))?.ID);
        track.Duration = "4s";
        editor.Execute("Edit timing", () => SvgTimelineService.Apply(editor.Document!, track));
        Assert.Same(animator, editor.Renderer.AnimationController);
        Assert.Equal(TimeSpan.FromSeconds(4), editor.Renderer.AnimationTime);
        Assert.Equal("shape", editor.Renderer.HitTestTopmostElement(new ShimSkiaSharp.SKPoint(30, 15))?.ID);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void SaveAndSaveAsPreservePlayheadAndNativeAnimation(bool saveAs)
    {
        var directory = Path.Combine(Path.GetTempPath(), "unodraw-animation-save-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            var sourcePath = Path.Combine(directory, "drawing.svg");
            var targetPath = saveAs ? Path.Combine(directory, "renamed.svg") : sourcePath;
            using var editor = new SvgEditorController();
            editor.LoadSvg(Drawing, sourcePath);
            var track = SvgTimelineService.CreateTrack(editor.Document!.GetElementById("shape"), "x", "0", "100");
            editor.Execute("Animate shape", () => SvgTimelineService.Apply(editor.Document!, track));
            editor.Renderer.SetAnimationTime(TimeSpan.FromSeconds(1));
            editor.Save(targetPath);
            Assert.Equal(TimeSpan.FromSeconds(1), editor.Renderer.AnimationTime);
            Assert.Equal("shape", editor.Renderer.HitTestTopmostElement(new ShimSkiaSharp.SKPoint(55, 15))?.ID);
            Assert.Equal(0, editor.Document!.GetElementById<SvgRectangle>("shape").X.Value);
            Assert.Single(SvgTimelineService.ReadTracks(editor.Document!));
            using var restored = new SvgEditorController();
            restored.LoadSvg(File.ReadAllText(targetPath), targetPath);
            Assert.Single(SvgTimelineService.ReadTracks(restored.Document!));
            Assert.Equal(0, restored.Document!.GetElementById<SvgRectangle>("shape").X.Value);
            restored.Renderer.SetAnimationTime(TimeSpan.FromSeconds(1));
            Assert.Equal("shape", restored.Renderer.HitTestTopmostElement(new ShimSkiaSharp.SKPoint(55, 15))?.ID);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void FreezeSurvivesSerializationAndHoldsTheFinalFrame()
    {
        using var editor = Editor();
        var track = SvgTimelineService.CreateTrack(editor.Document!.GetElementById("shape"), "x", "0", "100");
        SvgTimelineService.Apply(editor.Document!, track);
        using var restored = new SvgEditorController();
        restored.LoadSvg(editor.GetSvg());
        Assert.True(Assert.Single(SvgTimelineService.ReadTracks(restored.Document!)).Freeze);
        Assert.Contains("fill=\"freeze\"", restored.GetSvg());
        Assert.DoesNotContain("fill:freeze", restored.GetSvg());
        var animation = XDocument.Parse(restored.GetSvg()).Descendants(XName.Get("animate", "http://www.w3.org/2000/svg")).Single();
        Assert.Equal("x", animation.Attribute("attributeName")?.Value);
        Assert.Equal("0;100", animation.Attribute("values")?.Value);
        Assert.Equal("2s", animation.Attribute("dur")?.Value);
        Assert.DoesNotContain(animation.Attributes(), attribute => attribute.Name.NamespaceName == "http://www.w3.org/2000/svg");
        using var animator = new SvgAnimationController(restored.Document!);
        Assert.Equal(100, animator.CreateAnimatedDocument(TimeSpan.FromSeconds(4)).GetElementById<SvgRectangle>("shape").X.Value);
    }

    [Fact]
    public void SplineAndDiscreteKeysUseNativeInterpolation()
    {
        using var editor = Editor();
        var track = SvgTimelineService.CreateTrack(editor.Document!.GetElementById("shape"), "x", "0", "100");
        track.Interpolation = SvgAnimationCalcMode.Spline;
        track.Keys[0].Spline = ".42 0 1 1";
        SvgTimelineService.Apply(editor.Document!, track);
        using (var animator = new SvgAnimationController(editor.Document!))
        {
            var value = animator.CreateAnimatedDocument(TimeSpan.FromSeconds(1)).GetElementById<SvgRectangle>("shape").X.Value;
            Assert.InRange(value, 30, 33);
        }
        track.Interpolation = SvgAnimationCalcMode.Discrete;
        track.Keys[1].Offset = .5;
        SvgTimelineService.Apply(editor.Document!, track);
        using var discrete = new SvgAnimationController(editor.Document!);
        Assert.Equal(0, discrete.CreateAnimatedDocument(TimeSpan.FromSeconds(.9)).GetElementById<SvgRectangle>("shape").X.Value);
        Assert.Equal(100, discrete.CreateAnimatedDocument(TimeSpan.FromSeconds(1.1)).GetElementById<SvgRectangle>("shape").X.Value);
    }

    [Fact]
    public void TransformAndMotionTracksRemainStandardSvg()
    {
        using var editor = Editor();
        var track = SvgTimelineService.CreateTrack(editor.Document!.GetElementById("shape"), "transform", "0 0", "100 20");
        track.Kind = SvgTimelineTrackKind.Transform;
        SvgTimelineService.Apply(editor.Document!, track);
        using (var animator = new SvgAnimationController(editor.Document!))
        {
            var transform = Assert.IsType<SvgTranslate>(Assert.Single(animator.CreateAnimatedDocument(TimeSpan.FromSeconds(1)).GetElementById("shape").Transforms));
            Assert.Equal(50, transform.X);
            Assert.Equal(10, transform.Y);
        }
        SvgTimelineService.Delete(editor.Document!, track);
        var motion = SvgTimelineService.CreateTrack(editor.Document!.GetElementById("shape"));
        motion.Kind = SvgTimelineTrackKind.Motion;
        motion.ValueMode = SvgTimelineValueMode.MotionPath;
        motion.MotionPathReference = "#route";
        motion.MotionRotation = "0";
        SvgTimelineService.Apply(editor.Document!, motion);
        Assert.Contains("mpath", editor.GetSvg());
        using var motionAnimator = new SvgAnimationController(editor.Document!);
        var translated = Assert.IsType<SvgTranslate>(Assert.Single(motionAnimator.CreateAnimatedDocument(TimeSpan.FromSeconds(1)).GetElementById("shape").Transforms));
        Assert.Equal(50, translated.X, 2);
    }

    [Fact]
    public void TrackUpdatesPreserveIdentityDynamicTimingAndCustomAttributes()
    {
        using var editor = new SvgEditorController();
        editor.LoadSvg("""
            <svg xmlns="http://www.w3.org/2000/svg" width="200" height="100">
              <rect id="shape" width="10" height="10">
                <animate id="existing" attributeName="x" values="0;100" dur="4s" begin="shape.click;5s"
                 end="other.end" min="1s" max="20s" restart="whenNotActive" repeatDur="10s" data-note="preserve"/>
              </rect>
            </svg>
            """);
        var before = editor.GetSvg();
        var track = Assert.Single(SvgTimelineService.ReadTracks(editor.Document!));
        Assert.Equal(before, editor.GetSvg());
        var animation = track.Source;
        track.Duration = "6s";
        track.Keys[1].Value = "150";
        SvgTimelineService.Apply(editor.Document!, track);
        Assert.Same(animation, track.Source);
        Assert.Equal("existing", animation!.ID);
        Assert.Equal("shape.click;5s", animation.Begin);
        Assert.Equal("other.end", animation.End);
        Assert.Equal("1s", animation.Minimum);
        Assert.Equal("20s", animation.Maximum);
        Assert.Equal("10s", animation.RepeatDuration);
        Assert.Equal(SvgAnimationRestart.WhenNotActive, animation.Restart);
        Assert.Equal("preserve", animation.CustomAttributes["data-note"]);
    }

    [Fact]
    public void DuplicateGetsUniqueIdentityAndRetainsUnexposedSettings()
    {
        using var editor = Editor();
        var original = SvgTimelineService.CreateTrack(editor.Document!.GetElementById("shape"));
        SvgTimelineService.Apply(editor.Document!, original);
        original.Source!.CustomAttributes["data-note"] = "keep";
        original.Source.End = "shape.click";
        var duplicate = SvgTimelineService.DuplicateTrack(original);
        SvgTimelineService.Apply(editor.Document!, duplicate);
        Assert.NotEqual(original.Source.ID, duplicate.Source!.ID);
        Assert.Equal("keep", duplicate.Source.CustomAttributes["data-note"]);
        Assert.Equal("shape.click", duplicate.Source.End);
        Assert.Equal(2, SvgTimelineService.ReadTracks(editor.Document!).Count);
    }

    [Theory]
    [InlineData(-.1)]
    [InlineData(1.1)]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    public void InvalidKeyTimesCannotMutateDocument(double position)
    {
        using var editor = Editor();
        var before = editor.GetSvg();
        var track = SvgTimelineService.CreateTrack(editor.Document!.GetElementById("shape"));
        track.Keys[1].Offset = position;
        Assert.Throws<ArgumentException>(() => SvgTimelineService.Apply(editor.Document!, track));
        Assert.Equal(before, editor.GetSvg());
    }

    [Fact]
    public void MalformedEasingAndLockedTargetsAreRejectedAtomically()
    {
        using var editor = Editor();
        var shape = editor.Document!.GetElementById("shape");
        var track = SvgTimelineService.CreateTrack(shape);
        var before = editor.GetSvg();
        track.Interpolation = SvgAnimationCalcMode.Spline;
        track.Keys[0].Spline = "-1 0 1 1";
        Assert.Throws<ArgumentException>(() => SvgTimelineService.Apply(editor.Document!, track));
        Assert.Equal(before, editor.GetSvg());
        track.Keys[0].Spline = "0 0 1 1";
        shape.CustomAttributes["data-unodraw-locked"] = "true";
        before = editor.GetSvg();
        Assert.Throws<InvalidOperationException>(() => SvgTimelineService.Apply(editor.Document!, track));
        Assert.Equal(before, editor.GetSvg());
    }

    [Fact]
    public void BakeProducesStaticEditableFrameAndLeavesSourceUnchanged()
    {
        using var editor = Editor();
        var track = SvgTimelineService.CreateTrack(editor.Document!.GetElementById("shape"), "x", "0", "100");
        SvgTimelineService.Apply(editor.Document!, track);
        var original = editor.GetSvg();
        var snapshot = SvgTimelineService.BakeFrame(editor.Document!, TimeSpan.FromSeconds(1));
        Assert.Equal(50, snapshot.GetElementById<SvgRectangle>("shape").X.Value);
        Assert.Empty(snapshot.Descendants().OfType<SvgAnimationElement>());
        Assert.NotNull(snapshot.GetElementById("route"));
        Assert.Equal(original, editor.GetSvg());
        using var rendered = new SKSvg();
        rendered.FromSvg(snapshot.GetXML());
        Assert.False(rendered.HasAnimations);
        Assert.NotNull(rendered.Picture);
    }

    [Fact]
    public void ImplicitToAndByAnimationsRetainTheirValueSemantics()
    {
        using var editor = new SvgEditorController();
        editor.LoadSvg("""
            <svg xmlns="http://www.w3.org/2000/svg" width="200" height="100">
              <rect id="shape" x="40" width="10" height="10"><animate attributeName="x" to="100" dur="2s"/></rect>
            </svg>
            """);
        var track = Assert.Single(SvgTimelineService.ReadTracks(editor.Document!));
        Assert.Equal(SvgTimelineValueMode.To, track.ValueMode);
        track.Duration = "4s";
        SvgTimelineService.Apply(editor.Document!, track);
        var animate = Assert.IsType<SvgAnimate>(track.Source);
        Assert.True(string.IsNullOrEmpty(animate.Values));
        Assert.True(string.IsNullOrEmpty(animate.From));
        using var runtime = new SvgAnimationController(editor.Document!);
        Assert.Equal(70, runtime.CreateAnimatedDocument(TimeSpan.FromSeconds(2)).GetElementById<SvgRectangle>("shape").X.Value);
    }

    [Fact]
    public void DiscreteImportWithoutKeyTimesKeepsItsIntervals()
    {
        using var editor = new SvgEditorController();
        editor.LoadSvg("""
            <svg xmlns="http://www.w3.org/2000/svg" width="200" height="100">
              <rect id="shape" width="10" height="10"><animate attributeName="x" calcMode="discrete" values="0;10;20" dur="3s"/></rect>
            </svg>
            """);
        var track = Assert.Single(SvgTimelineService.ReadTracks(editor.Document!));
        Assert.Equal(1d / 3, track.Keys[1].Offset, 6);
        Assert.Equal(2d / 3, track.Keys[2].Offset, 6);
        SvgTimelineService.Apply(editor.Document!, track);
        using var runtime = new SvgAnimationController(editor.Document!);
        Assert.Equal(10, runtime.CreateAnimatedDocument(TimeSpan.FromSeconds(1.5)).GetElementById<SvgRectangle>("shape").X.Value);
        Assert.Equal(20, runtime.CreateAnimatedDocument(TimeSpan.FromSeconds(2.5)).GetElementById<SvgRectangle>("shape").X.Value);
    }

    [Theory]
    [InlineData("250ms", .25)]
    [InlineData("2min", 120)]
    [InlineData("01:02.5", 62.5)]
    [InlineData("1:02:03", 3723)]
    [InlineData("-2s", -2)]
    public void SvgClockFormatsAreParsedInvariantly(string text, double expected)
    {
        Assert.True(SvgTimelineService.TryParseClock(text, out var actual));
        Assert.Equal(expected, actual, 6);
    }

    private static SvgEditorController Editor()
    {
        var editor = new SvgEditorController();
        editor.LoadSvg(Drawing);
        return editor;
    }
}
