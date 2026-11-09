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
        IsAlphaEnabled = true,
        IsAlphaSliderVisible = true,
        IsAlphaTextInputVisible = true,
        IsColorSpectrumVisible = true,
        IsColorSliderVisible = true,
        IsHexInputVisible = true,
        IsColorChannelTextInputVisible = true,
        IsMoreButtonVisible = false,
        HorizontalAlignment = HorizontalAlignment.Stretch,
        MinWidth = 0
    };
    private readonly StudioPaintChip _preview = new() { Label = "Draft", IsEnabled = false };
    private readonly TextBlock _status = new() { TextWrapping = TextWrapping.Wrap };
    private readonly Button _apply = new() { Content = "Apply color", HorizontalAlignment = HorizontalAlignment.Stretch };
    private readonly Button _reset = new() { Content = "Reset draft", HorizontalAlignment = HorizontalAlignment.Stretch };
    private readonly Button _none = new() { Content = "No paint", HorizontalAlignment = HorizontalAlignment.Stretch };
    private SvgEditorController? _controller;
    private SvgSolidPaintDraft? _draft;
    private readonly StudioDraftInputTracker _draftInputs;
    private bool _dirty, _syncing, _loaded, _disposed, _refreshPending, _applying, _strokeTarget;
    public Action? BeforeApply { get; set; }
    /// <summary>Optional host coordination. Return false to keep every retained draft on its current target.</summary>
    public Func<bool, bool>? PaintTargetChangeRequested { get; set; }
    public bool IsStrokeTarget => _strokeTarget;
    public bool HasPendingDraft => _dirty || _draftInputs.HasPendingInput;

    public StudioColorEditor()
    {
        IsTabStop = false;
        _draftInputs = new StudioDraftInputTracker(this, () => _syncing || _disposed, UpdateStatus);
        Resources.MergedDictionaries.Add(new StudioThemeResources());
        var content = new StackPanel { Spacing = 12 };
        var header = new Grid { ColumnSpacing = 8 };
        header.ColumnDefinitions.Add(new ColumnDefinition());
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        header.Children.Add(_target);
        header.Children.Add(_preview);
        Grid.SetColumn(_preview, 1);
        content.Children.Add(header);
        var actions = new StudioFieldGrid { MinimumColumnWidth = 112 };
        actions.Children.Add(_apply);
        actions.Children.Add(_reset);
        StudioTheme.ApplyRole(_apply, StudioActionRole.Primary, this);
        StudioTheme.ApplyRole(_none, StudioActionRole.Quiet, this);
        _status.Style = (Style)Resources["StudioHintStyle"];
        content.Children.Add(actions);
        content.Children.Add(_status);
        content.Children.Add(_picker);
        content.Children.Add(_none);
        Content = content;
        AutomationProperties.SetName(_target, "Color target");
        AutomationProperties.SetAutomationId(_target, "StudioColorTarget");
        AutomationProperties.SetName(_picker, "Draft solid paint color");
        AutomationProperties.SetAutomationId(_picker, "StudioColorPicker");
        AutomationProperties.SetAutomationId(_apply, "StudioColorApply");
        _target.SelectionChanging += (_, args) =>
        {
            if (!_syncing)
                args.Cancel = !TrySetPaintTarget(args.SelectedIndex == 1);
        };
        _target.SelectionChanged += (_, _) =>
        {
            if (_syncing)
                return;
            var requested = _target.SelectedIndex == 1;
            _syncing = true;
            try
            { _target.SelectedIndex = _strokeTarget ? 1 : 0; }
            finally { _syncing = false; }
            TrySetPaintTarget(requested);
        };
        _picker.ColorChanged += (_, args) =>
        {
            if (_syncing || _disposed || _draft is null)
                return;
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
            if (_loaded || _disposed)
                return;
            _loaded = true;
            if (_controller is not null)
                _controller.Changed += ControllerChanged;
            Synchronize();
        };
        Unloaded += (_, _) =>
        {
            if (_controller is not null)
                _controller.Changed -= ControllerChanged;
            _loaded = false;
        };
        ResetDraft();
    }
    public bool TrySetPaintTarget(bool stroke)
    {
        if (!CanChangePaintTarget(stroke))
            return false;
        if (PaintTargetChangeRequested is { } request && !request(stroke))
            return false;
        SynchronizePaintTarget(stroke);
        return true;
    }
    internal bool CanChangePaintTarget(bool stroke)
    {
        if (_disposed)
            return false;
        if (stroke == _strokeTarget || !HasPendingDraft)
            return true;
        _status.Text = "Apply or Reset this draft before changing its paint target.";
        return false;
    }
    internal void SynchronizePaintTarget(bool stroke)
    {
        if (stroke == _strokeTarget)
            return;
        _strokeTarget = stroke;
        _syncing = true;
        try
        { _target.SelectedIndex = stroke ? 1 : 0; }
        finally { _syncing = false; }
        ResetDraft();
    }
    public void BindController(SvgEditorController? controller)
    {
        if (_disposed || ReferenceEquals(controller, _controller))
            return;
        if (_loaded && _controller is not null)
            _controller.Changed -= ControllerChanged;
        _controller = controller;
        if (_loaded && _controller is not null)
            _controller.Changed += ControllerChanged;
        Synchronize();
    }
    private void ControllerChanged(object? sender, EventArgs args)
    {
        if (_refreshPending || _disposed || _applying)
            return;
        _refreshPending = true;
        if (!DispatcherQueue.TryEnqueue(() =>
        {
            _refreshPending = false;
            if (!_disposed && _loaded)
                Synchronize();
        }))
            _refreshPending = false;
    }
    private void Synchronize()
    {
        if (HasPendingDraft)
            UpdateStatus();
        else
            ResetDraft();
    }
    public void ResetDraft()
    {
        if (_disposed)
            return;
        _syncing = true;
        _dirty = false;
        _draftInputs.Clear();
        _draft = null;
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
        _apply.IsEnabled = current && HasPendingDraft;
        _none.IsEnabled = current;
        _preview.IsMixed = !_dirty && _draft?.IsMixed == true;
        _status.Text = !current ? "The document or selection changed. Reset before applying."
            : HasPendingDraft ? "Unapplied color. Apply saves one undo step; Reset discards it."
            : "Color alpha combines with paint and object opacity.";
    }
    private void Apply(bool none)
    {
        if (_disposed)
            return;
        try
        {
            BeforeApply?.Invoke();
            if (_controller is null || _draft is null)
                throw new InvalidOperationException("Select artwork and reset the color draft first.");
            (none ? _none : _apply).Focus(FocusState.Programmatic);
            if (!_draft.IsCurrent(_controller))
                throw new InvalidOperationException("The color target or document changed. Reset the color draft before applying it.");
            if (!none && !_dirty && !_draft.IsMixed && !_draft.HasNonSolidPaint)
            { ResetDraft(); return; }
            _applying = true;
            var color = _picker.Color;
            if (none)
                _draft.ApplyNone(_controller);
            else
                _draft.Apply(_controller, new SKColor(color.R, color.G, color.B, color.A));
            ResetDraft();
        }
        catch (Exception exception) { _status.Text = exception.Message; _apply.IsEnabled = false; }
        finally { _applying = false; }
    }
    public new void Dispose()
    {
        if (_disposed)
            return;
        _disposed = true;
        if (_controller is not null)
            _controller.Changed -= ControllerChanged;
        _draftInputs.Dispose();
        _controller = null;
        _draft = null;
        BeforeApply = null;
        PaintTargetChangeRequested = null;
        base.Dispose();
    }
}
