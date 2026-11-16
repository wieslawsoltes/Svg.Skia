using Svg.Editor.Mcp.Hosting;
using Svg.Editor.Skia.Uno;

namespace UnoDraw;

public sealed partial class App
{
    internal static EditorMcpConnectionSettings? McpStartup { get; set; }
    internal static Stream? McpStandardInput { get; set; }
    internal static Stream? McpStandardOutput { get; set; }

    partial void ConfigureMcpConnection(UnoDrawMcpConnection connection)
    {
        connection.StartupSettings = McpStartup;
        connection.StartLocalHost = async (target, settings, cancellationToken) =>
        {
            var options = new EditorMcpHostOptions
            {
                Port = settings.Port,
                Token = string.IsNullOrWhiteSpace(settings.Token) ? EditorMcpSecurity.CreateToken() : settings.Token
            };
            var host = await EditorMcpHost.StartAsync(target, options, cancellationToken);
            return new McpHostSession(host, host.McpEndpoint.ToString(), host.Token);
        };
        if (McpStandardInput is { } input && McpStandardOutput is { } output)
            connection.RunStandardInputOutput = (target, cancellationToken) =>
                EditorMcpHost.RunStdioAsync(target, input, output, cancellationToken);
    }
}
