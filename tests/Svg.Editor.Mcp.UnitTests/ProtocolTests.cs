using System.Collections.Concurrent;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Channels;
using ModelContextProtocol;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using Xunit;

namespace Svg.Editor.Mcp.UnitTests;

[Collection("MCP integration")]
public class ProtocolTests
{
    [Theory]
    [InlineData("2026-07-28")]
    [InlineData("2025-11-25")]
    [InlineData("2025-06-18")]
    public async Task OfficialClientDiscoversCompleteStableCatalog(string version)
    {
        await using var connection = await SdkConnection.CreateAsync(new FakeTarget(), version);
        var tools = await connection.Client.ListToolsAsync();
        Assert.Equal(EditorMcpCatalog.Tools.Count, tools.Count);
        Assert.Equal(EditorMcpCatalog.Tools.Select(tool => tool.Name).Order(StringComparer.Ordinal), tools.Select(tool => tool.Name));
        var first = await connection.Client.ListToolsAsync(new ListToolsRequestParams());
        Assert.Equal(EditorMcpServer.PageSize, first.Tools.Count);
        Assert.NotNull(first.NextCursor);
        var second = await connection.Client.ListToolsAsync(new ListToolsRequestParams { Cursor = first.NextCursor });
        Assert.Empty(first.Tools.Select(tool => tool.Name).Intersect(second.Tools.Select(tool => tool.Name)));
        Assert.All(first.Tools, tool =>
        {
            Assert.Equal("object", tool.InputSchema.GetProperty("type").GetString());
            Assert.False(tool.InputSchema.GetProperty("additionalProperties").GetBoolean());
            Assert.NotNull(tool.Annotations);
        });
        Assert.NotNull(connection.Client.ServerCapabilities.Resources);
        Assert.True(connection.Client.ServerCapabilities.Resources!.Subscribe);
        Assert.NotNull(connection.Client.ServerCapabilities.Prompts);
    }

    [Fact]
    public async Task InvalidPaginationCursorIsAProtocolError()
    {
        await using var connection = await SdkConnection.CreateAsync(new FakeTarget());
        var error = await Assert.ThrowsAsync<McpProtocolException>(async () =>
            await connection.Client.ListToolsAsync(new ListToolsRequestParams { Cursor = "not-a-cursor" }));
        Assert.Equal(McpErrorCode.InvalidParams, error.ErrorCode);
    }

    [Fact]
    public async Task UnknownToolDoesNotInvokeTheEditor()
    {
        var target = new FakeTarget();
        await using var connection = await SdkConnection.CreateAsync(target);
        await Assert.ThrowsAsync<McpProtocolException>(async () =>
            await connection.Client.CallToolAsync("unknown-tool"));
        Assert.Empty(target.Invocations);
    }

    [Theory]
    [InlineData("unodraw_document_new", "{\"width\":\"100\"}")]
    [InlineData("unodraw_document_new", "{\"width\":0}")]
    [InlineData("unodraw_document_new", "{\"unknown\":1}")]
    [InlineData("unodraw_document_open", "{}")]
    [InlineData("unodraw_document_export", "{\"format\":\"unknown\"}")]
    [InlineData("unodraw_draw", "{\"shape\":\"Rect\",\"start\":[0,\"1\"],\"end\":[10,10]}")]
    [InlineData("unodraw_transform", "{\"matrix\":[1,0,0,1,0]}")]
    [InlineData("unodraw_gradient", "{\"stops\":[{\"offset\":0,\"color\":\"#fff\"},{\"offset\":2,\"color\":\"#000\"}]}")]
    public async Task SchemaFailuresAreRecoverableToolErrorsBeforeEditorAccess(string tool, string arguments)
    {
        var target = new FakeTarget();
        await using var connection = await SdkConnection.CreateAsync(target);
        var result = await connection.Client.CallToolAsync(new CallToolRequestParams
        {
            Name = tool,
            Arguments = Arguments(arguments)
        });
        Assert.True(result.IsError);
        Assert.NotEmpty(result.Content.OfType<TextContentBlock>().Single().Text);
        Assert.Empty(target.Invocations);
    }

