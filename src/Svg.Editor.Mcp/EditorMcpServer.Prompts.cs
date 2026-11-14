using System.Text.Json;
using System.Text.Json.Nodes;
using ModelContextProtocol;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace Svg.Editor.Mcp;

internal sealed partial class EditorMcpRegistration
{
    private static readonly IReadOnlyList<Prompt> s_prompts = new[]
    {
        new Prompt
        {
            Name = "create-artwork",
            Title = "Create artwork",
            Description = "Create editable vector artwork in a new or explicitly selected document.",
            Arguments = [new PromptArgument { Name = "brief", Description = "The desired artwork and design constraints.", Required = true }]
        },
        new Prompt
        {
            Name = "edit-selection",
            Title = "Edit selected artwork",
            Description = "Modify the current selection while preserving surrounding artwork and undo history.",
            Arguments = [new PromptArgument { Name = "instruction", Description = "The requested changes to selected elements.", Required = true }]
        },
        new Prompt
        {
            Name = "export-document",
            Title = "Export a document",
            Description = "Inspect the document and export the requested output format.",
            Arguments =
            [
                new PromptArgument { Name = "documentId", Description = "An open document ID; omitted means the active document." },
                new PromptArgument { Name = "format", Description = "The desired export format, such as svg or png.", Required = true }
            ]
        },
        new Prompt
        {
            Name = "inspect-document",
            Title = "Inspect a document",
            Description = "Review document structure, styling, geometry, and export readiness without changing artwork.",
            Arguments =
            [
                new PromptArgument { Name = "documentId", Description = "An open document ID; omitted means the active document." },
                new PromptArgument { Name = "focus", Description = "Optional focus: structure, geometry, styling, accessibility, or export." }
            ]
        }
    };

    public ValueTask<ListPromptsResult> ListPromptsAsync(RequestContext<ListPromptsRequestParams> request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var (prompts, cursor) = Page(s_prompts, request.Params?.Cursor, "prompts");
        return ValueTask.FromResult(new ListPromptsResult
        {
            Prompts = prompts,
            NextCursor = cursor,
            TimeToLive = TimeSpan.FromMinutes(5),
            CacheScope = CacheScope.Public
        });
    }

    public ValueTask<GetPromptResult> GetPromptAsync(RequestContext<GetPromptRequestParams> request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var prompt = s_prompts.FirstOrDefault(item => item.Name == request.Params?.Name)
            ?? throw new McpProtocolException("Unknown UnoDraw prompt.", McpErrorCode.InvalidParams);
        var arguments = request.Params.Arguments;
        var allowed = prompt.Arguments!.Select(argument => argument.Name).ToHashSet(StringComparer.Ordinal);
        if (arguments?.Keys.Any(key => !allowed.Contains(key)) == true)
        {
            throw new McpProtocolException("The prompt contains an unknown argument.", McpErrorCode.InvalidParams);
        }

        if (arguments?.Values.Any(value => value.ValueKind != JsonValueKind.String) == true)
        {
            throw new McpProtocolException("UnoDraw prompt arguments must be strings.", McpErrorCode.InvalidParams);
        }

        foreach (var argument in prompt.Arguments!)
        {
            if (argument.Required == true
                && (arguments is null || !arguments.TryGetValue(argument.Name, out var value) || string.IsNullOrWhiteSpace(value.GetString())))
            {
                throw new McpProtocolException($"Prompt argument '{argument.Name}' is required.", McpErrorCode.InvalidParams);
            }
        }

        if (arguments?.Values.Any(value => value.GetString()!.Length > 16 * 1024) == true)
        {
            throw new McpProtocolException("A prompt argument exceeds the 16 KiB limit.", McpErrorCode.InvalidParams);
        }

        var supplied = new JsonObject();
        if (arguments is not null)
        {
            foreach (var argument in arguments)
            {
                supplied[argument.Key] = argument.Value.GetString();
            }
        }

        var workflow = prompt.Name switch
        {
            "create-artwork" => "Read unodraw://workspace and unodraw://documents. Discover tools and their schemas. "
                + "Create a new document unless the user explicitly requested an existing one. Build editable SVG elements, "
                + "organize related artwork into groups, and apply fills, strokes, typography, and transforms. "
                + "Read back the resulting structure and render a PNG preview to inspect the composition.",
            "edit-selection" => "Read unodraw://selection and the active document's element tree. Identify the selected elements "
                + "and current revision. Apply the requested edits through the discovered tools using stable element references "
                + "and expectedRevision. Preserve surrounding artwork, use the editor's undo history, and inspect the result.",
            "export-document" => "Read unodraw://documents and inspect the requested document, artboards, and export tool schema. "
                + "Export the requested format and return the resulting data or image to the user. Preserve editable source "
                + "and explain which document and bounds were exported.",
            _ => "Read unodraw://documents and the requested document's SVG source and element tree. Review the requested focus "
                + "using read-only tools. Check geometry, styling, grouping, references, and document bounds as relevant. "
                + "Report concrete findings with element references; do not change the document during the inspection."
        };
        return ValueTask.FromResult(new GetPromptResult
        {
            Description = prompt.Description,
            Messages = [new PromptMessage
            {
                Role = Role.User,
                Content = new TextContentBlock { Text = workflow + "\n\nUser parameters (JSON):\n" + supplied.ToJsonString() }
            }]
        });
    }

