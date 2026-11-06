using Microsoft.UI.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Windows.System;
using Windows.UI.Core;

namespace Svg.Editor.Skia.Uno;

public sealed class StudioChoiceChangingEventArgs : EventArgs
{
    public StudioChoiceChangingEventArgs(int previousIndex, int selectedIndex)
    { PreviousIndex = previousIndex; SelectedIndex = selectedIndex; }
    public int PreviousIndex { get; }
    public int SelectedIndex { get; }
    public bool Cancel { get; set; }
}

/// <summary>
/// A short, always-visible choice set using native RadioButton selection peers.
/// Items are snapshotted on ItemsSource assignment (up to 16 plain-text labels).
/// Selection changes never recreate the children. A single Tab stop enters the
/// selected choice; arrows/Home/End navigate, respecting right-to-left flow.
/// Modified shortcuts remain available to the containing editor.
/// </summary>
public sealed partial class StudioChoiceStrip : UserControl
{
    public static readonly DependencyProperty HeaderProperty = DependencyProperty.Register(nameof(Header), typeof(string), typeof(StudioChoiceStrip), new PropertyMetadata(string.Empty, HeaderChanged));
    public static readonly DependencyProperty ItemsSourceProperty = DependencyProperty.Register(nameof(ItemsSource), typeof(IEnumerable<string>), typeof(StudioChoiceStrip), new PropertyMetadata(null, ItemsChanged));
    public static readonly DependencyProperty SelectedIndexProperty = DependencyProperty.Register(nameof(SelectedIndex), typeof(int), typeof(StudioChoiceStrip), new PropertyMetadata(-1, IndexChanged));
    private readonly StudioFieldGrid _fields = new() { MaximumColumns = 4, MinimumColumnWidth = 72, Spacing = 4 };
    private readonly TextBlock _header = new() { TextWrapping = TextWrapping.Wrap, Visibility = Visibility.Collapsed };
    private readonly List<RadioButton> _buttons = new();
    private readonly string _groupName = "StudioChoice_" + Guid.NewGuid().ToString("N");
    private bool _syncing, _requesting;

    public string Header { get => (string)GetValue(HeaderProperty); set => SetValue(HeaderProperty, value); }
    public IEnumerable<string>? ItemsSource { get => (IEnumerable<string>?)GetValue(ItemsSourceProperty); set => SetValue(ItemsSourceProperty, value); }
    public int SelectedIndex { get => (int)GetValue(SelectedIndexProperty); set => SetValue(SelectedIndexProperty, value); }
    public event EventHandler<StudioChoiceChangingEventArgs>? SelectionChanging;
    public event EventHandler? SelectionChanged;

    public StudioChoiceStrip()
    {
        IsTabStop = false;
        HorizontalContentAlignment = HorizontalAlignment.Stretch;
        Resources.MergedDictionaries.Add(new StudioSelectionResources());
        _header.FontSize = (double)Resources["StudioLabelSize"];
        _header.Foreground = (Brush)Resources["StudioMuted"];
        _header.FontWeight = Microsoft.UI.Text.FontWeights.SemiBold;
        var root = new StackPanel { Spacing = 6 };
        root.Children.Add(_header); root.Children.Add(_fields); Content = root;
    }

    private static void HeaderChanged(DependencyObject sender, DependencyPropertyChangedEventArgs args)
        => ((StudioChoiceStrip)sender).SynchronizeHeader();
    private static void ItemsChanged(DependencyObject sender, DependencyPropertyChangedEventArgs args)
        => ((StudioChoiceStrip)sender).RebuildItems();
    private static void IndexChanged(DependencyObject sender, DependencyPropertyChangedEventArgs args)
    {
        var owner = (StudioChoiceStrip)sender;
        if (owner._syncing) return;
        var index = owner.SelectedIndex;
        if (index < -1 || index >= owner._buttons.Count)
        {
            owner._syncing = true;
            try { owner.SelectedIndex = -1; }
            finally { owner._syncing = false; }
        }
        owner.SynchronizeSelection();
        if ((int)args.OldValue != owner.SelectedIndex) owner.SelectionChanged?.Invoke(owner, EventArgs.Empty);
        // A consumer can reject a draft-target change by restoring its previous index.
        owner.SynchronizeSelection();
    }

