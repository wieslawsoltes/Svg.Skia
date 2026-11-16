using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using ModelContextProtocol;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace Svg.Editor.Mcp;

/// <summary>Creates the same MCP surface for desktop, browser relay, and in-process hosts.</summary>
public static class EditorMcpServer
{
    public const string ServerName = "UnoDraw";
    public const string ServerVersion = "1.0.0";
    public const int PageSize = 32;

    public static McpServer Create(ITransport transport, IEditorMcpTarget target, CancellationToken lifetimeToken = default)
    {
        ArgumentNullException.ThrowIfNull(transport);
        ArgumentNullException.ThrowIfNull(target);
        var bounded = new EditorMcpRequestBudgetTransport(transport, lifetimeToken);
        var options = CreateOptions(target, bounded.LifetimeToken);
        bounded.Configure(options);
        return McpServer.Create(bounded, options);
    }

    public static McpServerOptions CreateOptions(IEditorMcpTarget target, CancellationToken lifetimeToken = default)
    {
        var options = new McpServerOptions();
        Configure(options, target, lifetimeToken);
        return options;
    }

    /// <summary>
    /// Configures an SDK host without replacing its transport. The optional registration can be
    /// disposed by a host to release legacy subscriptions before its lifetime token is cancelled.
    /// </summary>
    public static IDisposable Configure(McpServerOptions options, IEditorMcpTarget target, CancellationToken lifetimeToken = default)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(target);
        var registration = new EditorMcpRegistration(target, lifetimeToken);
        options.ServerInfo = new Implementation { Name = ServerName, Version = ServerVersion, Title = "UnoDraw editor" };
        // null deliberately supports both per-request metadata and initialize-handshake clients.
        options.ProtocolVersion = null;
        options.ScopeRequests = false;
        options.ServerInstructions = "Edit the user's open UnoDraw workspace. Read workspace and document state before edits. "
            + "Use documentId to target a document and expectedRevision for optimistic concurrency. "
            + "Element references are unique SVG IDs or revision-scoped paths. Editor changes use the existing undo history. "
            + "Discover supported commands and drawing tools before invoking them. Export returns data to the client. "
            + "Interactive commands require explicit opt-in; arbitrary code execution is not exposed.";
        options.Capabilities = new ServerCapabilities
        {
            Tools = new ToolsCapability { ListChanged = false },
            Resources = new ResourcesCapability { Subscribe = true, ListChanged = false },
            Prompts = new PromptsCapability { ListChanged = false },
            Completions = new CompletionsCapability()
        };
        options.Handlers = new McpServerHandlers
        {
            ListToolsHandler = registration.ListToolsAsync,
            CallToolHandler = registration.CallToolAsync,
            ListResourcesHandler = registration.ListResourcesAsync,
            ListResourceTemplatesHandler = registration.ListResourceTemplatesAsync,
            ReadResourceHandler = registration.ReadResourceAsync,
            ListPromptsHandler = registration.ListPromptsAsync,
            GetPromptHandler = registration.GetPromptAsync,
            CompleteHandler = registration.CompleteAsync,
            SubscribeToResourcesHandler = registration.SubscribeAsync,
            UnsubscribeFromResourcesHandler = registration.UnsubscribeAsync,
            SubscriptionsListenHandler = registration.ListenAsync
        };
        return registration;
    }
}

internal sealed partial class EditorMcpRegistration : IDisposable
{
    private readonly IEditorMcpTarget _target;
    private readonly CancellationToken _lifetimeToken;
    private readonly IReadOnlyList<EditorMcpTool> _tools;
    private readonly Dictionary<string, EditorMcpTool> _toolsByName;
    private readonly EditorMcpLegacySubscriptions _legacySubscriptions;

    public EditorMcpRegistration(IEditorMcpTarget target, CancellationToken lifetimeToken)
    {
        _target = target;
        _lifetimeToken = lifetimeToken;
        _tools = EditorMcpCatalog.Tools.OrderBy(tool => tool.Name, StringComparer.Ordinal).ToArray();
        _toolsByName = _tools.ToDictionary(tool => tool.Name, StringComparer.Ordinal);
        _legacySubscriptions = new EditorMcpLegacySubscriptions(target, lifetimeToken);
    }

    public ValueTask<ListToolsResult> ListToolsAsync(RequestContext<ListToolsRequestParams> request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var (tools, cursor) = Page(_tools, request.Params?.Cursor, "tools");
        return ValueTask.FromResult(new ListToolsResult
        {
            Tools = tools.Select(tool => new Tool
            {
                Name = tool.Name,
                Description = tool.Description,
                InputSchema = tool.InputSchema,
                Annotations = new ToolAnnotations
                {
                    ReadOnlyHint = tool.ReadOnly,
                    DestructiveHint = tool.Destructive,
                    IdempotentHint = tool.Idempotent || tool.ReadOnly,
                    // Documents can contain external SVG references. This hint must not imply
                    // that the existing renderer or an interactive command cannot access them.
                    OpenWorldHint = true
                }
            }).ToArray(),
            NextCursor = cursor,
            TimeToLive = TimeSpan.FromMinutes(5),
            CacheScope = CacheScope.Public
        });
    }

