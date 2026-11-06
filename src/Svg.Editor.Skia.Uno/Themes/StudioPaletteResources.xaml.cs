using Microsoft.UI.Xaml;

namespace Svg.Editor.Skia.Uno;

/// <summary>
/// Semantic studio colors and density tokens. Merge before studio styles; custom
/// controls also load this palette so they work outside the complete workspace.
/// Consumers may override named resources after this dictionary in their own scope.
/// </summary>
public sealed partial class StudioPaletteResources : ResourceDictionary
{
    public StudioPaletteResources() => InitializeComponent();
}
