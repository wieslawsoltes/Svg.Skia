namespace Svg.Editor.Skia.Uno;

public sealed partial class SvgEditorWorkspace
{
    partial void BuildProductCommands()
    {
        void Add(string id, string label, Action execute)
            => _commands.Add(new EditorCommand(id, label, "Object", string.Empty, () =>
            {
                execute();
                return Task.CompletedTask;
            }));
        _commands.Add(new EditorCommand("filterstudio", "Filter Studio…", "Object", string.Empty, FilterStudioAsync));
        Add("removefilters", "Remove filters", () => Controller?.RemoveSelectionFilters());
        Add("alphamask", "Create alpha mask", () => Controller?.CreateOpacityMask(false));
        Add("luminancemask", "Create luminance mask", () => Controller?.CreateOpacityMask(true));
        Add("releasemask", "Release mask and restore artwork", () => Controller?.ReleaseOpacityMasks(true));
        Add("disablemask", "Disable mask", () => Controller?.ReleaseOpacityMasks(false));
    }
}
