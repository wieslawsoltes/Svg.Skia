using System.Buffers;
using System.Net.WebSockets;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization.Metadata;

namespace Svg.Editor.Mcp.Transport;

/// <summary>Private, bounded messages between an editor and its local MCP host.</summary>
public static class EditorMcpBridgeProtocol
{
    public const int Version = 1;
    public const int MaximumMessageBytes = 32 * 1024 * 1024;
    public const int MaximumPendingRequests = 32;
    private static readonly JsonTypeInfo<JsonObject> s_messageType = new EditorMcpJsonContext(new JsonSerializerOptions
    {
        // These bytes are WebSocket JSON, never HTML. Preserve base64 '+' bytes
        // so a valid 16 MiB export cannot expand past the 32 MiB frame budget.
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    }).JsonObject;

    public static async ValueTask<JsonObject?> ReceiveAsync(WebSocket socket, CancellationToken cancellationToken,
        int maximumMessageBytes = MaximumMessageBytes)
    {
        var rented = ArrayPool<byte>.Shared.Rent(16 * 1024);
        try
        {
            using var content = new MemoryStream();
            ValueWebSocketReceiveResult received;
            do
            {
                received = await socket.ReceiveAsync(rented.AsMemory(), cancellationToken).ConfigureAwait(false);
                if (received.MessageType == WebSocketMessageType.Close)
                    return null;
                if (received.MessageType != WebSocketMessageType.Text)
                    throw new InvalidDataException("The editor bridge accepts text messages only.");
                if (content.Length + received.Count > maximumMessageBytes)
                    throw new InvalidDataException("The editor bridge message exceeds its size limit.");
                content.Write(rented, 0, received.Count);
            }
            while (!received.EndOfMessage);

            return JsonNode.Parse(content.GetBuffer().AsSpan(0, checked((int)content.Length)),
                documentOptions: new JsonDocumentOptions { MaxDepth = 64 }) as JsonObject
                ?? throw new InvalidDataException("The editor bridge requires a JSON object.");
        }
        finally { ArrayPool<byte>.Shared.Return(rented); }
    }

    public static async ValueTask SendAsync(WebSocket socket, JsonObject message, SemaphoreSlim sendGate,
        CancellationToken cancellationToken)
    {
        await sendGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (socket.State != WebSocketState.Open)
                throw new IOException("The editor bridge is disconnected.");
            // Reserve the wire before allocating an encoded frame. Backpressure
            // must bound serialization buffers as well as concurrent socket writes.
            var data = JsonSerializer.SerializeToUtf8Bytes(message, s_messageType);
            if (data.Length > MaximumMessageBytes)
                throw new InvalidDataException($"The editor bridge message exceeds {MaximumMessageBytes / (1024 * 1024)} MiB.");
            await socket.SendAsync(data.AsMemory(), WebSocketMessageType.Text, true, cancellationToken).ConfigureAwait(false);
        }
        finally { sendGate.Release(); }
    }

    public static string RequiredString(JsonObject message, string name, int maximumLength = 4096)
    {
        if (message[name] is not JsonValue value || !value.TryGetValue<string>(out var text)
            || string.IsNullOrEmpty(text) || text.Length > maximumLength)
            throw new InvalidDataException($"The editor bridge requires a valid '{name}' string.");
        return text;
    }
}
