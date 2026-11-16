using System.Net;
using System.Net.Http.Headers;
using System.Text.Json.Nodes;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;
using Svg.Editor.Mcp.Hosting;
using Xunit;

namespace Svg.Editor.Mcp.UnitTests;

[Collection("MCP integration")]
public sealed class HostingSecurityTests
{
    [Theory]
    [InlineData("http://localhost:5173/", "http://localhost:5173")]
    [InlineData("https://example.test", "https://example.test")]
    public void OriginNormalizationRetainsOnlyExactOrigins(string input, string expected)
        => Assert.Equal(expected, EditorMcpSecurity.NormalizeOrigin(input));

    [Theory]
    [InlineData("*")]
    [InlineData("null")]
    [InlineData("https://example.test/editor")]
    [InlineData("https://example.test?token=secret")]
    [InlineData("https://user:secret@example.test")]
    [InlineData("file:///editor")]
    public void OriginsCannotContainWildcardsPathsCredentialsOrOpaqueValues(string origin)
        => Assert.Throws<ArgumentException>(() => EditorMcpSecurity.NormalizeOrigin(origin));

    [Fact]
    public void TokensAreIndependentAndNeverAcceptEmptyOrPartialMatches()
    {
        var first = EditorMcpSecurity.CreateToken();
        var second = EditorMcpSecurity.CreateToken();
        Assert.Equal(64, first.Length);
        Assert.NotEqual(first, second);
        Assert.True(EditorMcpSecurity.TokenMatches(first, first));
        Assert.False(EditorMcpSecurity.TokenMatches(first, null));
        Assert.False(EditorMcpSecurity.TokenMatches(first, ""));
        Assert.False(EditorMcpSecurity.TokenMatches(first, second));
        Assert.False(EditorMcpSecurity.TokenMatches(first, first[..32]));
    }

    [Fact]
    public async Task HttpRequiresBearerAndRejectsHostAndOriginRebinding()
    {
        var target = new RecordingTarget();
        var options = new EditorMcpHostOptions { Port = 0, AllowedOrigin = "https://editor.example" };
        await using var host = await EditorMcpHost.StartAsync(target, options);
        using var client = new HttpClient();
        using var unauthorized = await client.GetAsync(host.McpEndpoint);
        Assert.Equal(HttpStatusCode.Unauthorized, unauthorized.StatusCode);
        using var wrongToken = new HttpRequestMessage(HttpMethod.Get, host.McpEndpoint);
        wrongToken.Headers.Authorization = new AuthenticationHeaderValue("Bearer", host.BrowserToken);
        using var wrongTokenResult = await client.SendAsync(wrongToken);
        Assert.Equal(HttpStatusCode.Unauthorized, wrongTokenResult.StatusCode);
        using var wrongHost = new HttpRequestMessage(HttpMethod.Get, host.McpEndpoint);
        wrongHost.Headers.Host = "attacker.example:" + host.McpEndpoint.Port;
        wrongHost.Headers.Authorization = new AuthenticationHeaderValue("Bearer", host.Token);
        using var wrongHostResult = await client.SendAsync(wrongHost);
        Assert.Equal(HttpStatusCode.Forbidden, wrongHostResult.StatusCode);
        using var wrongOrigin = new HttpRequestMessage(HttpMethod.Get, host.McpEndpoint);
        wrongOrigin.Headers.Add("Origin", "https://attacker.example");
        wrongOrigin.Headers.Authorization = new AuthenticationHeaderValue("Bearer", host.Token);
        using var wrongOriginResult = await client.SendAsync(wrongOrigin);
        Assert.Equal(HttpStatusCode.Forbidden, wrongOriginResult.StatusCode);
        using var preflight = new HttpRequestMessage(HttpMethod.Options, host.McpEndpoint);
        preflight.Headers.Add("Origin", "https://editor.example");
        using var preflightResult = await client.SendAsync(preflight);
        Assert.Equal(HttpStatusCode.NoContent, preflightResult.StatusCode);
        Assert.Equal("https://editor.example", preflightResult.Headers.GetValues("Access-Control-Allow-Origin").Single());
        Assert.Equal(0, target.Invocations);
    }

