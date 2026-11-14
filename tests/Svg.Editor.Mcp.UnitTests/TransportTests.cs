using System.IO.Pipelines;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json.Nodes;
using Svg.Editor.Mcp.Hosting;
using Svg.Editor.Mcp.Transport;
using Xunit;

namespace Svg.Editor.Mcp.UnitTests;

[Collection("MCP integration")]
public sealed class TransportTests
{
    private const string Origin = "https://editor.example";

    [Theory]
    [InlineData("ws://127.0.0.1:52742/editor")]
    [InlineData("wss://localhost:52742/editor")]
    [InlineData("ws://[::1]:52742/editor")]
    public void BrowserAttachmentAllowsExplicitLoopbackEndpoints(string endpoint)
        => EditorMcpBridgeClient.ValidateEndpoint(new Uri(endpoint));

    [Theory]
    [InlineData("ws://remote.example/editor")]
    [InlineData("https://127.0.0.1/editor")]
    [InlineData("ws://user:token@127.0.0.1/editor")]
    [InlineData("ws://127.0.0.1/editor?token=secret")]
    [InlineData("ws://127.0.0.1/editor#token")]
    [InlineData("ws://127.0.0.1/mcp")]
    public void BrowserAttachmentRejectsRemoteHostsAndUrlCredentials(string endpoint)
        => Assert.Throws<ArgumentException>(() => EditorMcpBridgeClient.ValidateEndpoint(new Uri(endpoint)));

    [Fact]
    public async Task BrowserRelayRoundTripsOperationsAndResourceInvalidations()
    {
        await using var relay = new EditorMcpRelayTarget();
        await using var host = await EditorMcpHost.StartAsync(relay, new EditorMcpHostOptions { Port = 0, AllowedOrigin = Origin });
        var target = new BrowserTarget();
        await using var browser = new EditorMcpBridgeClient(target, Origin);
        await browser.ConnectAsync(host.BrowserEndpoint, host.BrowserToken);
        var result = await relay.InvokeAsync("unodraw_document_list", new JsonObject { ["value"] = "original" }, default);
        Assert.Equal("unodraw_document_list", result?["operation"]?.GetValue<string>());
        Assert.Equal("original", result?["arguments"]?["value"]?.GetValue<string>());
        var changed = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        relay.ResourceChanged += uri => changed.TrySetResult(uri);
        target.Notify("unodraw://documents");
        Assert.Equal("unodraw://documents", await changed.Task.WaitAsync(TimeSpan.FromSeconds(5)));
        await browser.DisconnectAsync();
        Assert.False(browser.IsConnected);
    }

