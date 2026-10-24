using System.Globalization;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using SkiaSharp;
using Svg.Editor.Skia;
using Uno.WinUI.Graphics2DSK;
using Windows.Foundation;

namespace Svg.Editor.Skia.Uno;

public sealed partial class SvgEditorWorkspace
{
    private async Task GradientMeshDialogAsync()
    {
        if (Controller is not { } controller || controller.Document is not { } document)
            return;
        SvgGroup? existing = null;
        if (controller.SelectedElements.Count == 1)
            for (SvgElement? ancestor = controller.SelectedElements[0]; ancestor is not null; ancestor = ancestor.Parent)
                if (ancestor is SvgGroup group && group.CustomAttributes.ContainsKey(SvgGradientMeshService.MeshAttribute))
                { existing = group; break; }
        if (existing is not null && SvgEditorController.IsEditLocked(existing))
            throw new InvalidOperationException("Unlock this mesh or its layer before editing it.");
        var viewBox = document.ViewBox;
        var bounds = viewBox.Width > 0 && viewBox.Height > 0
            ? SKRect.Create(viewBox.MinX + viewBox.Width * 0.2f, viewBox.MinY + viewBox.Height * 0.2f, viewBox.Width * 0.6f, viewBox.Height * 0.6f)
            : SKRect.Create(100, 100, 400, 300);
        var mesh = existing is null ? SvgGradientMeshService.CreateGrid(bounds) : SvgGradientMeshService.GetMesh(existing);
        var rows = NumberInput("Rows of nodes", mesh.Rows, 2, 16);
        var columns = NumberInput("Columns of nodes", mesh.Columns, 2, 16);
        var quality = NumberInput("Samples per patch edge", mesh.Subdivisions, 2, 128);
        var reset = new Button { Content = "Rebuild grid", VerticalAlignment = VerticalAlignment.Bottom };
        var point = new ComboBox { Header = "Mesh point", HorizontalAlignment = HorizontalAlignment.Stretch };
        var x = NumberInput("X", 0, -10000000, 10000000);
        var y = NumberInput("Y", 0, -10000000, 10000000);
        var ux = NumberInput("Horizontal tangent X", 0, -10000000, 10000000);
        var uy = NumberInput("Horizontal tangent Y", 0, -10000000, 10000000);
        var vx = NumberInput("Vertical tangent X", 0, -10000000, 10000000);
        var vy = NumberInput("Vertical tangent Y", 0, -10000000, 10000000);
        var color = new TextBox { Header = "Point color · #RRGGBB", MaxLength = 7 };
        var opacity = NumberInput("Opacity %", 100, 0, 100);
        var error = PaintErrorText();
        using var preview = new MeshPreview { Height = 270 };
        var panel = new StackPanel { Spacing = 12, MaxWidth = DialogContentWidth(640), Width = DialogContentWidth(640) };
        panel.Children.Add(new TextBlock
        {
            Text = "Drag a point or its tangent handles. Each patch follows cubic boundaries and blends its corner colors. The SVG stores portable vector polygons and an editable control network.",
            TextWrapping = TextWrapping.Wrap,
            Opacity = 0.72
        });
        panel.Children.Add(preview);
        panel.Children.Add(PaintColumns(rows, columns, reset));
        panel.Children.Add(PaintColumns(point, quality));
        panel.Children.Add(PaintColumns(x, y, color, opacity));
        panel.Children.Add(PaintColumns(ux, uy));
        panel.Children.Add(PaintColumns(vx, vy));
        panel.Children.Add(new TextBlock { Text = "More samples improve smoothness and increase SVG size. Rebuilding the grid replaces its point edits. Tangents are derivatives in SVG units; the visible handles are one third of their length.", TextWrapping = TextWrapping.Wrap, FontSize = 12, Opacity = 0.65 });
        panel.Children.Add(error);
        var dialog = MakeDialog(existing is null ? "Create gradient mesh" : "Edit gradient mesh", new ScrollViewer { Content = panel, MaxHeight = 650 }, existing is null ? "Create mesh" : "Update mesh");
        var loading = false;

        void LoadPoint()
        {
            if (point.SelectedIndex < 0 || point.SelectedIndex >= mesh.Nodes.Count)
                return;
            loading = true;
            var node = mesh.Nodes[point.SelectedIndex];
            x.Value = node.Position.X;
            y.Value = node.Position.Y;
            ux.Value = node.Horizontal.X;
            uy.Value = node.Horizontal.Y;
            vx.Value = node.Vertical.X;
            vy.Value = node.Vertical.Y;
            color.Text = $"#{node.Color.Red:X2}{node.Color.Green:X2}{node.Color.Blue:X2}";
            opacity.Value = node.Color.Alpha / 255d * 100;
            preview.SelectedIndex = point.SelectedIndex;
            preview.Invalidate();
            loading = false;
        }
        void Refresh(bool updateModel = true)
        {
            if (loading)
                return;
            try
            {
                if (quality.Value != Math.Floor(quality.Value))
                    throw new FormatException("The mesh sample count must be a whole number.");
                mesh.Subdivisions = (int)quality.Value;
                if (updateModel && point.SelectedIndex >= 0)
                {
                    var node = mesh.Nodes[point.SelectedIndex];
                    if (!uint.TryParse(color.Text.TrimStart('#'), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var rgb) || color.Text.Length != 7 || color.Text[0] != '#')
                        throw new FormatException("Enter the point color as #RRGGBB.");
                    if (!double.IsFinite(opacity.Value) || opacity.Value is < 0 or > 100)
                        throw new FormatException("Point opacity must be between 0 and 100%.");
                    node.Position = new SKPoint((float)x.Value, (float)y.Value);
                    node.Horizontal = new SKPoint((float)ux.Value, (float)uy.Value);
                    node.Vertical = new SKPoint((float)vx.Value, (float)vy.Value);
                    node.Color = new SKColor((byte)(rgb >> 16), (byte)(rgb >> 8), (byte)rgb, (byte)Math.Round(opacity.Value * 2.55));
                }
                preview.Update(mesh);
                error.Text = string.Empty;
                dialog.IsPrimaryButtonEnabled = true;
            }
            catch (Exception exception) { error.Text = exception.Message; dialog.IsPrimaryButtonEnabled = false; }
        }
        void RebuildPointList()
        {
            loading = true;
            point.Items.Clear();
            for (var row = 0; row < mesh.Rows; row++)
                for (var column = 0; column < mesh.Columns; column++)
                    point.Items.Add($"Row {row + 1} · Column {column + 1}");
            point.SelectedIndex = 0;
            loading = false;
            LoadPoint();
            Refresh(false);
        }
        reset.Click += (_, _) =>
        {
            try
            {
                if (rows.Value != Math.Floor(rows.Value) || columns.Value != Math.Floor(columns.Value))
                    throw new FormatException("Row and column counts must be whole numbers.");
                var positions = mesh.Nodes.Select(node => node.Position).ToArray();
                var rectangle = SKRect.Create(positions.Min(p => p.X), positions.Min(p => p.Y),
                    Math.Max(1, positions.Max(p => p.X) - positions.Min(p => p.X)), Math.Max(1, positions.Max(p => p.Y) - positions.Min(p => p.Y)));
                mesh = SvgGradientMeshService.CreateGrid(rectangle, (int)rows.Value, (int)columns.Value);
                loading = true;
                quality.Value = mesh.Subdivisions;
                loading = false;
                RebuildPointList();
            }
            catch (Exception exception) { error.Text = exception.Message; }
        };
        point.SelectionChanged += (_, _) => { if (!loading) LoadPoint(); };
        foreach (var number in new[] { x, y, ux, uy, vx, vy, opacity, quality })
            number.ValueChanged += (_, _) => Refresh();
        color.TextChanged += (_, _) => Refresh();
        preview.PointChanged += (_, _) =>
        {
            loading = true;
            point.SelectedIndex = preview.SelectedIndex;
            loading = false;
            LoadPoint();
        };
        preview.Failed += message => { error.Text = message; dialog.IsPrimaryButtonEnabled = false; };
        dialog.PrimaryButtonClick += (_, args) =>
        {
            try
            {
                var target = existing ?? new SvgGroup { ID = UniqueId("mesh") };
                controller.Execute(existing is null ? "Create gradient mesh" : "Edit gradient mesh", () =>
                {
                    SvgGradientMeshService.Apply(target, mesh);
                    if (existing is null)
                        document.Children.Add(target);
                    controller.Select(target);
                });
            }
            catch (Exception exception) { args.Cancel = true; error.Text = exception.Message; }
        };
        RebuildPointList();
        await ShowDialogAsync(dialog);
    }