    public async ValueTask<CompleteResult> CompleteAsync(RequestContext<CompleteRequestParams> request, CancellationToken cancellationToken)
    {
        var parameters = request.Params ?? throw new McpProtocolException("Completion parameters are required.", McpErrorCode.InvalidParams);
        IEnumerable<string> suggestions;
        if (parameters.Ref is PromptReference promptReference)
        {
            var prompt = s_prompts.FirstOrDefault(item => item.Name == promptReference.Name)
                ?? throw new McpProtocolException("Unknown UnoDraw prompt.", McpErrorCode.InvalidParams);
            if (prompt.Arguments?.Any(argument => argument.Name == parameters.Argument.Name) != true)
            {
                throw new McpProtocolException("Unknown prompt argument.", McpErrorCode.InvalidParams);
            }

            suggestions = parameters.Argument.Name switch
            {
                "focus" => ["accessibility", "export", "geometry", "structure", "styling"],
                "format" => ExportFormats(),
                "documentId" => await DocumentIdsAsync(cancellationToken).ConfigureAwait(false),
                _ => []
            };
        }
        else if (parameters.Ref is ResourceTemplateReference resourceReference
            && s_resourceTemplates.Any(template => template.UriTemplate == resourceReference.Uri)
            && parameters.Argument.Name == "documentId")
        {
            suggestions = await DocumentIdsAsync(cancellationToken).ConfigureAwait(false);
        }
        else
        {
            throw new McpProtocolException("Unknown resource template or completion argument.", McpErrorCode.InvalidParams);
        }

        var matches = suggestions.Where(value => value.StartsWith(parameters.Argument.Value, StringComparison.OrdinalIgnoreCase))
            .Distinct(StringComparer.Ordinal).OrderBy(value => value, StringComparer.Ordinal).ToArray();
        return new CompleteResult
        {
            Completion = new Completion { Values = matches.Take(100).ToArray(), Total = matches.Length, HasMore = matches.Length > 100 }
        };
    }

    private IEnumerable<string> ExportFormats()
    {
        if (_toolsByName.TryGetValue("unodraw_document_export", out var export)
            && export.InputSchema.TryGetProperty("properties", out var properties)
            && properties.TryGetProperty("format", out var format)
            && format.TryGetProperty("enum", out var values))
        {
            return values.EnumerateArray().Where(item => item.ValueKind == JsonValueKind.String).Select(item => item.GetString()!).ToArray();
        }

        return ["png", "svg"];
    }

    private async Task<IEnumerable<string>> DocumentIdsAsync(CancellationToken cancellationToken)
    {
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _lifetimeToken);
        var result = await _target.InvokeAsync("unodraw_document_list", new JsonObject(), linked.Token).ConfigureAwait(false);
        var documents = result as JsonArray ?? result?["documents"] as JsonArray;
        return documents?.OfType<JsonObject>().Select(item => item["documentId"]?.GetValue<string>() ?? item["id"]?.GetValue<string>())
            .Where(value => !string.IsNullOrEmpty(value)).Select(value => value!).ToArray() ?? [];
    }
}
