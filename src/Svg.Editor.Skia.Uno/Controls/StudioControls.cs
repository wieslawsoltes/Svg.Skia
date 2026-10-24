using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using SkiaSharp;

namespace Svg.Editor.Skia.Uno;

public sealed class StudioIconButton : Button
{
    public static readonly DependencyProperty LabelProperty = DependencyProperty.Register(nameof(Label), typeof(string), typeof(StudioIconButton), new PropertyMetadata(string.Empty, Changed));
    public static readonly DependencyProperty ShortcutProperty = DependencyProperty.Register(nameof(Shortcut), typeof(string), typeof(StudioIconButton), new PropertyMetadata(string.Empty, Changed));
    public string Label { get => (string)GetValue(LabelProperty); set => SetValue(LabelProperty, value); }
    public string Shortcut { get => (string)GetValue(ShortcutProperty); set => SetValue(ShortcutProperty, value); }
    public StudioIconButton() { DefaultStyleKey = typeof(StudioIconButton); UseSystemFocusVisuals = true; }
    private static void Changed(DependencyObject sender, DependencyPropertyChangedEventArgs args)
    {
        var button = (StudioIconButton)sender;
        AutomationProperties.SetName(button, button.Label);
        ToolTipService.SetToolTip(button, button.Label + (button.Shortcut.Length == 0 ? string.Empty : " (" + button.Shortcut + ")"));
    }
}

public sealed class StudioToolButton : ToggleButton
{
    public static readonly DependencyProperty LabelProperty = DependencyProperty.Register(nameof(Label), typeof(string), typeof(StudioToolButton), new PropertyMetadata(string.Empty, Changed));
    public static readonly DependencyProperty ShortcutProperty = DependencyProperty.Register(nameof(Shortcut), typeof(string), typeof(StudioToolButton), new PropertyMetadata(string.Empty, Changed));
    public static readonly DependencyProperty AlternateMarkProperty = DependencyProperty.Register(nameof(AlternateMark), typeof(string), typeof(StudioToolButton), new PropertyMetadata(string.Empty));
    public string Label { get => (string)GetValue(LabelProperty); set => SetValue(LabelProperty, value); }
    public string Shortcut { get => (string)GetValue(ShortcutProperty); set => SetValue(ShortcutProperty, value); }
    public string AlternateMark { get => (string)GetValue(AlternateMarkProperty); set => SetValue(AlternateMarkProperty, value); }
    public StudioToolButton() { DefaultStyleKey = typeof(StudioToolButton); UseSystemFocusVisuals = true; }
    private static void Changed(DependencyObject sender, DependencyPropertyChangedEventArgs args)
    {
        var button = (StudioToolButton)sender;
        AutomationProperties.SetName(button, button.Label);
        ToolTipService.SetToolTip(button, button.Label + (button.Shortcut.Length == 0 ? string.Empty : " (" + button.Shortcut + ")"));
    }
}

/// <summary>Reusable, retemplatable inspector section. Collapsing retains its live content.</summary>
public sealed class StudioSection : ContentControl
{
    public static readonly DependencyProperty HeaderProperty = DependencyProperty.Register(nameof(Header), typeof(string), typeof(StudioSection), new PropertyMetadata(string.Empty));
    public static readonly DependencyProperty IsExpandedProperty = DependencyProperty.Register(nameof(IsExpanded), typeof(bool), typeof(StudioSection), new PropertyMetadata(true, Changed));
    private ToggleButton? _header;
    private FrameworkElement? _presenter;
    public string Header { get => (string)GetValue(HeaderProperty); set => SetValue(HeaderProperty, value); }
    public bool IsExpanded { get => (bool)GetValue(IsExpandedProperty); set => SetValue(IsExpandedProperty, value); }
    public StudioSection() { DefaultStyleKey = typeof(StudioSection); IsTabStop = false; }
    protected override void OnApplyTemplate()
    {
        if (_header is not null) _header.Click -= Toggle;
        base.OnApplyTemplate();
        _header = GetTemplateChild("PART_Header") as ToggleButton;
        _presenter = GetTemplateChild("PART_Content") as FrameworkElement;
        if (_header is not null) _header.Click += Toggle;
        Sync();
    }
    private void Toggle(object sender, RoutedEventArgs args) => IsExpanded = _header?.IsChecked == true;
    private static void Changed(DependencyObject sender, DependencyPropertyChangedEventArgs args) => ((StudioSection)sender).Sync();
    private void Sync()
    {
        if (_header is not null) _header.IsChecked = IsExpanded;
        if (_presenter is not null) _presenter.Visibility = IsExpanded ? Visibility.Visible : Visibility.Collapsed;
    }
}

