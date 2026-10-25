---
title: "Svg.Editor.Skia.Uno"
---

# Svg.Editor.Skia.Uno

`Svg.Editor.Skia.Uno` contains the Uno Platform workspace and direct Skia editing canvas used by UnoDraw. It uses the same SVG DOM, geometry services and Svg.Skia renderer as the rest of the repository. The document stays editable SVG throughout open, edit, undo and save.

## Reference the library

The library targets `net10.0` and uses the repository's Uno SDK configuration. The UnoDraw application targets `net10.0-desktop` for Windows, macOS and Linux/X11; it does not require Android or iOS workloads.

While developing against this repository, reference the project from an Uno application:

```xml
<ItemGroup>
  <ProjectReference Include="..\..\src\Svg.Editor.Skia.Uno\Svg.Editor.Skia.Uno.csproj" />
</ItemGroup>
```

Use `samples/UnoDraw/UnoDraw.csproj` as the reference for the desktop host and matching SkiaSharp native assets. The packable library's package ID is `Svg.Editor.Skia.Uno`.

## Choose this library when

- You want a tabbed SVG editor workspace in an Uno desktop application.
- You need a canvas with selection, shapes, pen, freehand, text, node editing, pan and zoom.
- You want reusable document transactions and geometry operations that can be tested without opening a window.
- You want native SVG resource preservation and the repository's existing rendering and export support.

## Main types

| Type | Role |
| --- | --- |
| `SvgEditorWorkspace` | Document tabs, command search, tool rail, outline, contextual inspector, file workflows and recovery. |
| `SvgEditorCanvas` | `SKCanvasElement` surface that draws retained SVG pictures and interactive overlays. |
| `EditorDocument` | A tab's controller, file identity, display name and recovery identity. |
| `SvgEditorController` from `Svg.Editor.Skia` | SVG transactions, history, selection, geometry, commands and retained rendering. |
| `SvgEditorViewport` from `Svg.Editor.Skia` | Camera transforms, fitting, pan and cursor-anchored zoom. |

## Embed the workspace

Use the `Svg.Editor.Skia.Uno` namespace in Uno XAML:

```xml
<Page
    xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
    xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
    xmlns:editor="using:Svg.Editor.Skia.Uno">
    <editor:SvgEditorWorkspace x:Name="EditorWorkspace" />
</Page>
```

Assign the owning Uno `Window` to `HostWindow` before using file commands. The workspace uses it to initialize native picker ownership; UnoDraw sets `workspace.HostWindow = _window` immediately after creating its window.

```csharp
EditorWorkspace.HostWindow = window;
```

Once the workspace is loaded, create a document or open an existing file:

```csharp
EditorWorkspace.NewDocument(width: 1200, height: 800);
// Alternatively:
await EditorWorkspace.OpenPathAsync(svgPath);
```

Observe `WorkspaceTitleChanged` to update the host window title. Await `ConfirmCloseAsync()` before closing the host window and dispose the workspace after closing has been accepted. The UnoDraw `App` demonstrates the close guard needed to prevent a second close event from opening another confirmation.

## Host API

| Member | Purpose |
| --- | --- |
| `Documents`, `CurrentDocument` | Inspect open document tabs and the active tab. |
| `Controller` | Access the active document's framework-independent controller. |
| `Surface` | Access the interactive canvas and its viewport. |
| `HostWindow` | Supply the owning Uno `Window` for native open/save picker initialization. |
| `NewDocument(width, height)` | Create a new SVG artboard. |
| `OpenPathAsync(path, isExample)` | Load an SVG/SVGZ file, optionally treating it as an untitled example. |
| `OfferRecoveryAsync()` | Offer saved recovery snapshots during startup. |
| `ConfirmCloseAsync()` | Resolve unsaved document decisions before closing. |
| `WorkspaceTitleChanged` | Receive the active document's title and modified indicator. |

To host only the canvas, assign a `SvgEditorController` to `SvgEditorCanvas.Controller`. The host then owns document commands, controller lifetime and its surrounding UI. `InteractionFailed` reports failed pointer operations after provisional state is cancelled.

## Rendering and performance

The canvas replays a cached Svg.Skia `SKPicture` directly into Uno's Skia surface. Pan, zoom and selection do not reserialize the document or recompile its picture. Document edits invalidate the retained picture; a gesture is committed as one undo transaction.

Move, resize and rotate gestures replay a cached full-color selection picture with a translucent preview over the original artwork. The snapshot includes ancestor compositing effects and is recorded once at gesture start. Pointer movement changes its transform without rebuilding the SVG. Native polygon/polyline vertices and path nodes remain editable without replacing the original element just to select it.

The desktop host selects the graphics backend and can fall back to software. Direct canvas integration enables hardware acceleration when the host provides it; confirm the active backend and measure frame timing on the target machine. XML snapshot history and complex SVG filters or external resources still contribute to editing cost.

## Coverage and existing integrations

UnoDraw provides native SVG drawing, selection, arrangement, path commands, resource creation, source/attribute editing, playback and export. Dedicated advanced authoring interfaces do not cover every SVG feature that the renderer can load. Consult the [UnoDraw feature coverage guide](https://github.com/wieslawsoltes/Svg.Skia/blob/master/docs/unodraw.md) for the current boundaries and keyboard controls.

`Svg.Editor.Avalonia` and `Svg.Editor.Skia.Avalonia` remain separate, supported integration choices. Uno types are not replacements for Avalonia controls inside an Avalonia window.

## Related docs

- [Editor architecture](../editor/architecture)
- [Svg.Editor.Skia](svg-editor-skia)
- [Svg.Editor.Svg](svg-editor-svg)
- [Svg.Editor.Skia.Avalonia](svg-editor-skia-avalonia)
- [Samples and tools](../reference/samples-and-tools)
