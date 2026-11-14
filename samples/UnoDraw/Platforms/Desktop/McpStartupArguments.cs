using Svg.Editor.Skia.Uno;

namespace UnoDraw;

internal static class McpStartupArguments
{
    internal static EditorMcpConnectionSettings? Parse(IReadOnlyList<string> arguments, string? token)
    {
        EditorMcpConnectionMode? mode = null;
        var port = 52741;
        var readOnly = false;
        var hasOptions = false;
        for (var index = 0; index < arguments.Count; index++)
        {
            switch (arguments[index])
            {
                case "--mcp":
                case "--mcp-http":
                    SetMode(EditorMcpConnectionMode.LocalServer);
                    break;
                case "--mcp-stdio":
                    SetMode(EditorMcpConnectionMode.StandardInputOutput);
                    break;
                case "--mcp-port":
                    hasOptions = true;
                    if (++index >= arguments.Count || !int.TryParse(arguments[index], out port) || port is < 1024 or > 65535)
                        throw new ArgumentException("--mcp-port requires a port from 1024 to 65535.");
                    break;
                case "--mcp-read-only":
                    hasOptions = true;
                    readOnly = true;
                    break;
                default:
                    if (arguments[index].StartsWith("--mcp-", StringComparison.Ordinal))
                        throw new ArgumentException("Unknown MCP option: " + arguments[index]);
                    break;
            }
        }
        if (mode is null && hasOptions)
            throw new ArgumentException("Choose --mcp-http or --mcp-stdio before supplying MCP options.");
        return mode is { } selected
            ? new EditorMcpConnectionSettings(selected, Token: token ?? string.Empty, Port: port, ReadOnly: readOnly)
            : null;

        void SetMode(EditorMcpConnectionMode selected)
        {
            if (mode is not null && mode != selected)
                throw new ArgumentException("Choose only one of --mcp-http and --mcp-stdio.");
            mode = selected;
        }
    }
}
