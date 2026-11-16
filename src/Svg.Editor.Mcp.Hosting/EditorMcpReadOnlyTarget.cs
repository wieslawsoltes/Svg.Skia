using System.Text.Json.Nodes;

namespace Svg.Editor.Mcp.Hosting;

/// <summary>Enforces the read-only catalog boundary before an operation reaches the editor.</summary>
public sealed class EditorMcpReadOnlyTarget : IEditorMcpTarget
{
    private static readonly HashSet<string> s_readOnlyOperations = EditorMcpCatalog.Tools
        .Where(tool => tool.ReadOnly).Select(tool => tool.Name).ToHashSet(StringComparer.Ordinal);

    public EditorMcpReadOnlyTarget(IEditorMcpTarget target) => Inner = target ?? throw new ArgumentNullException(nameof(target));

    internal IEditorMcpTarget Inner { get; }

    public event Action<string>? ResourceChanged
    {
        add => Inner.ResourceChanged += value;
        remove => Inner.ResourceChanged -= value;
    }

    public ValueTask<JsonNode?> InvokeAsync(string operation, JsonObject arguments, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!s_readOnlyOperations.Contains(operation))
            throw new EditorMcpException("This MCP connection is read-only. The operation is not permitted.");
        return Inner.InvokeAsync(operation, arguments, cancellationToken);
    }
}
