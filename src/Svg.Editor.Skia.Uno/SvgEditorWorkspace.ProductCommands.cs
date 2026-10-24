namespace Svg.Editor.Skia.Uno;

public sealed partial class SvgEditorWorkspace
{
    partial void BuildProductCommands()
    {
        _commands.Add(new EditorCommand("filterstudio", "Filter Studio…", "Object", string.Empty, FilterStudioAsync));
        _commands.Add(new EditorCommand("removefilters", "Remove filters", "Object", string.Empty, () =>
        {
            Controller?.RemoveSelectionFilters();
            return Task.CompletedTask;
        }));
    }
}
