using System.Text.Json;

namespace Svg.Editor.Mcp;

/// <summary>A stable, explicitly described operation; discovery does not use reflection.</summary>
public sealed record EditorMcpTool(
    string Name,
    string Description,
    JsonElement InputSchema,
    bool ReadOnly = false,
    bool Destructive = false,
    bool Idempotent = false);

/// <summary>A deliberate editor failure whose message can be returned to an MCP client.</summary>
public sealed class EditorMcpException : Exception
{
    public EditorMcpException(string message) : base(message)
    {
    }
}
