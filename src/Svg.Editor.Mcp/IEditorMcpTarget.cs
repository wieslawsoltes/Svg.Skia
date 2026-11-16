using System.Text.Json.Nodes;

namespace Svg.Editor.Mcp;

/// <summary>
/// Executes the shared editor command catalog against a live workspace or a headless editor.
/// Implementations serialize mutations and dispatch live editor access to the UI thread.
/// </summary>
public interface IEditorMcpTarget
{
    ValueTask<JsonNode?> InvokeAsync(string operation, JsonObject arguments, CancellationToken cancellationToken);

    /// <summary>
    /// Reports a changed resource URI, or <c>*</c> when all workspace resources may have changed.
    /// Subscribers must read the resource again; this event does not carry document content.
    /// </summary>
    event Action<string>? ResourceChanged;
}
