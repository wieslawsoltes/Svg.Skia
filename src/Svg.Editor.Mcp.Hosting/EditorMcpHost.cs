using System.Net;
using System.Net.WebSockets;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.AspNetCore;
using ModelContextProtocol.Server;
using Svg.Editor.Mcp.Transport;

namespace Svg.Editor.Mcp.Hosting;

/// <summary>An explicitly enabled loopback MCP endpoint for a live editor or browser relay.</summary>
public sealed class EditorMcpHost : IAsyncDisposable
{
    private readonly WebApplication _application;
    private readonly CancellationTokenSource _stop;
    private int _disposed;

    private EditorMcpHost(WebApplication application, CancellationTokenSource stop, EditorMcpHostOptions options, Uri address)
    {
        _application = application;
        _stop = stop;
        Token = options.Token;
        BrowserToken = options.BrowserToken;
        McpEndpoint = new Uri(address, "/mcp");
        BrowserEndpoint = new UriBuilder(address) { Scheme = "ws", Path = "/editor" }.Uri;
    }

    public string Token { get; }
    public string BrowserToken { get; }
    public Uri McpEndpoint { get; }
    public Uri BrowserEndpoint { get; }

    public static async Task<EditorMcpHost> StartAsync(IEditorMcpTarget target, EditorMcpHostOptions options,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(options);
        options.Validate();
        var origin = EditorMcpSecurity.NormalizeOrigin(options.AllowedOrigin);
        var relayTarget = target as EditorMcpRelayTarget
            ?? (target as EditorMcpReadOnlyTarget)?.Inner as EditorMcpRelayTarget;
        if (relayTarget is not null && origin is null)
            throw new ArgumentException("A browser relay requires an explicit allowed browser origin.", nameof(options));
        var stop = new CancellationTokenSource();
        var builder = WebApplication.CreateSlimBuilder(new WebApplicationOptions
        {
            Args = [],
            ApplicationName = typeof(EditorMcpHost).Assembly.GetName().Name
        });
        // GUI applications and stdio MCP transports share stdout. Host diagnostics
        // must never corrupt the client's JSON-RPC stream or reveal bearer tokens.
        builder.Logging.ClearProviders();
        builder.WebHost.ConfigureKestrel(server =>
        {
            server.Listen(IPAddress.Loopback, options.Port);
            server.Limits.MaxRequestBodySize = EditorMcpBridgeProtocol.MaximumMessageBytes;
            server.Limits.MaxConcurrentConnections = 64;
            server.Limits.MaxConcurrentUpgradedConnections = 4;
            server.Limits.RequestHeadersTimeout = TimeSpan.FromSeconds(15);
        });
        builder.Services.AddMcpServer().WithHttpTransport(transport =>
        {
            transport.SessionMode = HttpServerSessionMode.StatefulForInitializeClients;
            transport.ConfigureSessionOptions = (_, serverOptions, _) =>
            {
                EditorMcpServer.Configure(serverOptions, target, stop.Token);
                return Task.CompletedTask;
            };
        });
        var application = builder.Build();
        application.UseWebSockets(new WebSocketOptions { KeepAliveInterval = TimeSpan.FromSeconds(30) });
        application.Use(async (context, next) =>
        {
            if (context.Connection.RemoteIpAddress is not { } peer || !IPAddress.IsLoopback(peer)
                || !EditorMcpSecurity.IsLoopbackHost(context.Request.Host.Host)
                || context.Request.Host.Port != context.Connection.LocalPort)
            {
                context.Response.StatusCode = StatusCodes.Status403Forbidden;
                return;
            }
            var requestOrigins = context.Request.Headers.Origin;
            var requestOrigin = requestOrigins.Count == 1 ? requestOrigins[0] : null;
            if (requestOrigins.Count > 1 || requestOrigins.Count == 1 && (origin is null
                || !string.Equals(requestOrigin, origin, StringComparison.Ordinal)))
            {
                context.Response.StatusCode = StatusCodes.Status403Forbidden;
                return;
            }
            context.Response.Headers.CacheControl = "no-store";
            context.Response.Headers["X-Content-Type-Options"] = "nosniff";
            if (requestOrigin is not null)
            {
                context.Response.Headers.AccessControlAllowOrigin = origin;
                context.Response.Headers.Vary = "Origin";
                context.Response.Headers.AccessControlExposeHeaders = "Mcp-Session-Id, MCP-Protocol-Version";
            }
            if (context.Request.Method == "OPTIONS" && requestOrigin is not null)
            {
                context.Response.Headers.AccessControlAllowMethods = "POST, GET, DELETE, OPTIONS";
                context.Response.Headers.AccessControlAllowHeaders = "Authorization, Content-Type, Accept, MCP-Protocol-Version, Mcp-Session-Id, Last-Event-ID, MCP-Method, MCP-Name, MCP-Client-Capabilities";
                context.Response.StatusCode = StatusCodes.Status204NoContent;
                return;
            }
            if (context.Request.Path == "/editor")
            {
                if (relayTarget is null || requestOrigin is null || !context.WebSockets.IsWebSocketRequest)
                {
                    context.Response.StatusCode = StatusCodes.Status403Forbidden;
                    return;
                }
                if (relayTarget.IsConnected)
                {
                    context.Response.StatusCode = StatusCodes.Status409Conflict;
                    return;
                }
            }
            else
            {
                if (!options.EnableHttp || context.Request.Path != "/mcp")
                {
                    context.Response.StatusCode = StatusCodes.Status404NotFound;
                    return;
                }
                var authorization = context.Request.Headers.Authorization;
                var supplied = authorization.Count == 1 && authorization[0] is { } header
                    && header.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase) ? header[7..] : null;
                if (!EditorMcpSecurity.TokenMatches(options.Token, supplied))
                {
                    context.Response.Headers.WWWAuthenticate = "Bearer realm=\"UnoDraw\"";
                    context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                    return;
                }
            }
            await next(context).ConfigureAwait(false);
        });
        var admission = new EditorMcpHttpAdmission();
        application.Use((context, next) => admission.InvokeAsync(context, next));
        if (relayTarget is not null)
        {
            application.Map("/editor", async context =>
            {
                using var socket = await context.WebSockets.AcceptWebSocketAsync().ConfigureAwait(false);
                using var timeout = CancellationTokenSource.CreateLinkedTokenSource(context.RequestAborted, stop.Token);
                timeout.CancelAfter(TimeSpan.FromSeconds(10));
                try
                {
                    var message = await EditorMcpBridgeProtocol.ReceiveAsync(socket, timeout.Token, 4096).ConfigureAwait(false);
                    if (message?["type"]?.GetValue<string>() != "authenticate"
                        || message["version"]?.GetValue<int>() != EditorMcpBridgeProtocol.Version
                        || !EditorMcpSecurity.TokenMatches(options.BrowserToken, message["token"]?.GetValue<string>()))
                    {
                        await socket.CloseAsync(WebSocketCloseStatus.PolicyViolation, "Connection rejected.", timeout.Token).ConfigureAwait(false);
                        return;
                    }
                    timeout.CancelAfter(Timeout.InfiniteTimeSpan);
                    await relayTarget.AttachAsync(socket, timeout.Token).ConfigureAwait(false);
                }
                catch (Exception error) when (error is WebSocketException or OperationCanceledException or InvalidDataException
                    or InvalidOperationException or System.Text.Json.JsonException or IOException)
                {
                    socket.Abort();
                }
            });
        }
        application.MapMcp("/mcp");
        try
        {
            await application.StartAsync(cancellationToken).ConfigureAwait(false);
            var addresses = application.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>();
            var address = new Uri(addresses?.Addresses.Single() ?? throw new InvalidOperationException("The MCP host did not bind its loopback endpoint."));
            return new EditorMcpHost(application, stop, options, address);
        }
        catch
        {
            stop.Cancel();
            await application.DisposeAsync().ConfigureAwait(false);
            stop.Dispose();
            throw;
        }
    }

    public static async Task RunStdioAsync(IEditorMcpTarget target, Stream stdin, Stream stdout,
        CancellationToken cancellationToken = default)
    {
        using var stop = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        await using var transport = new StreamServerTransport(stdin, stdout, "UnoDraw");
        await using var server = EditorMcpServer.Create(transport, target, stop.Token);
        try
        { await server.RunAsync(stop.Token).ConfigureAwait(false); }
        finally { stop.Cancel(); }
    }

    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
            return;
        _stop.Cancel();
        try
        {
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            await _application.StopAsync(timeout.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) { }
        finally
        {
            await _application.DisposeAsync().ConfigureAwait(false);
            _stop.Dispose();
        }
    }
}
