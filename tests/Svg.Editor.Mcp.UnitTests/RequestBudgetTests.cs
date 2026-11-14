using System.Text.Json.Nodes;
using System.Threading.Channels;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using Xunit;

namespace Svg.Editor.Mcp.UnitTests;

[Collection("MCP integration")]
public sealed class RequestBudgetTests
{
    [Fact]
    public async Task BlockedOutputRetainsOperationBudgetWhileCancellationRemainsLive()
    {
        var target = new BudgetTarget();
        await using var peer = new BudgetPeer(target);
        await peer.RequestAsync(1);
        await peer.WaitForAttemptAsync(1);
        await peer.RequestAsync(2, wait: true);
        await target.WaitStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await peer.RequestAsync(3);
        var rejected = Assert.IsType<JsonRpcError>(await peer.WaitForAttemptAsync(3));
        Assert.Equal(-32000, rejected.Error.Code);
        Assert.Equal(2, target.Invocations);

        // The first response is still blocked, and the second operation is waiting
        // in the target. A cancellation notification must nevertheless reach it.
        await peer.CancelAsync(2);
        await target.Cancelled.Task.WaitAsync(TimeSpan.FromSeconds(5));
        peer.AllowOutput();
        var delivered = new List<JsonObject>();
        for (var index = 0; index < 2; index++)
            delivered.Add(await peer.ReadAsync());
        Assert.Equal(new[] { 1, 3 }, delivered.Select(message => message["id"]!.GetValue<int>()).Order());
        await peer.RequestAsync(4);
        Assert.IsType<JsonRpcResponse>(await peer.WaitForAttemptAsync(4));
        Assert.Equal(4, (await peer.ReadAsync())["id"]!.GetValue<int>());
        Assert.Equal(3, target.Invocations);
        await peer.RequestAsync(5);
        Assert.Equal(5, (await peer.ReadAsync())["id"]!.GetValue<int>());
        Assert.Equal(4, target.Invocations);
    }

    [Fact]
    public async Task FloodingBlockedOutputClosesConnectionWithBoundedPendingErrors()
    {
        var target = new BudgetTarget();
        await using var peer = new BudgetPeer(target);
        await peer.RequestAsync(1);
        await peer.WaitForAttemptAsync(1);
        await peer.RequestAsync(2, wait: true);
        await target.WaitStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));
        for (var id = 3; id <= 10; id++)
        {
            await peer.RequestAsync(id);
            Assert.IsType<JsonRpcError>(await peer.WaitForAttemptAsync(id));
        }

        // Eight small rejections are enough. The next request terminates a peer
        // that is still refusing output instead of allocating another send task.
        await peer.RequestAsync(11);
        await peer.WaitForShutdownAsync();
        await target.Cancelled.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal(2, target.Invocations);
    }

    private sealed class BudgetTarget : IEditorMcpTarget
    {
        private int _invocations;
        public int Invocations => Volatile.Read(ref _invocations);
        public TaskCompletionSource WaitStarted { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Cancelled { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public event Action<string>? ResourceChanged { add { } remove { } }

        public async ValueTask<JsonNode?> InvokeAsync(string operation, JsonObject arguments, CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref _invocations);
            if (arguments["documentId"]?.GetValue<string>() == "wait")
            {
                WaitStarted.TrySetResult();
                try
                { await Task.Delay(Timeout.Infinite, cancellationToken); }
                catch (OperationCanceledException) { Cancelled.TrySetResult(); throw; }
            }
            return new JsonObject { ["svg"] = "<svg xmlns='http://www.w3.org/2000/svg'/>" };
        }
    }

    private sealed class BudgetPeer : IAsyncDisposable
    {
        private readonly CancellationTokenSource _lifetime = new(TimeSpan.FromSeconds(20));
        private readonly TaskCompletionSource _outputAllowed = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly Channel<JsonRpcMessage> _attempts = Channel.CreateUnbounded<JsonRpcMessage>();
        private readonly Channel<JsonObject> _delivered = Channel.CreateUnbounded<JsonObject>();
        private readonly McpJsonMessageTransport _transport;
        private readonly McpServer _server;
        private readonly Task _run;

        public BudgetPeer(IEditorMcpTarget target)
        {
            _transport = new McpJsonMessageTransport(async (json, cancellationToken) =>
            {
                await _outputAllowed.Task.WaitAsync(cancellationToken);
                await _delivered.Writer.WriteAsync(JsonNode.Parse(json)!.AsObject(), cancellationToken);
            });
            _server = EditorMcpServer.Create(new ObservedTransport(_transport, _attempts.Writer), target, _lifetime.Token);
            _run = _server.RunAsync(_lifetime.Token);
        }

        public void AllowOutput() => _outputAllowed.TrySetResult();

        public ValueTask RequestAsync(int id, bool wait = false) => _transport.ReceiveAsync(new JsonObject
        {
            ["jsonrpc"] = "2.0",
            ["id"] = id,
            ["method"] = "tools/call",
            ["params"] = new JsonObject
            {
                ["name"] = "unodraw_document_get",
                ["arguments"] = new JsonObject { ["documentId"] = wait ? "wait" : "current" },
                ["_meta"] = new JsonObject
                {
                    [MetaKeys.ProtocolVersion] = "2026-07-28",
                    [MetaKeys.ClientCapabilities] = new JsonObject()
                }
            }
        }.ToJsonString(), _lifetime.Token);

        public ValueTask CancelAsync(int id) => _transport.ReceiveAsync(new JsonObject
        {
            ["jsonrpc"] = "2.0",
            ["method"] = "notifications/cancelled",
            ["params"] = new JsonObject { ["requestId"] = id }
        }.ToJsonString(), _lifetime.Token);

        public async Task<JsonRpcMessage> WaitForAttemptAsync(int id)
        {
            var message = await _attempts.Reader.ReadAsync(_lifetime.Token).AsTask().WaitAsync(TimeSpan.FromSeconds(5));
            Assert.Equal(new RequestId(id), Assert.IsAssignableFrom<JsonRpcMessageWithId>(message).Id);
            return message;
        }

        public async Task<JsonObject> ReadAsync() =>
            await _delivered.Reader.ReadAsync(_lifetime.Token).AsTask().WaitAsync(TimeSpan.FromSeconds(5));

        public async Task WaitForShutdownAsync() => await _run.WaitAsync(TimeSpan.FromSeconds(5));

        public async ValueTask DisposeAsync()
        {
            await _lifetime.CancelAsync();
            AllowOutput();
            await _server.DisposeAsync();
            try
            { await _run; }
            catch (OperationCanceledException) { }
            _lifetime.Dispose();
        }

        private sealed class ObservedTransport(ITransport inner, ChannelWriter<JsonRpcMessage> attempts) : ITransport
        {
            public ChannelReader<JsonRpcMessage> MessageReader => inner.MessageReader;
            public string? SessionId => inner.SessionId;
            public ValueTask DisposeAsync() => inner.DisposeAsync();
            public async Task SendMessageAsync(JsonRpcMessage message, CancellationToken cancellationToken = default)
            {
                attempts.TryWrite(message);
                await inner.SendMessageAsync(message, cancellationToken);
            }
        }
    }
}