public sealed class StudioValueEditedEventArgs : EventArgs
{
    public StudioValueEditedEventArgs(double value) => Value = value;
    public double Value { get; }
}

/// <summary>Compact numeric editor. Model synchronization does not emit edits or overwrite focused text.</summary>
public sealed class StudioNumericField : Control
{
    public static readonly DependencyProperty HeaderProperty = DependencyProperty.Register(nameof(Header), typeof(string), typeof(StudioNumericField), new PropertyMetadata(string.Empty, Changed));
    public static readonly DependencyProperty ValueProperty = DependencyProperty.Register(nameof(Value), typeof(double), typeof(StudioNumericField), new PropertyMetadata(0d, Changed));
    public static readonly DependencyProperty MinimumProperty = DependencyProperty.Register(nameof(Minimum), typeof(double), typeof(StudioNumericField), new PropertyMetadata(-1000000d, Changed));
    public static readonly DependencyProperty MaximumProperty = DependencyProperty.Register(nameof(Maximum), typeof(double), typeof(StudioNumericField), new PropertyMetadata(1000000d, Changed));
    public static readonly DependencyProperty IsMixedProperty = DependencyProperty.Register(nameof(IsMixed), typeof(bool), typeof(StudioNumericField), new PropertyMetadata(false, Changed));
    public static readonly DependencyProperty SuffixProperty = DependencyProperty.Register(nameof(Suffix), typeof(string), typeof(StudioNumericField), new PropertyMetadata(string.Empty));
    private NumberBox? _number;
    private bool _syncing;
    private Func<bool>? _targetIsCurrent;
    public string Header { get => (string)GetValue(HeaderProperty); set => SetValue(HeaderProperty, value); }
    public string Suffix { get => (string)GetValue(SuffixProperty); set => SetValue(SuffixProperty, value); }
    public double Value { get => (double)GetValue(ValueProperty); set => SetValue(ValueProperty, value); }
    public double Minimum { get => (double)GetValue(MinimumProperty); set => SetValue(MinimumProperty, value); }
    public double Maximum { get => (double)GetValue(MaximumProperty); set => SetValue(MaximumProperty, value); }
    public bool IsMixed { get => (bool)GetValue(IsMixedProperty); set => SetValue(IsMixedProperty, value); }
    public Func<Func<bool>>? CaptureTargetValidation { get; set; }
    public event EventHandler<StudioValueEditedEventArgs>? ValueEdited;
    public event EventHandler<string>? EditRejected;
    public StudioNumericField() { DefaultStyleKey = typeof(StudioNumericField); IsTabStop = false; }
    protected override void OnApplyTemplate()
    {
        if (_number is not null) { _number.ValueChanged -= Edited; _number.GotFocus -= FocusEntered; _number.LostFocus -= FocusLeft; }
        base.OnApplyTemplate();
        _number = GetTemplateChild("PART_Number") as NumberBox;
        if (_number is not null)
        {
            _number.NumberFormatter = new EditorNumberFormatter();
            _number.ValueChanged += Edited; _number.GotFocus += FocusEntered; _number.LostFocus += FocusLeft;
        }
        Sync(true);
    }
    private void FocusEntered(object sender, RoutedEventArgs args) => _targetIsCurrent = CaptureTargetValidation?.Invoke();
    private void FocusLeft(object sender, RoutedEventArgs args)
    {
        if (!ContainsKeyboardFocus()) { _targetIsCurrent = null; Sync(true); }
    }
    private bool ContainsKeyboardFocus()
    {
        if (XamlRoot is null || _number is null) return false;
        var focused = FocusManager.GetFocusedElement(XamlRoot) as DependencyObject;
        while (focused is not null)
        {
            if (ReferenceEquals(focused, _number)) return true;
            focused = VisualTreeHelper.GetParent(focused);
        }
        return false;
    }
    private static void Changed(DependencyObject sender, DependencyPropertyChangedEventArgs args) => ((StudioNumericField)sender).Sync();
    private void Sync(bool force = false)
    {
        if (_syncing || _number is null || (!force && ContainsKeyboardFocus())) return;
        _syncing = true;
        try
        {
            _number.Minimum = Minimum; _number.Maximum = Math.Max(Minimum, Maximum);
            _number.PlaceholderText = IsMixed ? "Mixed" : string.Empty;
            _number.Value = IsMixed ? double.NaN : Value;
            AutomationProperties.SetName(_number, Header);
        }
        finally { _syncing = false; }
    }
    private void Edited(NumberBox sender, NumberBoxValueChangedEventArgs args)
    {
        if (_syncing || !double.IsFinite(args.NewValue)) return;
        if (_targetIsCurrent?.Invoke() == false)
        {
            Sync(true); EditRejected?.Invoke(this, "The selection changed while this numeric value was being edited. The edit was not applied."); return;
        }
        _syncing = true;
        try { Value = args.NewValue; IsMixed = false; }
        finally { _syncing = false; }
        ValueEdited?.Invoke(this, new StudioValueEditedEventArgs(args.NewValue));
    }
}