    private sealed class MeshPreview : SKCanvasElement, IDisposable
    {
        private SvgGradientMesh? _mesh;
        private SKPicture? _picture;
        private SKRect _bounds;
        private SKMatrix _view = SKMatrix.Identity;
        private bool _dragging;
        private int _handle;
        public int SelectedIndex { get; set; }
        public event EventHandler? PointChanged;
        public event Action<string>? Failed;

        public MeshPreview()
        {
            PointerPressed += (_, args) =>
            {
                if (_mesh is null || !args.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
                    return;
                var pointer = args.GetCurrentPoint(this).Position;
                var position = new SKPoint((float)pointer.X, (float)pointer.Y);
                var closest = 14f;
                var index = -1;
                var handle = 0;
                for (var i = 0; i < _mesh.Nodes.Count; i++)
                {
                    var distance = SKPoint.Distance(position, _view.MapPoint(_mesh.Nodes[i].Position));
                    if (distance < closest)
                    { closest = distance; index = i; }
                }
                if (SelectedIndex >= 0 && SelectedIndex < _mesh.Nodes.Count)
                {
                    var node = _mesh.Nodes[SelectedIndex];
                    foreach (var item in new[] { (node.Horizontal, 1), (node.Vertical, 2) })
                    {
                        var p = new SKPoint(node.Position.X + item.Item1.X / 3, node.Position.Y + item.Item1.Y / 3);
                        var distance = SKPoint.Distance(position, _view.MapPoint(p));
                        if (distance < closest)
                        { closest = distance; index = SelectedIndex; handle = item.Item2; }
                    }
                }
                if (index < 0)
                    return;
                SelectedIndex = index;
                _handle = handle;
                _dragging = CapturePointer(args.Pointer);
                args.Handled = true;
                PointChanged?.Invoke(this, EventArgs.Empty);
                Invalidate();
            };
            PointerMoved += (_, args) =>
            {
                if (!_dragging || _mesh is null || !_view.TryInvert(out var inverse))
                    return;
                var pointer = args.GetCurrentPoint(this).Position;
                var position = inverse.MapPoint(new SKPoint((float)pointer.X, (float)pointer.Y));
                var node = _mesh.Nodes[SelectedIndex];
                if (_handle == 0)
                    node.Position = Clamp(position);
                else
                {
                    var tangent = new SKPoint((position.X - node.Position.X) * 3, (position.Y - node.Position.Y) * 3);
                    if (_handle == 1)
                        node.Horizontal = Clamp(tangent);
                    else
                        node.Vertical = Clamp(tangent);
                }
                TryRebuild(true);
                PointChanged?.Invoke(this, EventArgs.Empty);
                args.Handled = true;
            };
            PointerReleased += (_, args) => { if (!_dragging) return; _dragging = false; ReleasePointerCaptures(); TryRebuild(false); args.Handled = true; };
            PointerCaptureLost += (_, _) => { if (!_dragging) return; _dragging = false; TryRebuild(false); };
        }

        private static SKPoint Clamp(SKPoint point) => new(Math.Clamp(point.X, -10000000, 10000000), Math.Clamp(point.Y, -10000000, 10000000));
        private void TryRebuild(bool interactive)
        {
            try
            { RebuildPicture(interactive); }
            catch (Exception exception) { Failed?.Invoke(exception.Message); }
        }

        public void Update(SvgGradientMesh mesh) { _mesh = mesh; RebuildPicture(false); }

        private void RebuildPicture(bool interactive)
        {
            if (_mesh is null)
                return;
            var savedQuality = _mesh.Subdivisions;
            IReadOnlyList<SvgMeshTriangle> triangles;
            try
            {
                if (interactive)
                    _mesh.Subdivisions = Math.Min(8, savedQuality);
                triangles = SvgGradientMeshService.Tessellate(_mesh);
            }
            finally { _mesh.Subdivisions = savedQuality; }
            if (!_dragging)
            {
                var positions = triangles.SelectMany(t => new[] { t.A, t.B, t.C }).ToArray();
                _bounds = new SKRect(positions.Min(p => p.X), positions.Min(p => p.Y), positions.Max(p => p.X), positions.Max(p => p.Y));
            }
            using var recorder = new SKPictureRecorder();
            var canvas = recorder.BeginRecording(SKRect.Create(-10000000, -10000000, 20000000, 20000000));
            using var paint = new SKPaint { IsAntialias = false };
            foreach (var triangle in triangles)
            {
                using var builder = new SKPathBuilder();
                builder.MoveTo(triangle.A);
                builder.LineTo(triangle.B);
                builder.LineTo(triangle.C);
                builder.Close();
                using var path = builder.Detach();
                paint.Color = triangle.Color;
                canvas.DrawPath(path, paint);
            }
            var picture = recorder.EndRecording();
            _picture?.Dispose();
            _picture = picture;
            Invalidate();
        }

        protected override void RenderOverride(SKCanvas canvas, Size area)
        {
            if (_picture is null || _mesh is null || area.Width < 40 || area.Height < 40)
                return;
            var scale = (float)Math.Min((area.Width - 64) / Math.Max(1, _bounds.Width), (area.Height - 64) / Math.Max(1, _bounds.Height));
            _view = new SKMatrix(scale, 0, (float)area.Width / 2 - _bounds.MidX * scale,
                0, scale, (float)area.Height / 2 - _bounds.MidY * scale, 0, 0, 1);
            using (new SKAutoCanvasRestore(canvas))
            { canvas.Concat(_view); canvas.DrawPicture(_picture); }
            using var stroke = new SKPaint { IsAntialias = true, Style = SKPaintStyle.Stroke, Color = new SKColor(100, 110, 230), StrokeWidth = 1.5f };
            using var fill = new SKPaint { IsAntialias = true, Color = SKColors.White };
            for (var i = 0; i < _mesh.Nodes.Count; i++)
            {
                var node = _mesh.Nodes[i];
                var position = _view.MapPoint(node.Position);
                if (i == SelectedIndex)
                    foreach (var tangent in new[] { node.Horizontal, node.Vertical })
                    {
                        var handle = _view.MapPoint(new SKPoint(node.Position.X + tangent.X / 3, node.Position.Y + tangent.Y / 3));
                        canvas.DrawLine(position, handle, stroke);
                        canvas.DrawCircle(handle, 4, fill);
                        canvas.DrawCircle(handle, 4, stroke);
                    }
                var radius = i == SelectedIndex ? 6 : 4;
                canvas.DrawCircle(position, radius, fill);
                canvas.DrawCircle(position, radius, stroke);
            }
        }
        public new void Dispose() { _picture?.Dispose(); base.Dispose(); }
    }
}
