using System.Threading.Channels;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace Svg.Editor.Mcp;

/// <summary>
/// Bounds expensive stdio/in-process requests through response delivery. HTTP hosts
/// apply their own admission budget across sessions and response streams.
/// </summary>
internal sealed class EditorMcpRequestBudgetTransport : ITransport
{
    private const int MaximumOperations = 2;
    private const int MaximumSubscriptions = 8;
    private const int MaximumRejections = 8;
    private const int MaximumOutgoingMessages = 16;
    private readonly ITransport _inner;
    private readonly CancellationTokenSource _lifetime;
    private readonly SemaphoreSlim _operations = new(MaximumOperations, MaximumOperations);
    private readonly SemaphoreSlim _subscriptions = new(MaximumSubscriptions, MaximumSubscriptions);
    private readonly SemaphoreSlim _rejections = new(MaximumRejections, MaximumRejections);
    private readonly SemaphoreSlim _outgoing = new(MaximumOutgoingMessages, MaximumOutgoingMessages);
    private int _disposed;

    public EditorMcpRequestBudgetTransport(ITransport inner, CancellationToken lifetimeToken)
    {
        _inner = inner;
        _lifetime = CancellationTokenSource.CreateLinkedTokenSource(lifetimeToken);
        LifetimeToken = _lifetime.Token;
    }

    public CancellationToken LifetimeToken { get; }
    public ChannelReader<JsonRpcMessage> MessageReader => _inner.MessageReader;
    public string? SessionId => _inner.SessionId;

    public void Configure(McpServerOptions options)
    {
        options.Filters.Message.IncomingFilters.Add(next => async (context, cancellationToken) =>
        {
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, LifetimeToken);
            linked.Token.ThrowIfCancellationRequested();
            var request = context.JsonRpcMessage as JsonRpcRequest;
            var budget = request?.Method switch
            {
                "tools/call" or "resources/read" => _operations,
                "subscriptions/listen" => _subscriptions,
                _ => null
            };
            if (budget is null)
            {
                // Cancellation and control messages must stay live while the
                // editor or a client reading a large response occupies the budget.
                await next(context, linked.Token).ConfigureAwait(false);
                return;
            }

            if (budget.Wait(0))
            {
                try
                {
                    // The SDK message pipeline awaits the complete response send,
                    // unlike a tool handler/filter which ends before serialization.
                    await next(context, linked.Token).ConfigureAwait(false);
                }
                finally { budget.Release(); }
                return;
            }

            if (!_rejections.Wait(0))
            {
                // A peer that continues flooding while refusing to read cannot
                // retain an unbounded set of pending error responses either.
                await DisposeAsync().ConfigureAwait(false);
                return;
            }

            try
            {
                await context.Server.SendMessageAsync(new JsonRpcError
                {
                    Id = request!.Id,
                    Error = new JsonRpcErrorDetail
                    {
                        Code = -32000,
                        Message = ReferenceEquals(budget, _subscriptions)
                            ? "Eight subscription streams are already active. Cancel a subscription before retrying."
                            : "The editor is busy delivering two operations. Read pending responses before retrying."
                    }
                }, linked.Token).ConfigureAwait(false);
            }
            finally { _rejections.Release(); }
        });
    }

    public async Task SendMessageAsync(JsonRpcMessage message, CancellationToken cancellationToken = default)
    {
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, LifetimeToken);
        linked.Token.ThrowIfCancellationRequested();
        if (!_outgoing.Wait(0))
        {
            await DisposeAsync().ConfigureAwait(false);
            throw new OperationCanceledException("The client is not reading MCP responses.", LifetimeToken);
        }

        try
        {
            await _inner.SendMessageAsync(message, linked.Token).ConfigureAwait(false);
        }
        finally { _outgoing.Release(); }
    }

    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
            return;
        await _lifetime.CancelAsync().ConfigureAwait(false);
        await _inner.DisposeAsync().ConfigureAwait(false);
        _lifetime.Dispose();
    }
}