    [Fact]
    public async Task AcceptedArgumentsRemainTypedAndStructuredResultsRoundTrip()
    {
        var target = new FakeTarget();
        await using var connection = await SdkConnection.CreateAsync(target);
        var result = await connection.Client.CallToolAsync(new CallToolRequestParams
        {
            Name = "unodraw_document_new",
            Arguments = Arguments("{\"width\":123.5,\"height\":80,\"name\":\"Test\"}")
        });
        Assert.False(result.IsError);
        Assert.Equal(123.5, target.Invocations.Single().Arguments["width"]!.GetValue<double>());
        Assert.Equal("unodraw_document_new", result.StructuredContent!.Value.GetProperty("operation").GetString());
        Assert.Equal("Test", result.StructuredContent.Value.GetProperty("arguments").GetProperty("name").GetString());
    }

    [Fact]
    public async Task EditorFailuresRemainErrorsAndInternalExceptionsAreSanitized()
    {
        var target = new FakeTarget { Failure = new EditorMcpException("This element is locked.") };
        await using var connection = await SdkConnection.CreateAsync(target);
        var error = await connection.Client.CallToolAsync("unodraw_document_get");
        Assert.True(error.IsError);
        Assert.Equal("This element is locked.", error.Content.OfType<TextContentBlock>().Single().Text);
        target.Failure = new IOException("secret filesystem path");
        error = await connection.Client.CallToolAsync("unodraw_document_get");
        Assert.True(error.IsError);
        Assert.DoesNotContain("secret", error.Content.OfType<TextContentBlock>().Single().Text);
    }

    [Fact]
    public async Task ImageExportReturnsNativeMcpImageContent()
    {
        var target = new FakeTarget
        {
            Result = new JsonObject { ["mimeType"] = "image/png", ["base64"] = Convert.ToBase64String([1, 2, 3]), ["width"] = 1 }
        };
        await using var connection = await SdkConnection.CreateAsync(target);
        var result = await connection.Client.CallToolAsync("unodraw_document_export");
        Assert.False(result.IsError);
        var image = Assert.Single(result.Content.OfType<ImageContentBlock>());
        Assert.Equal("image/png", image.MimeType);
        Assert.Equal(new byte[] { 1, 2, 3 }, image.DecodedData.ToArray());
        Assert.DoesNotContain("base64", result.Content.OfType<TextContentBlock>().Single().Text);
        Assert.False(result.StructuredContent!.Value.TryGetProperty("base64", out _));
        Assert.Equal("image", result.StructuredContent.Value.GetProperty("contentType").GetString());
    }

    [Theory]
    [InlineData("image/png")]
    [InlineData("application/pdf")]
    public async Task MaximumBinaryExportCrossesProtocolTransportWithoutPayloadDuplication(string mimeType)
    {
        var bytes = new byte[16 * 1024 * 1024];
        // This pattern produces many '+' characters. Escaping each one as a six-byte
        // Unicode sequence would exceed the frame budget despite valid export size.
        Array.Fill(bytes, (byte)0xfb);
        bytes[0] = 17;
        bytes[^1] = 31;
        var target = new FakeTarget
        {
            Result = new JsonObject { ["mimeType"] = mimeType, ["base64"] = Convert.ToBase64String(bytes) }
        };
        await using var connection = await SdkConnection.CreateAsync(target);
        var result = await connection.Client.CallToolAsync("unodraw_document_export");
        Assert.False(result.IsError);
        ReadOnlyMemory<byte> decoded = mimeType == "image/png"
            ? Assert.Single(result.Content.OfType<ImageContentBlock>()).DecodedData
            : Assert.IsType<BlobResourceContents>(Assert.Single(result.Content.OfType<EmbeddedResourceBlock>()).Resource).DecodedData;
        Assert.Equal(bytes.Length, decoded.Length);
        Assert.Equal(bytes[0], decoded.Span[0]);
        Assert.Equal(bytes[^1], decoded.Span[^1]);
        Assert.False(result.StructuredContent!.Value.TryGetProperty("base64", out _));
        Assert.True(result.Content.OfType<TextContentBlock>().Single().Text.Length < 1024);
    }

