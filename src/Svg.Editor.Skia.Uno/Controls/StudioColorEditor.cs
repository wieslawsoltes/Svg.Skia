using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using SkiaSharp;
using Svg.Editor.Skia;

namespace Svg.Editor.Skia.Uno;

/// <summary>Retained solid-paint mixer with explicit application and safe nonmodal draft ownership.</summary>
public sealed class StudioColorEditor : UserControl, IDisposable
{
    private readonly StudioChoiceStrip _target = new() { ItemsSource = new[] { "Fill", "Stroke" }, SelectedIndex = 0, HorizontalAlignment = HorizontalAlignment.Stretch };
    private readonly ColorPicker _picker = new()
    {
        IsAlphaEnabled = true, IsAlphaSliderVisible = true, IsAlphaTextInputVisible = true,
        IsColorSpectrumVisible = true, IsColorSliderVisible = true, IsHexInputVisible = true,
        IsColorChannelTextInputVisible = true, IsMoreButtonVisible = false,
        HorizontalAlignment = HorizontalAlignment.Stretch, MinWidth = 0
    };
    private readonly StudioPaintChip _preview = new() { Label = "Draft", IsEnabled = false };
    private readonly TextBlock _status = new() { TextWrapping = TextWrapping.Wrap };
    private readonly Button _apply = new() { Content = "Apply color", HorizontalAlignment = HorizontalAlignment.Stretch };
    private readonly Button _reset = new() { Content = "Reset draft", HorizontalAlignment = HorizontalAlignment.Stretch };
    private readonly Button _none = new() { Content = "No paint", HorizontalAlignment = HorizontalAlignment.Stretch };
    private SvgEditorController? _controller;
    private SvgSolidPaintDraft? _draft;
    private bool _dirty, _syncing, _loaded, _disposed, _refreshPending, _applying;
    public Action? BeforeApply { get; set; }
    public bool HasPendingDraft => _dirty;