/// <summary>Paint selector distinguishing solid, none, linked paint and mixed values.</summary>
public sealed class StudioPaintChip : Button
{
    public static readonly DependencyProperty PaintProperty = DependencyProperty.Register(nameof(Paint), typeof(string), typeof(StudioPaintChip), new PropertyMetadata("none", Changed));
    public static readonly DependencyProperty LabelProperty = DependencyProperty.Register(nameof(Label), typeof(string), typeof(StudioPaintChip), new PropertyMetadata("Fill", Changed));
    public static readonly DependencyProperty IsMixedProperty = DependencyProperty.Register(nameof(IsMixed), typeof(bool), typeof(StudioPaintChip), new PropertyMetadata(false, Changed));
    public static readonly DependencyProperty IsCompactProperty = DependencyProperty.Register(nameof(IsCompact), typeof(bool), typeof(StudioPaintChip), new PropertyMetadata(false, Changed));
    public static readonly DependencyProperty LabelVisibilityProperty = DependencyProperty.Register(nameof(LabelVisibility), typeof(Visibility), typeof(StudioPaintChip), new PropertyMetadata(Visibility.Visible));
    public static readonly DependencyProperty SwatchProperty = DependencyProperty.Register(nameof(Swatch), typeof(Brush), typeof(StudioPaintChip), new PropertyMetadata(null));
    public static readonly DependencyProperty MarkProperty = DependencyProperty.Register(nameof(Mark), typeof(string), typeof(StudioPaintChip), new PropertyMetadata("∅"));
    public string Paint { get => (string)GetValue(PaintProperty); set => SetValue(PaintProperty, value); }
    public string Label { get => (string)GetValue(LabelProperty); set => SetValue(LabelProperty, value); }
    public bool IsMixed { get => (bool)GetValue(IsMixedProperty); set => SetValue(IsMixedProperty, value); }
    public bool IsCompact { get => (bool)GetValue(IsCompactProperty); set => SetValue(IsCompactProperty, value); }
    public Visibility LabelVisibility { get => (Visibility)GetValue(LabelVisibilityProperty); private set => SetValue(LabelVisibilityProperty, value); }
    public Brush? Swatch { get => (Brush?)GetValue(SwatchProperty); private set => SetValue(SwatchProperty, value); }
    public string Mark { get => (string)GetValue(MarkProperty); private set => SetValue(MarkProperty, value); }
    public StudioPaintChip() { DefaultStyleKey = typeof(StudioPaintChip); UseSystemFocusVisuals = true; UpdatePaint(); }
    private static void Changed(DependencyObject sender, DependencyPropertyChangedEventArgs args) => ((StudioPaintChip)sender).UpdatePaint();
    private void UpdatePaint()
    {
        var value = Paint ?? "none";
        var mark = IsMixed ? "—" : value == "none" ? "∅" : "↗";
        var color = SKColors.Transparent;
        if (!IsMixed && value != "none" && !value.StartsWith("url(", StringComparison.OrdinalIgnoreCase))
        {
            try { color = SKColor.Parse(value); mark = string.Empty; }
            catch (Exception) { /* Non-solid SVG paint stays visibly linked. */ }
        }
        Swatch = new SolidColorBrush(Windows.UI.Color.FromArgb(color.Alpha, color.Red, color.Green, color.Blue));
        Mark = mark; LabelVisibility = IsCompact ? Visibility.Collapsed : Visibility.Visible;
        var description = Label + ": " + (IsMixed ? "Mixed" : value);
        AutomationProperties.SetName(this, description); ToolTipService.SetToolTip(this, description);
    }
}
