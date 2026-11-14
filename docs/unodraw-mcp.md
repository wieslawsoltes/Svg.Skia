# UnoDraw MCP

UnoDraw's Model Context Protocol integration connects an MCP client to the live
editor workspace. Automation uses the same SVG document, controller, retained
renderer and history as native tools. Changes appear in the open document and
remain editable through the workspace.

## Connection model

Desktop integration supports a locally hosted MCP endpoint and a standard-input
and standard-output connection. A browser workspace connects outward to the
local UnoDraw MCP bridge. The bridge exposes standard MCP transports to clients;
the WebSocket between the bridge and the application is a private attachment
protocol, not an additional MCP transport.

MCP connections are explicit and remain disabled in an ordinary editor session.
Local network endpoints bind to loopback and require a connection token. Browser
attachments additionally validate the exact page origin. Disconnecting prevents
new operations and cancels pending requests without replaying mutations.

## Editor semantics

- Open documents have stable session identities. Filenames and tab positions
  are display information, not document handles.
- Read operations do not create SVG IDs or change document history.
- Operations that change a document use its existing transactions. A failed
  atomic edit restores the previous document state.
- Element paths are revision-sensitive. Callers must refresh stale document
  information before applying changes to structural paths.
- Typed operations cover document, selection, geometry, paint and workspace
  tasks. SVG source and resource operations preserve access to the complete
  editable document model.
- Command discovery includes availability and interaction requirements. Native
  dialogs and pickers require an explicit interactive invocation.
- Export returns document or image content to the MCP client. It does not
  assume that a browser can write to a path on the client's computer.

## Protocol implementation

The integration uses the official [MCP C# SDK](https://github.com/modelcontextprotocol/csharp-sdk).
Protocol discovery, initialization, request IDs and cancellation remain the
SDK's responsibility. Editor tools use explicit JSON schemas and dispatch
tables, avoiding reflection-based command execution.

The reusable [EditorTheme workspace](unodraw-editor-theme.md) remains the
interaction surface. See the [UnoDraw guide](unodraw.md) for the editor's tools,
document formats, shortcuts and platform builds.
