using System.Collections.Concurrent;
using System.Net;
using System.Net.WebSockets;
using System.Text.Json.Nodes;
using System.Threading.Channels;

namespace Svg.Editor.Mcp.Transport;

/// <summary>Connects a live editor to a local MCP bridge without listening in the browser.</summary>
public sealed class EditorMcpBridgeClient : IAsyncDisposable
{
    private readonly IEditorMcpTarget _target;
    private readonly string? _nativeOrigin;
    private readonly SemaphoreSlim _lifecycleGate = new(1, 1);
    private Connection? _connection;
    private bool _disposed;

    public EditorMcpBridgeClient(IEditorMcpTarget target, string? nativeOrigin = null)
    {
        _target = target ?? throw new ArgumentNullException(nameof(target));
        if (nativeOrigin is not null && OperatingSystem.IsBrowser())
            throw new ArgumentException("Browser WebSocket origins are assigned by the browser.", nameof(nativeOrigin));
        _nativeOrigin = nativeOrigin;
    }

    public bool IsConnected => _connection is { Socket.State: WebSocketState.Open, Authenticated: true };
    public Task Completion => _connection?.Reader ?? Task.CompletedTask;
    public event Action<string>? StatusChanged;

    public async Task ConnectAsync(Uri endpoint, string token, CancellationToken cancellationToken = default)
    {
        ValidateEndpoint(endpoint);
        if (string.IsNullOrWhiteSpace(token) || token.Length > 512)
            throw new ArgumentException("Enter the connection token displayed by the local bridge.", nameof(token));
        await _lifecycleGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (_connection is not null)
                throw new InvalidOperationException("Disconnect the current MCP bridge before connecting another.");
            var connection = new Connection();
            try
            {
                if (_nativeOrigin is not null && !OperatingSystem.IsBrowser())
                    connection.Socket.Options.SetRequestHeader("Origin", _nativeOrigin);
                using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                timeout.CancelAfter(TimeSpan.FromSeconds(15));
                await connection.Socket.ConnectAsync(endpoint, timeout.Token).ConfigureAwait(false);
                await EditorMcpBridgeProtocol.SendAsync(connection.Socket, new JsonObject
                {
                    ["type"] = "authenticate",
                    ["version"] = EditorMcpBridgeProtocol.Version,
                    ["token"] = token
                }, connection.SendGate, timeout.Token).ConfigureAwait(false);
                var reply = await EditorMcpBridgeProtocol.ReceiveAsync(connection.Socket, timeout.Token).ConfigureAwait(false);
                if (reply?["type"]?.GetValue<string>() != "ready")
                    throw new InvalidOperationException("The local MCP bridge rejected the connection. Check its token and allowed browser origin.");
                connection.Authenticated = true;
                _connection = connection;
                _target.ResourceChanged += OnResourceChanged;
                connection.Notifications = NotifyResourcesAsync(connection);
                connection.Reader = ReadAsync(connection);
                StatusChanged?.Invoke("Connected to the local MCP bridge.");
            }
            catch
            {
                connection.Dispose();
                throw;
            }
        }
        finally { _lifecycleGate.Release(); }
    }

    public async Task DisconnectAsync(CancellationToken cancellationToken = default)
    {
        await _lifecycleGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var connection = _connection;
            _connection = null;
            _target.ResourceChanged -= OnResourceChanged;
            if (connection is null)
                return;
            connection.StopRequests();
            try
            { await connection.Reader.ConfigureAwait(false); }
            catch (OperationCanceledException) { }
            connection.Dispose();
            StatusChanged?.Invoke("MCP disconnected.");
        }
        finally { _lifecycleGate.Release(); }
    }

    private async Task ReadAsync(Connection connection)
    {
        string? failure = null;
        try
        {
            while (!connection.Stop.IsCancellationRequested)
            {
                var message = await EditorMcpBridgeProtocol.ReceiveAsync(connection.Socket, connection.Stop.Token).ConfigureAwait(false);
                if (message is null)
                    break;
                var type = EditorMcpBridgeProtocol.RequiredString(message, "type", 40);
                var id = EditorMcpBridgeProtocol.RequiredString(message, "id", 128);
                if (type == "cancel")
                {
                    if (connection.Requests.TryGetValue(id, out var request))
                        try
                        { request.Cancel(); }
                        catch (ObjectDisposedException) { }
                    continue;
                }
                if (type != "invoke")
                    throw new InvalidDataException("The local MCP bridge sent an unsupported message.");
                var operation = EditorMcpBridgeProtocol.RequiredString(message, "operation", 128);
                var arguments = message["arguments"] as JsonObject
                    ?? throw new InvalidDataException("The local MCP bridge requires object arguments.");
                if (connection.Requests.Count >= EditorMcpBridgeProtocol.MaximumPendingRequests)
                {
                    await SendErrorAsync(connection, id, "The editor already has 32 pending MCP requests.").ConfigureAwait(false);
                    continue;
                }
                var cancellation = connection.AddRequest(id);
                _ = ExecuteAsync(connection, id, operation, (JsonObject)arguments.DeepClone(), cancellation);
            }
        }
        catch (Exception error) when (error is not OutOfMemoryException)
        {
            if (!connection.Stop.IsCancellationRequested)
                failure = "MCP bridge disconnected: " + error.Message;
        }
        finally
        {
            var endedRemotely = !connection.Stop.IsCancellationRequested;
            connection.StopRequests();
            _target.ResourceChanged -= OnResourceChanged;
            await connection.Drained.Task.ConfigureAwait(false);
            await connection.Notifications.ConfigureAwait(false);
            Interlocked.CompareExchange(ref _connection, null, connection);
            connection.Dispose();
            if (endedRemotely)
                StatusChanged?.Invoke(failure ?? "MCP bridge disconnected.");
        }
    }

    private async Task ExecuteAsync(Connection connection, string id, string operation, JsonObject arguments,
        CancellationTokenSource cancellation)
    {
        var entered = false;
        try
        {
            // Keep the result's lifetime inside the execution gate through send.
            // Queued exports retain arguments, never multiple full image buffers.
            await connection.ExecuteGate.WaitAsync(cancellation.Token).ConfigureAwait(false);
            entered = true;
            var result = await _target.InvokeAsync(operation, arguments, cancellation.Token).ConfigureAwait(false);
            await EditorMcpBridgeProtocol.SendAsync(connection.Socket, new JsonObject
            {
                ["type"] = "result",
                ["id"] = id,
                ["result"] = result?.DeepClone()
            }, connection.SendGate, connection.Stop.Token).ConfigureAwait(false);
        }
        catch (Exception error) when (error is not OutOfMemoryException)
        {
            if (!connection.Stop.IsCancellationRequested)
                await SendErrorAsync(connection, id, error is OperationCanceledException ? "The MCP operation was cancelled." : error.Message).ConfigureAwait(false);
        }
        finally
        {
            if (entered)
                connection.ExecuteGate.Release();
            connection.Requests.TryRemove(id, out _);
            cancellation.Dispose();
            if (Interlocked.Decrement(ref connection.ActiveRequests) == 0 && connection.Stop.IsCancellationRequested)
                connection.Drained.TrySetResult();
        }
    }

    private static async Task SendErrorAsync(Connection connection, string id, string message)
    {
        try
        {
            await EditorMcpBridgeProtocol.SendAsync(connection.Socket, new JsonObject
            {
                ["type"] = "error",
                ["id"] = id,
                ["message"] = message.Length <= 4096 ? message : message[..4096]
            }, connection.SendGate, connection.Stop.Token).ConfigureAwait(false);
        }
        catch (Exception error) when (error is IOException or WebSocketException or OperationCanceledException or ObjectDisposedException) { }
    }

    private void OnResourceChanged(string uri)
    {
        if (_connection is { Authenticated: true } connection)
        {
            lock (connection.ResourceGate)
            {
                if (connection.PendingResources.Count >= 32 || uri == "*")
                {
                    connection.PendingResources.Clear();
                    connection.PendingResources.Add("*");
                }
                else if (!connection.PendingResources.Contains("*"))
                    connection.PendingResources.Add(uri);
            }
            connection.ResourceSignals.Writer.TryWrite(true);
        }
    }

    private static async Task NotifyResourcesAsync(Connection connection)
    {
        try
        {
            await foreach (var _ in connection.ResourceSignals.Reader.ReadAllAsync(connection.Stop.Token).ConfigureAwait(false))
            {
                string[] resources;
                lock (connection.ResourceGate)
                {
                    resources = connection.PendingResources.ToArray();
                    connection.PendingResources.Clear();
                }
                foreach (var uri in resources)
                    await EditorMcpBridgeProtocol.SendAsync(connection.Socket, new JsonObject
                    { ["type"] = "resource", ["uri"] = uri }, connection.SendGate, connection.Stop.Token).ConfigureAwait(false);
            }
        }
        catch (Exception error) when (error is IOException or WebSocketException or OperationCanceledException or ObjectDisposedException) { }
    }

    public static void ValidateEndpoint(Uri endpoint)
    {
        ArgumentNullException.ThrowIfNull(endpoint);
        var loopback = endpoint.IsAbsoluteUri && (endpoint.Host.Equals("localhost", StringComparison.OrdinalIgnoreCase)
            || IPAddress.TryParse(endpoint.Host.Trim('[', ']'), out var address) && IPAddress.IsLoopback(address));
        if (!loopback || endpoint.Scheme is not ("ws" or "wss") || endpoint.AbsolutePath != "/editor"
            || endpoint.UserInfo.Length != 0 || endpoint.Query.Length != 0 || endpoint.Fragment.Length != 0)
            throw new ArgumentException("Use a loopback ws:// or wss:// URL ending in /editor, without credentials, query or fragment.", nameof(endpoint));
    }

    public async ValueTask DisposeAsync()
    {
        _disposed = true;
        await DisconnectAsync().ConfigureAwait(false);
    }

    private sealed class Connection : IDisposable
    {
        private int _disposed;
        private readonly object _stopGate = new();
        internal ClientWebSocket Socket { get; } = new();
        internal CancellationTokenSource Stop { get; } = new();
        internal SemaphoreSlim SendGate { get; } = new(1, 1);
        internal SemaphoreSlim ExecuteGate { get; } = new(1, 1);
        internal ConcurrentDictionary<string, CancellationTokenSource> Requests { get; } = new(StringComparer.Ordinal);
        internal int ActiveRequests;
        internal TaskCompletionSource Drained { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal bool Authenticated { get; set; }
        internal Task Reader { get; set; } = Task.CompletedTask;
        internal Task Notifications { get; set; } = Task.CompletedTask;
        internal object ResourceGate { get; } = new();
        internal HashSet<string> PendingResources { get; } = new(StringComparer.Ordinal);
        internal Channel<bool> ResourceSignals { get; } = Channel.CreateBounded<bool>(new BoundedChannelOptions(1)
        { FullMode = BoundedChannelFullMode.DropWrite, SingleReader = true, SingleWriter = false });

        internal CancellationTokenSource AddRequest(string id)
        {
            lock (_stopGate)
            {
                Stop.Token.ThrowIfCancellationRequested();
                var cancellation = CancellationTokenSource.CreateLinkedTokenSource(Stop.Token);
                if (!Requests.TryAdd(id, cancellation))
                {
                    cancellation.Dispose();
                    throw new InvalidDataException("The local MCP bridge reused an active request identifier.");
                }
                Interlocked.Increment(ref ActiveRequests);
                return cancellation;
            }
        }

        internal void StopRequests()
        {
            lock (_stopGate)
            {
                if (_disposed != 0)
                    return;
                Authenticated = false;
                Stop.Cancel();
                foreach (var pending in Requests.Values)
                    try
                    { pending.Cancel(); }
                    catch (ObjectDisposedException) { }
                if (Volatile.Read(ref ActiveRequests) == 0)
                    Drained.TrySetResult();
                ResourceSignals.Writer.TryComplete();
                Socket.Abort();
            }
        }

        public void Dispose()
        {
            lock (_stopGate)
            {
                if (Interlocked.Exchange(ref _disposed, 1) != 0)
                    return;
                Stop.Cancel();
                Socket.Dispose();
                Stop.Dispose();
                SendGate.Dispose();
                ExecuteGate.Dispose();
            }
        }
    }
}