    public StudioColorEditor()
    {
        IsTabStop = false;
        Resources.MergedDictionaries.Add(new StudioThemeResources());
        var content = new StackPanel { Spacing = 12 };
        var header = new Grid { ColumnSpacing = 8 };
        header.ColumnDefinitions.Add(new ColumnDefinition());
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        header.Children.Add(_target); header.Children.Add(_preview); Grid.SetColumn(_preview, 1);
        content.Children.Add(header);
        var actions = new StudioFieldGrid { MinimumColumnWidth = 112 };
        actions.Children.Add(_apply); actions.Children.Add(_reset);
        StudioTheme.ApplyRole(_apply, StudioActionRole.Primary, this);
        StudioTheme.ApplyRole(_none, StudioActionRole.Quiet, this);
        _status.Style = (Style)Resources["StudioHintStyle"];
        content.Children.Add(actions); content.Children.Add(_status);
        content.Children.Add(_picker); content.Children.Add(_none);
        Content = content;
        AutomationProperties.SetName(_target, "Color target");
        AutomationProperties.SetAutomationId(_target, "StudioColorTarget");
        AutomationProperties.SetName(_picker, "Draft solid paint color");
        AutomationProperties.SetAutomationId(_picker, "StudioColorPicker");
        AutomationProperties.SetAutomationId(_apply, "StudioColorApply");
        _target.SelectionChanging += (_, args) =>
        {
            if (!_syncing && _dirty)
            {
                args.Cancel = true;
                _status.Text = "Apply or Reset this draft before changing its paint target.";
            }
        };
        _target.SelectionChanged += (_, _) =>
        {
            if (_syncing) return;
            // Programmatic target changes have the same guard as pointer/keyboard ones.
            if (_dirty && _draft is not null)
            {
                _syncing = true; _target.SelectedIndex = _draft.IsStroke ? 1 : 0; _syncing = false;
                _status.Text = "Apply or Reset this draft before changing its paint target."; return;
            }
            ResetDraft();
        };
        _picker.ColorChanged += (_, args) =>
        {
            if (_syncing || _disposed || _draft is null) return;
            _dirty = true;
            var color = args.NewColor;
            _preview.Paint = SvgSolidPaintDraft.FormatColor(new SKColor(color.R, color.G, color.B, color.A));
            UpdateStatus();
        };
        _reset.Click += (_, _) => ResetDraft();
        _apply.Click += (_, _) => Apply(false);
        _none.Click += (_, _) => Apply(true);
        Loaded += (_, _) =>
        {
            if (_loaded || _disposed) return;
            _loaded = true;
            if (_controller is not null) _controller.Changed += ControllerChanged;
            Synchronize();
        };
        Unloaded += (_, _) =>
        {
            if (_controller is not null) _controller.Changed -= ControllerChanged;
            _loaded = false;
        };
        ResetDraft();
    }
    public void BindController(SvgEditorController? controller)
    {
        if (_disposed || ReferenceEquals(controller, _controller)) return;
        if (_loaded && _controller is not null) _controller.Changed -= ControllerChanged;
        _controller = controller;
        if (_loaded && _controller is not null) _controller.Changed += ControllerChanged;
        Synchronize();
    }
    private void ControllerChanged(object? sender, EventArgs args)
    {
        if (_refreshPending || _disposed || _applying) return;
        _refreshPending = true;
        if (!DispatcherQueue.TryEnqueue(() =>
        {
            _refreshPending = false;
            if (!_disposed && _loaded) Synchronize();
        })) _refreshPending = false;
    }
    private void Synchronize()
    {
        if (_dirty) UpdateStatus(); else ResetDraft();
    }
    public void ResetDraft()
    {
        if (_disposed) return;
        _syncing = true; _dirty = false; _draft = null;
        try
        {
            if (_controller is null || _controller.SelectedElements.Count == 0)
            { _status.Text = "Select artwork to mix a fill or stroke color."; return; }
            _draft = new SvgSolidPaintDraft(_controller, _target.SelectedIndex == 1);
            var color = _draft.InitialColor ?? SKColors.Black;
            _picker.Color = Windows.UI.Color.FromArgb(color.Alpha, color.Red, color.Green, color.Blue);
            _picker.PreviousColor = _picker.Color;
            _preview.Paint = SvgSolidPaintDraft.FormatColor(color);
            _preview.IsMixed = _draft.IsMixed;
            _status.Text = _draft.IsMixed ? "Mixed paints. Apply replaces them with one color."
                : _draft.HasNonSolidPaint ? "Linked or non-solid paint. Apply explicitly replaces it."
                : "Color alpha combines with paint and object opacity.";
        }
        catch (Exception exception) { _status.Text = exception.Message; }
        finally
        {
            _syncing = false;
            _picker.IsEnabled = _draft is not null;
            _apply.IsEnabled = false;
            _none.IsEnabled = _draft?.IsCurrent(_controller) == true;
            _reset.IsEnabled = _controller is not null;
        }
    }
    private void UpdateStatus()
    {
        var current = _draft?.IsCurrent(_controller) == true;
        _apply.IsEnabled = current && _dirty;
        _none.IsEnabled = current;
        _preview.IsMixed = false;
        _status.Text = !current ? "The document or selection changed. Reset before applying."
            : "Unapplied color. Apply saves one undo step; Reset discards it.";
    }
    private void Apply(bool none)
    {
        if (_disposed) return;
        try
        {
            BeforeApply?.Invoke();
            if (_controller is null || _draft is null) throw new InvalidOperationException("Select artwork and reset the color draft first.");
            _applying = true;
            var color = _picker.Color;
            if (none) _draft.ApplyNone(_controller);
            else _draft.Apply(_controller, new SKColor(color.R, color.G, color.B, color.A));
            ResetDraft();
        }
        catch (Exception exception) { _status.Text = exception.Message; _apply.IsEnabled = false; }
        finally { _applying = false; }
    }
    public new void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        if (_controller is not null) _controller.Changed -= ControllerChanged;
        _controller = null; _draft = null; BeforeApply = null;
        base.Dispose();
    }
}
