using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization.Metadata;
using System.Threading.Channels;
using ModelContextProtocol;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace Svg.Editor.Mcp;

internal sealed partial class EditorMcpRegistration
{
    private static readonly JsonTypeInfo<RequestId> s_requestIdTypeInfo =
        (JsonTypeInfo<RequestId>)McpJsonUtilities.DefaultOptions.GetTypeInfo(typeof(RequestId));

    public ValueTask<EmptyResult> SubscribeAsync(RequestContext<SubscribeRequestParams> request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var uri = request.Params?.Uri;
        ValidateSubscriptionUri(uri);
        _legacySubscriptions.Subscribe(request.Server, uri!);
        return ValueTask.FromResult(new EmptyResult());
    }

    public ValueTask<EmptyResult> UnsubscribeAsync(RequestContext<UnsubscribeRequestParams> request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var uri = request.Params?.Uri;
        ValidateSubscriptionUri(uri);
        _legacySubscriptions.Unsubscribe(uri!);
        return ValueTask.FromResult(new EmptyResult());
    }

    public async ValueTask<EmptyResult> ListenAsync(RequestContext<SubscriptionsListenRequestParams> request, CancellationToken cancellationToken)
    {
        var requested = request.Params?.Notifications
            ?? throw new McpProtocolException("Subscription notification filters are required.", McpErrorCode.InvalidParams);
        var uris = (requested.ResourceSubscriptions ?? []).Distinct(StringComparer.Ordinal).ToArray();
        if (uris.Length > 128)
        {
            throw new McpProtocolException("At most 128 resource subscriptions are allowed per listen request.", McpErrorCode.InvalidParams);
        }

        foreach (var uri in uris)
        {
            ValidateSubscriptionUri(uri);
        }

        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _lifetimeToken);
        var signal = Channel.CreateBounded<bool>(new BoundedChannelOptions(1)
        {
            FullMode = BoundedChannelFullMode.DropWrite,
            SingleReader = true,
            SingleWriter = false,
            AllowSynchronousContinuations = false
        });
        var gate = new object();
        var dirty = new HashSet<string>(StringComparer.Ordinal);
        var subscriptions = uris.ToHashSet(StringComparer.Ordinal);
        var subscriptionId = JsonSerializer.SerializeToNode(request.JsonRpcRequest.Id, s_requestIdTypeInfo);

        void OnResourceChanged(string uri)
        {
            lock (gate)
            {
                if (uri == "*")
                {
                    dirty.UnionWith(subscriptions);
                }
                else if (subscriptions.Contains(uri))
                {
                    dirty.Add(uri);
                }

                if (dirty.Count != 0)
                {
                    signal.Writer.TryWrite(true);
                }
            }
        }

        _target.ResourceChanged += OnResourceChanged;
        try
        {
            var filters = new JsonObject();
            if (requested.ResourceSubscriptions is not null)
            {
                filters["resourceSubscriptions"] = new JsonArray(uris.Select(uri => (JsonNode?)JsonValue.Create(uri)).ToArray());
            }

            await request.Server.SendMessageAsync(new JsonRpcNotification
            {
                Method = NotificationMethods.SubscriptionsAcknowledgedNotification,
                Params = new JsonObject
                {
                    ["notifications"] = filters,
                    ["_meta"] = new JsonObject { [MetaKeys.SubscriptionId] = subscriptionId?.DeepClone() }
                }
            }, linked.Token).ConfigureAwait(false);

            await foreach (var _ in signal.Reader.ReadAllAsync(linked.Token).ConfigureAwait(false))
            {
                string[] changed;
                lock (gate)
                {
                    changed = dirty.OrderBy(uri => uri, StringComparer.Ordinal).ToArray();
                    dirty.Clear();
                }

                foreach (var uri in changed)
                {
                    await request.Server.SendMessageAsync(new JsonRpcNotification
                    {
                        Method = NotificationMethods.ResourceUpdatedNotification,
                        Params = new JsonObject
                        {
                            ["uri"] = uri,
                            ["_meta"] = new JsonObject { [MetaKeys.SubscriptionId] = subscriptionId?.DeepClone() }
                        }
                    }, linked.Token).ConfigureAwait(false);
                }
            }

            return new EmptyResult();
        }
        finally
        {
            _target.ResourceChanged -= OnResourceChanged;
            signal.Writer.TryComplete();
        }
    }

    private static void ValidateSubscriptionUri(string? uri)
    {
        if (uri is null || !TryResolveResource(uri, out _, out _, out _))
        {
            throw new McpProtocolException("Unknown UnoDraw resource URI.", McpErrorCode.InvalidParams);
        }
    }
}

