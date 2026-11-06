using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Media;
using Windows.System;

namespace Svg.Editor.Skia.Uno;

/// <summary>
/// Retained text field with explicit commit boundaries. Validation failures retain the
/// user's text; Escape restores the synchronized value without creating an edit.
/// </summary>
public sealed class StudioTextField : UserControl
{
    private readonly TextBox _input;
    private string _baseline = string.Empty;
    private bool _committing;
    private Func<bool>? _targetIsCurrent;
    public event EventHandler<string>? TextEdited;
    public event EventHandler<Exception>? ValidationFailed;
    public event EventHandler? EditCanceled;
    /// <summary>Captures a target-identity check when the field adopts a document value.</summary>
    public Func<Func<bool>>? CaptureTargetValidation { get; set; }
    public bool HasPendingEdit => _input.Text != _baseline;
    public StudioTextField(string label, bool multiline = false)
    {
        IsTabStop = false;
        _input = new TextBox { Header = label, FontSize = 12, MinHeight = 28, AcceptsReturn = multiline,
            TextWrapping = multiline ? TextWrapping.Wrap : TextWrapping.NoWrap, MaxLength = multiline ? 65536 : 4096 };
        AutomationProperties.SetName(_input, label);
        _input.LostFocus += (_, _) => TryCommit();
        _input.KeyDown += (_, args) =>
        {
            if (args.Key == VirtualKey.Escape)
            {
                _input.Text = _baseline; args.Handled = true; EditCanceled?.Invoke(this, EventArgs.Empty);
            }
            else if (args.Key == VirtualKey.Enter && !multiline) { TryCommit(); args.Handled = true; }
        };
        Content = _input;
    }
    public void Synchronize(string? value, bool mixed = false)
    {
        if (HasPendingEdit || _committing) return;
        _baseline = mixed ? string.Empty : value ?? string.Empty;
        _input.PlaceholderText = mixed ? "Mixed values" : string.Empty;
        _input.Text = _baseline;
        _targetIsCurrent = CaptureTargetValidation?.Invoke();
    }
    public void Commit()
    {
        if (_committing || !HasPendingEdit) return;
        if (_targetIsCurrent?.Invoke() == false)
            throw new InvalidOperationException("The target of this pending field changed. Press Escape in the field to discard it, or return to its original selection before applying.");
        if (!IsEnabled) return;
        _committing = true;
        try
        {
            var text = _input.Text;
            TextEdited?.Invoke(this, text);
            _baseline = text;
        }
        finally { _committing = false; }
    }
    private void TryCommit()
    {
        try { Commit(); }
        catch (Exception exception) { ValidationFailed?.Invoke(this, exception); }
    }
}

/// <summary>Keyboard-focusable nine-point anchor selector used by world-space transforms.</summary>
public sealed class StudioReferencePoint : UserControl
{
    private readonly ToggleButton[] _buttons = new ToggleButton[9];
    private int _selectedIndex;
    public event EventHandler? SelectionChanged;
    public int SelectedIndex
    {
        get => _selectedIndex;
        set
        {
            if (value < 0 || value >= 9) throw new ArgumentOutOfRangeException(nameof(value));
            _selectedIndex = value;
            for (var i = 0; i < _buttons.Length; i++) _buttons[i].IsChecked = i == value;
            SelectionChanged?.Invoke(this, EventArgs.Empty);
        }
    }
    public StudioReferencePoint()
    {
        IsTabStop = false;
        Resources.MergedDictionaries.Add(new StudioThemeResources());
        var grid = new Grid { Width = 86, Height = 86, ColumnSpacing = 4, RowSpacing = 4 };
        for (var i = 0; i < 3; i++) { grid.ColumnDefinitions.Add(new ColumnDefinition()); grid.RowDefinitions.Add(new RowDefinition()); }
        var labels = new[] { "Top left", "Top center", "Top right", "Middle left", "Center", "Middle right", "Bottom left", "Bottom center", "Bottom right" };
        for (var i = 0; i < 9; i++)
        {
            var index = i;
            // Explicit targets prevent native ToggleButton's content-sized alignment
            // from reducing a dot-only anchor to a narrow sliver inside its grid cell.
            var button = new StudioToolButton
            {
                Label = labels[i] + " transform reference", Content = "•", Width = 24, Height = 24,
                MinWidth = 0, MinHeight = 0, Padding = new Thickness(0), IsChecked = i == 0,
                HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center
            };
            _buttons[i] = button;
            AutomationProperties.SetAutomationId(button, "StudioReferencePoint" + i);
            ToolTipService.SetToolTip(button, labels[i] + " · arrow keys choose adjacent reference points");
            button.Click += (_, _) => SelectedIndex = index;
            button.KeyDown += (_, args) =>
            {
                var next = args.Key switch
                {
                    VirtualKey.Left => index % 3 > 0 ? index - 1 : index,
                    VirtualKey.Right => index % 3 < 2 ? index + 1 : index,
                    VirtualKey.Up => index >= 3 ? index - 3 : index,
                    VirtualKey.Down => index < 6 ? index + 3 : index,
                    VirtualKey.Home => 0,
                    VirtualKey.End => 8,
                    _ => -1
                };
                if (next < 0) return;
                SelectedIndex = next; _buttons[next].Focus(FocusState.Keyboard); args.Handled = true;
            };
            var cell = new Border
            {
                BorderBrush = (Brush)Resources["StudioBorder"], Background = (Brush)Resources["StudioInputBackground"],
                BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(3), Child = button
            };
            Grid.SetRow(cell, i / 3); Grid.SetColumn(cell, i % 3); grid.Children.Add(cell);
        }
        Content = grid;
    }
}
