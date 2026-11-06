using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Windows.System;

namespace Svg.Editor.Skia.Uno;

public sealed partial class StudioDockGroup
{
    private MenuFlyout? _panelSwitcher;
    private int _panelSwitcherVersion;

    private void ClosePanelSwitcher()
    {
        _panelSwitcherVersion++;
        var previous = _panelSwitcher;
        _panelSwitcher = null;
        previous?.Hide();
    }

    private void OpenPanelSwitcher(FrameworkElement anchor)
    {
        ClosePanelSwitcher();
        if (_items.Count == 0) return;
        var version = _panelSwitcherVersion;
        var menu = _panelSwitcher = new MenuFlyout();
        var entries = new List<ToggleMenuFlyoutItem>();
        string? requested = null;
        var open = false;
        foreach (var panel in _items.ToArray())
        {
            var entry = new ToggleMenuFlyoutItem { Text = panel.Title, IsChecked = panel.Id == _activeId };
            entry.Click += (_, _) =>
            {
                requested = panel.Id;
                // Defer panel activation until the popup has released input/focus.
                // This is also explicit for platforms where toggles may stay open.
                menu.Hide();
            };
            entry.AddHandler(KeyDownEvent, new KeyEventHandler((_, args) =>
            {
                if (args.Key is not (VirtualKey.Home or VirtualKey.End) || entries.Count == 0) return;
                var target = args.Key == VirtualKey.Home ? entries[0] : entries[entries.Count - 1];
                target.Focus(FocusState.Keyboard);
                args.Handled = true;
            }), true);
            entries.Add(entry); menu.Items.Add(entry);
        }
        menu.Opened += (_, _) =>
        {
            open = true;
            DispatcherQueue.TryEnqueue(() =>
            {
                if (!open || version != _panelSwitcherVersion || !IsLoaded) return;
                (entries.FirstOrDefault(item => item.IsChecked) ?? entries[0]).Focus(FocusState.Programmatic);
            });
        };
        menu.Closed += (_, _) =>
        {
            open = false;
            if (ReferenceEquals(_panelSwitcher, menu)) _panelSwitcher = null;
            DispatcherQueue.TryEnqueue(() =>
            {
                if (version != _panelSwitcherVersion || !IsLoaded || XamlRoot is not { } root) return;
                var focused = FocusManager.GetFocusedElement(root) as DependencyObject;
                // Do not steal focus from a dialog that opened while the flyout closed.
                for (var current = focused; current is not null; current = VisualTreeHelper.GetParent(current))
                    if (current is ContentDialog) return;
                if (requested is { } id)
                    Activate(id);
                else if (focused is FrameworkElement { IsLoaded: true } && !ReferenceEquals(focused, anchor) &&
                         !entries.Any(item => ReferenceEquals(item, focused)))
                    return; // Preserve an outside click's new focus target.
                if (_activeId is { } active && _tabs.TryGetValue(active, out var tab) && tab.IsLoaded)
                    tab.Focus(FocusState.Keyboard);
            });
        };
        menu.ShowAt(anchor);
    }
}
