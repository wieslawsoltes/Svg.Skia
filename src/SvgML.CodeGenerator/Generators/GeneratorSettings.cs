
namespace CodeGenerator;

internal class GeneratorSettings
{
    // C# reserved words
    public HashSet<string> ReservedWords { get; init; } =
    [
        "in",
        "operator",
        "switch"
    ];

    public string ElementBaseType { get; init; } = "Avalonia.Controls.Control";

    public string BaseWriterType { get; init; } = "element";

    public string BasePath { get; init; } = "Generated";

    public string Namespace { get; init; } = "SvgML";

    public TypeDef[] TypeDefs { get; init; } = [];
}
