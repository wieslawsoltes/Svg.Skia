using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Svg.Editor.Skia.Uno;

/// <summary>
/// Merge after XamlControlsResources to theme an application, or within a workspace
/// to keep styling scoped. Uses native input templates; only action buttons replace
/// their visual chrome. Applications can override tokens after the dictionary.
/// </summary>
public sealed partial class StudioThemeResources : ResourceDictionary
{
    public StudioThemeResources() => InitializeComponent();
}

/// <summary>Explicit button roles for code-created studios; no visual-tree scanning.</summary>
public enum StudioActionRole { Normal, Primary, Quiet, Destructive }

public static class StudioTheme
{
    // A separate dictionary instance per control scope avoids sharing a visual parent.
    // Styles themselves are immutable and may be shared by controls on the UI thread.
    public static void ApplyRole(Button button, StudioActionRole role)
    {
        if (button is null) throw new ArgumentNullException(nameof(button));
        if (!Enum.IsDefined(typeof(StudioActionRole), role)) throw new ArgumentOutOfRangeException(nameof(role));
        var key = role switch
        {
            StudioActionRole.Primary => "StudioPrimaryButtonStyle",
            StudioActionRole.Quiet => "StudioQuietButtonStyle",
            StudioActionRole.Destructive => "StudioDangerButtonStyle",
            _ => "StudioButtonStyle"
        };
        // Reuse the host's overridable dictionary where present; standalone controls
        // acquire a scoped instance instead of altering Application.Current.Resources.
        if (Application.Current?.Resources.TryGetValue(key, out var style) == true && style is Style shared)
            button.Style = shared;
        else
        {
            var resources = new StudioThemeResources();
            button.Resources.MergedDictionaries.Add(resources);
            button.Style = (Style)resources[key];
        }
    }
}
