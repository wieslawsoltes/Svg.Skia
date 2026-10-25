using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.Foundation;

namespace Svg.Editor.Skia.Uno;

public sealed partial class SvgEditorWorkspace
{
    private int _appearanceRevealRequest;

    /// <summary>
    /// Scrolls an explicitly requested section to the top after docking/expansion has
    /// been measured. Immediate bring-into-view can use the old scroll extent while
    /// a retained panel is moving into its compact overlay, revealing the wrong editor.
    /// </summary>
    private void RevealAppearanceSection(StudioSection section)
    {
        section.IsExpanded = true;
        var request = ++_appearanceRevealRequest;
        DispatcherQueue.TryEnqueue(DispatcherQueuePriority.Low, () =>
        {
            if (_disposed || request != _appearanceRevealRequest || !section.IsLoaded) return;
            if (_studioPanels["appearance"].Content is not ScrollViewer { Content: FrameworkElement content } viewer) return;
            viewer.UpdateLayout();
            if (!section.IsLoaded || section.ActualHeight <= 0) return;
            var offset = section.TransformToVisual(content).TransformPoint(new Point(0, 0)).Y;
            if (!double.IsFinite(offset)) return;
            viewer.ChangeView(null, Math.Clamp(offset, 0, Math.Max(0, viewer.ScrollableHeight)), null, disableAnimation: true);
        });
    }
}
