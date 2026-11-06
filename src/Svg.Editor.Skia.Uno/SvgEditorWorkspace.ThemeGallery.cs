using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Svg.Editor.Skia.Uno;

public sealed partial class SvgEditorWorkspace
{
    private void InitializeThemeGallery()
    {
        InitializeStudioAppAccelerators();
        // Registered panel roots are known before they are loaded. Normalize their
        // insets once, without searching the live visual tree on selection or redraw.
        var inset = (Thickness)Resources["StudioPanelPadding"];
        var spacing = (double)Resources["StudioSectionGap"];
        foreach (var item in _studioPanels.Values)
            if (item.Content is ScrollViewer { Content: StackPanel panel })
            { panel.Margin = inset; panel.Spacing = spacing; }

        _commands.Add(new EditorCommand("control-gallery", "Control gallery", "Window", string.Empty, async () =>
        {
            var gallery = new StudioControlGallery { Width = DialogContentWidth(640) };
            var dialog = MakeDialog("Studio controls", gallery, string.Empty, "Close");
            dialog.Resources.MergedDictionaries.Add(new StudioThemeResources());
            await ShowDialogAsync(dialog);
        }));
        void Theme(string id, string label, ElementTheme theme)
            => _commands.Add(new EditorCommand(id, label, "View", string.Empty, () =>
            {
                RequestedTheme = theme;
                return Task.CompletedTask;
            }));
        Theme("theme-light", "Use light interface", ElementTheme.Light);
        Theme("theme-dark", "Use dark interface", ElementTheme.Dark);
        Theme("theme-system", "Follow system appearance", ElementTheme.Default);
    }
}
