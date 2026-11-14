# UnoDraw MCP browser bridge

This companion connects an MCP client to a live UnoDraw browser tab. The same editor tools, resources, prompts, history, and document model are available through the desktop application's embedded MCP host. The browser bridge hosts the protocol locally because a browser tab cannot listen for incoming TCP connections.

See [the complete MCP guide](../../docs/unodraw-mcp.md) for the operation reference, document and element identifiers, transactions, export, resource subscriptions, and complete editing examples.

## Build

Install the .NET 10 SDK selected by the repository's `global.json`, initialize its submodules, and run:

```sh
dotnet build tools/UnoDraw.Mcp/UnoDraw.Mcp.csproj -c Release
```

The executable assembly is `tools/UnoDraw.Mcp/bin/Release/net10.0/UnoDraw.Mcp.dll`. A framework-dependent deployment needs the .NET 10 and ASP.NET Core 10 runtimes. Alternatively publish a self-contained application for the destination OS:

```sh
dotnet publish tools/UnoDraw.Mcp/UnoDraw.Mcp.csproj -c Release -r linux-x64 --self-contained true -o artifacts/unodraw-mcp
```

Use `win-x64`, `win-arm64`, `osx-arm64`, or another supported runtime identifier for a different machine.

## Connect over stdio

Configure the MCP client to launch the built assembly. Use an absolute path; launching `dotnet run` would mix build output into stdout before the bridge can reserve it for JSON-RPC.

```json
{
  "mcpServers": {
    "unodraw": {
      "command": "dotnet",
      "args": [
        "/absolute/path/UnoDraw.Mcp.dll",
        "--origin",
        "https://wieslawsoltes.github.io"
      ],
      "env": {
        "UNODRAW_MCP_BROWSER_TOKEN": "replace-with-a-random-token-of-at-least-32-characters"
      }
    }
  }
}
```

Replace the token placeholder with a generated random token before starting the client. After it launches the bridge, open **MCP Connection** in UnoDraw, choose the browser connection, enter `ws://127.0.0.1:52742/editor`, paste the same browser attachment token, and connect. Discovery is available before attachment; operations that need the editor return a clear disconnected error until the tab connects.

The token is sent in the first private WebSocket message. It is never included in a URL. The browser supplies its actual origin, which must equal `--origin`. For the published editor under `https://wieslawsoltes.github.io/Svg.Skia/unodraw/`, the origin is `https://wieslawsoltes.github.io`, without a path. For a development server, include the port, such as `http://localhost:5000`.

All diagnostic output goes to stderr. Closing the MCP client's stdin stops the stdio server and the companion. The browser does not reconnect or replay editing operations automatically.

## Connect over Streamable HTTP

Start a persistent bridge in a terminal:

```sh
dotnet /absolute/path/UnoDraw.Mcp.dll --http --origin https://wieslawsoltes.github.io
```

The process displays a browser attachment token and a separate MCP client bearer token on stderr. Connect the browser using the attachment token, then configure the MCP client:

```json
{
  "mcpServers": {
    "unodraw": {
      "url": "http://127.0.0.1:52742/mcp",
      "headers": {
        "Authorization": "Bearer REPLACE_WITH_MCP_CLIENT_TOKEN"
      }
    }
  }
}
```

Client configuration field names can differ. The protocol endpoint is `/mcp`, and every request needs its bearer header. The MCP client token cannot authenticate a browser attachment, and the browser token cannot call the HTTP endpoint. Ctrl+C stops both endpoints and cancels pending requests.

The host uses the official C# MCP SDK with both the `2026-07-28` request/discovery protocol and clients using the `2025-11-25` initialize handshake. Legacy initialize clients receive sessions; current clients use stateless HTTP requests on the same endpoint. The removed HTTP+SSE transport's separate `/sse` and `/message` endpoints are not exposed.

## Options and environment

| Setting | Meaning | Default |
| --- | --- | --- |
| `--origin ORIGIN` | Exact HTTP(S) origin of the browser page. Required. | None |
| `--port PORT` | TCP port on IPv4 loopback. Range 1–65535. | `52742` |
| `--http` | Keep Streamable HTTP available until Ctrl+C. | Stdio |
| `--read-only` | Reject every operation not marked read-only in the shared catalog. | Editing enabled |
| `--describe` | Print the complete tool catalog as deterministic JSON on stdout and exit. Needs no origin or connection. | — |
| `--help`, `-h` | Print CLI usage on stderr and exit. | — |
| `UNODRAW_MCP_BROWSER_TOKEN` | Token for the browser attachment. | Random 256-bit token |
| `UNODRAW_MCP_TOKEN` | Token for HTTP MCP clients. | Separate random 256-bit token |

Configured tokens must contain 32–512 printable ASCII characters without whitespace, and the two tokens must differ. Starting the process again generates new tokens unless explicitly supplied through the environment. The desktop application's embedded host uses port `52741` by default, so it can run alongside the browser bridge.

Read-only mode still allows source inspection, document/resource lists, and export. Selection changes, canvas navigation, UI commands, and mutations are rejected if their operation is not explicitly read-only. The browser connection can independently choose read-only access; either restriction is sufficient to prevent edits.

The offline catalog includes each tool's name, description, complete input schema, and MCP annotations. It is generated directly from the same descriptors used by the live server, without initializing a native renderer:

```sh
dotnet /absolute/path/UnoDraw.Mcp.dll --describe > unodraw-mcp-tools.json
```

`--describe` is an explicit offline export mode; normal stdio sessions continue to reserve stdout exclusively for JSON-RPC.

## Connection behavior and limits

- The host binds only to `127.0.0.1`. Requests with non-loopback peer addresses or Host headers are rejected.
- Browser origins are matched exactly. Wildcards, opaque origins, URL paths, and URL credentials are not accepted as origins.
- Only one authenticated browser tab can attach to each companion. A second tab receives a connection conflict without replacing the first.
- The authentication frame is limited to 4 KiB and must arrive within 10 seconds. Normal private bridge messages are limited to 32 MiB, allowing base64 export of up to 16 MiB of rendered output without lowering image quality.
- At most 32 editor operations may be pending. Each has a two-minute deadline; cancellation propagates to the editor. An operation already executing synchronously may finish before the UI can observe cancellation.
- HTTP retains at most two ordinary POST responses through serialization and sending. Eight subscription streams and four small control requests have separate capacity so a slow export cannot block cancellation. Overload returns HTTP 429 with `Retry-After: 1`; it does not queue an edit for replay.
- Resource notifications carry URIs, not document content. Repeated notifications are coalesced and bounded; clients read the resource again to obtain its current value.
- Disconnecting cancels pending requests. Reconnecting creates a fresh attachment and never replays an edit from the previous tab.
- The normal editor remains unchanged until a connection is explicitly enabled. No hosted relay service, account, or external control endpoint is involved.

If the browser blocks a connection to a local WebSocket, check its local-network permission and console. The client accepts `wss://` loopback URLs ending in `/editor` for an explicitly configured local TLS proxy; the companion itself serves loopback HTTP/WS. Keep the same Origin and token checks at the companion. Changing the URL to a public network host is deliberately rejected.
