using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Svg.Editor.Mcp.Transport;

namespace Svg.Editor.Mcp.Hosting;

/// <summary>Bounds response buffers even when an authenticated HTTP client stops reading.</summary>
internal sealed class EditorMcpHttpAdmission
{
    private const int PrefixLimit = 16 * 1024;
    private readonly SemaphoreSlim _classifiers = new(8, 8);
    private readonly SemaphoreSlim _responses = new(2, 2);
    private readonly SemaphoreSlim _subscriptions = new(8, 8);
    private readonly SemaphoreSlim _controls = new(4, 4);

    internal async Task InvokeAsync(HttpContext context, RequestDelegate next)
    {
        if (!HttpMethods.IsPost(context.Request.Method) || context.Request.Path != "/mcp")
        {
            await next(context).ConfigureAwait(false);
            return;
        }
        if (!await _classifiers.WaitAsync(0, context.RequestAborted).ConfigureAwait(false))
        {
            Reject(context);
            return;
        }
        string? method;
        try
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(context.RequestAborted);
            timeout.CancelAfter(TimeSpan.FromSeconds(5));
            method = await ReadSmallMessageMethodAsync(context.Request, timeout.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (!context.RequestAborted.IsCancellationRequested)
        {
            context.Response.StatusCode = StatusCodes.Status408RequestTimeout;
            return;
        }
        finally { _classifiers.Release(); }

        // Header-only classification is unsafe for legacy clients: those revisions
        // do not require the SDK to verify MCP-Method against the actual body.
        var gate = method == "subscriptions/listen" ? _subscriptions
            : method is "initialize" or "server/discover" or "ping" or "resources/subscribe" or "resources/unsubscribe"
                || method?.StartsWith("notifications/", StringComparison.Ordinal) == true ? _controls : _responses;
        if (!await gate.WaitAsync(0, context.RequestAborted).ConfigureAwait(false))
        {
            Reject(context);
            return;
        }
        try
        {
            // Retain admission through SDK serialization and the complete response
            // write, rather than releasing it when the editor returns a large image.
            await next(context).ConfigureAwait(false);
        }
        finally { gate.Release(); }
    }

    private static async Task<string?> ReadSmallMessageMethodAsync(HttpRequest request, CancellationToken cancellationToken)
    {
        if (request.ContentLength is > PrefixLimit)
            return null;
        request.EnableBuffering(32 * 1024, EditorMcpBridgeProtocol.MaximumMessageBytes);
        try
        {
            var prefix = new byte[PrefixLimit + 1];
            var length = 0;
            while (length < prefix.Length)
            {
                var count = await request.Body.ReadAsync(prefix.AsMemory(length), cancellationToken).ConfigureAwait(false);
                if (count == 0)
                    break;
                length += count;
            }
            // A large request always consumes an ordinary response slot. Small
            // cancellation and subscription messages need no full-body allocation.
            if (length > PrefixLimit)
                return null;
            try
            {
                using var json = JsonDocument.Parse(prefix.AsMemory(0, length), new JsonDocumentOptions { MaxDepth = 64 });
                return json.RootElement.ValueKind == JsonValueKind.Object
                    && json.RootElement.TryGetProperty("method", out var method) && method.ValueKind == JsonValueKind.String
                    ? method.GetString() : null;
            }
            catch (JsonException)
            {
                return null; // The SDK reports the actual protocol parse error.
            }
        }
        finally { request.Body.Position = 0; }
    }

    private static void Reject(HttpContext context)
    {
        context.Response.StatusCode = StatusCodes.Status429TooManyRequests;
        context.Response.Headers.RetryAfter = "1";
    }
}
