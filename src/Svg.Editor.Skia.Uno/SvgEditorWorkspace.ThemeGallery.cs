using Microsoft.UI.Xaml;

namespace Svg.Editor.Skia.Uno;

public sealed partial class SvgEditorWorkspace
{
    private void InitializeThemeGallery()
    {
        _commands.Add(new EditorCommand("control-gallery", "Control gallery", "Window", string.Empty, async () =>
        {
            var gallery = new StudioControlGallery { Width = DialogContentWidth(640) };
            var dialog = MakeDialog("Studio controls", gallery, string.Empty, "Close");
            // An embedded workspace may not own the application's resource dictionary.
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