    [Theory]
    [InlineData("2025-11-25")]
    [InlineData("2026-07-28")]
    public async Task OfficialClientNegotiatesBothProtocolGenerationsAndInvokesTools(string version)
    {
        var target = new RecordingTarget();
        await using var host = await EditorMcpHost.StartAsync(target, new EditorMcpHostOptions { Port = 0 });
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        await using var transport = new HttpClientTransport(new HttpClientTransportOptions
        {
            Endpoint = host.McpEndpoint,
            TransportMode = HttpTransportMode.StreamableHttp,
            AdditionalHeaders = new Dictionary<string, string> { ["Authorization"] = "Bearer " + host.Token }
        });
        await using var client = await McpClient.CreateAsync(transport,
            new McpClientOptions { ProtocolVersion = version }, cancellationToken: timeout.Token);
        Assert.Equal("UnoDraw", client.ServerInfo?.Name);
        var tools = await client.ListToolsAsync(cancellationToken: timeout.Token);
        Assert.Contains(tools, tool => tool.Name == "unodraw_document_list");
        var result = await client.CallToolAsync("unodraw_document_list", cancellationToken: timeout.Token);
        Assert.False(result.IsError);
        Assert.Contains(result.Content.OfType<TextContentBlock>(), content => content.Text.Contains("From transport", StringComparison.Ordinal));
        Assert.Equal(1, target.Invocations);
    }

    [Fact]
    public async Task LegacyHttpResourceUpdatesOutliveTheSubscriptionPost()
    {
        var target = new RecordingTarget();
        await using var host = await EditorMcpHost.StartAsync(target, new EditorMcpHostOptions { Port = 0 });
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        await using var transport = new HttpClientTransport(new HttpClientTransportOptions
        {
            Endpoint = host.McpEndpoint,
            TransportMode = HttpTransportMode.StreamableHttp,
            AdditionalHeaders = new Dictionary<string, string> { ["Authorization"] = "Bearer " + host.Token }
        });
        await using var client = await McpClient.CreateAsync(transport,
            new McpClientOptions { ProtocolVersion = "2025-11-25" }, cancellationToken: timeout.Token);
        var updated = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        await using var subscription = await client.SubscribeToResourceAsync("unodraw://selection", (notification, _) =>
        {
            updated.TrySetResult(notification.Uri);
            return ValueTask.CompletedTask;
        }, cancellationToken: timeout.Token);
        // The subscribe response has completed; updates must use the session's active GET stream.
        target.Change("unodraw://selection");
        Assert.Equal("unodraw://selection", await updated.Task.WaitAsync(TimeSpan.FromSeconds(5)));
    }

    [Fact]
    public async Task ReadOnlyWrapperRejectsMutationsBeforeForwardingAndKeepsReads()
    {
        var target = new RecordingTarget();
        var readOnly = new EditorMcpReadOnlyTarget(target);
        await readOnly.InvokeAsync("unodraw_document_list", new JsonObject(), default);
        Assert.Equal(1, target.Invocations);
        await Assert.ThrowsAsync<EditorMcpException>(async () =>
            await readOnly.InvokeAsync("unodraw_document_new", new JsonObject(), default));
        await Assert.ThrowsAsync<EditorMcpException>(async () =>
            await readOnly.InvokeAsync("an_unknown_operation", new JsonObject(), default));
        Assert.Equal(1, target.Invocations);
    }

    private sealed class RecordingTarget : IEditorMcpTarget
    {
        public int Invocations { get; private set; }
        public event Action<string>? ResourceChanged;
        public void Change(string uri) => ResourceChanged?.Invoke(uri);
        public ValueTask<JsonNode?> InvokeAsync(string operation, JsonObject arguments, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Invocations++;
            return ValueTask.FromResult<JsonNode?>(new JsonObject
            { ["documents"] = new JsonArray(new JsonObject { ["name"] = "From transport", ["documentId"] = "d1" }) });
        }
    }
}