/// <summary>
/// Legacy sessions retain their own subscriptions. The target event hub only holds weak
/// references, so a closed SDK session cannot be kept alive by a long-lived editor window.
/// </summary>
internal sealed class EditorMcpLegacySubscriptions : IDisposable
{
    private static readonly ConditionalWeakTable<IEditorMcpTarget, ChangeHub> s_hubs = new();
    private readonly IEditorMcpTarget _target;
    private readonly CancellationToken _lifetimeToken;
    private readonly object _gate = new();
    private readonly HashSet<string> _uris = new(StringComparer.Ordinal);
    private readonly HashSet<string> _dirty = new(StringComparer.Ordinal);
    private McpServer? _server;
    private CancellationTokenRegistration _lifetimeRegistration;
    private bool _registered;
    private bool _sending;
    private bool _disposed;

    public EditorMcpLegacySubscriptions(IEditorMcpTarget target, CancellationToken lifetimeToken)
    {
        _target = target;
        _lifetimeToken = lifetimeToken;
    }

    public void Subscribe(McpServer server, string uri)
    {
        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            _lifetimeToken.ThrowIfCancellationRequested();
            if (_uris.Count >= 128 && !_uris.Contains(uri))
            {
                throw new McpProtocolException("At most 128 resources may be subscribed in a session.", McpErrorCode.InvalidParams);
            }

            _server = server;
            _uris.Add(uri);
            if (!_registered)
            {
                _registered = true;
                s_hubs.GetValue(_target, static target => new ChangeHub(target)).Add(this);
                var weak = new WeakReference<EditorMcpLegacySubscriptions>(this);
                _lifetimeRegistration = _lifetimeToken.Register(static state =>
                {
                    if (((WeakReference<EditorMcpLegacySubscriptions>)state!).TryGetTarget(out var subscription))
                    {
                        subscription.Dispose();
                    }
                }, weak);
            }
        }
    }

    public void Unsubscribe(string uri)
    {
        lock (_gate)
        {
            _uris.Remove(uri);
            _dirty.Remove(uri);
            if (_uris.Count == 0)
            {
                _server = null;
            }
        }
    }

    public void Dispose()
    {
        lock (_gate)
        {
            _disposed = true;
            _sending = false;
            _server = null;
            _uris.Clear();
            _dirty.Clear();
        }

        _lifetimeRegistration.Unregister();
    }

    private void Changed(string uri)
    {
        lock (_gate)
        {
            if (_disposed || _server is null)
            {
                return;
            }

            if (uri == "*")
            {
                _dirty.UnionWith(_uris);
            }
            else if (_uris.Contains(uri))
            {
                _dirty.Add(uri);
            }

            if (_sending || _dirty.Count == 0)
            {
                return;
            }

            _sending = true;
        }

        _ = SendChangesAsync();
    }

    private async Task SendChangesAsync()
    {
        try
        {
            while (true)
            {
                string[] changed;
                McpServer server;
                lock (_gate)
                {
                    if (_disposed || _server is null || _dirty.Count == 0)
                    {
                        _sending = false;
                        return;
                    }

                    server = _server;
                    changed = _dirty.OrderBy(uri => uri, StringComparer.Ordinal).ToArray();
                    _dirty.Clear();
                }

                using var timeout = CancellationTokenSource.CreateLinkedTokenSource(_lifetimeToken);
                timeout.CancelAfter(TimeSpan.FromSeconds(10));
                foreach (var uri in changed)
                {
                    lock (_gate)
                    {
                        if (_disposed || !_uris.Contains(uri))
                        {
                            continue;
                        }
                    }

                    await server.SendMessageAsync(new JsonRpcNotification
                    {
                        Method = NotificationMethods.ResourceUpdatedNotification,
                        Params = new JsonObject { ["uri"] = uri }
                    }, timeout.Token).ConfigureAwait(false);
                }
            }
        }
        catch (Exception)
        {
            // A transport that has closed or cannot drain is no longer a subscriber.
            Dispose();
        }
    }

    private sealed class ChangeHub
    {
        private readonly object _gate = new();
        private readonly List<WeakReference<EditorMcpLegacySubscriptions>> _subscriptions = [];

        public ChangeHub(IEditorMcpTarget target)
        {
            target.ResourceChanged += OnChanged;
        }

        public void Add(EditorMcpLegacySubscriptions subscription)
        {
            lock (_gate)
            {
                _subscriptions.RemoveAll(reference => !reference.TryGetTarget(out _));
                _subscriptions.Add(new WeakReference<EditorMcpLegacySubscriptions>(subscription));
            }
        }

        private void OnChanged(string uri)
        {
            EditorMcpLegacySubscriptions[] subscriptions;
            lock (_gate)
            {
                _subscriptions.RemoveAll(reference => !reference.TryGetTarget(out _));
                subscriptions = _subscriptions.Select(reference => reference.TryGetTarget(out var value) ? value : null)
                    .OfType<EditorMcpLegacySubscriptions>().ToArray();
            }

            foreach (var subscription in subscriptions)
            {
                subscription.Changed(uri);
            }
        }
    }
}
