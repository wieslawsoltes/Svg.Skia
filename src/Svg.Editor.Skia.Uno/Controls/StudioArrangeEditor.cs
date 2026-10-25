using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Svg.Editor.Skia;

namespace Svg.Editor.Skia.Uno;

/// <summary>Reference-aware arrangement with responsive, equal-width action groups.</summary>
public sealed class StudioArrangeEditor : UserControl, IDisposable
{
    private sealed record KeyChoice(SvgVisualElement Element, string Label) { public override string ToString() => Label; }
    private readonly ComboBox _reference = new()
    { Header = "Align to", ItemsSource = new[] { "Selection bounds", "Artboard", "Key object · stays fixed" }, SelectedIndex = 0, HorizontalAlignment = HorizontalAlignment.Stretch };
    private readonly ComboBox _key = new() { Header = "Key object", HorizontalAlignment = HorizontalAlignment.Stretch, Visibility = Visibility.Collapsed };
    private readonly CheckBox _exact = new() { Content = "Use an exact gap" };
    private readonly NumberBox _gap = new()
    { Header = "Gap · canvas units", Value = 12, Minimum = -1000000, Maximum = 1000000, SmallChange = 1, LargeChange = 10,
        NumberFormatter = new EditorNumberFormatter(), SpinButtonPlacementMode = NumberBoxSpinButtonPlacementMode.Compact, Visibility = Visibility.Collapsed };
    private readonly TextBlock _hint = new() { TextWrapping = TextWrapping.Wrap };
    private readonly TextBlock _feedback = new() { TextWrapping = TextWrapping.Wrap };
    private readonly List<Button> _alignmentButtons = new();
    private readonly List<Button> _centerButtons = new();
    private readonly List<Button> _spacingButtons = new();
    private SvgEditorController? _controller;
    private bool _loaded, _queued, _syncing, _disposed;
    public Action? BeforeApply { get; set; }

