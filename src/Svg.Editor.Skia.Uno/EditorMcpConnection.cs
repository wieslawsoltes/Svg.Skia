namespace Svg.Editor.Skia.Uno;

/// <summary>The connection transports offered by an editor host.</summary>
public enum EditorMcpConnectionMode
{
    LocalServer,
    BrowserBridge,
    StandardInputOutput
}

/// <summary>Session-only connection settings. Tokens are never persisted in workspaces.</summary>
public sealed record EditorMcpConnectionSettings(
    EditorMcpConnectionMode Mode,
    string Address = "ws://127.0.0.1:52742/editor",
    string Token = "",
    int Port = 52741,
    bool ReadOnly = false);

/// <summary>A snapshot of the connection currently attached to the workspace.</summary>
public sealed record EditorMcpConnectionState(
    bool IsConnected,
    string Status,
    string Endpoint = "",
    string Token = "",
    string ClientConfiguration = "",
    bool ReadOnly = false);

/// <summary>Host-owned transport lifecycle used by the themed connection dialog.</summary>
public interface IEditorMcpConnection : IAsyncDisposable
{
    bool SupportsLocalServer { get; }
    EditorMcpConnectionState State { get; }
    event EventHandler? StateChanged;
    Task StartAsync(EditorMcpConnectionSettings settings, CancellationToken cancellationToken = default);
    Task StopAsync(CancellationToken cancellationToken = default);
}
