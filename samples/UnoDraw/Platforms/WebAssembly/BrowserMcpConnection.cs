using System.Runtime.InteropServices.JavaScript;
using System.Text.Json.Nodes;
using Microsoft.UI.Xaml;
using Svg.Editor.Skia.Uno;

namespace UnoDraw;

/// <summary>Explicit browser attachment API for hosts and automated integration checks.</summary>
public static partial class BrowserMcpConnection
{
    private static UnoDrawMcpConnection Connection => (Application.Current as App)?.McpConnection
        ?? throw new InvalidOperationException("The UnoDraw workspace is not ready.");

    /// <summary>Attach this tab to an authenticated local bridge. Never reads tokens from URLs.</summary>
    [JSExport]
    public static async Task<string> Connect(string endpoint, string token, bool readOnly)
    {
        await Connection.StartAsync(new EditorMcpConnectionSettings(
            EditorMcpConnectionMode.BrowserBridge, endpoint, token, ReadOnly: readOnly));
        return Status();
    }

    /// <summary>Cancel pending requests and detach the current tab.</summary>
    [JSExport]
    public static async Task<string> Disconnect()
    {
        await Connection.StopAsync();
        return Status();
    }

    /// <summary>Return connection status without exposing either authentication token.</summary>
    [JSExport]
    public static string Status()
    {
        var state = Connection.State;
        return new JsonObject
        {
            ["connected"] = state.IsConnected,
            ["status"] = state.Status,
            ["endpoint"] = state.Endpoint,
            ["readOnly"] = state.ReadOnly
        }.ToJsonString();
    }
}
