namespace Svg.Editor.Skia.Uno;

public sealed partial class SvgEditorWorkspace
{
    private void InitializeThemeGallery()
    {
        _commands.Add(new EditorCommand("control-gallery", "Control gallery", "Window", string.Empty, async () =>
        {
            var gallery = new StudioControlGallery { Width = DialogContentWidth(640) };
            var dialog = MakeDialog("Studio controls", gallery, string.Empty, "Close");
            // A reusable embedded workspace may not own the application's resources.
            dialog.Resources.MergedDictionaries.Add(new StudioThemeResources());
            await ShowDialogAsync(dialog);
        }));
    }
}
