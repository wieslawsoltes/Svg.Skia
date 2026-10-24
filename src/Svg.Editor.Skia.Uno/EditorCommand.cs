namespace Svg.Editor.Skia.Uno;

internal sealed class EditorCommand
{
    public EditorCommand(string id, string label, string group, string shortcut, Func<Task> execute)
    {
        Id = id;
        Label = label;
        Group = group;
        Shortcut = shortcut;
        Execute = execute;
    }

    public string Id { get; }
    public string Label { get; }
    public string Group { get; }
    public string Shortcut { get; }
    public Func<Task> Execute { get; }
    public override string ToString() => string.IsNullOrEmpty(Shortcut) ? $"{Group} · {Label}" : $"{Group} · {Label}     {Shortcut}";
}