    [Fact]
    public async Task ResourcesUseRealEditorOperationsAndDocumentIds()
    {
        var target = new FakeTarget { Result = new JsonObject { ["svg"] = "<svg xmlns=\"http://www.w3.org/2000/svg\"/>" } };
        await using var connection = await SdkConnection.CreateAsync(target);
        var resources = await connection.Client.ListResourcesAsync();
        Assert.Contains(resources, resource => resource.Uri == "unodraw://selection");
        var result = await connection.Client.ReadResourceAsync("unodraw://document/document-one/source");
        var content = Assert.IsType<TextResourceContents>(Assert.Single(result.Contents));
        Assert.Equal("image/svg+xml", content.MimeType);
        Assert.StartsWith("<svg ", content.Text);
        Assert.Equal("unodraw_document_get", target.Invocations.Last().Operation);
        Assert.Equal("document-one", target.Invocations.Last().Arguments["documentId"]!.GetValue<string>());
        await connection.Client.ReadResourceAsync("unodraw://selection");
        Assert.Equal("unodraw_selection_get", target.Invocations.Last().Operation);
        await Assert.ThrowsAsync<McpProtocolException>(async () =>
            await connection.Client.ReadResourceAsync("unodraw://document/..%2Fprivate/source"));
    }

    [Fact]
    public async Task PromptsValidateInputsAndCompletionUsesDocumentDiscovery()
    {
        var target = new FakeTarget
        {
            Result = new JsonObject { ["documents"] = new JsonArray(new JsonObject { ["documentId"] = "doc-a" }, new JsonObject { ["id"] = "doc-b" }) }
        };
        await using var connection = await SdkConnection.CreateAsync(target);
        var prompts = await connection.Client.ListPromptsAsync();
        Assert.Equal(4, prompts.Count);
        var prompt = await connection.Client.GetPromptAsync("create-artwork", new Dictionary<string, object?> { ["brief"] = "A blue circle" });
        Assert.Contains("A blue circle", Assert.IsType<TextContentBlock>(Assert.Single(prompt.Messages).Content).Text);
        await Assert.ThrowsAsync<McpProtocolException>(async () => await connection.Client.GetPromptAsync("create-artwork"));
        var result = await connection.Client.CompleteAsync(new ResourceTemplateReference { Uri = "unodraw://document/{documentId}/source" }, "documentId", "doc-");
        Assert.Equal(new[] { "doc-a", "doc-b" }, result.Completion.Values);
        Assert.Equal("unodraw_document_list", target.Invocations.Single().Operation);
        var format = await connection.Client.CompleteAsync(new PromptReference { Name = "export-document" }, "format", "p");
        Assert.Contains("png", format.Completion.Values);
        Assert.DoesNotContain("svg", format.Completion.Values);
    }

    [Fact]
    public async Task ModernProtocolRequiresMetadataAndSupportsDiscoverWithoutInitialize()
    {
        await using var peer = new WirePeer(new FakeTarget());
        var missingMetadata = await peer.RequestAsync("server/discover", new JsonObject(), modern: false);
        Assert.NotNull(missingMetadata["error"]);
        var discovery = await peer.RequestAsync("server/discover", new JsonObject());
        Assert.NotNull(discovery["result"]);
        Assert.Equal(0, discovery["result"]!["ttlMs"]!.GetValue<int>());
        Assert.Equal("private", discovery["result"]!["cacheScope"]!.GetValue<string>());
        var result = await peer.RequestAsync("tools/list", new JsonObject());
        Assert.Equal("complete", result["result"]!["resultType"]!.GetValue<string>());
        Assert.Equal(300000, result["result"]!["ttlMs"]!.GetValue<int>());
        Assert.Equal("public", result["result"]!["cacheScope"]!.GetValue<string>());
        Assert.Equal("UnoDraw", result["result"]!["_meta"]![MetaKeys.ServerInfo]!["name"]!.GetValue<string>());
        var unsupported = await peer.RequestAsync("tools/list", new JsonObject
        {
            ["_meta"] = new JsonObject { [MetaKeys.ProtocolVersion] = "2099-01-01", [MetaKeys.ClientCapabilities] = new JsonObject() }
        }, modern: false);
        Assert.Equal(-32022, unsupported["error"]!["code"]!.GetValue<int>());
    }