    [Fact]
    public async Task BridgePreservesMaximumBinaryExportWithoutEscapingInflation()
    {
        var bytes = new byte[16 * 1024 * 1024];
        Array.Fill(bytes, (byte)0xfb);
        var encoded = Convert.ToBase64String(bytes);
        await using var relay = new EditorMcpRelayTarget();
        await using var host = await EditorMcpHost.StartAsync(relay, new EditorMcpHostOptions { Port = 0, AllowedOrigin = Origin });
        var target = new BrowserTarget { Result = new JsonObject { ["base64"] = encoded } };
        await using var browser = new EditorMcpBridgeClient(target, Origin);
        await browser.ConnectAsync(host.BrowserEndpoint, host.BrowserToken);
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(20));
        var result = await relay.InvokeAsync("unodraw_document_export", new JsonObject(), timeout.Token);
        Assert.Equal(encoded, result?["base64"]?.GetValue<string>());
    }

    [Fact]
    public async Task BrowserRelayRequiresAttachmentTokenAndRejectsSecondPeer()
    {
        await using var relay = new EditorMcpRelayTarget();
        await using var host = await EditorMcpHost.StartAsync(relay, new EditorMcpHostOptions { Port = 0, AllowedOrigin = Origin });
        await using var wrong = new EditorMcpBridgeClient(new BrowserTarget(), Origin);
        await Assert.ThrowsAsync<InvalidOperationException>(() => wrong.ConnectAsync(host.BrowserEndpoint, host.Token));
        Assert.False(relay.IsConnected);
        await using var first = new EditorMcpBridgeClient(new BrowserTarget(), Origin);
        await first.ConnectAsync(host.BrowserEndpoint, host.BrowserToken);
        await using var second = new EditorMcpBridgeClient(new BrowserTarget(), Origin);
        await Assert.ThrowsAsync<WebSocketException>(() => second.ConnectAsync(host.BrowserEndpoint, host.BrowserToken));
        Assert.True(first.IsConnected);
    }

    [Fact]
    public async Task BrowserRelayRejectsWrongOriginEvenWithValidAttachmentToken()
    {
        await using var relay = new EditorMcpRelayTarget();
        await using var host = await EditorMcpHost.StartAsync(relay, new EditorMcpHostOptions { Port = 0, AllowedOrigin = Origin });
        await using var browser = new EditorMcpBridgeClient(new BrowserTarget(), "https://attacker.example");
        await Assert.ThrowsAsync<WebSocketException>(() => browser.ConnectAsync(host.BrowserEndpoint, host.BrowserToken));
        Assert.False(relay.IsConnected);
    }

    [Fact]
    public async Task CancellationReachesEditorAndDisconnectedRequestsAreNeverReplayed()
    {
        await using var relay = new EditorMcpRelayTarget();
        await using var host = await EditorMcpHost.StartAsync(relay, new EditorMcpHostOptions { Port = 0, AllowedOrigin = Origin });
        var target = new BrowserTarget { Block = true };
        await using var browser = new EditorMcpBridgeClient(target, Origin);
        await browser.ConnectAsync(host.BrowserEndpoint, host.BrowserToken);
        using var cancellation = new CancellationTokenSource();
        var pending = relay.InvokeAsync("unodraw_document_list", new JsonObject(), cancellation.Token).AsTask();
        await target.Started.Task.WaitAsync(TimeSpan.FromSeconds(5));
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => pending);
        await target.Cancelled.Task.WaitAsync(TimeSpan.FromSeconds(5));
        target.Started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var interrupted = relay.InvokeAsync("unodraw_document_list", new JsonObject(), default).AsTask();
        await target.Started.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await browser.DisconnectAsync();
        await Assert.ThrowsAnyAsync<IOException>(() => interrupted);
        Assert.Equal(2, target.Invocations);
        target.Block = false;
        // The server's receive loop observes the close asynchronously; await its
        // status event rather than sleeping before a new authenticated attachment.
        if (relay.IsConnected)
        {
            var disconnected = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            relay.StatusChanged += status => { if (!relay.IsConnected) disconnected.TrySetResult(); };
            if (!relay.IsConnected)
                disconnected.TrySetResult();
            await disconnected.Task.WaitAsync(TimeSpan.FromSeconds(5));
        }
        await browser.ConnectAsync(host.BrowserEndpoint, host.BrowserToken);
        await relay.InvokeAsync("unodraw_document_list", new JsonObject(), default);
        Assert.Equal(3, target.Invocations);
    }

    [Fact]
    public async Task CancellingQueuedCallDoesNotEnterEditorWhileFirstCallIsActive()
    {
        await using var relay = new EditorMcpRelayTarget();
        await using var host = await EditorMcpHost.StartAsync(relay, new EditorMcpHostOptions { Port = 0, AllowedOrigin = Origin });
        var target = new BrowserTarget { Block = true };
        await using var browser = new EditorMcpBridgeClient(target, Origin);
        await browser.ConnectAsync(host.BrowserEndpoint, host.BrowserToken);
        using var firstCancellation = new CancellationTokenSource();
        var first = relay.InvokeAsync("unodraw_document_get", new JsonObject(), firstCancellation.Token).AsTask();
        await target.Started.Task.WaitAsync(TimeSpan.FromSeconds(5));
        using var queuedCancellation = new CancellationTokenSource(TimeSpan.FromMilliseconds(200));
        var queued = relay.InvokeAsync("unodraw_document_export", new JsonObject(), queuedCancellation.Token).AsTask();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => queued);
        Assert.Equal(1, target.Invocations);
        firstCancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => first);
        await target.Cancelled.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal(1, target.Invocations);
    }

    [Fact]
    public async Task StdioFramesOnlyJsonRpcAndFinishesAtEndOfInput()
    {
        var input = new Pipe();
        var output = new Pipe();
        await using var inputStream = input.Reader.AsStream();
        await using var outputStream = output.Writer.AsStream();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var running = EditorMcpHost.RunStdioAsync(new BrowserTarget(), inputStream, outputStream, timeout.Token);
        using var reader = new StreamReader(output.Reader.AsStream(), Encoding.UTF8, leaveOpen: true);
        await input.Writer.WriteAsync(Encoding.UTF8.GetBytes("{\"jsonrpc\":\"2.0\",\"id\":1,\"method\":\"initialize\",\"params\":{\"protocolVersion\":\"2025-11-25\",\"capabilities\":{},\"clientInfo\":{\"name\":\"TransportTests\",\"version\":\"1\"}}}\n"), timeout.Token);
        var initialized = JsonNode.Parse(await reader.ReadLineAsync(timeout.Token) ?? throw new IOException("MCP stdout ended before initialize."));
        Assert.Equal(1, initialized?["id"]?.GetValue<int>());
        Assert.Equal("UnoDraw", initialized?["result"]?["serverInfo"]?["name"]?.GetValue<string>());
        await input.Writer.WriteAsync(Encoding.UTF8.GetBytes("{\"jsonrpc\":\"2.0\",\"method\":\"notifications/initialized\"}\n{\"jsonrpc\":\"2.0\",\"id\":2,\"method\":\"tools/list\"}\n"), timeout.Token);
        var tools = JsonNode.Parse(await reader.ReadLineAsync(timeout.Token) ?? throw new IOException("MCP stdout ended before tools/list."));
        Assert.Equal(2, tools?["id"]?.GetValue<int>());
        Assert.NotEmpty((JsonArray)tools!["result"]!["tools"]!);
        await input.Writer.CompleteAsync();
        await running.WaitAsync(timeout.Token);
    }

    private sealed class BrowserTarget : IEditorMcpTarget
    {
        public bool Block { get; set; }
        public JsonNode? Result { get; set; }
        public int Invocations { get; private set; }
        public TaskCompletionSource Started { get; set; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Cancelled { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public event Action<string>? ResourceChanged;
        public void Notify(string uri) => ResourceChanged?.Invoke(uri);
        public async ValueTask<JsonNode?> InvokeAsync(string operation, JsonObject arguments, CancellationToken cancellationToken)
        {
            Invocations++;
            Started.TrySetResult();
            if (Block)
            {
                try
                { await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken); }
                catch (OperationCanceledException) { Cancelled.TrySetResult(); throw; }
            }
            return Result?.DeepClone() ?? new JsonObject { ["operation"] = operation, ["arguments"] = arguments.DeepClone() };
        }
    }
}
