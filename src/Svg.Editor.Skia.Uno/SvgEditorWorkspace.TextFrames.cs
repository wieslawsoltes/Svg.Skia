using System.Globalization;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using SkiaSharp;
using Svg.Editor.Skia;
using Svg.Skia;
using Uno.WinUI.Graphics2DSK;
using Windows.Foundation;

namespace Svg.Editor.Skia.Uno;

public sealed partial class SvgEditorWorkspace
{
    private async Task TextFramesDialogAsync()
    {
        if (Controller is not { Document: not null } controller)
            throw new InvalidOperationException("Open a document before creating paragraph frames.");
        var existing = SvgTextStoryService.FindStory(controller.SelectedElements.FirstOrDefault());
        if (existing is not null && SvgEditorController.IsEditLocked(existing))
            throw new InvalidOperationException("Unlock the story before editing its text or frames.");
        var story = existing is null ? new SvgTextStory { Text = "Your story starts here.\n\nCreate linked frames to continue the text across your composition." }
            : SvgTextStoryService.Read(existing);
        if (existing is null)
            story.Frames.Add(SvgTextStoryService.CreateFrameFromSelection(controller));
        var detached = new List<SvgTextStory>();
        var loading = true;
        var selectedFrame = 0;
        var text = new TextBox
        {
            Header = "Story · select text to format a range",
            Text = story.Text,
            AcceptsReturn = true,
            TextWrapping = TextWrapping.Wrap,
            MinHeight = 140,
            MaxHeight = 240,
            MaxLength = SvgTextLayoutService.MaximumCharacters
        };
        var family = new TextBox { Header = "Font family", Text = story.DefaultStyle.FontFamily };
        var size = NumberInput("Size", story.DefaultStyle.FontSize, 1, 10000);
        var weight = new ComboBox { Header = "Weight", HorizontalAlignment = HorizontalAlignment.Stretch };
        foreach (var value in Enumerable.Range(1, 9).Select(value => value * 100))
            weight.Items.Add(value);
        weight.SelectedIndex = Math.Clamp(story.DefaultStyle.FontWeight / 100 - 1, 0, 8);
        var italic = new CheckBox { Content = "Italic", IsChecked = story.DefaultStyle.Italic, VerticalAlignment = VerticalAlignment.Bottom };
        var color = new TextBox { Header = "Text paint", Text = story.DefaultStyle.Fill };
        var tracking = NumberInput("Tracking", story.DefaultStyle.LetterSpacing, 0, 10000);
        var features = new TextBox { Header = "OpenType features", Text = story.DefaultStyle.FontFeatureSettings, PlaceholderText = "normal or 'liga' 0, 'kern' 1" };
        var formatSelection = new Button { Content = "Apply typography to selected text" };
        var formatDefault = new Button { Content = "Set default typography" };
        var alignment = new ComboBox { Header = "Alignment", HorizontalAlignment = HorizontalAlignment.Stretch };
        foreach (var name in new[] { "Start", "Center", "End", "Justify" })
            alignment.Items.Add(name);
        alignment.SelectedIndex = (int)story.Paragraph.Alignment;
        var rtl = new CheckBox { Content = "Right to left", IsChecked = story.Paragraph.RightToLeft, VerticalAlignment = VerticalAlignment.Bottom };
        var lineHeight = NumberInput("Line height ×", story.Paragraph.LineHeight, 0.5, 10);
        var before = NumberInput("Space before", story.Paragraph.SpaceBefore, 0, 100000);
        var after = NumberInput("Space after", story.Paragraph.SpaceAfter, 0, 100000);
        var first = NumberInput("First line indent", story.Paragraph.FirstLineIndent, -100000, 100000);
        var left = NumberInput("Left indent", story.Paragraph.LeftIndent, 0, 100000);
        var right = NumberInput("Right indent", story.Paragraph.RightIndent, 0, 100000);
        var formatParagraph = new Button { Content = "Apply to current paragraph" };
        var resetParagraphs = new Button { Content = "Use these settings for all paragraphs" };
        var frames = new ListView { Height = 112, SelectionMode = ListViewSelectionMode.Single };
        var frameName = new TextBox { Header = "Frame name" };
        var x = NumberInput("X", 0, -10000000, 10000000);
        var y = NumberInput("Y", 0, -10000000, 10000000);
        var width = NumberInput("Width", 420, 1, 1000000);
        var height = NumberInput("Height", 300, 1, 1000000);
        var padding = NumberInput("Inset", 12, 0, 100000);
        var columns = NumberInput("Columns", 1, 1, 16);
        var gap = NumberInput("Column gap", 20, 0, 100000);
        var add = new Button { Content = "+ Link frame" };
        var remove = new Button { Content = "Remove frame" };
        var up = new Button { Content = "↑" };
        var down = new Button { Content = "↓" };
        ToolTipService.SetToolTip(up, "Move frame earlier in the text flow");
        ToolTipService.SetToolTip(down, "Move frame later in the text flow");
        var unlink = new Button { Content = "Split chain before selected frame" };
        var status = new TextBlock { TextWrapping = TextWrapping.Wrap, FontSize = 12, Opacity = 0.8 };
        var error = PaintErrorText();
        using var preview = new TextStoryPreview { Height = 180 };
        var compact = (XamlRoot?.Size.Width ?? 1000) < 760;
        UIElement TextColumns(params UIElement[] controls)
        {
            if (!compact)
                return PaintColumns(controls);
            var stack = new StackPanel { Spacing = 10 };
            foreach (var control in controls)
                stack.Children.Add(control);
            return stack;
        }
        var panel = new StackPanel { Spacing = 12, MinWidth = Math.Min(450, Math.Max(220, (XamlRoot?.Size.Width ?? 1000) - 80)), MaxWidth = 720 };
        panel.Children.Add(new TextBlock
        {
            Text = "Text flows through frames in list order. Your SVG contains editable text; the complete story remains available even when frames overflow.",
            TextWrapping = TextWrapping.Wrap,
            Opacity = 0.72
        });
        panel.Children.Add(preview);
        panel.Children.Add(status);
        panel.Children.Add(text);
        panel.Children.Add(TextColumns(family, size, weight, italic));
        panel.Children.Add(TextColumns(color, tracking));
        panel.Children.Add(features);
        panel.Children.Add(TextColumns(formatSelection, formatDefault));
        panel.Children.Add(new TextBlock { Text = "Paragraph settings · apply to current or all", FontWeight = Microsoft.UI.Text.FontWeights.SemiBold });
        panel.Children.Add(TextColumns(alignment, lineHeight, rtl));
        panel.Children.Add(TextColumns(before, after, first));
        panel.Children.Add(TextColumns(left, right));
        panel.Children.Add(TextColumns(formatParagraph, resetParagraphs));
        panel.Children.Add(new TextBlock { Text = "Linked frames · flow order", FontWeight = Microsoft.UI.Text.FontWeights.SemiBold });
        panel.Children.Add(frames);
        panel.Children.Add(TextColumns(add, remove, up, down));
        panel.Children.Add(unlink);
        panel.Children.Add(frameName);
        panel.Children.Add(TextColumns(x, y, width, height));
        panel.Children.Add(TextColumns(padding, columns, gap));
        panel.Children.Add(error);
        var dialog = MakeDialog(existing is null ? "Create paragraph frames" : "Edit paragraph frames",
            new ScrollViewer { Content = panel, MaxHeight = Math.Clamp((XamlRoot?.Size.Height ?? 830) - 180, 240, 650) }, existing is null ? "Create story" : "Apply story");
        var timer = DispatcherQueue.CreateTimer();
        timer.Interval = TimeSpan.FromMilliseconds(180);
        timer.IsRepeating = false;

        int StoryOffset(int offset) => text.Text.Substring(0, Math.Clamp(offset, 0, text.Text.Length))
            .Replace("\r\n", "\n").Replace('\r', '\n').Length;

        SvgStoryTextStyle CurrentStyle() => new()
        {
            FontFamily = family.Text.Trim(),
            FontSize = (float)size.Value,
            FontWeight = (weight.SelectedIndex + 1) * 100,
            Italic = italic.IsChecked == true,
            Fill = color.Text.Trim(),
            LetterSpacing = (float)tracking.Value,
            FontFeatureSettings = features.Text.Trim()
        };

        SvgParagraphFormat CurrentParagraph() => new()
        {
            Alignment = (SvgParagraphAlignment)Math.Max(0, alignment.SelectedIndex),
            LineHeight = (float)lineHeight.Value,
            SpaceBefore = (float)before.Value,
            SpaceAfter = (float)after.Value,
            FirstLineIndent = (float)first.Value,
            LeftIndent = (float)left.Value,
            RightIndent = (float)right.Value,
            RightToLeft = rtl.IsChecked == true
        };

        void SaveFrame()
        {
            var frame = story.Frames[selectedFrame];
            frame.Name = frameName.Text;
            frame.X = (float)x.Value;
            frame.Y = (float)y.Value;
            frame.Width = (float)width.Value;
            frame.Height = (float)height.Value;
            frame.Padding = (float)padding.Value;
            frame.Columns = double.IsFinite(columns.Value) ? (int)Math.Round(columns.Value) : 0;
            frame.ColumnGap = (float)gap.Value;
        }

        bool UpdatePreview()
        {
            if (loading)
                return false;
            timer.Stop();
            try
            {
                SaveFrame();
                SvgTextStoryService.ReplaceText(story, text.Text);
                var layout = SvgTextLayoutService.Layout(story, controller.Renderer.AssetLoader);
                preview.Update(story, layout, selectedFrame, controller.Document!);
                var overset = layout.Lines.Count(line => line.Width > line.AvailableWidth + 0.001f);
                status.Text = $"{story.Frames.Count} frame(s) · {layout.Lines.Count} lines · {story.Styles.Count} styled range(s)"
                    + (layout.HasOverflow ? $" · OVERFLOW: {layout.OverflowLength:N0} characters remain" : " · All text placed")
                    + (overset > 0 ? $" · {overset} line(s) contain a character wider than the frame" : string.Empty)
                    + (detached.Count > 0 ? $" · {detached.Count} independent chain(s) will be created" : string.Empty);
                error.Text = string.Empty;
                dialog.IsPrimaryButtonEnabled = true;
                return true;
            }
            catch (Exception exception)
            {
                error.Text = exception.Message;
                dialog.IsPrimaryButtonEnabled = false;
                return false;
            }
        }

        void Schedule()
        {
            if (loading)
                return;
            timer.Stop();
            timer.Start();
        }

        void ShowFrame(int index)
        {
            loading = true;
            selectedFrame = Math.Clamp(index, 0, story.Frames.Count - 1);
            var frame = story.Frames[selectedFrame];
            frames.ItemsSource = story.Frames.Select((item, order) => $"{order + 1}. {item.Name} · {item.Width:0.#} × {item.Height:0.#} · {item.Columns} column(s)").ToArray();
            frames.SelectedIndex = selectedFrame;
            frameName.Text = frame.Name;
            x.Value = frame.X;
            y.Value = frame.Y;
            width.Value = frame.Width;
            height.Value = frame.Height;
            padding.Value = frame.Padding;
            columns.Value = frame.Columns;
            gap.Value = frame.ColumnGap;
            remove.IsEnabled = story.Frames.Count > 1;
            up.IsEnabled = selectedFrame > 0;
            down.IsEnabled = selectedFrame < story.Frames.Count - 1;
            unlink.IsEnabled = selectedFrame > 0;
            add.IsEnabled = story.Frames.Count < SvgTextLayoutService.MaximumFrames;
            loading = false;
        }

        timer.Tick += (_, _) => UpdatePreview();
        text.TextChanged += (_, _) => Schedule();
        foreach (var box in new[] { family, color, features, frameName })
            box.TextChanged += (_, _) => Schedule();
        foreach (var box in new[] { size, tracking, lineHeight, before, after, first, left, right, x, y, width, height, padding, columns, gap })
            box.ValueChanged += (_, _) => Schedule();
        weight.SelectionChanged += (_, _) => Schedule();
        alignment.SelectionChanged += (_, _) => Schedule();
        italic.Checked += (_, _) => Schedule();
        italic.Unchecked += (_, _) => Schedule();
        rtl.Checked += (_, _) => Schedule();
        rtl.Unchecked += (_, _) => Schedule();
        frames.SelectionChanged += (_, _) =>
        {
            if (loading || frames.SelectedIndex < 0)
                return;
            var index = frames.SelectedIndex;
            SaveFrame();
            ShowFrame(index);
            UpdatePreview();
        };
        add.Click += (_, _) =>
        {
            if (!UpdatePreview())
                return;
            var frame = story.Frames[selectedFrame].Clone();
            frame.Key = Guid.NewGuid().ToString("N");
            frame.Name = "Text frame " + (story.Frames.Count + 1);
            frame.X += frame.Width + 24;
            story.Frames.Insert(selectedFrame + 1, frame);
            ShowFrame(selectedFrame + 1);
            UpdatePreview();
        };
        remove.Click += (_, _) =>
        {
            if (story.Frames.Count <= 1 || !UpdatePreview())
                return;
            story.Frames.RemoveAt(selectedFrame);
            ShowFrame(Math.Min(selectedFrame, story.Frames.Count - 1));
            UpdatePreview();
        };
        void MoveFrame(int direction)
        {
            if (!UpdatePreview())
                return;
            var next = selectedFrame + direction;
            if (next < 0 || next >= story.Frames.Count)
                return;
            var frame = story.Frames[selectedFrame];
            story.Frames.RemoveAt(selectedFrame);
            story.Frames.Insert(next, frame);
            ShowFrame(next);
            UpdatePreview();
        }
        up.Click += (_, _) => MoveFrame(-1);
        down.Click += (_, _) => MoveFrame(1);
        unlink.Click += (_, _) =>
        {
            if (!UpdatePreview() || selectedFrame == 0)
                return;
            var split = SvgTextStoryService.SplitAtFrame(story, selectedFrame, controller.Renderer.AssetLoader);
            detached.Add(split.After);
            story = split.Before;
            loading = true;
            text.Text = story.Text;
            loading = false;
            ShowFrame(story.Frames.Count - 1);
            UpdatePreview();
        };
        formatSelection.Click += (_, _) =>
        {
            try
            {
                if (!UpdatePreview())
                    return;
                if (text.SelectionLength <= 0)
                    throw new InvalidOperationException("Select a range in the story text first.");
                var start = StoryOffset(text.SelectionStart);
                var end = StoryOffset(text.SelectionStart + text.SelectionLength);
                SvgTextStoryService.ApplyStyle(story, start, end - start, CurrentStyle());
                UpdatePreview();
            }
            catch (Exception exception) { error.Text = exception.Message; }
        };
        formatParagraph.Click += (_, _) =>
        {
            if (!UpdatePreview())
                return;
            var paragraphIndex = story.Text.Substring(0, StoryOffset(text.SelectionStart)).Count(character => character == '\n');
            story.ParagraphOverrides[paragraphIndex] = CurrentParagraph();
            UpdatePreview();
        };
        formatDefault.Click += (_, _) => { story.DefaultStyle = CurrentStyle(); UpdatePreview(); };
        resetParagraphs.Click += (_, _) => { story.Paragraph = CurrentParagraph(); story.ParagraphOverrides.Clear(); UpdatePreview(); };
        dialog.PrimaryButtonClick += (_, args) => { if (!UpdatePreview()) args.Cancel = true; };
        ShowFrame(0);
        UpdatePreview();
        try
        {
            if (await ShowDialogAsync(dialog) != ContentDialogResult.Primary)
                return;
            if (!ReferenceEquals(Controller, controller))
                throw new InvalidOperationException("The active document changed while editing the story.");
            SvgTextStoryService.ApplyIndependentStories(controller, new[] { story }.Concat(detached).ToArray(), existing);
        }
        finally { timer.Stop(); }
    }