    [Fact]
    public async Task LiveResourcesArePrivateAndImmediatelyStale()
    {
        await using var peer = new WirePeer(new FakeTarget());
        var response = await peer.RequestAsync("resources/read", new JsonObject { ["uri"] = "unodraw://selection" });
        Assert.Equal("private", response["result"]!["cacheScope"]!.GetValue<string>());
        Assert.Equal(0, response["result"]!["ttlMs"]!.GetValue<int>());
    }

    [Fact]
    public async Task ModernSubscriptionIsAcknowledgedTaggedAndReleasedOnCancellation()
    {
        var target = new FakeTarget();
        await using var peer = new WirePeer(target);
        await peer.SendAsync(100, "subscriptions/listen", new JsonObject
        {
            ["notifications"] = new JsonObject
            {
                ["resourceSubscriptions"] = new JsonArray("unodraw://selection"),
                ["toolsListChanged"] = true
            }
        });
        var acknowledgement = await peer.ReadAsync();
        Assert.Equal(NotificationMethods.SubscriptionsAcknowledgedNotification, acknowledgement["method"]!.GetValue<string>());
        Assert.Equal(100, acknowledgement["params"]!["_meta"]![MetaKeys.SubscriptionId]!.GetValue<int>());
        Assert.Null(acknowledgement["params"]!["notifications"]!["toolsListChanged"]);
        target.Change("unodraw://selection");
        var notification = await peer.ReadAsync();
        Assert.Equal(NotificationMethods.ResourceUpdatedNotification, notification["method"]!.GetValue<string>());
        Assert.Equal(100, notification["params"]!["_meta"]![MetaKeys.SubscriptionId]!.GetValue<int>());
        Assert.Equal("unodraw://selection", notification["params"]!["uri"]!.GetValue<string>());
        await peer.NotifyAsync("notifications/cancelled", new JsonObject { ["requestId"] = 100 });
        await target.NoSubscribers.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal(0, target.SubscriberCount);
    }

    [Fact]
    public async Task ModernSubscriptionCapacityIsBoundedAndRecoveredAfterCancellation()
    {
        var target = new FakeTarget();
        await using var peer = new WirePeer(target);
        static JsonObject Filters() => new()
        {
            ["notifications"] = new JsonObject { ["resourceSubscriptions"] = new JsonArray("unodraw://selection") }
        };
        for (var id = 100; id < 108; id++)
        {
            await peer.SendAsync(id, "subscriptions/listen", Filters());
            Assert.Equal(NotificationMethods.SubscriptionsAcknowledgedNotification, (await peer.ReadAsync())["method"]!.GetValue<string>());
        }
        Assert.Equal(8, target.SubscriberCount);
        await peer.SendAsync(108, "subscriptions/listen", Filters());
        var busy = await peer.ReadAsync();
        Assert.Equal(108, busy["id"]!.GetValue<int>());
        Assert.Equal(-32000, busy["error"]!["code"]!.GetValue<int>());
        Assert.Equal(8, target.SubscriberCount);

        for (var id = 100; id < 108; id++)
            await peer.NotifyAsync("notifications/cancelled", new JsonObject { ["requestId"] = id });
        await target.NoSubscribers.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await peer.SendAsync(109, "subscriptions/listen", Filters());
        Assert.Equal(NotificationMethods.SubscriptionsAcknowledgedNotification, (await peer.ReadAsync())["method"]!.GetValue<string>());
        Assert.Equal(1, target.SubscriberCount);
    }

    [Fact]
    public async Task LegacySubscriptionNotificationsAreScopedToSubscribedUris()
    {
        var target = new FakeTarget();
        await using var peer = new WirePeer(target);
        await peer.InitializeLegacyAsync();
        var subscription = await peer.RequestAsync("resources/subscribe", new JsonObject { ["uri"] = "unodraw://selection" }, modern: false);
        Assert.NotNull(subscription["result"]);
        target.Change("unodraw://selection");
        var notification = await peer.ReadAsync();
        Assert.Equal(NotificationMethods.ResourceUpdatedNotification, notification["method"]!.GetValue<string>());
        Assert.Equal("unodraw://selection", notification["params"]!["uri"]!.GetValue<string>());
        await peer.RequestAsync("resources/unsubscribe", new JsonObject { ["uri"] = "unodraw://selection" }, modern: false);
        target.Change("unodraw://selection");
        Assert.False(peer.HasMessages);
    }