    private void RebuildItems()
    {
        var labels = ItemsSource?.Take(17).ToArray() ?? Array.Empty<string>();
        if (labels.Length > 16 || labels.Any(label => string.IsNullOrWhiteSpace(label) || label.Length > 128))
            throw new ArgumentException("Provide at most 16 nonempty choice labels of at most 128 characters.", nameof(ItemsSource));
        var focused = _buttons.FindIndex(button => button.FocusState != FocusState.Unfocused);
        var previous = SelectedIndex;
        _syncing = true;
        try
        {
            _buttons.Clear(); _fields.Children.Clear();
            for (var index = 0; index < labels.Length; index++)
            {
                var choice = index;
                var button = new RadioButton
                {
                    Content = labels[index], GroupName = _groupName,
                    Style = (Style)Resources["StudioChoiceItemStyle"]
                };
                button.Checked += (_, _) => { if (!_syncing) RequestSelection(choice); };
                button.AddHandler(KeyDownEvent, new KeyEventHandler((_, args) => ChoiceKeyDown(choice, args)), true);
                ToolTipService.SetToolTip(button, labels[index]);
                _buttons.Add(button); _fields.Children.Add(button);
            }
            SelectedIndex = labels.Length == 0 ? -1 : Math.Clamp(previous, 0, labels.Length - 1);
        }
        finally { _syncing = false; }
        SynchronizeHeader(); SynchronizeSelection();
        if (previous != SelectedIndex) SelectionChanged?.Invoke(this, EventArgs.Empty);
        if (focused >= 0 && SelectedIndex >= 0) _buttons[SelectedIndex].Focus(FocusState.Keyboard);
    }

    private void SynchronizeHeader()
    {
        _header.Text = Header ?? string.Empty;
        _header.Visibility = string.IsNullOrEmpty(_header.Text) ? Visibility.Collapsed : Visibility.Visible;
        foreach (var button in _buttons)
            AutomationProperties.SetName(button, string.IsNullOrEmpty(Header) ? (string)button.Content : Header + ": " + button.Content);
    }

    private void SynchronizeSelection()
    {
        var previous = _syncing;
        _syncing = true;
        try
        {
            for (var index = 0; index < _buttons.Count; index++)
            {
                _buttons[index].IsChecked = index == SelectedIndex;
                _buttons[index].IsTabStop = index == (SelectedIndex < 0 ? 0 : SelectedIndex);
            }
        }
        finally { _syncing = previous; }
    }

    private void RequestSelection(int index)
    {
        if (_syncing || _requesting || !IsEnabled) return;
        _requesting = true;
        try
        {
            if (index != SelectedIndex)
            {
                var change = new StudioChoiceChangingEventArgs(SelectedIndex, index);
                SelectionChanging?.Invoke(this, change);
                if (!change.Cancel) SelectedIndex = index;
            }
        }
        finally { _requesting = false; SynchronizeSelection(); }
    }

    private void ChoiceKeyDown(int index, KeyRoutedEventArgs args)
    {
        if (_buttons.Count == 0 || !IsEnabled || IsDown(VirtualKey.Control) || IsDown(VirtualKey.Menu) ||
            IsDown(VirtualKey.LeftWindows) || IsDown(VirtualKey.RightWindows)) return;
        var forward = FlowDirection == FlowDirection.RightToLeft ? -1 : 1;
        var next = args.Key switch
        {
            VirtualKey.Right => index + forward,
            VirtualKey.Left => index - forward,
            VirtualKey.Down => index + 1,
            VirtualKey.Up => index - 1,
            VirtualKey.Home => 0,
            VirtualKey.End => _buttons.Count - 1,
            VirtualKey.Enter => index,
            _ => int.MinValue
        };
        if (next == int.MinValue) return;
        RequestSelection((next + _buttons.Count) % _buttons.Count);
        if (SelectedIndex >= 0) _buttons[SelectedIndex].Focus(FocusState.Keyboard);
        args.Handled = true;
    }

    private static bool IsDown(VirtualKey key)
        => (InputKeyboardSource.GetKeyStateForCurrentThread(key) & CoreVirtualKeyStates.Down) != 0;
}
