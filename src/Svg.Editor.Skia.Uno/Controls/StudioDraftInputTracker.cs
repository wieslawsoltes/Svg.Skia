using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;

namespace Svg.Editor.Skia.Uno;

/// <summary>Retains uncommitted native text input without replacing native parsing or composition.</summary>
internal sealed class StudioDraftInputTracker : IDisposable
{
    private readonly FrameworkElement _owner;
    private readonly Func<bool> _synchronizing;
    private readonly Action _changed;
    private readonly HashSet<TextBox> _inputs = new();
    private readonly HashSet<TextBox> _pendingInputs = new();
    private bool _disposed;

    public StudioDraftInputTracker(FrameworkElement owner, Func<bool> synchronizing, Action changed)
    {
        _owner = owner;
        _synchronizing = synchronizing;
        _changed = changed;
        owner.Loaded += OnFocusOrLoaded;
        owner.GotFocus += OnFocusOrLoaded;
        owner.Unloaded += OnUnloaded;
    }

    public bool HasPendingInput => _pendingInputs.Count > 0;

    public void Clear() => _pendingInputs.Clear();

    private void OnFocusOrLoaded(object sender, RoutedEventArgs args)
    {
        if (_disposed)
            return;
        var current = new HashSet<TextBox>();
        void Visit(DependencyObject element)
        {
            if (element is TextBox input)
                current.Add(input);
            for (var index = 0; index < VisualTreeHelper.GetChildrenCount(element); index++)
                Visit(VisualTreeHelper.GetChild(element, index));
        }
        Visit(_owner);
        foreach (var input in _inputs.Where(input => !current.Contains(input)).ToArray())
        {
            input.TextChanging -= OnTextChanging;
            input.LostFocus -= OnInputLostFocus;
            _inputs.Remove(input);
        }
        foreach (var input in current)
            if (_inputs.Add(input))
            {
                input.TextChanging += OnTextChanging;
                input.LostFocus += OnInputLostFocus;
            }
    }

    private void OnTextChanging(TextBox sender, TextBoxTextChangingEventArgs args)
    {
        if (_disposed || _synchronizing() || sender.FocusState == FocusState.Unfocused)
            return;
        _pendingInputs.Add(sender);
        _changed();
    }

    private void OnInputLostFocus(object sender, RoutedEventArgs args)
    {
        if (_disposed || sender is not TextBox input || !_pendingInputs.Contains(input))
            return;
        // ColorPicker accepts channel/hex changes as they are typed and restores
        // invalid text in its own LostFocus handler. Observe that native result;
        // same-value input must not leave an otherwise clean draft blocked.
        for (DependencyObject? parent = VisualTreeHelper.GetParent(input); parent is not null; parent = VisualTreeHelper.GetParent(parent))
        {
            if (parent is ColorPicker)
            {
                _pendingInputs.Remove(input);
                _changed();
                return;
            }
            if (ReferenceEquals(parent, _owner))
                return;
        }
    }

    private void DetachInputs()
    {
        foreach (var input in _inputs)
        {
            input.TextChanging -= OnTextChanging;
            input.LostFocus -= OnInputLostFocus;
        }
        _inputs.Clear();
    }

    private void OnUnloaded(object sender, RoutedEventArgs args) => DetachInputs();

    public void Dispose()
    {
        if (_disposed)
            return;
        _disposed = true;
        _owner.Loaded -= OnFocusOrLoaded;
        _owner.GotFocus -= OnFocusOrLoaded;
        _owner.Unloaded -= OnUnloaded;
        DetachInputs();
        _pendingInputs.Clear();
    }
}
