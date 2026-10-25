---
title: "Architecture"
---

# Architecture

The editor stack is layered so applications can pick the lowest level that still solves the problem.

## Dependency order

| Layer | Depends on | Main responsibility |
| --- | --- | --- |
| `Svg.Editor.Core` | `Svg.Model` | Host-agnostic state such as the active document, outline tree, selected ids, settings, clipboard, and undo/redo history |
| `Svg.Editor.Svg` | `Svg.Editor.Core`, `Svg.Model`, `Svg.Skia` | SVG document loading, save/export helpers, resource-path rebasing, properties, layers, patterns, swatches, symbols, styles, and creation tools |
| `Svg.Editor.Skia` | `Svg.Editor.Core`, `Svg.Editor.Svg`, `Svg.Model`, `Svg.Skia` | Transactional document controller, retained selection, viewport, transforms, path editing, align/distribute, and overlays |
| `Svg.Editor.Skia.Uno` | `Svg.Editor.Skia`, Uno Platform | Direct Skia editing canvas and the tabbed workspace hosted by UnoDraw |
| `Svg.Editor.Avalonia` | `Svg.Editor.Core`, `Svg.Editor.Svg` | Reusable Avalonia panels, property editors, standalone dialog views, and dialog/file-dialog service abstractions |
| `Svg.Editor.Skia.Avalonia` | `Svg.Editor.Avalonia`, `Svg.Editor.Core`, `Svg.Editor.Skia`, `Svg.Editor.Svg`, `Svg.Controls.Skia.Avalonia` | The interactive `SvgEditorSurface` and the default `SvgEditorWorkspace` composition |

## Package responsibilities

### `Svg.Editor.Core`

Use this layer when your host already has its own UI and needs a durable editor session object. `SvgEditorSession` stores the document reference, current file, title, filter text, selected ids, artboards, outline nodes, settings, clipboard contents, and undo/redo snapshots.

### `Svg.Editor.Svg`

This layer owns document mutation and resource-oriented models. It is the package behind the property inspector, layer browser, pattern list, swatches, symbols, and styles. It also carries `SvgDocumentService` for open, save, XML round-tripping, and export helpers. `SvgResourcePathService` preserves relative resource locations when a document is saved into another directory, including nested XML bases and CSS URLs.

### `Svg.Editor.Skia`

This layer owns the editing math and framework-independent document controller. `SvgEditorController` provides SVG transactions, dirty checkpoints, selection, geometry commands, and retained pictures. `SvgEditorViewport` handles camera transforms and anchored zoom without changing the document. The existing `SelectionService`, `PathService`, `AlignService`, and `RenderingService` remain available, as do the `SvgEditorInteractionController` and `SvgEditorOverlayRenderer` adapters.

### `Svg.Editor.Skia.Uno`

This layer provides the Uno workspace used by UnoDraw. `SvgEditorCanvas` derives from `SKCanvasElement`, draws directly into the host's Skia surface, and replays the controller's cached SVG picture. `SvgEditorWorkspace` composes document tabs, the tool rail, searchable outline, contextual inspector, command search, file workflows, and recovery. Acceleration depends on the desktop host's graphics backend; a software fallback remains possible.

### `Svg.Editor.Avalonia`

This layer exposes reusable controls and standalone views rather than a monolithic window. The main reusable panels are `DocumentOutlineView`, `ResourceBrowserView`, `PropertyInspectorView`, `ToolPaletteView`, and `StatusBarView`. Dialog workflows are abstracted through `ISvgEditorDialogService` and `ISvgEditorFileDialogService`.

### `Svg.Editor.Skia.Avalonia`

This remains the complete Avalonia composition. `SvgEditorSurface` extends `Avalonia.Svg.Skia.Svg` with public overlay and interaction adapters, and `SvgEditorWorkspace` composes the menu, tool palette, left-side panels, canvas, and property inspector. Migrating the desktop sample to Uno does not remove this package or change its hosting API.

## Avalonia host-owned seams

The reusable workspace is intentionally not the final application shell. Hosts remain responsible for:

- window creation and app branding through `WorkspaceTitlePrefix`,
- default resource lookup via `ResourceAssembly`,
- preview window ownership via `PreviewRequested`,
- modal editors via `DialogService`,
- file picking and save locations via `FileDialogService`.

That design keeps the reusable packages independent from any single desktop app policy.

## Uno host lifecycle

Uno hosts create their own window, attach `Svg.Editor.Skia.Uno.SvgEditorWorkspace`, observe `WorkspaceTitleChanged`, and load documents through `OpenPathAsync` or `NewDocument`. Before closing, await `ConfirmCloseAsync`; dispose the workspace after closing is accepted. Each `EditorDocument` owns its controller, history, file identity, and recovery identity.

## Sample-to-package split

`samples/UnoDraw` is the desktop application host. It:

- instantiates the Uno `SvgEditorWorkspace`,
- creates and brands the desktop window,
- coordinates dirty-document confirmation before closing,
- opens a command-line SVG path or the default `Assets/Welcome.svg`,
- offers recovery of unsaved documents.

Reusable editor code lives under `src/Svg.Editor.*`. UnoDraw targets `net10.0-desktop`; the reusable Avalonia packages remain available for Avalonia hosts.

## Choosing the smallest useful layer

- Choose `Svg.Editor.Skia.Uno` for the direct Skia canvas and workspace inside an Uno desktop app.
- Choose `Svg.Editor.Skia.Avalonia` for the existing editor experience inside an Avalonia app.
- Choose `Svg.Editor.Avalonia` when you want the panels and dialogs but not the default canvas/workspace.
- Choose `Svg.Editor.Skia` when you are building your own interactive surface or canvas control.
- Choose `Svg.Editor.Svg` when the task is document/resource mutation without UI.
- Choose `Svg.Editor.Core` when you only need common session state and history primitives.