    [Fact]
    public async Task CancellationReachesTheEditorOperation()
    {
        var target = new FakeTarget { WaitForCancellation = true };
        await using var peer = new WirePeer(target);
        await peer.SendAsync(100, "tools/call", new JsonObject { ["name"] = "unodraw_document_get", ["arguments"] = new JsonObject() });
        await target.Started.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await peer.NotifyAsync("notifications/cancelled", new JsonObject { ["requestId"] = 100 });
        await target.Cancelled.Task.WaitAsync(TimeSpan.FromSeconds(5));
    }

    [Theory]
    [InlineData("invalid JSON", -32700)]
    [InlineData("[]", -32600)]
    [InlineData("{\"jsonrpc\":\"2.0\",\"id\":null,\"method\":\"tools/list\"}", -32600)]
    public async Task MessageTransportRejectsMalformedEnvelopesWithoutInvokingEditor(string message, int code)
    {
        var target = new FakeTarget();
        await using var peer = new WirePeer(target);
        await peer.ReceiveRawAsync(message);
        var error = await peer.ReadAsync();
        Assert.NotNull(error["error"]);
        Assert.Equal(code, error["error"]!["code"]!.GetValue<int>());
        Assert.Empty(target.Invocations);
    }

    private static Dictionary<string, JsonElement> Arguments(string json)
    {
        using var document = JsonDocument.Parse(json);
        return document.RootElement.EnumerateObject().ToDictionary(property => property.Name, property => property.Value.Clone());
    }

