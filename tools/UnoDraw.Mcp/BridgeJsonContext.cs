using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace UnoDraw.Mcp;

[JsonSourceGenerationOptions(WriteIndented = true)]
[JsonSerializable(typeof(JsonArray))]
internal partial class BridgeJsonContext : JsonSerializerContext
{
}
