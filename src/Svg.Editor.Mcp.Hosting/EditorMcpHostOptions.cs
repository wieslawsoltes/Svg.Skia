namespace Svg.Editor.Mcp.Hosting;

/// <summary>Explicit opt-in settings. Every host binds only to IPv4 loopback.</summary>
public sealed class EditorMcpHostOptions
{
    public int Port { get; init; } = 52741;
    public string Token { get; init; } = EditorMcpSecurity.CreateToken();
    public string BrowserToken { get; init; } = EditorMcpSecurity.CreateToken();
    public string? AllowedOrigin { get; init; }
    public bool EnableHttp { get; init; } = true;

    internal void Validate()
    {
        if (Port is < 0 or > 65535)
            throw new ArgumentOutOfRangeException(nameof(Port), "Choose a TCP port between 1 and 65535, or zero for an ephemeral test port.");
        EditorMcpSecurity.ValidateToken(Token, nameof(Token));
        EditorMcpSecurity.ValidateToken(BrowserToken, nameof(BrowserToken));
        if (EditorMcpSecurity.TokenMatches(Token, BrowserToken))
            throw new ArgumentException("The MCP client token and browser attachment token must be different.");
        _ = EditorMcpSecurity.NormalizeOrigin(AllowedOrigin);
    }
}
