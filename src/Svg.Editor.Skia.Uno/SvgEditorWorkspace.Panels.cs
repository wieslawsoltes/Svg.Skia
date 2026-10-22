using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;

namespace Svg.Editor.Skia.Uno;

public sealed partial class SvgEditorWorkspace
{
    private Flyout? _panelFlyout;

    private void ShowWorkspacePanel(Border panel, string title)
    {
        // A menu item closes its owning popup after Click returns. Opening the panel
        // in that callback would let the same dismissal immediately hide this flyout.
        DispatcherQueue.TryEnqueue(() =>
        {
            if (!_disposed)
                OpenWorkspacePanel(panel, title);
        });
    }

    private void OpenWorkspacePanel(Border panel, string title)
    {
        _panelFlyout?.Hide();
        if (panel.Child is not { } content)
            return;
        panel.Child = null;
        var host = new Grid
        {
            Width = Math.Clamp(ActualWidth - 64, 240, 340),
            Height = Math.Clamp(ActualHeight - 160, 220, 660),
            RowSpacing = 8,
            RequestedTheme = ActualTheme
        };
        host.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        host.RowDefinitions.Add(new RowDefinition());
        host.Children.Add(new TextBlock { Text = title, FontSize = 16, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold });
        Grid.SetRow(content, 1);
        host.Children.Add(content);
        var flyout = new Flyout { Content = host, Placement = FlyoutPlacementMode.Full };
        _panelFlyout = flyout;
        flyout.Closed += (_, _) =>
        {
            try
            { if (!_disposed) CommitInspectorEdits(); }
            catch (Exception exception) { _ = ShowErrorAsync(exception.Message); }
            finally
            {
                host.Children.Remove(content);
                Grid.SetRow(content, 0);
                panel.Child = content;
                if (ReferenceEquals(_panelFlyout, flyout))
                    _panelFlyout = null;
            }
        };
        flyout.ShowAt(this);
    }
}
