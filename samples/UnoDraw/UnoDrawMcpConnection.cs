using System.Text.Json.Nodes;
using Svg.Editor.Mcp;
using Svg.Editor.Mcp.Transport;
using Svg.Editor.Skia.Uno;

namespace UnoDraw;

/// <summary>Owns one explicit connection without changing the workspace's rendering path.</summary>
internal sealed class UnoDrawMcpConnection(Func<bool, IEditorMcpTarget> createTarget) : IEditorMcpConnection
{
    private readonly SemaphoreSlim _gate = new(1, 1);
    private CancellationTokenSource? _lifetime;
    private IEditorMcpTarget? _target;
    private IAsyncDisposable? _connection;
    private Task? _runTask;
    private bool _disposed;
    private int _disposeRequested;
    private long _generation;

    internal Func<IEditorMcpTarget, EditorMcpConnectionSettings, CancellationToken, Task<McpHostSession>>? StartLocalHost { get; set; }
    internal Func<IEditorMcpTarget, CancellationToken, Task>? RunStandardInputOutput { get; set; }
    internal EditorMcpConnectionSettings? StartupSettings { get; set; }
    public bool SupportsLocalServer => StartLocalHost is not null;
    public EditorMcpConnectionState State { get; private set; } = new(false, "Disconnected");
    public event EventHandler? StateChanged;

    internal async Task StartOnLaunchAsync()
    {
        if (StartupSettings is not { } settings)
            return;
        StartupSettings = null;
        try
        {
            await StartAsync(settings);
            Console.Error.WriteLine("UnoDraw MCP: " + State.Status);
            if (!string.IsNullOrEmpty(State.ClientConfiguration))
                Console.Error.WriteLine(State.ClientConfiguration);
        }
        catch (Exception error)
        {
            Console.Error.WriteLine("UnoDraw MCP could not start: " + error.Message);
        }
    }

    public async Task StartAsync(EditorMcpConnectionSettings settings, CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            ObjectDisposedException.ThrowIf(_disposed || Volatile.Read(ref _disposeRequested) != 0, this);
            await StopCoreAsync();
            if (settings.Mode == EditorMcpConnectionMode.LocalServer && StartLocalHost is null)
                throw new InvalidOperationException("This platform connects through the UnoDraw MCP bridge.");
            if (settings.Mode == EditorMcpConnectionMode.StandardInputOutput && RunStandardInputOutput is null)
                throw new InvalidOperationException("Launch the desktop application with --mcp-stdio to use this transport.");

            _lifetime = new CancellationTokenSource();
            using var starting = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _lifetime.Token);
            _target = createTarget(settings.ReadOnly);
            var generation = ++_generation;
            SetState(new(false, "Connecting…", ReadOnly: settings.ReadOnly));

            if (settings.Mode == EditorMcpConnectionMode.LocalServer)
            {
                var session = await StartLocalHost!(_target, settings, starting.Token);
                _connection = session.Lifetime;
                starting.Token.ThrowIfCancellationRequested();
                var configuration = new JsonObject
                {
                    ["mcpServers"] = new JsonObject
                    {
                        ["unodraw"] = new JsonObject
                        {
                            ["type"] = "http",
                            ["url"] = session.Endpoint,
                            ["headers"] = new JsonObject { ["Authorization"] = "Bearer " + session.Token }
                        }
                    }
                }.ToJsonString(new() { WriteIndented = true });
                SetState(new(true, settings.ReadOnly ? "Local server running · inspection only" : "Local server running · editing enabled",
                    session.Endpoint, session.Token, configuration, settings.ReadOnly));
            }
            else if (settings.Mode == EditorMcpConnectionMode.BrowserBridge)
            {
                if (!Uri.TryCreate(settings.Address, UriKind.Absolute, out var endpoint))
                    throw new ArgumentException("Enter a valid ws:// or wss:// bridge address.");
                var client = new EditorMcpBridgeClient(_target);
                _connection = client;
                await client.ConnectAsync(endpoint, settings.Token, starting.Token);
                SetState(new(true, settings.ReadOnly ? "Bridge connected · inspection only" : "Bridge connected · editing enabled",
                    endpoint.ToString(), ReadOnly: settings.ReadOnly));
                _ = ObserveCompletionAsync(client.Completion, generation);
            }
            else
            {
                SetState(new(true, settings.ReadOnly ? "Standard input/output connected · inspection only" : "Standard input/output connected · editing enabled",
                    "stdio", ReadOnly: settings.ReadOnly));
                _runTask = RunStandardInputOutput!(_target, _lifetime.Token);
                _ = ObserveCompletionAsync(_runTask, generation);
            }
        }
        catch (Exception error)
        {
            await StopCoreAsync();
            SetState(new(false, "Connection failed: " + error.Message));
            throw;
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task StopAsync(CancellationToken cancellationToken = default)
    {
        CancelCurrentLifetime();
        await _gate.WaitAsync(cancellationToken);
        try
        {
            await StopCoreAsync();
            SetState(new(false, "Disconnected"));
        }
        finally
        {
            _gate.Release();
        }
    }

    private async Task ObserveCompletionAsync(Task completion, long generation)
    {
        var status = "Disconnected";
        try
        {
            await completion;
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception error)
        {
            status = "Disconnected: " + error.Message;
        }
        await _gate.WaitAsync();
        try
        {
            if (_disposed || generation != _generation)
                return;
            await StopCoreAsync();
            SetState(new(false, status));
        }
        finally
        {
            _gate.Release();
        }
    }

    private async Task StopCoreAsync()
    {
        ++_generation;
        var lifetime = _lifetime;
        _lifetime = null;
        lifetime?.Cancel();
        // Cancel the workspace adapter while its dispatcher and dialog tree are
        // still alive. A transport may be draining an interactive editor call.
        var target = _target;
        _target = null;
        var connection = _connection;
        _connection = null;
        var runTask = _runTask;
        _runTask = null;
        try
        {
            if (target is IDisposable disposable)
                disposable.Dispose();
        }
        finally
        {
            try
            {
                if (connection is not null)
                    await connection.DisposeAsync();
            }
            finally
            {
                try
                {
                    // Retain the raw stdio operation, not its observer: the observer
                    // acquires this same gate to publish completion. A canceled
                    // request must finish unwinding before the workspace is replaced
                    // or disposed, just as it does for the owned HTTP/socket hosts.
                    if (runTask is not null)
                        await runTask;
                }
                catch (OperationCanceledException) { }
                catch (Exception error)
                {
                    Console.Error.WriteLine("UnoDraw MCP stopped: " + error.Message);
                }
                finally { lifetime?.Dispose(); }
            }
        }
    }

    private void SetState(EditorMcpConnectionState state)
    {
        State = state;
        StateChanged?.Invoke(this, EventArgs.Empty);
    }

    public async ValueTask DisposeAsync()
    {
        Interlocked.Exchange(ref _disposeRequested, 1);
        CancelCurrentLifetime();
        await _gate.WaitAsync();
        try
        {
            if (_disposed)
                return;
            _disposed = true;
            await StopCoreAsync();
            SetState(new(false, "Disconnected"));
        }
        finally
        {
            _gate.Release();
        }
    }

    private void CancelCurrentLifetime()
    {
        // Stopping while ConnectAsync owns the gate must cancel its socket/startup
        // operation immediately, rather than waiting for its connection deadline.
        var lifetime = _lifetime;
        try
        { lifetime?.Cancel(); }
        catch (ObjectDisposedException) { }
    }
}

internal sealed record McpHostSession(IAsyncDisposable Lifetime, string Endpoint, string Token);
