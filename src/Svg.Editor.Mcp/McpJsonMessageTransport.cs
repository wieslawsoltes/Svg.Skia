using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using System.Threading.Channels;
using ModelContextProtocol;
using ModelContextProtocol.Protocol;

namespace Svg.Editor.Mcp;

/// <summary>
/// An in-process message transport for browser integrations and protocol tests.
/// Each ingress/egress value is one complete UTF-8 JSON-RPC message, never a batch.
/// Network hosts must authenticate their attachment before passing messages here.
/// </summary>
public sealed class McpJsonMessageTransport : ITransport
{
    public const int MaximumMessageBytes = 32 * 1024 * 1024;

    private static readonly JsonTypeInfo<JsonRpcMessage> s_incomingMessageTypeInfo =
        (JsonTypeInfo<JsonRpcMessage>)McpJsonUtilities.ProtocolOptions.GetTypeInfo(typeof(JsonRpcMessage));
    private static readonly JsonTypeInfo<JsonRpcMessage> s_outgoingMessageTypeInfo =
        (JsonTypeInfo<JsonRpcMessage>)new JsonSerializerOptions(McpJsonUtilities.ProtocolOptions)
        {
            // This is a JSON message channel, never HTML. Avoid expanding '+' in large
            // base64 exports to six bytes while retaining ordinary JSON string escaping.
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
        }.GetTypeInfo(typeof(JsonRpcMessage));

    private readonly Channel<JsonRpcMessage> _incoming;
    private readonly Func<string, CancellationToken, Task> _send;
    private readonly SemaphoreSlim _sendGate = new(1, 1);
    private int _disposed;

    public McpJsonMessageTransport(Func<string, CancellationToken, Task> send, int capacity = 32)
    {
        ArgumentNullException.ThrowIfNull(send);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(capacity);
        _send = send;
        _incoming = Channel.CreateBounded<JsonRpcMessage>(new BoundedChannelOptions(capacity)
        {
            FullMode = BoundedChannelFullMode.Wait,
            SingleReader = true,
            SingleWriter = false,
            AllowSynchronousContinuations = false
        });
    }

    public ChannelReader<JsonRpcMessage> MessageReader => _incoming.Reader;

    public string? SessionId => null;

    public async ValueTask ReceiveAsync(string json, CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) != 0, this);
        ArgumentNullException.ThrowIfNull(json);
        if (json.Length > MaximumMessageBytes || Encoding.UTF8.GetByteCount(json) > MaximumMessageBytes)
        {
            await SendProtocolErrorAsync(McpErrorCode.InvalidRequest, "The MCP message exceeds the 32 MiB limit.", cancellationToken).ConfigureAwait(false);
            return;
        }

        JsonRpcMessage message;
        try
        {
            message = JsonSerializer.Deserialize(json, s_incomingMessageTypeInfo)
                ?? throw new JsonException("Expected a JSON-RPC message.");
        }
        catch (JsonException)
        {
            var code = McpErrorCode.InvalidRequest;
            try
            {
                using var _ = JsonDocument.Parse(json);
            }
            catch (JsonException)
            {
                code = McpErrorCode.ParseError;
            }

            await SendProtocolErrorAsync(code, "Invalid JSON-RPC message.", cancellationToken).ConfigureAwait(false);
            return;
        }

        await _incoming.Writer.WriteAsync(message, cancellationToken).ConfigureAwait(false);
    }

    public async Task SendMessageAsync(JsonRpcMessage message, CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) != 0, this);
        ArgumentNullException.ThrowIfNull(message);
        await _sendGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) != 0, this);
            // Hold the gate while encoding as well as sending, so a slow recipient
            // cannot cause every waiting response to allocate another large string.
            var json = JsonSerializer.Serialize(message, s_outgoingMessageTypeInfo);
            if (json.Length > MaximumMessageBytes || Encoding.UTF8.GetByteCount(json) > MaximumMessageBytes)
            {
                throw new InvalidOperationException("The MCP response exceeds the 32 MiB transport limit.");
            }

            await _send(json, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _sendGate.Release();
        }
    }

    public ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposed, 1) == 0)
        {
            _incoming.Writer.TryComplete();
        }

        return ValueTask.CompletedTask;
    }

    private Task SendProtocolErrorAsync(McpErrorCode code, string message, CancellationToken cancellationToken)
    {
        return SendMessageAsync(new JsonRpcError
        {
            Error = new JsonRpcErrorDetail { Code = (int)code, Message = message }
        }, cancellationToken);
    }
}
