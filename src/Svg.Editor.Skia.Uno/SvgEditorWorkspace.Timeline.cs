using System.Diagnostics;
using System.Globalization;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using SkiaSharp;
using Svg.Editor.Skia;
using Uno.WinUI.Graphics2DSK;
using Windows.Foundation;

namespace Svg.Editor.Skia.Uno;

public sealed partial class SvgEditorWorkspace
{
    private TimeSpan _animationPlaybackOrigin;
    private async Task ShowTimelineAsync()
    {
        if (Controller is not { Document: { } source } controller)
            return;
        CommitInspectorEdits();
        Canvas.CancelGesture();
        _animationTimer.Stop();
        _animationClock.Stop();
        var revision = controller.DocumentRevision;
        var originalTime = controller.Renderer.AnimationTime;
        var returnTime = originalTime;
        using var draft = new SvgEditorController();
        var draftDocument = (SvgDocument)source.DeepCopy();
        draftDocument.BaseUri = source.BaseUri;
        draft.LoadDocument(draftDocument, controller.Session.CurrentFile);
        var availableWidth = DialogContentWidth(1100);
        var narrow = availableWidth < 780;
        using var preview = new TimelinePreview(draft) { Height = narrow ? 165 : 200, MinWidth = Math.Min(280, availableWidth) };
        using var ruler = new TimelineRuler { MinWidth = 500, Height = 130 };
        var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(16) };
        var clock = new Stopwatch();
        var playStart = 0d;
        var playhead = 0d;
        var displayClock = Stopwatch.StartNew();
        var changing = false;
        var closed = false;
        var playing = false;
        SvgTimelineTrack? current = null;
        var loadedSignature = string.Empty;
        var tracks = new List<SvgTimelineTrack>();
        var trackList = new ListView { Height = 170, SelectionMode = ListViewSelectionMode.Single };
        var target = new ComboBox { Header = "Target object", HorizontalAlignment = HorizontalAlignment.Stretch };
        var kind = TimelineCombo("Track kind", Enum.GetNames<SvgTimelineTrackKind>());
        var attribute = new TextBox { Header = "Attribute", Text = "opacity", PlaceholderText = "opacity, fill, x, d…" };
        var addTrack = new Button { Content = "Add track" };
        var deleteTrack = new Button { Content = "Delete track" };
        var duplicateTrack = new Button { Content = "Duplicate track" };
        var trackTitle = new TextBlock { FontWeight = Microsoft.UI.Text.FontWeights.SemiBold, FontSize = 16 };
        var begin = new TextBox { Header = "Begin · seconds or SVG timing", Text = "0s" };
        var duration = new TextBox { Header = "Duration", Text = "2s" };
        var repeats = new TextBox { Header = "Repeat count", Text = "1", PlaceholderText = "1 or indefinite" };
        var interpolation = TimelineCombo("Interpolation", Enum.GetNames<SvgAnimationCalcMode>());
        var transformType = TimelineCombo("Transform", Enum.GetNames<SvgAnimateTransformType>());
        var mode = TimelineCombo("Value source", Enum.GetNames<SvgTimelineValueMode>());
        var freeze = new CheckBox { Content = "Hold final value", IsChecked = true };
        var additive = new CheckBox { Content = "Add to base value" };
        var accumulate = new CheckBox { Content = "Accumulate repeats" };
        var from = new TextBox { Header = "From", Text = "0" };
        var to = new TextBox { Header = "To / set value", Text = "1" };
        var by = new TextBox { Header = "By", Text = "1" };
        var motionPath = new TextBox { Header = "Motion path · SVG d", AcceptsReturn = true, TextWrapping = TextWrapping.Wrap, MaxHeight = 80 };
        var motionReference = new TextBox { Header = "Referenced motion path · optional #id" };
        var rotation = new TextBox { Header = "Motion rotation", Text = "auto" };
        var keys = new StackPanel { Spacing = 5 };
        var addKey = new Button { Content = "Add key at playhead" };
        var applyTrack = new Button { Content = "Update preview" };
        var undo = new Button { Content = "Undo" };
        var redo = new Button { Content = "Redo" };
        var play = new Button { Content = "▶ Play" };
        var rewind = new Button { Content = "|◀" };
        var loop = new CheckBox { Content = "Loop preview", IsChecked = true, VerticalAlignment = VerticalAlignment.Center };
        var time = NumberInput("Playhead · seconds", 0, 0, 604800);
        var span = NumberInput("View duration · seconds", 5, .01, 604800);
        var error = new TextBlock { Foreground = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 221, 86, 98)), TextWrapping = TextWrapping.Wrap };
        var info = new TextBlock
        {
            Text = "Edit standard SVG animation tracks. Click the ruler to scrub; drag a diamond to move a key. Preview changes stay here until you save the timeline.",
            TextWrapping = TextWrapping.Wrap,
            Opacity = .7,
            FontSize = 12
        };
        FrameworkElement Row(params UIElement[] controls)
        {
            if (!narrow)
                return PaintColumns(controls);
            var stack = new StackPanel { Spacing = 8 };
            foreach (var control in controls)
                stack.Children.Add(control);
            return stack;
        }

        var form = new StackPanel { Spacing = 8 };
        form.Children.Add(trackTitle);
        form.Children.Add(Row(begin, duration, repeats));
        form.Children.Add(Row(interpolation, mode, transformType));
        form.Children.Add(Row(freeze, additive, accumulate));
        form.Children.Add(Row(from, to, by));
        form.Children.Add(motionPath);
        form.Children.Add(motionReference);
        form.Children.Add(rotation);
        form.Children.Add(new TextBlock { Text = "KEYFRAMES · time, value and easing to next key", FontSize = 11, Opacity = .65 });
        form.Children.Add(keys);
        form.Children.Add(Row(addKey, applyTrack));
        var left = new StackPanel { Spacing = 8 };
        if (!narrow)
            left.Width = 230;
        left.Children.Add(new TextBlock { Text = "DOCUMENT TRACKS", FontSize = 11, Opacity = .65 });
        left.Children.Add(trackList);
        left.Children.Add(Row(deleteTrack, duplicateTrack));
        left.Children.Add(target);
        left.Children.Add(kind);
        left.Children.Add(attribute);
        left.Children.Add(addTrack);
        var right = new StackPanel { Spacing = 8 };
        right.Children.Add(preview);
        right.Children.Add(new ScrollViewer { Content = form, MaxHeight = 280, VerticalScrollMode = ScrollMode.Enabled, VerticalScrollBarVisibility = ScrollBarVisibility.Visible, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled });
        var columns = new Grid { ColumnSpacing = 16 };
        if (narrow)
        {
            columns.RowSpacing = 12;
            columns.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            columns.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            Grid.SetRow(right, 1);
        }
        else
        {
            columns.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            columns.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            Grid.SetColumn(right, 1);
        }
        columns.Children.Add(left);
        columns.Children.Add(right);
        var panel = new StackPanel { Spacing = 10, Width = Math.Min(1100, availableWidth) };
        panel.Children.Add(info);
        panel.Children.Add(columns);
        panel.Children.Add(new ScrollViewer { Content = ruler, MaxHeight = 220, VerticalScrollMode = ScrollMode.Enabled, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollMode = ScrollMode.Enabled, HorizontalScrollBarVisibility = ScrollBarVisibility.Auto });
        panel.Children.Add(Row(rewind, play, undo, redo, loop));
        panel.Children.Add(Row(time, span));
        panel.Children.Add(error);
        var dialog = MakeDialog("Animation timeline", new ScrollViewer { Content = panel, MaxHeight = Math.Max(220, (XamlRoot?.Size.Height ?? 940) - 160), VerticalScrollMode = ScrollMode.Enabled, VerticalScrollBarVisibility = ScrollBarVisibility.Visible, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled }, "Save timeline");
        dialog.Resources["ContentDialogMaxWidth"] = 1160d;

        void Fail(Exception exception)
        {
            timer.Stop();
            clock.Stop();
            playing = false;
            play.Content = "▶ Play";
            error.Text = exception.Message;
        }

        void Seek(double seconds, bool updateTimeDisplay = true)
        {
            seconds = Math.Clamp(seconds, 0, span.Value);
            playhead = seconds;
            if (updateTimeDisplay || displayClock.ElapsedMilliseconds >= 100)
            {
                changing = true;
                time.Value = seconds;
                changing = false;
                displayClock.Restart();
            }
            draft.Renderer.SetAnimationTime(TimeSpan.FromSeconds(seconds));
            ruler.Time = seconds;
            ruler.Invalidate();
            preview.Invalidate();
        }

        void UpdateRuler()
        {
            ruler.Tracks = tracks;
            ruler.SelectedTrack = current is null ? -1 : tracks.IndexOf(current);
            ruler.Duration = span.Value;
            ruler.Height = Math.Max(80, 34 + tracks.Count * 30);
            ruler.Invalidate();
        }

        void ReadFields()
        {
            if (current is null)
                return;
            current.Begin = begin.Text;
            current.Duration = duration.Text;
            current.RepeatCount = repeats.Text;
            current.Interpolation = (SvgAnimationCalcMode)Math.Max(0, interpolation.SelectedIndex);
            current.ValueMode = (SvgTimelineValueMode)Math.Max(0, mode.SelectedIndex);
            current.TransformType = (SvgAnimateTransformType)Math.Max(0, transformType.SelectedIndex);
            current.Freeze = freeze.IsChecked == true;
            current.Additive = additive.IsChecked == true;
            current.Accumulate = accumulate.IsChecked == true;
            current.From = from.Text;
            current.To = to.Text;
            current.By = by.Text;
            current.MotionPath = motionPath.Text;
            current.MotionPathReference = motionReference.Text;
            current.MotionRotation = rotation.Text;
        }

        bool CommitTrack()
        {
            if (current is null)
                return true;
            try
            {
                ReadFields();
                if (TimelineSignature(current) == loadedSignature)
                    return true;
                SvgTimelineService.Validate(current);
                draft.Execute("Edit animation track", () => SvgTimelineService.Apply(draft.Document!, current));
                loadedSignature = TimelineSignature(current);
                error.Text = string.Empty;
                undo.IsEnabled = draft.CanUndo;
                redo.IsEnabled = draft.CanRedo;
                Seek(playhead);
                UpdateRuler();
                return true;
            }
            catch (Exception exception) { Fail(exception); return false; }
        }

        void BuildKeys()
        {
            keys.Children.Clear();
            if (current is null)
                return;
            foreach (var key in current.Keys.OrderBy(key => key.Offset))
            {
                var offset = NumberInput("Time %", key.Offset * 100, 0, 100);
                var value = new TextBox { Header = "Value", Text = key.Value };
                var spline = new TextBox { Header = "Easing x1 y1 x2 y2", Text = key.Spline, PlaceholderText = ".42 0 .58 1" };
                var easing = TimelineCombo("Preset", new[] { "Custom", "Linear", "Ease", "Ease in", "Ease out", "Ease in/out" });
                easing.SelectedIndex = 0;
                var remove = new Button { Content = "−", VerticalAlignment = VerticalAlignment.Bottom, Width = 34, Padding = new Thickness(0) };
                ToolTipService.SetToolTip(remove, "Delete this keyframe");
                offset.ValueChanged += (_, _) => { key.Offset = offset.Value / 100; UpdateRuler(); };
                value.TextChanged += (_, _) => key.Value = value.Text;
                spline.TextChanged += (_, _) => key.Spline = spline.Text;
                easing.SelectionChanged += (_, _) =>
                {
                    if (easing.SelectedIndex > 0)
                        spline.Text = new[] { "", "0 0 1 1", ".25 .1 .25 1", ".42 0 1 1", "0 0 .58 1", ".42 0 .58 1" }[easing.SelectedIndex];
                };
                remove.Click += (_, _) => { current?.Keys.Remove(key); BuildKeys(); UpdateRuler(); };
                keys.Children.Add(Row(offset, value, spline, easing, remove));
            }
        }

        void LoadTrack(SvgTimelineTrack? track)
        {
            changing = true;
            current = track;
            form.Visibility = track is null ? Visibility.Collapsed : Visibility.Visible;
            deleteTrack.IsEnabled = duplicateTrack.IsEnabled = track is not null;
            if (track is not null)
            {
                trackTitle.Text = track.Label;
                begin.Text = track.Begin;
                duration.Text = track.Duration;
                repeats.Text = track.RepeatCount;
                interpolation.SelectedIndex = (int)track.Interpolation;
                mode.SelectedIndex = (int)track.ValueMode;
                transformType.SelectedIndex = (int)track.TransformType;
                transformType.Visibility = track.Kind == SvgTimelineTrackKind.Transform ? Visibility.Visible : Visibility.Collapsed;
                motionPath.Visibility = motionReference.Visibility = rotation.Visibility = track.Kind == SvgTimelineTrackKind.Motion ? Visibility.Visible : Visibility.Collapsed;
                freeze.IsChecked = track.Freeze;
                additive.IsChecked = track.Additive;
                accumulate.IsChecked = track.Accumulate;
                from.Text = track.From;
                to.Text = track.To;
                by.Text = track.By;
                motionPath.Text = track.MotionPath;
                motionReference.Text = track.MotionPathReference;
                rotation.Text = track.MotionRotation;
            }
            else
                trackTitle.Text = "Add an animation track";
            BuildKeys();
            UpdateRuler();
            loadedSignature = current is null ? string.Empty : TimelineSignature(current);
            changing = false;
        }

        void RebuildTracks(string? selectId = null)
        {
            changing = true;
            tracks = SvgTimelineService.ReadTracks(draft.Document!).ToList();
            trackList.ItemsSource = tracks;
            var index = selectId is null ? Math.Min(Math.Max(ruler.SelectedTrack, 0), tracks.Count - 1)
                : tracks.FindIndex(track => track.Source?.ID == selectId);
            trackList.SelectedIndex = index;
            var selectedTarget = (target.SelectedItem as ComboBoxItem)?.Tag as SvgElement;
            var selectedTargetId = selectedTarget?.ID;
            target.Items.Clear();
            foreach (var element in draft.Document!.Descendants().Where(element => element is SvgVisualElement || element is SvgGradientStop || element is SvgGradientServer))
                target.Items.Add(new ComboBoxItem { Content = string.IsNullOrEmpty(element.ID) ? global::Svg.Editor.Core.SvgElementInfo.GetElementName(element.GetType()) : element.ID, Tag = element });
            if (target.Items.Count > 0)
                target.SelectedItem = target.Items.OfType<ComboBoxItem>().FirstOrDefault(item =>
                    ReferenceEquals(item.Tag, selectedTarget) ||
                    (!string.IsNullOrEmpty(selectedTargetId) && (item.Tag as SvgElement)?.ID == selectedTargetId)) ?? target.Items[0];
            changing = false;
            LoadTrack(index >= 0 ? tracks[index] : null);
            undo.IsEnabled = draft.CanUndo;
            redo.IsEnabled = draft.CanRedo;
        }

        trackList.SelectionChanged += (_, _) =>
        {
            if (changing)
                return;
            var next = trackList.SelectedItem as SvgTimelineTrack;
            if (next == current)
                return;
            if (!CommitTrack())
            { changing = true; trackList.SelectedItem = current; changing = false; return; }
            LoadTrack(next);
        };
        addTrack.Click += (_, _) =>
        {
            if (!CommitTrack())
                return;
            try
            {
                if (target.SelectedItem is not ComboBoxItem { Tag: SvgElement element })
                    throw new InvalidOperationException("Choose an SVG object for the new track.");
                var track = SvgTimelineService.CreateTrack(element, attribute.Text.Trim());
                track.Kind = (SvgTimelineTrackKind)Math.Max(0, kind.SelectedIndex);
                if (track.Kind == SvgTimelineTrackKind.Transform)
                { track.AttributeName = "transform"; track.Additive = true; track.Keys[0].Value = "0 0"; track.Keys[1].Value = "100 0"; }
                if (track.Kind == SvgTimelineTrackKind.Motion)
                { track.Keys[0].Value = "0 0"; track.Keys[1].Value = "100 0"; }
                if (track.Kind == SvgTimelineTrackKind.Set)
                { track.ValueMode = SvgTimelineValueMode.To; track.To = "1"; }
                draft.Execute("Add animation track", () => SvgTimelineService.Apply(draft.Document!, track));
                RebuildTracks(track.Source?.ID);
                Seek(playhead);
                error.Text = string.Empty;
            }
            catch (Exception exception) { Fail(exception); }
        };
        deleteTrack.Click += (_, _) =>
        {
            if (current is null)
                return;
            try
            {
                draft.Execute("Delete animation track", () => SvgTimelineService.Delete(draft.Document!, current));
                current = null;
                RebuildTracks();
                Seek(playhead);
                error.Text = string.Empty;
            }
            catch (Exception exception) { Fail(exception); }
        };
        duplicateTrack.Click += (_, _) =>
        {
            if (!CommitTrack() || current?.Source is not { } sourceTrack)
                return;
            try
            {
                var copy = SvgTimelineService.ReadTrack(sourceTrack);
                // ReadTrack creates an independent editing model; the new native animation
                // receives a new document-unique ID when Apply attaches it.
                copy = SvgTimelineService.DuplicateTrack(copy);
                draft.Execute("Duplicate animation track", () => SvgTimelineService.Apply(draft.Document!, copy));
                RebuildTracks(copy.Source?.ID);
                Seek(playhead);
            }
            catch (Exception exception) { Fail(exception); }
        };
        applyTrack.Click += (_, _) => CommitTrack();
        addKey.Click += (_, _) =>
        {
            if (current is null)
                return;
            try
            {
                ReadFields();
                var offset = Math.Clamp((playhead - current.BeginSeconds) / Math.Max(.000001, current.DurationSeconds), 0, 1);
                if (current.Keys.Any(key => Math.Abs(key.Offset - offset) < .0000001))
                {
                    var ordered = current.Keys.OrderBy(key => key.Offset).ToArray();
                    if (ordered.Length > 1)
                    {
                        var pair = Enumerable.Range(0, ordered.Length - 1).OrderByDescending(i => ordered[i + 1].Offset - ordered[i].Offset).First();
                        offset = (ordered[pair].Offset + ordered[pair + 1].Offset) / 2;
                    }
                }
                var nearest = current.Keys.OrderBy(key => Math.Abs(key.Offset - offset)).FirstOrDefault();
                SvgTimelineService.SetKey(current, offset, nearest?.Value ?? "0");
                current.ValueMode = current.ValueMode == SvgTimelineValueMode.MotionPath ? current.ValueMode : SvgTimelineValueMode.KeyFrames;
                mode.SelectedIndex = (int)current.ValueMode;
                BuildKeys();
                UpdateRuler();
            }
            catch (Exception exception) { Fail(exception); }
        };
        undo.Click += (_, _) => { draft.Undo(); current = null; RebuildTracks(); Seek(playhead); error.Text = string.Empty; };
        redo.Click += (_, _) => { draft.Redo(); current = null; RebuildTracks(); Seek(playhead); error.Text = string.Empty; };
        ruler.SeekRequested += value =>
        {
            timer.Stop();
            clock.Stop();
            playing = false;
            play.Content = "▶ Play";
            Seek(value);
        };
        ruler.TrackSelected += index => { if (index >= 0 && index < tracks.Count) trackList.SelectedIndex = index; };
        ruler.KeyMoved += (trackIndex, keyIndex, offset) =>
        {
            if (trackIndex < 0 || trackIndex >= tracks.Count || keyIndex < 0 || keyIndex >= tracks[trackIndex].Keys.Count)
                return;
            if (current != tracks[trackIndex] && !CommitTrack())
                return;
            LoadTrack(tracks[trackIndex]);
            var key = current!.Keys[keyIndex];
            var old = key.Offset;
            key.Offset = offset;
            if (!CommitTrack())
                key.Offset = old;
            BuildKeys();
            UpdateRuler();
        };
        rewind.Click += (_, _) => { clock.Reset(); if (playing) clock.Start(); playStart = 0; Seek(0); };
        play.Click += (_, _) =>
        {
            if (playing)
            { timer.Stop(); clock.Stop(); playing = false; play.Content = "▶ Play"; Seek(playhead); return; }
            if (!CommitTrack())
                return;
            if (!double.IsFinite(span.Value) || span.Value <= 0)
            { error.Text = "Enter a positive preview duration."; return; }
            if (playhead >= span.Value)
                Seek(0);
            playStart = playhead;
            clock.Restart();
            playing = true;
            timer.Start();
            play.Content = "Ⅱ Pause";
        };
        timer.Tick += (_, _) =>
        {
            timer.Stop();
            if (closed || _disposed || !playing)
                return;
            try
            {
                var next = playStart + clock.Elapsed.TotalSeconds;
                if (next >= span.Value)
                {
                    if (loop.IsChecked == true)
                    { next %= span.Value; playStart = next; clock.Restart(); }
                    else
                    { next = span.Value; timer.Stop(); clock.Stop(); playing = false; play.Content = "▶ Play"; }
                }
                Seek(next, updateTimeDisplay: !playing);
                // Yield to input and finish this frame before scheduling another one.
                DispatcherQueue.TryEnqueue(Microsoft.UI.Dispatching.DispatcherQueuePriority.Low, () =>
                {
                    if (!closed && !_disposed && playing)
                        timer.Start();
                });
            }
            catch (Exception exception) { Fail(exception); }
        };
        time.ValueChanged += (_, _) =>
        {
            if (changing || !double.IsFinite(time.Value))
                return;
            playStart = time.Value;
            clock.Reset();
            if (playing)
                clock.Start();
            Seek(time.Value);
        };
        span.ValueChanged += (_, _) =>
        {
            if (!double.IsFinite(span.Value) || span.Value <= 0)
                return;
            UpdateRuler();
            Seek(Math.Min(playhead, span.Value));
        };
        dialog.PrimaryButtonClick += (_, args) =>
        {
            if (!CommitTrack())
            { args.Cancel = true; return; }
            try
            {
                if (_disposed || Controller != controller || controller.DocumentRevision != revision)
                    throw new InvalidOperationException("The source document changed while the timeline was open. Cancel and reopen it before saving.");
                if (draft.IsDirty)
                    controller.ReplaceSvg(draft.GetSvg(), "Edit animation timeline");
                returnTime = TimeSpan.FromSeconds(playhead);
            }
            catch (Exception exception) { args.Cancel = true; Fail(exception); }
        };
        RebuildTracks();
        if (controller.SelectedElements.FirstOrDefault() is { } selection)
        {
            var mapped = SvgTimelineService.MapElement(selection, draft.Document!);
            foreach (var item in target.Items.OfType<ComboBoxItem>())
                if (ReferenceEquals(item.Tag, mapped))
                    target.SelectedItem = item;
        }
        var extent = tracks.Select(track => Math.Max(0, track.BeginSeconds) + track.DurationSeconds).DefaultIfEmpty(5).Max();
        span.Value = Math.Clamp(extent, .01, 604800);
        Seek(Math.Min(originalTime.TotalSeconds, span.Value));
        try
        { await ShowDialogAsync(dialog); }
        finally
        {
            closed = true;
            playing = false;
            timer.Stop();
            clock.Stop();
            if (!_disposed && Controller == controller)
            { _animationPlaybackOrigin = returnTime; _animationClock.Reset(); controller.Renderer.SetAnimationTime(returnTime); Canvas.Invalidate(); }
        }
    }

    private void BakeAnimationFrame()
    {
        if (Controller is not { Document: { } document } controller)
            return;
        foreach (var animation in document.Descendants().OfType<SvgAnimationElement>())
            if (animation.TargetElement is { } target && SvgEditorController.IsEditLocked(target))
                throw new InvalidOperationException("Unlock animated objects and their layers before baking the document.");
        _animationTimer.Stop();
        _animationClock.Stop();
        var time = controller.Renderer.AnimationTime;
        var snapshot = SvgTimelineService.BakeFrame(document, time);
        controller.ReplaceSvg(snapshot.GetXML(), "Bake animation frame");
        _animationPlaybackOrigin = TimeSpan.Zero;
        _animationClock.Reset();
        StatusText.Text = $"Baked the {time.TotalSeconds:0.###} s frame into editable SVG.";
    }

    private static string TimelineSignature(SvgTimelineTrack track)
        => string.Join("\u001f", track.Kind, track.AttributeName, track.Begin, track.Duration, track.RepeatCount,
            track.Freeze, track.Additive, track.Accumulate, track.Interpolation, track.TransformType, track.ValueMode,
            track.From, track.To, track.By, track.MotionPath, track.MotionPathReference, track.MotionRotation,
            string.Join("\u001e", track.Keys.Select(key => key.Offset.ToString("R", CultureInfo.InvariantCulture) + "\u001d" + key.Value + "\u001d" + key.Spline)));

    private static ComboBox TimelineCombo(string header, IEnumerable<string> options)
    {
        var combo = new ComboBox { Header = header, HorizontalAlignment = HorizontalAlignment.Stretch };
        foreach (var option in options)
            combo.Items.Add(option);
        combo.SelectedIndex = 0;
        return combo;
    }

    private sealed class TimelinePreview(SvgEditorController controller) : SKCanvasElement, IDisposable
    {
        private readonly SKPaint _paint = new() { Color = SKColors.White };
        private bool _disposed;
        protected override void RenderOverride(SKCanvas canvas, Size area)
        {
            if (_disposed)
                return;
            using var restore = new SKAutoCanvasRestore(canvas);
            canvas.ClipRect(SKRect.Create((float)area.Width, (float)area.Height));
            canvas.Clear(new SKColor(34, 36, 46));
            if (controller.Renderer.Picture is not { } picture || picture.CullRect.IsEmpty)
                return;
            var bounds = picture.CullRect;
            var scale = Math.Min((float)(area.Width - 20) / bounds.Width, (float)(area.Height - 20) / bounds.Height);
            canvas.Translate((float)area.Width / 2, (float)area.Height / 2);
            canvas.Scale(scale);
            canvas.Translate(-bounds.MidX, -bounds.MidY);
            canvas.DrawRect(bounds, _paint);
            controller.Renderer.Draw(canvas);
        }
        public new void Dispose() { _disposed = true; _paint.Dispose(); base.Dispose(); }
    }

    private sealed class TimelineRuler : SKCanvasElement, IDisposable
    {
        private readonly SKPaint _paint = new() { IsAntialias = true };
        private readonly SKFont _font = new(SKTypeface.Default, 10);
        private readonly SKPath _diamond = CreateDiamond();
        private bool _disposed;
        private int _dragTrack = -1, _dragKey = -1;
        private double? _dragOffset;
        public IReadOnlyList<SvgTimelineTrack> Tracks { get; set; } = Array.Empty<SvgTimelineTrack>();
        public int SelectedTrack { get; set; } = -1;
        public double Duration { get; set; } = 5;
        public double Time { get; set; }
        public event Action<double>? SeekRequested;
        public event Action<int>? TrackSelected;
        public event Action<int, int, double>? KeyMoved;
        private const float LabelWidth = 165;
        private const float RowHeight = 30;
        private double TimeAt(double x) => Math.Clamp((x - LabelWidth) / Math.Max(1, ActualWidth - LabelWidth - 12) * Duration, 0, Duration);
        private float XAt(double seconds) => LabelWidth + (float)(seconds / Math.Max(.000001, Duration) * Math.Max(1, ActualWidth - LabelWidth - 12));

        public TimelineRuler()
        {
            PointerPressed += (_, args) =>
            {
                var point = args.GetCurrentPoint(this);
                if (!point.Properties.IsLeftButtonPressed)
                    return;
                var row = (int)Math.Floor((point.Position.Y - 30) / RowHeight);
                if (row >= 0 && row < Tracks.Count)
                {
                    TrackSelected?.Invoke(row);
                    var track = Tracks[row];
                    for (var key = 0; key < track.Keys.Count; key++)
                    {
                        var x = XAt(track.BeginSeconds + track.Keys[key].Offset * track.DurationSeconds);
                        if (Math.Abs(point.Position.X - x) <= 8)
                        {
                            _dragTrack = row;
                            _dragKey = key;
                            _dragOffset = track.Keys[key].Offset;
                            CapturePointer(args.Pointer);
                            args.Handled = true;
                            return;
                        }
                    }
                }
                if (point.Position.X >= LabelWidth)
                    SeekRequested?.Invoke(TimeAt(point.Position.X));
                args.Handled = true;
            };
            PointerMoved += (_, args) =>
            {
                if (_dragTrack < 0 || _dragTrack >= Tracks.Count)
                    return;
                var track = Tracks[_dragTrack];
                var original = track.Keys[_dragKey].Offset;
                _dragOffset = original == 0 || original == 1 && track.Interpolation != SvgAnimationCalcMode.Discrete
                    ? original
                    : Math.Clamp((TimeAt(args.GetCurrentPoint(this).Position.X) - track.BeginSeconds) / Math.Max(.000001, track.DurationSeconds), 0, 1);
                Invalidate();
                args.Handled = true;
            };
            PointerReleased += (_, args) =>
            {
                var track = _dragTrack;
                var key = _dragKey;
                var offset = _dragOffset;
                _dragTrack = _dragKey = -1;
                _dragOffset = null;
                ReleasePointerCapture(args.Pointer);
                if (track >= 0 && offset is { } value)
                    KeyMoved?.Invoke(track, key, value);
                Invalidate();
                args.Handled = true;
            };
            PointerCaptureLost += (_, _) => { _dragTrack = _dragKey = -1; _dragOffset = null; Invalidate(); };
        }

        protected override void RenderOverride(SKCanvas canvas, Size area)
        {
            if (_disposed)
                return;
            using var restore = new SKAutoCanvasRestore(canvas);
            canvas.ClipRect(SKRect.Create((float)area.Width, (float)area.Height));
            canvas.Clear(new SKColor(31, 33, 42));
            for (var i = 0; i <= 10; i++)
            {
                var seconds = i * Duration / 10;
                var x = XAt(seconds);
                _paint.Color = new SKColor(60, 63, 76);
                _paint.StrokeWidth = 1;
                canvas.DrawLine(x, 23, x, (float)area.Height, _paint);
                _paint.Color = new SKColor(175, 181, 197);
                canvas.DrawText(seconds.ToString("0.##", CultureInfo.InvariantCulture) + "s", x + 3, 15, SKTextAlign.Left, _font, _paint);
            }
            for (var row = 0; row < Tracks.Count; row++)
            {
                var track = Tracks[row];
                var y = 30 + row * RowHeight;
                if (row == SelectedTrack)
                { _paint.Color = new SKColor(100, 86, 190, 45); canvas.DrawRect(0, y, (float)area.Width, RowHeight, _paint); }
                _paint.Color = new SKColor(193, 198, 211);
                var label = track.Label.Length > 25 ? track.Label.Substring(0, 23) + "…" : track.Label;
                canvas.DrawText(label, 8, y + 19, SKTextAlign.Left, _font, _paint);
                _paint.Color = new SKColor(100, 87, 185, 110);
                canvas.DrawRect(XAt(track.BeginSeconds), y + 8, Math.Max(1, XAt(track.BeginSeconds + track.DurationSeconds) - XAt(track.BeginSeconds)), 14, _paint);
                for (var key = 0; key < track.Keys.Count; key++)
                {
                    var offset = row == _dragTrack && key == _dragKey && _dragOffset is { } dragging ? dragging : track.Keys[key].Offset;
                    var x = XAt(track.BeginSeconds + offset * track.DurationSeconds);
                    _paint.Color = row == SelectedTrack ? new SKColor(174, 160, 255) : new SKColor(133, 126, 184);
                    canvas.Save();
                    canvas.Translate(x, y + 15);
                    canvas.DrawPath(_diamond, _paint);
                    canvas.Restore();
                }
            }
            _paint.Color = new SKColor(245, 164, 109);
            _paint.StrokeWidth = 2;
            canvas.DrawLine(XAt(Time), 22, XAt(Time), (float)area.Height, _paint);
        }
        private static SKPath CreateDiamond()
        {
            using var builder = new SKPathBuilder();
            builder.MoveTo(0, -7);
            builder.LineTo(6, 0);
            builder.LineTo(0, 7);
            builder.LineTo(-6, 0);
            builder.Close();
            return builder.Detach();
        }
        public new void Dispose() { _disposed = true; _paint.Dispose(); _font.Dispose(); _diamond.Dispose(); base.Dispose(); }
    }
}
