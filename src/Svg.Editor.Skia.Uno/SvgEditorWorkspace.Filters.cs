using System.Text;
using System.Xml.Linq;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using SkiaSharp;
using Svg.Editor.Skia;
using Svg.Skia;
using Uno.WinUI.Graphics2DSK;
using Windows.Foundation;
using Windows.Storage;
using Windows.Storage.Pickers;

namespace Svg.Editor.Skia.Uno;

public sealed partial class SvgEditorWorkspace
{
    private async Task FilterStudioAsync()
    {
        if (Controller is not { } controller) return;
        var edit = controller.BeginFilterEdit();
        var selected = 0;
        var width = DialogContentWidth(760);
        var panel = new StackPanel { Width = width, Spacing = 12 };
        var status = PaintErrorText();
        var graphView = new Microsoft.UI.Xaml.Controls.Canvas { Width = Math.Max(240, width - 24) };
        var inspector = new StackPanel { Spacing = 10 };
        var fields = new List<(int Node, int Child, string Name, string Original, Func<string> Read)>();
        var undo = new Stack<string>();
        var redo = new Stack<string>();
        var historyBytes = 0;
        var loading = false;
        var closed = false;
        var live = new CheckBox { Content = "Live preview", IsChecked = true };
        var shared = new CheckBox { Content = "Edit the shared definition for all consumers", IsChecked = false, IsEnabled = edit.OriginalFilterId is not null };
        var bypass = new CheckBox { Content = "Reconnect consumers when removing a node", IsChecked = false };
        var primitive = new ComboBox { Header = "Add an effect", ItemsSource = SvgFilterCatalog.Primitives, SelectedIndex = 9, HorizontalAlignment = HorizontalAlignment.Stretch };
        var source = new TextBox { Header = "Filter XML · preserves custom attributes and extensions", AcceptsReturn = true, TextWrapping = TextWrapping.Wrap, MaxLength = SvgFilterGraph.MaximumCharacters, MinHeight = 100, MaxHeight = 230, Text = edit.Graph.GetXml() };
        using var preview = new FilterDocumentPreview { Height = 220 };
        panel.Children.Add(new TextBlock { Text = "Build an SVG effect graph. Select a node to edit its inputs and parameters. Changes stay in this draft until Apply.", TextWrapping = TextWrapping.Wrap, Opacity = 0.75 });
        panel.Children.Add(preview);
        panel.Children.Add(PaintColumns(live, shared));
        var tools = new StackPanel { Spacing = 8 };
        panel.Children.Add(tools);
        panel.Children.Add(new ScrollViewer { Content = graphView, MaxHeight = 330, HorizontalScrollBarVisibility = ScrollBarVisibility.Auto, VerticalScrollBarVisibility = ScrollBarVisibility.Auto });
        panel.Children.Add(bypass);
        panel.Children.Add(inspector);
        var sourcePanel = new StackPanel { Spacing = 8 };
        sourcePanel.Children.Add(source);
        var sourceButtons = new StackPanel { Spacing = 8 };
        sourcePanel.Children.Add(sourceButtons);
        panel.Children.Add(new Expander { Header = "Source and portable filter files", Content = sourcePanel, HorizontalAlignment = HorizontalAlignment.Stretch });
        panel.Children.Add(status);
        var dialog = MakeDialog("Filter Studio", panel);
        var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(250) };

