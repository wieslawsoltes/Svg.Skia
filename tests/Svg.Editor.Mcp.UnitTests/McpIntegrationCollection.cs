using Xunit;

namespace Svg.Editor.Mcp.UnitTests;

// The real transports deliberately exercise maximum-size exports. Running several
// copies in parallel would measure the test runner's peak memory rather than one
// bounded application connection, and can exhaust a small CI worker.
[CollectionDefinition("MCP integration", DisableParallelization = true)]
public sealed class McpIntegrationCollection
{
}
