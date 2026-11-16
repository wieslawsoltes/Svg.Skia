using System.Text.Json.Nodes;
using ModelContextProtocol;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace Svg.Editor.Mcp;

internal sealed partial class EditorMcpRegistration
{
    private static readonly IReadOnlyList<Resource> s_resources = new[]
    {
        Resource("unodraw://artboards", "Artboards", "The current document's artboards and active artboard."),
        Resource("unodraw://commands", "Commands", "Available editor commands, availability, and keyboard shortcuts."),
        Resource("unodraw://documents", "Documents", "Open documents and the active document."),
        Resource("unodraw://guides", "Guides", "The current document's ruler guides."),
        Resource("unodraw://selection", "Selection", "Selected elements and selection bounds."),
        Resource("unodraw://tools", "Drawing tools", "The editor's drawing tools and active tool."),
        Resource("unodraw://view", "View", "Canvas zoom, viewport, grid, rulers, and snapping state."),
        Resource("unodraw://workspace", "Workspace", "Workspace, document, selection, and UI context.")
    };

    private static readonly IReadOnlyList<ResourceTemplate> s_resourceTemplates = new[]
    {
        new ResourceTemplate
        {
            Name = "Document source",
            Description = "The complete SVG source of an open document. Use a documentId from unodraw://documents.",
            UriTemplate = "unodraw://document/{documentId}/source",
            MimeType = "image/svg+xml"
        },
        new ResourceTemplate
        {
            Name = "Document elements",
            Description = "The element tree of an open document, including reusable element references.",
            UriTemplate = "unodraw://document/{documentId}/elements",
            MimeType = "application/json"
        }
    };

    public ValueTask<ListResourcesResult> ListResourcesAsync(RequestContext<ListResourcesRequestParams> request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var (resources, cursor) = Page(s_resources, request.Params?.Cursor, "resources");
        return ValueTask.FromResult(new ListResourcesResult
        {
            Resources = resources,
            NextCursor = cursor,
            TimeToLive = TimeSpan.FromMinutes(5),
            CacheScope = CacheScope.Public
        });
    }

    public ValueTask<ListResourceTemplatesResult> ListResourceTemplatesAsync(RequestContext<ListResourceTemplatesRequestParams> request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var (templates, cursor) = Page(s_resourceTemplates, request.Params?.Cursor, "templates");
        return ValueTask.FromResult(new ListResourceTemplatesResult
        {
            ResourceTemplates = templates,
            NextCursor = cursor,
            TimeToLive = TimeSpan.FromMinutes(5),
            CacheScope = CacheScope.Public
        });
    }

    public async ValueTask<ReadResourceResult> ReadResourceAsync(RequestContext<ReadResourceRequestParams> request, CancellationToken cancellationToken)
    {
        if (request.Params is null || !TryResolveResource(request.Params.Uri, out var operation, out var arguments, out var source))
        {
            throw new McpProtocolException("Unknown UnoDraw resource URI.", McpErrorCode.InvalidParams);
        }

        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _lifetimeToken);
        try
        {
            var value = await _target.InvokeAsync(operation, arguments, linked.Token).ConfigureAwait(false);
            var text = source
                ? value?["svg"]?.GetValue<string>() ?? throw new EditorMcpException("The requested document has no SVG source.")
                : value?.ToJsonString() ?? "null";
            return new ReadResourceResult
            {
                TimeToLive = TimeSpan.Zero,
                CacheScope = CacheScope.Private,
                Contents = [new TextResourceContents
                {
                    Uri = request.Params.Uri,
                    MimeType = source ? "image/svg+xml" : "application/json",
                    Text = text
                }]
            };
        }
        catch (Exception exception) when (exception is EditorMcpException or ArgumentException or InvalidOperationException)
        {
            throw new McpProtocolException(exception.Message, McpErrorCode.InvalidParams);
        }
    }

    internal static bool TryResolveResource(string uri, out string operation, out JsonObject arguments, out bool source)
    {
        source = false;
        arguments = new JsonObject();
        operation = uri switch
        {
            "unodraw://artboards" => "unodraw_artboard_list",
            "unodraw://commands" => "unodraw_commands_list",
            "unodraw://documents" => "unodraw_document_list",
            "unodraw://guides" => "unodraw_guide_list",
            "unodraw://selection" => "unodraw_selection_get",
            "unodraw://tools" => "unodraw_tools_list",
            "unodraw://view" => "unodraw_view_get",
            "unodraw://workspace" => "unodraw_workspace_get",
            _ => string.Empty
        };
        if (operation.Length != 0)
        {
            return true;
        }

        const string prefix = "unodraw://document/";
        if (uri.Length > 2048 || !uri.StartsWith(prefix, StringComparison.Ordinal))
        {
            return false;
        }

        var parts = uri[prefix.Length..].Split('/');
        if (parts.Length != 2 || parts[1] is not ("source" or "elements"))
        {
            return false;
        }

        var documentId = Uri.UnescapeDataString(parts[0]);
        if (documentId.Length is 0 or > 256 || documentId is "." or ".."
            || documentId.IndexOfAny(['/', '\\', '?', '#', '\0']) >= 0)
        {
            return false;
        }

        arguments["documentId"] = documentId;
        source = parts[1] == "source";
        operation = source ? "unodraw_document_get" : "unodraw_element_list";
        return true;
    }

    private static Resource Resource(string uri, string name, string description)
    {
        return new Resource { Uri = uri, Name = name, Description = description, MimeType = "application/json" };
    }
}