        void Remember(string before)
        {
            undo.Push(before);
            historyBytes += before.Length * 2;
            if (undo.Count > 32 || historyBytes > 16 * 1024 * 1024)
            {
                var latest = undo.Peek(); undo.Clear(); undo.Push(latest); historyBytes = latest.Length * 2;
            }
            redo.Clear();
        }
        void CommitFields()
        {
            var changes = fields.Where(field => field.Read() != field.Original).ToArray();
            if (changes.Length == 0) return;
            var before = edit.Graph.GetXml();
            var draft = SvgFilterGraph.Parse(before);
            foreach (var field in changes) draft.SetAttribute(field.Node, field.Name, field.Read(), field.Child);
            if (draft.GetXml() != before) Remember(before);
            edit.Graph = draft;
            for (var i = 0; i < fields.Count; i++)
            {
                var field = fields[i];
                fields[i] = (field.Node, field.Child, field.Name, field.Read(), field.Read);
            }
        }
        void Schedule()
        {
            if (loading || closed) return;
            dialog.IsPrimaryButtonEnabled = false;
            timer.Stop(); timer.Start();
        }
        void RefreshPreview()
        {
            timer.Stop();
            if (closed) return;
            try
            {
                CommitFields();
                edit.Graph.ThrowIfInvalid();
                if (live.IsChecked == true) preview.Update(controller.CreateFilterPreviewRenderer(edit, shared.IsChecked == true));
                status.Text = live.IsChecked == true ? string.Empty : "Live preview paused. Apply still validates the complete graph.";
                dialog.IsPrimaryButtonEnabled = true;
                RebuildGraph();
            }
            catch (Exception exception)
            {
                status.Text = exception.Message;
                dialog.IsPrimaryButtonEnabled = false;
            }
        }
        void Change(Action action)
        {
            try
            {
                CommitFields();
                var before = edit.Graph.GetXml();
                action();
                if (edit.Graph.GetXml() != before) Remember(before);
                selected = Math.Clamp(selected, -1, edit.Graph.Nodes.Count - 1);
                RebuildInspector(); RebuildGraph(); Schedule();
            }
            catch (Exception exception) { status.Text = exception.Message; }
        }
        Button ActionButton(string label, Action action)
        {
            var button = new Button { Content = label };
            button.Click += (_, _) => Change(action);
            return button;
        }
        void RebuildGraph()
        {
            graphView.Children.Clear();
            var nodes = edit.Graph.Nodes;
            var edges = edit.Graph.Connections;
            graphView.Height = Math.Max(82, nodes.Count * 78 + 20);
            var wire = new SolidColorBrush(Windows.UI.Color.FromArgb(160, 130, 121, 205));
            foreach (var edge in edges.Where(edge => edge.Source >= 0))
            {
                var from = 44 + edge.Source * 78;
                var to = 44 + edge.Target * 78;
                var lane = 8 + edge.Source % 5 * 12;
                var figure = new PathFigure { StartPoint = new Point(84, from), IsClosed = false };
                figure.Segments.Add(new BezierSegment { Point1 = new Point(lane, from), Point2 = new Point(lane, to), Point3 = new Point(84, to) });
                var geometry = new PathGeometry(); geometry.Figures.Add(figure);
                graphView.Children.Add(new Microsoft.UI.Xaml.Shapes.Path { Data = geometry, Stroke = wire, StrokeThickness = 1.5, IsHitTestVisible = false });
            }
            for (var index = 0; index < nodes.Count; index++)
            {
                var captured = index;
                var node = nodes[index];
                var content = new StackPanel { Spacing = 3 };
                content.Children.Add(new TextBlock { Text = $"{index + 1}. {node.Name.LocalName}  →  {(string?)node.Attribute("result") ?? "(next)"}", FontWeight = FontWeights.SemiBold, TextTrimming = TextTrimming.CharacterEllipsis });
                content.Children.Add(new TextBlock { Text = string.Join(" · ", edges.Where(edge => edge.Target == captured).Select(edge => $"{edge.Port}: {edge.Value}")), FontSize = 11, Opacity = 0.7, TextTrimming = TextTrimming.CharacterEllipsis });
                var button = new Button { Content = content, Width = Math.Max(150, graphView.Width - 96), Height = 64, HorizontalContentAlignment = HorizontalAlignment.Stretch };
                if (index == selected) button.BorderBrush = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 124, 108, 255));
                button.BorderThickness = new Thickness(index == selected ? 2 : 1);
                Microsoft.UI.Xaml.Controls.Canvas.SetLeft(button, 84);
                Microsoft.UI.Xaml.Controls.Canvas.SetTop(button, 12 + index * 78);
                button.Click += (_, _) => Change(() => selected = captured);
                graphView.Children.Add(button);
            }
            if (nodes.Count == 0) graphView.Children.Add(new TextBlock { Text = "No primitives. Add an effect to begin.", Margin = new Thickness(12) });
        }
        void AddFields(XElement element, int nodeIndex, int childIndex = -1)
        {
            foreach (var name in SvgFilterCatalog.Attributes(element.Name.LocalName))
            {
                var original = (string?)element.Attribute(SvgFilterCatalog.AttributeName(name)) ?? string.Empty;
                var options = name is "in" or "in2"
                    ? SvgFilterCatalog.Sources.Concat(edit.Graph.Nodes.Take(Math.Max(0, nodeIndex)).Select(node => (string?)node.Attribute("result")).Where(value => !string.IsNullOrEmpty(value)).Select(value => value!)).Distinct().ToArray()
                    : SvgFilterCatalog.Choices(element.Name.LocalName, name).ToArray();
                if (options.Length > 0)
                {
                    var choices = new[] { string.Empty }.Concat(options).Concat(string.IsNullOrEmpty(original) ? Array.Empty<string>() : new[] { original }).Distinct().ToArray();
                    var combo = new ComboBox { Header = name + " · blank uses SVG default", ItemsSource = choices, SelectedItem = original, HorizontalAlignment = HorizontalAlignment.Stretch };
                    fields.Add((nodeIndex, childIndex, name, original, () => combo.SelectedItem as string ?? string.Empty));
                    combo.SelectionChanged += (_, _) => Schedule();
                    inspector.Children.Add(combo);
                }
                else
                {
                    var text = new TextBox { Header = name, Text = original, PlaceholderText = "SVG default", MaxLength = 65536 };
                    if (name is "kernelMatrix" or "values" or "tableValues") { text.AcceptsReturn = true; text.TextWrapping = TextWrapping.Wrap; text.MaxHeight = 140; }
                    fields.Add((nodeIndex, childIndex, name, original, () => text.Text));
                    text.TextChanged += (_, _) => Schedule();
                    inspector.Children.Add(text);
                }
            }
        }
        void RebuildInspector()
        {
            loading = true;
            try
            {
                fields.Clear(); inspector.Children.Clear();
                var root = edit.Graph.ToElement();
                inspector.Children.Add(new TextBlock { Text = "Filter region and color space", FontWeight = FontWeights.SemiBold });
                AddFields(root, -1);
                var nodes = edit.Graph.Nodes;
                if (selected < 0 || selected >= nodes.Count) return;
                var node = nodes[selected];
                inspector.Children.Add(new TextBlock { Text = $"{selected + 1}. {node.Name.LocalName}", FontSize = 18, Margin = new Thickness(0, 12, 0, 0) });
                AddFields(node, selected);
                var childNames = SvgFilterCatalog.Children(node.Name.LocalName);
                if (childNames.Count > 0)
                {
                    var choice = new ComboBox { Header = "Nested operation", ItemsSource = childNames, SelectedIndex = 0, HorizontalAlignment = HorizontalAlignment.Stretch };
                    inspector.Children.Add(PaintColumns(choice, ActionButton("Add / replace", () => edit.Graph.AddChild(selected, (string)choice.SelectedItem))));
                }
                var children = node.Elements().ToArray();
                for (var child = 0; child < children.Length; child++)
                {
                    var captured = child;
                    inspector.Children.Add(PaintColumns(new TextBlock { Text = children[child].Name.LocalName, FontWeight = FontWeights.SemiBold }, ActionButton("Remove child", () => edit.Graph.RemoveChild(selected, captured))));
                    AddFields(children[child], selected, child);
                }
            }
            finally { loading = false; }
        }
        tools.Children.Add(PaintColumns(primitive, ActionButton("Add", () => selected = edit.Graph.Add((string)primitive.SelectedItem))));
        tools.Children.Add(PaintColumns(
            ActionButton("Duplicate", () => selected = edit.Graph.Duplicate(selected)),
            ActionButton("Move up", () => { edit.Graph.Move(selected, selected - 1); selected--; }),
            ActionButton("Move down", () => { edit.Graph.Move(selected, selected + 1); selected++; }),
            ActionButton("Remove", () => edit.Graph.Remove(selected, bypass.IsChecked == true))));
        var undoButton = new Button { Content = "Undo draft" };
        var redoButton = new Button { Content = "Redo draft" };
        void NavigateHistory(bool forward)
        {
            try
            {
                CommitFields();
                var from = forward ? redo : undo;
                var to = forward ? undo : redo;
                if (from.Count == 0) return;
                to.Push(edit.Graph.GetXml());
                edit.Graph = SvgFilterGraph.Parse(from.Pop());
                selected = Math.Min(selected, edit.Graph.Nodes.Count - 1);
                RebuildInspector(); RebuildGraph(); Schedule();
            }
            catch (Exception exception) { status.Text = exception.Message; }
        }
        undoButton.Click += (_, _) => NavigateHistory(false);
        redoButton.Click += (_, _) => NavigateHistory(true);
        tools.Children.Add(PaintColumns(undoButton, redoButton));
        sourceButtons.Children.Add(PaintColumns(
            ActionButton("Refresh source", () => source.Text = edit.Graph.GetXml()),
            ActionButton("Load source into draft", () => { edit.Graph = SvgFilterGraph.Parse(source.Text); selected = 0; })));
        var import = new Button { Content = "Import filter…" };
        var export = new Button { Content = "Export filter…" };
        import.Click += async (_, _) =>
        {
            try
            {
                var picker = new FileOpenPicker(); picker.FileTypeFilter.Add(".svgfilter"); picker.FileTypeFilter.Add(".xml"); InitializePicker(picker);
                var file = await picker.PickSingleFileAsync(); if (file is null || closed) return;
                if ((await file.GetBasicPropertiesAsync()).Size > SvgFilterGraph.MaximumCharacters * 4UL) throw new InvalidOperationException("The filter file is too large.");
                var text = await FileIO.ReadTextAsync(file);
                if (!closed) Change(() => { edit.Graph = SvgFilterGraph.Parse(text); selected = 0; source.Text = text; });
            }
            catch (Exception exception) { status.Text = exception.Message; }
        };
        export.Click += async (_, _) =>
        {
            try
            {
                CommitFields(); edit.Graph.ThrowIfInvalid();
                var xml = edit.Graph.GetXml();
                var picker = new FileSavePicker { SuggestedFileName = "effect", DefaultFileExtension = ".svgfilter" };
                picker.FileTypeChoices.Add("SVG filter graph", new List<string> { ".svgfilter" }); InitializePicker(picker);
                var file = await picker.PickSaveFileAsync(); if (file is not null) await WriteFileAsync(file, Encoding.UTF8.GetBytes(xml));
            }
            catch (Exception exception) { status.Text = exception.Message; }
        };
        sourceButtons.Children.Add(PaintColumns(import, export));
        live.Checked += (_, _) => Schedule(); live.Unchecked += (_, _) => Schedule();
        shared.Checked += (_, _) => Schedule(); shared.Unchecked += (_, _) => Schedule();
        timer.Tick += (_, _) => RefreshPreview();
        dialog.PrimaryButtonClick += (_, args) =>
        {
            try { CommitFields(); controller.ApplyFilterEdit(edit, shared.IsChecked == true); }
            catch (Exception exception) { args.Cancel = true; status.Text = exception.Message; }
        };
        RebuildInspector(); RebuildGraph();
        dialog.Opened += (_, _) => RefreshPreview();
        try { await ShowDialogAsync(dialog); }
        finally { closed = true; timer.Stop(); }
    }

    private sealed class FilterDocumentPreview : SKCanvasElement, IDisposable
    {
        private SKSvg? _renderer;
        public void Update(SKSvg renderer)
        {
            var previous = _renderer; _renderer = renderer; previous?.Dispose(); Invalidate();
        }
        protected override void RenderOverride(SKCanvas canvas, Size area)
        {
            using var restore = new SKAutoCanvasRestore(canvas);
            canvas.ClipRect(SKRect.Create((float)area.Width, (float)area.Height));
            using var checker = new SKPaint { Color = new SKColor(145, 150, 160, 35) };
            for (var y = 0; y < area.Height; y += 16)
                for (var x = 0; x < area.Width; x += 16)
                    if (((x / 16 + y / 16) & 1) == 0) canvas.DrawRect(x, y, 16, 16, checker);
            if (_renderer?.Picture is not { } picture || area.Width <= 24 || area.Height <= 24) return;
            var bounds = picture.CullRect;
            var scale = Math.Min((area.Width - 24) / Math.Max(1, bounds.Width), (area.Height - 24) / Math.Max(1, bounds.Height));
            canvas.Translate((float)area.Width / 2, (float)area.Height / 2);
            canvas.Scale((float)scale);
            canvas.Translate(-bounds.MidX, -bounds.MidY);
            _renderer.Draw(canvas);
        }
        public new void Dispose() { _renderer?.Dispose(); _renderer = null; base.Dispose(); }
    }
}
