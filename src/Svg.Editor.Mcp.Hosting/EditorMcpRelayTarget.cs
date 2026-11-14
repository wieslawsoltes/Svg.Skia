using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Text.Json.Nodes;
using Svg.Editor.Mcp.Transport;

namespace Svg.Editor.Mcp.Hosting;

/// <summary>Forwards editor operations to one explicitly authenticated live browser tab.</summary>
public sealed class EditorMcpRelayTarget : IEditorMcpTarget, IAsyncDisposable
{
    private readonly object _gate = new();
    private Connection? _connection;
    private bool _disposed;

    public bool IsConnected { get { lock (_gate) return _connection is not null; } }
    public event Action<string>? ResourceChanged;
    public event Action<string>? StatusChanged;

    public async ValueTask<JsonNode?> InvokeAsync(string operation, JsonObject arguments, CancellationToken cancellationToken)
    {
        Connection connection;
        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            connection = _connection ?? throw new InvalidOperationException("No editor is connected. Open MCP Connection in UnoDraw and connect using the browser attachment token.");
        }
        cancellationToken.ThrowIfCancellationRequested();
        if (!connection.TryAcquire())
            throw new InvalidOperationException("The editor bridge already has 32 pending requests.");
        var id = Guid.NewGuid().ToString("N");
        var completion = new TaskCompletionSource<JsonNode?>(TaskCreationOptions.RunContinuationsAsynchronously);
        if (!connection.Pending.TryAdd(id, completion))
        {
            connection.Release();
            throw new InvalidOperationException("Unable to allocate an editor request identifier.");
        }
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, connection.Stop.Token);
        timeout.CancelAfter(TimeSpan.FromMinutes(2));
        try
        {
            await EditorMcpBridgeProtocol.SendAsync(connection.Socket, new JsonObject
            {
                ["type"] = "invoke",
                ["id"] = id,
                ["operation"] = operation,
                ["arguments"] = arguments.DeepClone()
            }, connection.SendGate, timeout.Token).ConfigureAwait(false);
            return await completion.Task.WaitAsync(timeout.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            // A cancelled operation belongs to this connection only. It is never
            // replayed if another tab attaches while the caller is unwinding.
            await CancelAsync(connection, id).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
            if (connection.Stop.IsCancellationRequested)
                throw new IOException("The editor disconnected during the MCP operation.");
            throw new TimeoutException("The editor did not complete the MCP operation within two minutes.");
        }
        finally
        {
            connection.Pending.TryRemove(id, out _);
            connection.Release();
        }
    }

    internal async Task AttachAsync(WebSocket socket, CancellationToken cancellationToken)
    {
        Connection connection;
        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (_connection is not null)
                throw new InvalidOperationException("Another editor is already attached to this bridge.");
            connection = new Connection(socket, cancellationToken);
            _connection = connection;
        }
        try
        {
            await EditorMcpBridgeProtocol.SendAsync(socket, new JsonObject
            { ["type"] = "ready", ["version"] = EditorMcpBridgeProtocol.Version }, connection.SendGate, cancellationToken).ConfigureAwait(false);
            StatusChanged?.Invoke("Editor connected.");
            while (!connection.Stop.IsCancellationRequested)
            {
                var message = await EditorMcpBridgeProtocol.ReceiveAsync(socket, connection.Stop.Token).ConfigureAwait(false);
                if (message is null)
                    break;
                var type = EditorMcpBridgeProtocol.RequiredString(message, "type", 40);
                if (type == "resource")
                {
                    ResourceChanged?.Invoke(EditorMcpBridgeProtocol.RequiredString(message, "uri", 2048));
                    continue;
                }
                var id = EditorMcpBridgeProtocol.RequiredString(message, "id", 128);
                if (type is not ("result" or "error"))
                    throw new InvalidDataException("The editor sent an unsupported bridge message.");
                if (!connection.Pending.TryRemove(id, out var completion))
                    continue; // A completed result may race its caller's cancellation.
                if (type == "result")
                    completion.TrySetResult(message["result"]?.DeepClone());
                else
                    completion.TrySetException(new InvalidOperationException(EditorMcpBridgeProtocol.RequiredString(message, "message")));
            }
        }
        finally
        {
            lock (_gate)
                if (ReferenceEquals(_connection, connection))
                    _connection = null;
            connection.Stop.Cancel();
            foreach (var pending in connection.Pending.Values)
                pending.TrySetException(new IOException("The editor disconnected during the MCP operation."));
            connection.Pending.Clear();
            socket.Abort();
            connection.End();
            await connection.Drained.Task.ConfigureAwait(false);
            connection.Stop.Dispose();
            connection.SendGate.Dispose();
            StatusChanged?.Invoke("Editor disconnected.");
        }
    }

    private static async Task CancelAsync(Connection connection, string id)
    {
        try
        {
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(2));
            await EditorMcpBridgeProtocol.SendAsync(connection.Socket, new JsonObject
            { ["type"] = "cancel", ["id"] = id }, connection.SendGate, timeout.Token).ConfigureAwait(false);
        }
        catch (Exception error) when (error is IOException or WebSocketException or OperationCanceledException or ObjectDisposedException) { }
    }

    public ValueTask DisposeAsync()
    {
        lock (_gate)
        {
            _disposed = true;
            _connection?.Stop.Cancel();
            _connection?.Socket.Abort();
            _connection = null;
        }
        return ValueTask.CompletedTask;
    }

    private sealed class Connection
    {
        internal Connection(WebSocket socket, CancellationToken cancellationToken)
        { Socket = socket; Stop = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken); }
        internal WebSocket Socket { get; }
        internal CancellationTokenSource Stop { get; }
        internal SemaphoreSlim SendGate { get; } = new(1, 1);
        internal ConcurrentDictionary<string, TaskCompletionSource<JsonNode?>> Pending { get; } = new(StringComparer.Ordinal);
        internal TaskCompletionSource Drained { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly object _gate = new();
        private bool _accepting = true;
        private int _active;

        internal bool TryAcquire()
        {
            lock (_gate)
            {
                if (!_accepting)
                    throw new IOException("The editor bridge is disconnected.");
                if (_active == EditorMcpBridgeProtocol.MaximumPendingRequests)
                    return false;
                _active++;
                return true;
            }
        }

        internal void Release()
        {
            lock (_gate)
                if (--_active == 0 && !_accepting)
                    Drained.TrySetResult();
        }

        internal void End()
        {
            lock (_gate)
            {
                _accepting = false;
                if (_active == 0)
                    Drained.TrySetResult();
            }
        }
    }
}