    private sealed class FakeTarget : IEditorMcpTarget
    {
        private readonly object _subscriberGate = new();
        private Action<string>? _changed;
        public ConcurrentQueue<(string Operation, JsonObject Arguments)> Invocations { get; } = new();
        public JsonNode? Result { get; set; }
        public Exception? Failure { get; set; }
        public bool WaitForCancellation { get; set; }
        public TaskCompletionSource Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Cancelled { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource NoSubscribers { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public int SubscriberCount { get { lock (_subscriberGate) return _changed?.GetInvocationList().Length ?? 0; } }

        public event Action<string>? ResourceChanged
        {
            add { lock (_subscriberGate) _changed += value; }
            remove
            {
                lock (_subscriberGate)
                {
                    _changed -= value;
                    if (_changed is null)
                        NoSubscribers.TrySetResult();
                }
            }
        }

        public void Change(string uri)
        {
            Action<string>? changed;
            lock (_subscriberGate)
                changed = _changed;
            changed?.Invoke(uri);
        }

        public async ValueTask<JsonNode?> InvokeAsync(string operation, JsonObject arguments, CancellationToken cancellationToken)
        {
            Invocations.Enqueue((operation, (JsonObject)arguments.DeepClone()));
            if (Failure is not null)
            {
                throw Failure;
            }

            if (WaitForCancellation)
            {
                Started.TrySetResult();
                try
                {
                    await Task.Delay(Timeout.Infinite, cancellationToken);
                }
                catch (OperationCanceledException)
                {
                    Cancelled.TrySetResult();
                    throw;
                }
            }

            return Result?.DeepClone() ?? new JsonObject { ["operation"] = operation, ["arguments"] = arguments.DeepClone() };
        }
    }

    private sealed class SdkConnection : IAsyncDisposable
    {
        private readonly CancellationTokenSource _lifetime;
        private readonly McpServer _server;
        private readonly Task _run;
        public McpClient Client { get; }

        private SdkConnection(McpClient client, McpServer server, Task run, CancellationTokenSource lifetime)
        {
            Client = client;
            _server = server;
            _run = run;
            _lifetime = lifetime;
        }

        public static async Task<SdkConnection> CreateAsync(IEditorMcpTarget target, string version = "2026-07-28")
        {
            var lifetime = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            McpJsonMessageTransport? serverTransport = null;
            var clientTransport = new McpJsonMessageTransport((json, token) => serverTransport!.ReceiveAsync(json, token).AsTask());
            serverTransport = new McpJsonMessageTransport((json, token) => clientTransport.ReceiveAsync(json, token).AsTask());
            var server = EditorMcpServer.Create(serverTransport, target, lifetime.Token);
            var run = server.RunAsync(lifetime.Token);
            var client = await McpClient.CreateAsync(new ExistingTransport(clientTransport),
                new McpClientOptions { ProtocolVersion = version }, cancellationToken: lifetime.Token);
            return new SdkConnection(client, server, run, lifetime);
        }

        public async ValueTask DisposeAsync()
        {
            await Client.DisposeAsync();
            await _lifetime.CancelAsync();
            await _server.DisposeAsync();
            try
            { await _run; }
            catch (OperationCanceledException) { }
            _lifetime.Dispose();
        }

        private sealed class ExistingTransport(ITransport transport) : IClientTransport
        {
            public string Name => "UnoDraw protocol test";
            public Task<ITransport> ConnectAsync(CancellationToken cancellationToken = default) => Task.FromResult(transport);
        }
    }

    private sealed class WirePeer : IAsyncDisposable
    {
        private readonly CancellationTokenSource _lifetime = new(TimeSpan.FromSeconds(30));
        private readonly Channel<JsonObject> _outgoing = Channel.CreateUnbounded<JsonObject>();
        private readonly McpJsonMessageTransport _transport;
        private readonly McpServer _server;
        private readonly Task _run;
        private int _nextId;

        public WirePeer(IEditorMcpTarget target)
        {
            _transport = new McpJsonMessageTransport((json, _) =>
            {
                _outgoing.Writer.TryWrite(JsonNode.Parse(json)!.AsObject());
                return Task.CompletedTask;
            });
            _server = EditorMcpServer.Create(_transport, target, _lifetime.Token);
            _run = _server.RunAsync(_lifetime.Token);
        }

        public bool HasMessages => _outgoing.Reader.TryPeek(out _);
        public ValueTask ReceiveRawAsync(string json) => _transport.ReceiveAsync(json, _lifetime.Token);
        public async Task<JsonObject> ReadAsync() => await _outgoing.Reader.ReadAsync(_lifetime.Token).AsTask().WaitAsync(TimeSpan.FromSeconds(5));

        public async Task<JsonObject> RequestAsync(string method, JsonObject parameters, bool modern = true)
        {
            var id = Interlocked.Increment(ref _nextId);
            await SendAsync(id, method, parameters, modern);
            var response = await ReadAsync();
            Assert.Equal(id, response["id"]!.GetValue<int>());
            return response;
        }

        public ValueTask SendAsync(int id, string method, JsonObject parameters, bool modern = true)
        {
            if (modern)
            {
                parameters["_meta"] = new JsonObject
                {
                    [MetaKeys.ProtocolVersion] = "2026-07-28",
                    [MetaKeys.ClientCapabilities] = new JsonObject(),
                    [MetaKeys.ClientInfo] = new JsonObject { ["name"] = "UnoDraw protocol tests", ["version"] = "1" }
                };
            }

            return _transport.ReceiveAsync(new JsonObject
            {
                ["jsonrpc"] = "2.0",
                ["id"] = id,
                ["method"] = method,
                ["params"] = parameters
            }.ToJsonString(), _lifetime.Token);
        }

        public ValueTask NotifyAsync(string method, JsonObject parameters)
        {
            return _transport.ReceiveAsync(new JsonObject
            {
                ["jsonrpc"] = "2.0",
                ["method"] = method,
                ["params"] = parameters
            }.ToJsonString(), _lifetime.Token);
        }

        public async Task InitializeLegacyAsync()
        {
            var result = await RequestAsync("initialize", new JsonObject
            {
                ["protocolVersion"] = "2025-11-25",
                ["capabilities"] = new JsonObject(),
                ["clientInfo"] = new JsonObject { ["name"] = "UnoDraw protocol tests", ["version"] = "1" }
            }, modern: false);
            Assert.NotNull(result["result"]);
            await NotifyAsync("notifications/initialized", new JsonObject());
        }

        public async ValueTask DisposeAsync()
        {
            await _lifetime.CancelAsync();
            await _server.DisposeAsync();
            try
            { await _run; }
            catch (OperationCanceledException) { }
            _lifetime.Dispose();
        }
    }
}
