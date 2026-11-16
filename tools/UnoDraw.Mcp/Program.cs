using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Svg.Editor.Mcp;
using Svg.Editor.Mcp.Hosting;

namespace UnoDraw.Mcp;

internal static class Program
{
    private static async Task<int> Main(string[] args)
    {
        var stdout = Console.OpenStandardOutput();
        // Reserve stdout for the MCP protocol before any host or editor library loads.
        Console.SetOut(Console.Error);
        try
        {
            var arguments = Parse(args);
            if (arguments.Help)
            {
                Console.Error.WriteLine("""
                    UnoDraw local MCP bridge

                    dotnet UnoDraw.Mcp.dll --origin https://your-editor-origin [--port 52742] [--http] [--read-only]

                    The default transport is stdio. --http keeps an authenticated Streamable
                    HTTP endpoint available until Ctrl+C. The browser attaches separately to
                    ws://127.0.0.1:52742/editor through UnoDraw's MCP Connection command.

                    Required: --origin is the browser page's exact origin, with no path.
                    Optional: --port selects a loopback port (1–65535).
                    --read-only limits clients to catalogued read-only operations and resources.
                    --describe prints the complete tool catalog as JSON on stdout and exits;
                    it needs no origin, token, browser, or native renderer.
                    Tokens: UNODRAW_MCP_TOKEN authenticates HTTP MCP clients;
                    UNODRAW_MCP_BROWSER_TOKEN authenticates the browser attachment.
                    Tokens must differ and contain 32–512 printable ASCII characters.
                    Missing tokens are generated and displayed on stderr, never stdout.
                    --help prints these instructions. All connections are local and opt-in.
                    """);
                return 0;
            }
            if (arguments.Describe)
            {
                var catalog = new JsonArray(EditorMcpCatalog.Tools.OrderBy(tool => tool.Name, StringComparer.Ordinal)
                    .Select(tool => (JsonNode)new JsonObject
                    {
                        ["name"] = tool.Name,
                        ["description"] = tool.Description,
                        ["inputSchema"] = JsonNode.Parse(tool.InputSchema.GetRawText()),
                        ["annotations"] = new JsonObject
                        {
                            ["readOnlyHint"] = tool.ReadOnly,
                            ["destructiveHint"] = tool.Destructive,
                            ["idempotentHint"] = tool.Idempotent || tool.ReadOnly,
                            ["openWorldHint"] = true
                        }
                    }).ToArray());
                await stdout.WriteAsync(JsonSerializer.SerializeToUtf8Bytes(catalog, BridgeJsonContext.Default.JsonArray));
                await stdout.WriteAsync(Encoding.UTF8.GetBytes("\n"));
                await stdout.FlushAsync();
                return 0;
            }
            using var stop = new CancellationTokenSource();
            Console.CancelKeyPress += OnCancel;
            void OnCancel(object? sender, ConsoleCancelEventArgs eventArgs)
            { eventArgs.Cancel = true; stop.Cancel(); }
            try
            {
                await using var target = new EditorMcpRelayTarget();
                target.StatusChanged += status => Console.Error.WriteLine(status);
                IEditorMcpTarget exposedTarget = arguments.ReadOnly ? new EditorMcpReadOnlyTarget(target) : target;
                await using var host = await EditorMcpHost.StartAsync(exposedTarget, new EditorMcpHostOptions
                {
                    Port = arguments.Port,
                    AllowedOrigin = arguments.Origin,
                    EnableHttp = arguments.Http,
                    Token = Environment.GetEnvironmentVariable("UNODRAW_MCP_TOKEN") ?? EditorMcpSecurity.CreateToken(),
                    BrowserToken = Environment.GetEnvironmentVariable("UNODRAW_MCP_BROWSER_TOKEN") ?? EditorMcpSecurity.CreateToken()
                }, stop.Token);
                Console.Error.WriteLine($"UnoDraw browser endpoint: {host.BrowserEndpoint}");
                Console.Error.WriteLine($"Allowed browser origin: {EditorMcpSecurity.NormalizeOrigin(arguments.Origin)}");
                Console.Error.WriteLine($"Browser attachment token: {host.BrowserToken}");
                if (arguments.Http)
                {
                    Console.Error.WriteLine($"MCP endpoint: {host.McpEndpoint}");
                    Console.Error.WriteLine($"MCP client bearer token: {host.Token}");
                    await Task.Delay(Timeout.InfiniteTimeSpan, stop.Token);
                }
                else
                {
                    Console.Error.WriteLine("MCP stdio ready. Connect the browser before invoking editor tools.");
                    await EditorMcpHost.RunStdioAsync(exposedTarget, Console.OpenStandardInput(), stdout, stop.Token);
                }
            }
            catch (OperationCanceledException) when (stop.IsCancellationRequested) { }
            finally { Console.CancelKeyPress -= OnCancel; }
            return 0;
        }
        catch (Exception error) when (error is not OutOfMemoryException)
        {
            Console.Error.WriteLine("UnoDraw MCP bridge: " + error.Message);
            return 1;
        }
    }

    private static Arguments Parse(string[] args)
    {
        var origin = (string?)null;
        var port = 52742;
        var http = false;
        var help = false;
        var readOnly = false;
        var describe = false;
        for (var index = 0; index < args.Length; index++)
        {
            switch (args[index])
            {
                case "--help" or "-h":
                    help = true;
                    break;
                case "--http":
                    http = true;
                    break;
                case "--read-only":
                    readOnly = true;
                    break;
                case "--describe":
                    describe = true;
                    break;
                case "--origin":
                    if (++index >= args.Length)
                        throw new ArgumentException("--origin requires the browser page's exact origin.");
                    origin = EditorMcpSecurity.NormalizeOrigin(args[index]);
                    break;
                case "--port":
                    if (++index >= args.Length || !int.TryParse(args[index], NumberStyles.None, CultureInfo.InvariantCulture, out port)
                        || port is < 1 or > 65535)
                        throw new ArgumentException("--port requires a TCP port between 1 and 65535.");
                    break;
                default:
                    throw new ArgumentException("Unknown argument: " + args[index] + ". Use --help for usage.");
            }
        }
        if (!help && !describe && origin is null)
            throw new ArgumentException("Supply --origin with the browser page's exact origin, for example https://wieslawsoltes.github.io.");
        return new Arguments(origin, port, http, help, readOnly, describe);
    }

    private sealed record Arguments(string? Origin, int Port, bool Http, bool Help, bool ReadOnly, bool Describe);
}
