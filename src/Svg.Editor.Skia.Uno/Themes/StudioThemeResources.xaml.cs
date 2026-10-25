using System.Runtime.CompilerServices;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;

namespace Svg.Editor.Skia.Uno;

/// <summary>
/// Merge after XamlControlsResources at application or workspace scope. Native input
/// templates remain intact. Consumers can override named resources in their scope.
/// </summary>
public sealed partial class StudioThemeResources : ResourceDictionary
{
    public StudioThemeResources() => InitializeComponent();
}

public enum StudioActionRole { Normal, Primary, Quiet, Destructive }

public static class StudioTheme
{
    private static readonly ConditionalWeakTable<Button, StudioThemeResources> Fallbacks = new();

    /// <summary>
    /// Applies an explicit action role using the button's resource scope before the
    /// application. Pass the owning editor as scope when building an unparented button.
    /// Call again after intentionally moving the button into a different style scope.
    /// </summary>
    public static void ApplyRole(Button button, StudioActionRole role, FrameworkElement? scope = null)
    {
        ArgumentNullException.ThrowIfNull(button);
        if (!Enum.IsDefined(typeof(StudioActionRole), role)) throw new ArgumentOutOfRangeException(nameof(role));
        var key = role switch
        {
            StudioActionRole.Primary => "StudioPrimaryButtonStyle",
            StudioActionRole.Quiet => "StudioQuietButtonStyle",
            StudioActionRole.Destructive => "StudioDangerButtonStyle",
            _ => "StudioButtonStyle"
        };
        for (DependencyObject? current = scope ?? button; current is not null;)
        {
            if (current is FrameworkElement element && element.Resources.TryGetValue(key, out var value) && value is Style local)
            { button.Style = local; return; }
            current = current is FrameworkElement { Parent: { } parent } ? parent : VisualTreeHelper.GetParent(current);
        }
        if (Application.Current?.Resources.TryGetValue(key, out var shared) == true && shared is Style style)
        { button.Style = style; return; }

        // The fallback is weakly owned, UI-thread local to the button, and created once
        // even when its role changes. It never modifies Application.Current.Resources.
        var resources = Fallbacks.GetValue(button, owner =>
        {
            var dictionary = new StudioThemeResources();
            owner.Resources.MergedDictionaries.Add(dictionary);
            return dictionary;
        });
        button.Style = (Style)resources[key];
    }
}