    public StudioArrangeEditor()
    {
        IsTabStop = false;
        Resources.MergedDictionaries.Add(new StudioThemeResources());
        _hint.Style = _feedback.Style = (Style)Resources["StudioHintStyle"];
        var panel = new StackPanel { Spacing = 12 };
        panel.Children.Add(_reference); panel.Children.Add(_key); panel.Children.Add(_hint);
        var align = new StudioFieldGrid { MaximumColumns = 3, MinimumColumnWidth = 72, Spacing = 6 };
        foreach (var entry in new[]
        {
            (SvgAlignment.Left, "Left"), (SvgAlignment.HorizontalCenter, "Center X"), (SvgAlignment.Right, "Right"),
            (SvgAlignment.Top, "Top"), (SvgAlignment.VerticalCenter, "Center Y"), (SvgAlignment.Bottom, "Bottom")
        })
        {
            var button = ActionButton(entry.Item2, () => _controller!.AlignSelection(entry.Item1,
                (SvgAlignmentTarget)_reference.SelectedIndex, (_key.SelectedItem as KeyChoice)?.Element));
            AutomationProperties.SetAutomationId(button, "StudioAlign" + entry.Item1);
            ToolTipService.SetToolTip(button, "Align " + entry.Item2 + " to the chosen reference. A key object is not moved.");
            _alignmentButtons.Add(button); align.Children.Add(button);
        }
        panel.Children.Add(new StudioSection { Header = "Align edges and centers", Content = align });
        var centers = new StudioFieldGrid { MinimumColumnWidth = 112 };
        foreach (var axis in Enum.GetValues<SvgArrangementAxis>())
        {
            var button = ActionButton(axis == SvgArrangementAxis.Horizontal ? "Centers X" : "Centers Y", () => _controller!.DistributeSelectionCenters(axis));
            ToolTipService.SetToolTip(button, "Equalize center distances. The two outer centers remain fixed; requires three objects.");
            _centerButtons.Add(button); centers.Children.Add(button);
        }
        panel.Children.Add(new StudioSection { Header = "Distribute centers", Content = centers });
        var spacing = new StackPanel { Spacing = 8 }; spacing.Children.Add(_exact); spacing.Children.Add(_gap);
        var gaps = new StudioFieldGrid { MinimumColumnWidth = 112 };
        foreach (var axis in Enum.GetValues<SvgArrangementAxis>())
        {
            var label = axis == SvgArrangementAxis.Horizontal ? "Horizontal gaps" : "Vertical gaps";
            var button = ActionButton(label, () =>
            {
                var exact = _exact.IsChecked == true;
                if (exact && !double.IsFinite(_gap.Value)) throw new InvalidOperationException("Enter a finite spacing value.");
                _controller!.SpaceSelection(axis, exact ? _gap.Value : null,
                    exact && _reference.SelectedIndex == (int)SvgAlignmentTarget.KeyObject ? (_key.SelectedItem as KeyChoice)?.Element : null);
            });
            AutomationProperties.SetAutomationId(button, "StudioSpace" + axis);
            ToolTipService.SetToolTip(button, "Equalize edge-to-edge " + label.ToLowerInvariant() + ". Exact gap values may be negative for overlap.");
            _spacingButtons.Add(button); gaps.Children.Add(button);
        }
        spacing.Children.Add(gaps);
        panel.Children.Add(new StudioSection { Header = "Space between objects", Content = spacing });
        panel.Children.Add(_feedback);
        panel.Children.Add(new StudioSection
        {
            Header = "How spacing works", IsExpanded = false,
            Content = new TextBlock
            {
                Text = "Automatic gaps use the selected objects' span. Exact gaps start at the first spatial object, or around the fixed key. Negative gaps intentionally overlap objects.",
                Style = (Style)Resources["StudioHintStyle"]
            }
        });
        Content = panel;
        AutomationProperties.SetAutomationId(_reference, "StudioAlignReference");
        AutomationProperties.SetAutomationId(_gap, "StudioAlignGap");
        AutomationProperties.SetName(_key, "Fixed key object");
        AutomationProperties.SetLiveSetting(_feedback, Microsoft.UI.Xaml.Automation.Peers.AutomationLiveSetting.Polite);
        _reference.SelectionChanged += (_, _) => Refresh();
        _exact.Checked += (_, _) => Refresh(); _exact.Unchecked += (_, _) => Refresh();
        Loaded += (_, _) =>
        {
            if (_loaded || _disposed) return;
            _loaded = true; if (_controller is not null) _controller.Changed += Changed; Refresh();
        };
        Unloaded += (_, _) => { if (_controller is not null) _controller.Changed -= Changed; _loaded = false; };
        Refresh();
    }
    public void BindController(SvgEditorController? controller)
    {
        if (_disposed || ReferenceEquals(controller, _controller)) return;
        if (_loaded && _controller is not null) _controller.Changed -= Changed;
        _controller = controller;
        if (_loaded && _controller is not null) _controller.Changed += Changed;
        Refresh();
    }
    private Button ActionButton(string label, Action action)
    {
        var button = new Button { Content = label, HorizontalAlignment = HorizontalAlignment.Stretch };
        button.Click += (_, _) =>
        {
            try
            {
                BeforeApply?.Invoke();
                if (_controller is null) throw new InvalidOperationException("Open a document and select artwork first.");
                var revision = _controller.DocumentRevision; action();
                _feedback.Text = revision == _controller.DocumentRevision ? "Already arranged. No undo entry was added." : _controller.LastEditLabel;
                Refresh();
            }
            catch (Exception exception) { _feedback.Text = exception.Message; }
        };
        return button;
    }
    private void Changed(object? sender, EventArgs args)
    {
        if (_queued || _disposed) return;
        _queued = true;
        if (!DispatcherQueue.TryEnqueue(() => { _queued = false; if (!_disposed && _loaded) Refresh(); })) _queued = false;
    }
    private void Refresh()
    {
        if (_syncing || _disposed) return;
        _syncing = true;
        try
        {
            var selected = _controller?.GetArrangementElements() ?? Array.Empty<SvgVisualElement>();
            var editable = selected.Length > 0 && !selected.Any(SvgEditorController.IsEditLocked);
            var keyMode = _reference.SelectedIndex == (int)SvgAlignmentTarget.KeyObject;
            _key.Visibility = keyMode ? Visibility.Visible : Visibility.Collapsed;
            var previous = (_key.SelectedItem as KeyChoice)?.Element;
            var choices = selected.Select((element, index) => new KeyChoice(element,
                string.IsNullOrEmpty(element.ID) ? $"{index + 1}. {element.GetType().Name.Replace("Svg", string.Empty)}" : element.ID)).ToArray();
            _key.ItemsSource = choices;
            _key.SelectedItem = choices.FirstOrDefault(choice => ReferenceEquals(choice.Element, previous)) ?? choices.LastOrDefault();
            _key.IsEnabled = editable;
            if (keyMode) _exact.IsChecked = true;
            _exact.IsEnabled = !keyMode;
            _gap.Visibility = _exact.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
            foreach (var button in _alignmentButtons) button.IsEnabled = editable && (_reference.SelectedIndex == (int)SvgAlignmentTarget.Artboard || selected.Length >= 2);
            foreach (var button in _centerButtons) button.IsEnabled = editable && selected.Length >= 3;
            foreach (var button in _spacingButtons) button.IsEnabled = editable && selected.Length >= 2;
            _hint.Text = !editable ? "Select unlocked artwork. One object can align to the artboard."
                : keyMode ? "The key stays fixed; other objects move around it."
                : _reference.SelectedIndex == (int)SvgAlignmentTarget.Artboard ? "Align to the artboard. Spacing acts within the selection."
                : $"{selected.Length} objects · selection bounds";
            _feedback.Visibility = string.IsNullOrEmpty(_feedback.Text) ? Visibility.Collapsed : Visibility.Visible;
        }
        finally { _syncing = false; }
    }
    public new void Dispose()
    {
        if (_disposed) return;
        _disposed = true; if (_controller is not null) _controller.Changed -= Changed;
        _controller = null; BeforeApply = null; base.Dispose();
    }
}