    private sealed class TextStoryPreview : SKCanvasElement, IDisposable
    {
        private SKSvg? _renderer;
        private SvgTextFrameDefinition[] _frames = Array.Empty<SvgTextFrameDefinition>();
        private int _selected;

        public void Update(SvgTextStory story, SvgTextLayoutResult layout, int selected, SvgDocument source)
        {
            var document = new SvgDocument { Width = 1000000, Height = 1000000, Overflow = SvgOverflow.Visible };
            foreach (var definitions in source.Children.OfType<SvgDefinitionList>())
                document.Children.Add(definitions.DeepCopy());
            document.Children.Add(SvgTextStoryService.CreateGroup(story, layout));
            var next = new SKSvg();
            try
            { next.FromSvgDocument(document); }
            catch { next.Dispose(); throw; }
            _renderer?.Dispose();
            _renderer = next;
            _frames = story.Frames.Select(frame => frame.Clone()).ToArray();
            _selected = selected;
            Invalidate();
        }

        protected override void RenderOverride(SKCanvas canvas, Size area)
        {
            if (_renderer?.Picture is not { } picture || _frames.Length == 0 || area.Width <= 24 || area.Height <= 24)
                return;
            var bounds = new SKRect(_frames.Min(frame => frame.X), _frames.Min(frame => frame.Y),
                _frames.Max(frame => frame.X + frame.Width), _frames.Max(frame => frame.Y + frame.Height));
            var scale = Math.Min((area.Width - 24) / bounds.Width, (area.Height - 24) / bounds.Height);
            using var restore = new SKAutoCanvasRestore(canvas);
            canvas.ClipRect(SKRect.Create((float)area.Width, (float)area.Height));
            canvas.Translate((float)area.Width / 2, (float)area.Height / 2);
            canvas.Scale((float)scale);
            canvas.Translate(-bounds.MidX, -bounds.MidY);
            using var paint = new SKPaint { IsAntialias = true, Color = SKColors.White };
            for (var index = 0; index < _frames.Length; index++)
            {
                var frame = _frames[index];
                var rectangle = SKRect.Create(frame.X, frame.Y, frame.Width, frame.Height);
                paint.Style = SKPaintStyle.Fill;
                paint.Color = SKColors.White;
                canvas.DrawRect(rectangle, paint);
                paint.Style = SKPaintStyle.Stroke;
                paint.StrokeWidth = (float)(1.5 / scale);
                paint.Color = index == _selected ? new SKColor(120, 100, 230) : new SKColor(145, 150, 170);
                canvas.DrawRect(rectangle, paint);
            }
            canvas.DrawPicture(picture);
        }

        public new void Dispose()
        {
            _renderer?.Dispose();
            base.Dispose();
        }
    }
}