    public async ValueTask<CallToolResult> CallToolAsync(RequestContext<CallToolRequestParams> request, CancellationToken cancellationToken)
    {
        var parameters = request.Params;
        if (parameters is null || !_toolsByName.TryGetValue(parameters.Name, out var tool))
        {
            throw new McpProtocolException("Unknown UnoDraw tool. Call tools/list to discover available tools.", McpErrorCode.InvalidParams);
        }

        cancellationToken.ThrowIfCancellationRequested();
        _lifetimeToken.ThrowIfCancellationRequested();
        try
        {
            var arguments = new JsonObject();
            if (parameters.Arguments is not null)
            {
                foreach (var argument in parameters.Arguments)
                {
                    arguments.Add(argument.Key, JsonNode.Parse(argument.Value.GetRawText()));
                }
            }

            EditorMcpSchemaValidator.Validate(tool.InputSchema,
                JsonSerializer.SerializeToElement(arguments, EditorMcpJsonContext.Default.JsonObject));
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _lifetimeToken);
            var result = await _target.InvokeAsync(tool.Name, arguments, linked.Token).ConfigureAwait(false);
            return CreateToolResult(result);
        }
        catch (EditorMcpException exception)
        {
            return ToolError(exception.Message);
        }
        catch (ArgumentException exception)
        {
            return ToolError(exception.Message);
        }
        catch (InvalidOperationException exception)
        {
            return ToolError(exception.Message);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested || _lifetimeToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception)
        {
            return ToolError("The editor could not complete this operation. Read the workspace state and retry with valid arguments.");
        }
    }

    public void Dispose()
    {
        _legacySubscriptions.Dispose();
    }

    private static CallToolResult CreateToolResult(JsonNode? value)
    {
        var result = value as JsonObject ?? new JsonObject { ["result"] = value?.DeepClone() };
        var content = new List<ContentBlock>();
        if (result["mimeType"] is JsonValue mimeValue && mimeValue.TryGetValue<string>(out var mimeType)
            && result["base64"] is JsonValue dataValue && dataValue.TryGetValue<string>(out var data))
        {
            // Carry large export bytes exactly once. Duplicating them in text and structured
            // metadata would inflate a permitted 16 MiB export beyond the transport budget.
            var metadata = new JsonObject();
            foreach (var property in result)
            {
                if (property.Key != "base64")
                    metadata.Add(property.Key, property.Value?.DeepClone());
            }
            result = metadata;
            var encoded = Encoding.UTF8.GetBytes(data);
            if (mimeType is "image/png" or "image/jpeg" or "image/webp")
            {
                content.Add(new ImageContentBlock { MimeType = mimeType, Data = encoded });
                result["contentType"] = "image";
            }
            else
            {
                // The UUID identifies this embedded value; it is not a retained server resource.
                var uri = "urn:uuid:" + Guid.NewGuid().ToString("D");
                content.Add(new EmbeddedResourceBlock
                {
                    Resource = new BlobResourceContents { Uri = uri, MimeType = mimeType, Blob = encoded }
                });
                result["contentType"] = "resource";
                result["resourceUri"] = uri;
            }

            content.Insert(0, new TextContentBlock { Text = result.ToJsonString() });
        }
        else
        {
            content.Add(new TextContentBlock { Text = result.ToJsonString() });
        }

        return new CallToolResult
        {
            Content = content,
            StructuredContent = JsonSerializer.SerializeToElement(result, EditorMcpJsonContext.Default.JsonObject),
            IsError = false
        };
    }

    private static CallToolResult ToolError(string message)
    {
        return new CallToolResult
        {
            IsError = true,
            Content = [new TextContentBlock { Text = message }]
        };
    }

    private static (IList<T> Values, string? NextCursor) Page<T>(IReadOnlyList<T> values, string? cursor, string kind)
    {
        var offset = 0;
        if (cursor is not null)
        {
            try
            {
                var decoded = Encoding.UTF8.GetString(Convert.FromBase64String(cursor));
                var parts = decoded.Split(':');
                if (parts.Length != 3 || parts[0] != "unodraw-v1" || parts[1] != kind
                    || !int.TryParse(parts[2], NumberStyles.None, CultureInfo.InvariantCulture, out offset)
                    || offset <= 0 || offset >= values.Count || offset % EditorMcpServer.PageSize != 0)
                {
                    throw new FormatException();
                }
            }
            catch (FormatException)
            {
                throw new McpProtocolException("Invalid or expired pagination cursor.", McpErrorCode.InvalidParams);
            }
        }

        var page = values.Skip(offset).Take(EditorMcpServer.PageSize).ToArray();
        var next = offset + page.Length;
        return (page, next < values.Count
            ? Convert.ToBase64String(Encoding.UTF8.GetBytes($"unodraw-v1:{kind}:{next.ToString(CultureInfo.InvariantCulture)}"))
            : null);
    }
}
