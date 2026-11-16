using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json.Nodes;
using Svg.Editor.Mcp.Hosting;
using Xunit;

namespace Svg.Editor.Mcp.UnitTests;

[Collection("MCP integration")]
public sealed class HttpAdmissionTests
{
    [Fact]
    public async Task SaturatedResponsesKeepCancellationAvailableAndRejectHeaderBypass()
    {
        var target = new PausedTarget();
        await using var host = await EditorMcpHost.StartAsync(target, new EditorMcpHostOptions { Port = 0 });
        using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(15) };
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", host.Token);
        client.DefaultRequestHeaders.Accept.ParseAdd("application/json, text/event-stream");
        using var initialized = await client.PostAsync(host.McpEndpoint, Json("""
            {"jsonrpc":"2.0","id":1,"method":"initialize","params":{"protocolVersion":"2025-11-25","capabilities":{},"clientInfo":{"name":"AdmissionTests","version":"1"}}}
            """));
        initialized.EnsureSuccessStatusCode();
        client.DefaultRequestHeaders.Add("Mcp-Session-Id", initialized.Headers.GetValues("Mcp-Session-Id").Single());
        client.DefaultRequestHeaders.Add("MCP-Protocol-Version", "2025-11-25");
        using var ready = await client.PostAsync(host.McpEndpoint, Json("""
            {"jsonrpc":"2.0","method":"notifications/initialized"}
            """));
        ready.EnsureSuccessStatusCode();

        using var stop = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var first = client.PostAsync(host.McpEndpoint, Json("""
            {"jsonrpc":"2.0","id":101,"method":"tools/call","params":{"name":"unodraw_document_list","arguments":{}}}
            """), stop.Token);
        var second = client.PostAsync(host.McpEndpoint, Json("""
            {"jsonrpc":"2.0","id":102,"method":"tools/call","params":{"name":"unodraw_document_list","arguments":{}}}
            """), stop.Token);
        try
        {
            await target.BothStarted.Task.WaitAsync(stop.Token);
            using var overflow = new HttpRequestMessage(HttpMethod.Post, host.McpEndpoint)
            {
                Content = Json("""{"jsonrpc":"2.0","id":103,"method":"tools/list"}""")
            };
            // The older protocol does not validate this header. It must not let
            // an ordinary response impersonate a subscription or cancellation.
            overflow.Headers.Add("MCP-Method", "subscriptions/listen");
            using var rejected = await client.SendAsync(overflow, stop.Token);
            Assert.Equal(HttpStatusCode.TooManyRequests, rejected.StatusCode);
            Assert.Equal("1", rejected.Headers.GetValues("Retry-After").Single());
            using var cancelled = await client.PostAsync(host.McpEndpoint, Json("""
                {"jsonrpc":"2.0","method":"notifications/cancelled","params":{"requestId":101,"reason":"admission test"}}
                """), stop.Token);
            Assert.Equal(HttpStatusCode.Accepted, cancelled.StatusCode);
            await target.Cancelled.Task.WaitAsync(stop.Token);
            Assert.Equal(2, target.Invocations);
        }
        finally
        {
            stop.Cancel();
            await FinishAsync(first);
            await FinishAsync(second);
        }
    }

    private static StringContent Json(string value) => new(value, Encoding.UTF8, "application/json");

    private static async Task FinishAsync(Task<HttpResponseMessage> request)
    {
        try
        { (await request).Dispose(); }
        catch (OperationCanceledException) { }
        catch (HttpRequestException) { }
    }

    private sealed class PausedTarget : IEditorMcpTarget
    {
        private int _invocations;
        public int Invocations => Volatile.Read(ref _invocations);
        public TaskCompletionSource BothStarted { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Cancelled { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public event Action<string>? ResourceChanged { add { } remove { } }
        public async ValueTask<JsonNode?> InvokeAsync(string operation, JsonObject arguments, CancellationToken cancellationToken)
        {
            if (Interlocked.Increment(ref _invocations) == 2)
                BothStarted.TrySetResult();
            try
            { await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken); }
            catch (OperationCanceledException) { Cancelled.TrySetResult(); throw; }
            return null;
        }
    }
}
