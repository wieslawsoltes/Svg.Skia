---
title: "UnoDraw MCP"
---

# UnoDraw MCP

UnoDraw exposes its live editor through the Model Context Protocol (MCP). A client
can inspect documents, create and edit artwork, change the workspace, render
previews, and export deliverables. Operations reuse the existing SVG controller,
rendering, resource services, and undo history. The browser and desktop share the
same tool catalog and document semantics.

Use this guide for connections and complete workflows. The
[tool reference](mcp-tools) describes every tool, argument, enum,
validation bound, and nested input schema. The [editor guide](https://github.com/wieslawsoltes/Svg.Skia/blob/master/docs/unodraw.md) explains
native tools and shortcuts; the [EditorTheme guide](https://github.com/wieslawsoltes/Svg.Skia/blob/master/docs/unodraw-editor-theme.md)
explains the controls and workspace that automation operates.

## Choose a connection

| Application | MCP transport | How to connect |
| --- | --- | --- |
| Running desktop app | Streamable HTTP on loopback | **Window → MCP Connection → Start server**, then copy the client configuration. |
| Client-launched desktop app | Standard input/output | Launch the built application with `--mcp-stdio`. A graphical desktop is required. |
| Browser tab | Local bridge, exposing stdio or Streamable HTTP | Start `UnoDraw.Mcp`, then attach the tab through **Window → MCP Connection**. |

Ordinary editor launches do not start a listener or connect to a bridge. Tokens
and connection settings are session-only; they are not saved in SVG documents,
workspace profiles, recovery files, or URL parameters. A connection can be
restricted to inspection and export. Disconnecting cancels pending requests and
does not replay edits when a connection is restored.

The MCP client or bridge runs on the same computer as the editor. A hosted client
that cannot reach that computer needs its own supported local connector. UnoDraw's
built-in listener is a loopback service, not an internet-facing OAuth service.

## Desktop: connect to a running workspace

Build with the repository's .NET 10 SDK and initialize the submodules:

```sh
git submodule update --init --recursive
dotnet build samples/UnoDraw/UnoDraw.csproj -f net10.0-desktop -c Release
dotnet samples/UnoDraw/bin/Release/net10.0-desktop/UnoDraw.dll
```

Open **Window → MCP Connection**. Choose the port, optionally select **Allow
inspection and export only**, and choose **Start server**. The default endpoint
is `http://127.0.0.1:52741/mcp`. The dialog displays the endpoint, its generated
bearer token, and **Copy client configuration**. Close the dialog before asking a
client to edit; open native dialogs prevent concurrent automation mutations.

The copied configuration has this shape. Client-specific configuration containers
may use another name, but the URL and authorization header are the same:

```json
{
  "mcpServers": {
    "unodraw": {
      "type": "http",
      "url": "http://127.0.0.1:52741/mcp",
      "headers": {
        "Authorization": "Bearer <token copied from the connection dialog>"
      }
    }
  }
}
```

Return to **Window → MCP Connection** and choose **Disconnect** to stop the server.
The editor remains usable with its current documents and history.

Closing the desktop window first disconnects MCP and drains its pending editor
calls before asking about unsaved documents. Picker results that arrive after
cancellation are discarded. If you cancel closing, the workspace remains open
and disconnected; reconnect through the same dialog when ready.

### Desktop startup options

```sh
dotnet samples/UnoDraw/bin/Release/net10.0-desktop/UnoDraw.dll --mcp-http --mcp-port 52741 drawing.svg
```

| Option | Behavior |
| --- | --- |
| `--mcp-http` or `--mcp` | Start the loopback HTTP endpoint after the initial document and recovery workflow are ready. |
| `--mcp-port PORT` | Choose an HTTP port from 1024 to 65535; default 52741. |
| `--mcp-stdio` | Serve MCP over the process's standard input/output. Mutually exclusive with HTTP startup. |
| `--mcp-read-only` | Permit inspection and export; reject editing, selection changes, UI commands, and settings changes. |
| `UNODRAW_MCP_TOKEN` environment variable | Supply a persistent HTTP bearer token instead of generating a new session token. |

Tokens must contain 32–512 printable ASCII characters without whitespace. A new
random token is generated when none is supplied. Startup diagnostics and generated
HTTP connection information go to standard error. Desktop framework-dependent
builds need the .NET and ASP.NET Core 10 runtimes; the .NET SDK includes both.
Self-contained desktop publishing can include those runtimes with the application.

### Desktop standard input/output

Build first, then configure the client to launch the compiled application. Use
absolute paths and the correct working environment for your installation:

```json
{
  "mcpServers": {
    "unodraw": {
      "command": "dotnet",
      "args": [
        "/absolute/path/to/UnoDraw.dll",
        "--mcp-stdio"
      ]
    }
  }
}
```

Do not use `dotnet run` as the protocol executable: restore and build output are
not MCP messages. UnoDraw reserves stdout for JSON-RPC before starting the native
host, and directs managed application logs to stderr. This transport does not
open a network listener. Add `--mcp-read-only` for an inspection-only process.
The application still owns a visible workspace; normal save and close behavior
continues to apply.

## Browser: attach a tab to the local bridge

A browser page cannot listen for incoming MCP network connections. The small
`UnoDraw.Mcp` companion accepts the client's standard MCP connection and forwards
operations to one explicitly attached tab. The tab makes an outbound WebSocket
connection. This private attachment protocol is not an additional MCP transport.

Build the companion:

```sh
dotnet build tools/UnoDraw.Mcp/UnoDraw.Mcp.csproj -c Release
```

### HTTP bridge

For the published editor, start:

```sh
dotnet tools/UnoDraw.Mcp/bin/Release/net10.0/UnoDraw.Mcp.dll \
  --http --origin https://wieslawsoltes.github.io
```

For a local editor hosted at `http://localhost:5000`, instead use
`--origin http://localhost:5000`. An origin contains only the scheme, host and
optional port. Do not include `/Svg.Skia/unodraw/` or any other path. `localhost`
and `127.0.0.1` are different origins.

The companion prints four connection values to stderr:

1. The browser endpoint, normally `ws://127.0.0.1:52742/editor`.
2. The exact permitted browser origin.
3. A **browser attachment token** for the tab.
4. The MCP HTTP endpoint and a separate **MCP client bearer token**.

In the browser editor, open **Window → MCP Connection**, enter the WebSocket
address and the **browser attachment token**, and choose **Connect**. Close the
dialog. Configure the MCP client with `http://127.0.0.1:52742/mcp` and the **MCP
client bearer token**. The two tokens intentionally differ and cannot be swapped.

Only one tab can attach to a bridge at a time. Use another port and bridge process
for another tab. Closing or reloading a tab disconnects it; reconnect explicitly
and read fresh document identities before continuing.

Some browser policies require permission to contact the local network or restrict
an HTTPS page's connection to a local WebSocket. If connection is blocked, inspect
the browser error and allow the supported local-network permission, serve the
editor locally, or put a trusted local TLS proxy in front of the bridge and use
`wss://localhost:PORT/editor`. A TLS proxy must preserve the page's `Origin` and
forward the upstream `Host` as the bridge's loopback address and port. Do not
remove origin validation or put tokens in URL query strings.

### Standard-input/output bridge

Omit `--http` when the MCP client launches the bridge:

```json
{
  "mcpServers": {
    "unodraw-browser": {
      "command": "dotnet",
      "args": [
        "/absolute/path/to/UnoDraw.Mcp.dll",
        "--origin",
        "https://wieslawsoltes.github.io"
      ],
      "env": {
        "UNODRAW_MCP_BROWSER_TOKEN": "<your randomly generated attachment token>"
      }
    }
  }
}
```

Replace the environment value with a generated token of at least 32 characters,
then enter that same value in the browser connection dialog. Omitting the
variable generates a token and prints it in the client's server log. The bridge
can answer discovery requests before a tab attaches; editing calls report that
no editor is connected until attachment succeeds.

| Companion option | Behavior |
| --- | --- |
| `--origin ORIGIN` | Required for serving a browser bridge; permits exactly one browser origin. |
| `--port PORT` | Select the loopback port; default 52742. |
| `--http` | Expose Streamable HTTP instead of stdio as the MCP client transport. |
| `--read-only` | Reject all catalog operations that can change documents, selection, UI, or settings, regardless of tab preferences. |
| `--describe` | Print the entire tool catalog and input schemas as JSON, then exit. Requires no tab, origin, or renderer. |
| `--help` | Show command-line usage. |
| `UNODRAW_MCP_BROWSER_TOKEN` | Browser attachment credential; generated when omitted. |
| `UNODRAW_MCP_TOKEN` | HTTP MCP client credential; generated when omitted. Must differ from the attachment token. |

### Browser host API

Embedding hosts can use the same explicit connection lifecycle as the dialog:

```javascript
const exports = await Module.getAssemblyExports("UnoDraw");
const mcp = exports.UnoDraw.BrowserMcpConnection;

const connected = JSON.parse(await mcp.Connect(
  "ws://127.0.0.1:52742/editor",
  attachmentToken,
  false // true restricts this tab to inspection and export
));
const status = JSON.parse(mcp.Status());
const disconnected = JSON.parse(await mcp.Disconnect());
```

Call this after the workspace has loaded. `Status()` contains connection state,
endpoint, and read-only mode, without credentials. The API does not accept or
evaluate script supplied by the MCP client, and does not activate from a URL.

## Discover tools and context

Use `tools/list`, following `nextCursor` until it is absent. Tool names begin with
`unodraw_`. Every tool publishes a JSON input schema; unknown fields, invalid enum
values, malformed arrays, out-of-range numbers, and oversized input are rejected.
Use the [complete generated reference](mcp-tools) for exact parameters.

| Area | Tools, without the `unodraw_` prefix |
| --- | --- |
| Documents | `document_list`, `document_get`, `document_new`, `document_open`, `document_activate`, `document_close`, `document_replace`, `document_export` |
| Objects and hierarchy | `element_list`, `element_get`, `element_insert`, `element_update`, `element_delete`, `element_reparent`, `element_rename`, `layer` |
| Selection and history | `selection_get`, `selection_set`, `edit`, `batch` |
| Geometry and drawing | `draw`, `transform`, `arrange`, `path`, `measure` |
| Paint and resources | `paint`, `gradient`, `filter`, `resource_list`, `resource_import` |
| Artboards and guides | `artboard_list`, `artboard_edit`, `guide_list`, `guide_edit` |
| Advanced authoring | `text`, `text_story`, `brush`, `mesh`, `symbol`, `timeline_get`, `timeline_edit` |
| Print and inspection | `preflight`, `print_get`, `print_set`, `print_export` |
| Workspace controls | `commands_list`, `commands_run`, `tools_list`, `tools_select`, `tools_options`, `view_get`, `view_set`, `workspace_get`, `workspace_set`, `shortcuts_get`, `shortcuts_set` |

Native command discovery includes every registered command's ID, label, group,
resolved shortcut, enabled/checked state and interaction requirement. Tool
selection is similarly driven by the current tool catalog, rather than a second
hard-coded set of keyboard shortcuts. Workspace and shortcut profile operations
use the editor's existing serializers and validators.

The connection lifecycle command is discoverable with `availableOverMcp: false`.
Starting or stopping the connection stays local, preventing a remote command from
waiting for its own connection to shut down. Other interactive commands are
cancellable: disconnect closes an MCP-owned dialog, while native picker results
that arrive after cancellation are discarded.

### Resources

`resources/list` exposes these JSON resources:

| URI | Contents |
| --- | --- |
| `unodraw://documents` | Open document identities, names, dirty state and active document. |
| `unodraw://selection` | Selected element references and bounds. |
| `unodraw://artboards` | Current document's named artboards. |
| `unodraw://guides` | Current document's guides. |
| `unodraw://commands` | Native command catalog and availability. |
| `unodraw://tools` | Drawing tools and active tool. |
| `unodraw://view` | Viewport and view flags. |
| `unodraw://workspace` | Workspace layout, panels and presets. |

`resources/templates/list` also advertises
`unodraw://document/{documentId}/source` (`image/svg+xml`) and
`unodraw://document/{documentId}/elements` (`application/json`). The element
resource starts with a bounded page; use `element_list` and its `nextOffset` for
larger trees. Document and element reads do not generate SVG IDs or create undo
entries.

Clients can subscribe to context changes. Current clients use
`subscriptions/listen`; legacy clients use `resources/subscribe` and
`resources/unsubscribe`. Notifications invalidate context: read the resource again
to obtain its new state. There is no event replay after reconnect. Discovery lists
use deterministic ordering and short cache hints; live resource contents have zero
freshness time and private cache scope.

### Prompts

| Prompt | Arguments | Workflow |
| --- | --- | --- |
| `create-artwork` | Required `brief` | Inspect context, create editable artwork, organize it and render a preview. |
| `edit-selection` | Required `instruction` | Read selected objects and revision, edit them and verify the result. |
| `inspect-document` | Optional `documentId`, `focus` | Review structure, geometry, styling, accessibility or export readiness without edits. |
| `export-document` | Required `format`; optional `documentId` | Inspect the target and export the requested format. |

Prompts return workflow instructions for the client. Fetching a prompt does not
perform edits. Completion supports the advertised prompt/resource arguments.

## Work with documents safely

A `documentId` is the open document's stable GUID. It survives edits and tab
switches but should be rediscovered after closing, reopening or reloading the
application. Omit it to use the active document. UI operations address the active
workspace; they reject a different document ID rather than silently changing the
wrong tab.

Read `revision` from `document_get`, `element_list`, or a previous operation. Pass
that number as `expectedRevision` when editing. A mismatch reports stale context
and performs no requested document edit. Read again and decide how to apply the
change; do not blindly retry a mutation after a disconnect.

Element references are a unique SVG ID, `id:someId`, `root`, or
`path:0/2/1`. Structural paths use zero-based child indexes and require
`expectedRevision` when used for mutations. Prefer IDs for long-lived references.
`element_rename` updates document-local references; editing an `id` through the
ordinary attribute update API is rejected.

### Create artwork

The following blocks are the `name` and `arguments` passed to `tools/call`. An MCP
client supplies the JSON-RPC envelope and its negotiated protocol metadata.

```json
{
  "name": "unodraw_document_new",
  "arguments": { "width": 640, "height": 480, "name": "Composition.svg" }
}
```

Use the returned `documentId` for subsequent calls. Insert arbitrary SVG elements
or use the native `draw` tool:

```json
{
  "name": "unodraw_element_insert",
  "arguments": {
    "xml": "<g id='card'><rect id='background' x='40' y='40' width='560' height='400' rx='24' fill='#24243a'/><circle id='accent' cx='320' cy='220' r='96' fill='#7563ff'/></g>"
  }
}
```

The active-document example deliberately omits `documentId`. Production workflows
should carry the returned ID explicitly when they can overlap native tab changes.

### Select, transform, and style

```json
{
  "name": "unodraw_selection_set",
  "arguments": { "elements": ["accent"] }
}
```

```json
{
  "name": "unodraw_gradient",
  "arguments": {
    "kind": "Linear",
    "angle": 35,
    "stops": [
      { "offset": 0, "color": "#7563ff" },
      { "offset": 1, "color": "#00c6aa" }
    ]
  }
}
```

Transforms use picture coordinates and map through parent transforms. Alignment,
distribution, masks, booleans, path refinement, anchor operations, shape building,
and deformation use the same algorithms as native commands. Lock rules remain
active. Use the `layer` tool to change a branch's own visibility/lock, and unlock
an inherited lock on its parent before editing descendants.

### Group an atomic series of edits

Read the current revision before a batch. Replace the illustrative revision `17`
with the exact revision from that read:

```json
{
  "name": "unodraw_batch",
  "arguments": {
    "expectedRevision": 17,
    "label": "Move and recolor accent",
    "operations": [
      { "name": "unodraw_selection_set", "arguments": { "elements": ["accent"] } },
      { "name": "unodraw_transform", "arguments": { "dx": 24, "dy": -12 } },
      { "name": "unodraw_paint", "arguments": { "stroke": "#ffffff", "strokeWidth": 3 } }
    ]
  }
}
```

A batch creates one history entry and restores source and selection on failure.
It accepts up to 100 operations from its documented subset: element insertion,
update, deletion, rename and reparent; selection; transform; drawing; paint; text;
and resource import. It cannot change documents, open dialogs, export, invoke
undo/redo, or hold a transaction across asynchronous operations. The outer revision
is required; stale inner revisions are rejected.

Use `edit` with `action: "undo"` or `"redo"` to traverse normal editor history.
Source replacement is also undoable. Closing a dirty tab requires
`discardChanges: true`; export does not silently mark a document saved.

### Advanced authoring and complete SVG access

The typed tools expose native linked-text stories, brush libraries, gradient
meshes, symbol brushes, animation tracks, guides, artboards, filter graphs and
print settings. Read their native XML through the corresponding getter, modify
that representation, and apply it through the paired operation. This preserves
the metadata used by the existing editing panels.

Use `element_insert`, `element_update`, `resource_import`, and `document_replace`
for SVG structures outside a convenience tool's parameter list. These operations
support the editor's complete SVG DOM rather than a reduced drawing format.
SVG references, effects, script/resource behavior and font availability follow
the existing renderer's policies; importing XML is not a promise that external
resource references cannot be resolved. XML document-type declarations are
ignored for native serializer compatibility; external XML entities are never
resolved at the automation boundary.

`commands_run` executes existing commands. Dialog and picker commands require
`allowInteraction: true` and wait for native interaction. Parameterized tools are
the appropriate choice for unattended work. Read-only connections reject command
execution and mixed read/write tools, even when a particular action could be a
read; use their dedicated read-only resources or getters.

## Preview, export, and print

`document_export` supports SVG, SVGZ, PNG, JPEG, WebP, PDF and XPS. Choose full
artwork, `selectionOnly: true`, or an `artboardId`. Rendered images keep native
sRGB rendering and the editor's quality settings. JPEG has an opaque background.
The explicit `scale` range is 0.01–16; oversized results are rejected, never
silently downsampled.

For example:

```json
{
  "name": "unodraw_document_export",
  "arguments": { "format": "png", "scale": 1, "transparent": true }
}
```

MCP results contain metadata in `structuredContent` and native payload blocks:

| Format | Result payload |
| --- | --- |
| SVG | Editable XML text in the structured result and text content. |
| PNG, JPEG, WebP | One MCP `image` block with its MIME type and base64 `data`; structured metadata identifies the content type and byte count. |
| SVGZ, PDF, XPS, separation ZIP | One embedded `resource` block with base64 `resource.blob`, MIME type and an inline export URI; structured metadata identifies that resource. |

Binary bytes appear only once in the response. The inline resource is carried in
that result; it is not an arbitrary filesystem path or a persistent download
endpoint. Clients should decode/save the payload using their normal artifact
handling. Exports do not change the document, history, selection, or clipboard.

Use `preflight` for native document checks, `print_get` / `print_set` for complete
physical page setup, and `print_export` for page-layout PDF or process/named-ink
separation ZIP. A base64 ICC output profile can be supplied for profiled CMYK
conversion. Named inks require the print service's supported separation path or
explicit conversion to process colors. These APIs preserve the existing
[print support boundaries](https://github.com/wieslawsoltes/Svg.Skia/blob/master/docs/unodraw.md); they do not submit jobs to a printer.

## Protocol and operational behavior

The implementation uses the official MCP C# SDK 2.4.0. It supports the
[2026-07-28 protocol](https://modelcontextprotocol.io/specification/2026-07-28)
with `server/discover` and per-request metadata, while retaining the SDK's legacy
`initialize` handshake compatibility. Streamable HTTP uses the SDK's hybrid
session mode for older clients. Use a conforming MCP client instead of manually
mixing current headers with a legacy handshake.

| Boundary | Limit or behavior |
| --- | --- |
| Complete SVG input | 4,194,304 characters. |
| Typical SVG fragment/resource input | 1,048,576 characters; exact limits are in each schema. |
| Element listing | Up to 1000 elements per page. |
| Atomic batch | Up to 100 supported operations in one document. |
| Raster export | Up to 40 million output pixels and 16 MiB encoded binary output. |
| Binary export | Up to 16 MiB; no implicit quality reduction. |
| Private bridge message | Up to 32 MiB, allowing base64 overhead. |
| Pending bridge calls | At most 32; UI operations are serialized. |
| HTTP response concurrency | At most 2 ordinary responses; separate limits preserve cancellation/control requests and live subscriptions. Excess requests receive HTTP 429 with `Retry-After`. |
| Stdio response concurrency | At most 2 tool/resource responses and 8 live subscriptions. Excess requests receive JSON-RPC error `-32000`; cancellation and control messages remain available. |
| Slow stdio clients | Response slots remain occupied through delivery. Pending sends and overload replies are bounded; sustained flooding against blocked output disconnects the session. |
| Attachment authentication | First WebSocket message, bounded to 4 KiB with a short timeout. |
| Connection credentials | Separate browser and HTTP tokens; no token in URL. |
| Replay | None; a new connection requires new context reads. |

The server binds to IPv4 loopback and checks the remote address, `Host`, exact
browser `Origin`, and the appropriate token. Browser origins cannot use a native
server that has no allowed origin. The optional read-only restriction is enforced
at operation dispatch, not only described in tool hints. Local read-only exports
still use the renderer and its normal resource handling.

Errors distinguish malformed protocol requests from tool failures such as stale
revisions, missing documents, locked artwork or unavailable commands. Cancellation
removes queued work before it starts and propagates to cooperative operations.
An already committed synchronous edit is not undone merely because its caller
stopped waiting. After cancellation or connection loss, read document state before
issuing another mutation.

## Troubleshooting

| Symptom | Check |
| --- | --- |
| HTTP 401 | Use the MCP client bearer token, including the `Bearer ` prefix. |
| HTTP 403 or rejected WebSocket | Check the exact origin, loopback host/port, and browser attachment token. A native HTTP server does not accept browser-origin requests. |
| Another tab cannot attach | Disconnect the first tab or use a separate bridge/port. |
| Client lists tools but edits fail | Attach the tab, close native dialogs, inspect read-only mode, and read current document IDs. |
| Stale revision | Read the document again and reconcile the requested edit. |
| Path reference rejected | Supply its matching `expectedRevision`, or use a unique SVG ID. |
| Command requires interaction | Use its parameterized tool, or explicitly set `allowInteraction: true` and complete the dialog. |
| Export too large | Choose a smaller artboard/selection, an explicit lower scale, or a vector format. |
| Missing fonts/resources | Inspect the same document in the native workspace; MCP uses the same font and resource environment. |
| Stdio contains non-JSON output | Launch the built DLL, not a build command or a shell wrapper that writes banners to stdout. |
| Browser reload disconnected | Reattach explicitly and rediscover the open documents. |

## Embedding and validation

A host assigns an `IEditorMcpConnection` to `SvgEditorWorkspace.McpConnection` to
provide the themed connection UI. `workspace.CreateMcpTarget(readOnly)` supplies
the UI-dispatched backend. Dispose that target when its connection ends.
`EditorMcpServer.Create` / `CreateOptions` expose the protocol over an SDK
transport; `EditorMcpHost` supplies the authenticated loopback HTTP and stdio hosts.
`EditorMcpBridgeClient` connects a workspace backend to the local companion.

The core adapter can also be tested without Uno using `EditorMcpController` over
an existing `SvgEditorController`. Protocol tests use the official client,
including current and legacy versions, notifications, cancellation, pagination
and native payload blocks. Transport tests exercise real loopback HTTP/WebSockets,
authentication, origin checks, disconnects and read-only enforcement. The live
smoke harness performs edits and native PNG exports in the actual desktop and
published browser workspaces, comparing undo/redo pixels without relaxing image
quality.

```sh
dotnet test tests/Svg.Editor.Mcp.UnitTests/Svg.Editor.Mcp.UnitTests.csproj -c Release
dotnet build tools/UnoDraw.Mcp/UnoDraw.Mcp.csproj -c Release
dotnet tools/UnoDraw.Mcp/bin/Release/net10.0/UnoDraw.Mcp.dll --describe
```

The existing UnoDraw and Docs workflows run these checks; there is no separate
MCP workflow. Generate the checked tool-reference pages from the compiled catalog
with the following command. Add `--check` to verify that the pages still match
the executable schema.

```sh
python scripts/generate_unodraw_mcp_docs.py --bridge tools/UnoDraw.Mcp/bin/Release/net10.0/UnoDraw.Mcp.dll
```
