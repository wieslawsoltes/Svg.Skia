using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Input;
using Windows.System;

namespace Svg.Editor.Skia.Uno;

/// <summary>
/// Retained text field with explicit commit boundaries. Validation failures retain the
/// user's text; Escape restores the last synchronized value without creating an edit.
/// </summary>
public sealed class StudioTextField : UserControl
{
    private readonly TextBox _input;
    private string _baseline = string.Empty;
    private bool _committing;
    public event EventHandler<string>? TextEdited;
    public event EventHandler<Exception>? ValidationFailed;
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
            if (args.Key == VirtualKey.Escape) { _input.Text = _baseline; args.Handled = true; }
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
    }
    public void Commit()
    {
        if (_committing || !IsEnabled || !HasPendingEdit) return;
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
    private int _selectedIndex = 0;
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
        var grid = new Grid { Width = 78, Height = 66, ColumnSpacing = 2, RowSpacing = 2 };
        for (var i = 0; i < 3; i++) { grid.ColumnDefinitions.Add(new ColumnDefinition()); grid.RowDefinitions.Add(new RowDefinition()); }
        var labels = new[] { "Top left", "Top center", "Top right", "Middle left", "Center", "Middle right", "Bottom left", "Bottom center", "Bottom right" };
        for (var i = 0; i < 9; i++)
        {
            var index = i;
            var button = _buttons[i] = new ToggleButton { Content = "·", MinWidth = 0, MinHeight = 0, Padding = new Thickness(0), CornerRadius = new CornerRadius(2), IsChecked = i == 0 };
            AutomationProperties.SetName(button, labels[i] + " transform reference");
            ToolTipService.SetToolTip(button, labels[i]);
            button.Click += (_, _) => SelectedIndex = index;
            Grid.SetRow(button, i / 3); Grid.SetColumn(button, i % 3); grid.Children.Add(button);
        }
        Content = grid;
    }
}
