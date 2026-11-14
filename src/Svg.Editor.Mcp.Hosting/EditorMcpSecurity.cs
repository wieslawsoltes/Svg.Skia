using System.Net;
using System.Security.Cryptography;
using System.Text;

namespace Svg.Editor.Mcp.Hosting;

/// <summary>Validation shared by embedded and companion loopback hosts.</summary>
public static class EditorMcpSecurity
{
    public static string CreateToken() => Convert.ToHexString(RandomNumberGenerator.GetBytes(32)).ToLowerInvariant();

    public static bool TokenMatches(string expected, string? supplied)
    {
        if (supplied is null || supplied.Length > 512)
            return false;
        var expectedHash = SHA256.HashData(Encoding.UTF8.GetBytes(expected));
        var suppliedHash = SHA256.HashData(Encoding.UTF8.GetBytes(supplied));
        return CryptographicOperations.FixedTimeEquals(expectedHash, suppliedHash);
    }

    public static bool IsLoopbackHost(string? host)
        => host is not null && (host.Equals("localhost", StringComparison.OrdinalIgnoreCase)
            || IPAddress.TryParse(host.Trim('[', ']'), out var address) && IPAddress.IsLoopback(address));

    public static string? NormalizeOrigin(string? origin)
    {
        if (string.IsNullOrWhiteSpace(origin))
            return null;
        if (!Uri.TryCreate(origin, UriKind.Absolute, out var uri) || uri.Scheme is not ("http" or "https")
            || uri.UserInfo.Length != 0 || uri.AbsolutePath != "/" || uri.Query.Length != 0 || uri.Fragment.Length != 0)
            throw new ArgumentException("The browser origin must contain only an http(s) scheme, host and optional port.", nameof(origin));
        return uri.GetLeftPart(UriPartial.Authority);
    }

    public static void ValidateToken(string token, string parameterName)
    {
        if (token.Length < 32 || token.Length > 512 || token.Any(character => character <= ' ' || character >= 127))
            throw new ArgumentException("MCP tokens must contain 32–512 printable ASCII characters without whitespace.", parameterName);
    }
}
