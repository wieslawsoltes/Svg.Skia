using Microsoft.UI.Xaml.Controls;

namespace Svg.Editor.Skia.Uno;

public sealed partial class SvgEditorWorkspace
{
    private void ShowWorkspacePanel(Border panel, string title)
    {
        var id = ReferenceEquals(panel, OutlinePanel) ? "layers"
            : ReferenceEquals(panel, InspectorPanel) ? "properties"
            : ReferenceEquals(panel, AttributesPanel) ? "attributes" : "resources";
        // The retained in-workspace overlay remains usable beside the canvas at narrow
        // widths. No popup close event can accidentally return a live view to two parents.
        DispatcherQueue.TryEnqueue(() => { if (!_disposed) ApplySafely(() => OpenStudioPanel(id)); });
    }
}
