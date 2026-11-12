using Microsoft.UI.Xaml;

namespace Svg.Editor.Skia.Uno;

/// <summary>
/// Compact editor controls, menus, docking chrome and input templates. Merge after
/// XamlControlsResources at application or workspace scope. Named resources may be
/// overridden by the host; all brushes update with light, dark and high-contrast themes.
/// </summary>
public sealed partial class EditorTheme : ResourceDictionary
{
    public EditorTheme() => InitializeComponent();
}
