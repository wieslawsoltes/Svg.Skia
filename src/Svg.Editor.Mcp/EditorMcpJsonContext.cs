using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace Svg.Editor.Mcp;

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(JsonNode))]
[JsonSerializable(typeof(JsonObject))]
[JsonSerializable(typeof(JsonArray))]
internal partial class EditorMcpJsonContext : JsonSerializerContext
{
}
