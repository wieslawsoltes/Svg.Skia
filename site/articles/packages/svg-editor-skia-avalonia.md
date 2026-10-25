---
title: "Svg.Editor.Skia.Avalonia"
---

# Svg.Editor.Skia.Avalonia

`Svg.Editor.Skia.Avalonia` is the complete editor composition for Avalonia. It exposes the interactive surface and ready-made workspace originally extracted from AvalonDraw. The desktop application has moved to UnoDraw; this Avalonia library remains available with its existing hosting API. For the Uno workspace, see [Svg.Editor.Skia.Uno](svg-editor-skia-uno).

## Install

```bash
dotnet add package Svg.Editor.Skia.Avalonia
```

## Choose this package when

- you want an embeddable SVG editor in an Avalonia app,
- you need the Skia-backed editing surface plus the default side panels,
- you want to keep an existing Avalonia editor integration,
- you want a packaged entry point that already composes the lower editor libraries.

## Main types

| Type | Role |
| --- | --- |
| `SvgEditorWorkspace` | Full editor composition including menu, tool palette, panels, surface, and status bar |
| `SvgEditorSurface` | Skia-backed SVG surface with public `InteractionController` and `OverlayRenderer` hooks |

## Minimal embed

```xml
<editor:SvgEditorWorkspace x:Name="EditorWorkspace" />
```

```csharp
EditorWorkspace.WorkspaceTitlePrefix = "MyApp";
EditorWorkspace.ResourceAssembly = typeof(MainWindow).Assembly;
EditorWorkspace.DialogService = new SvgEditorDialogService();
EditorWorkspace.FileDialogService = new SvgEditorFileDialogService();
EditorWorkspace.LoadDocument("Assets/__tiger.svg");
```

## Public host seams

| Member | Use it for |
| --- | --- |
| `Session` | Session state, history, nodes, artboards, selected ids, and settings |
| `Surface` | Direct control access for the Skia-backed canvas |
| `WorkspaceTitlePrefix` | App branding |
| `ResourceAssembly` | Embedded asset lookup |
| `DialogService` | Custom modal or inline editor hosting |
| `FileDialogService` | Custom storage or picker integration |
| `PreviewRequested` | Preview window or preview pane integration |
| `WorkspaceTitleChanged` | Sync host window titles |

## Surface-specific hooks

`SvgEditorSurface` inherits from `Avalonia.Svg.Skia.Svg` and adds:

- `InteractionController`
- `OverlayRenderer`

Use those when the host wants the reusable canvas but plans to wire the surrounding UI itself.

## Related docs

- [Embedding SvgEditorWorkspace](../editor/embedding-workspace)
- [Composing Panels and Dialogs](../editor/composing-panels-and-dialogs)
- [Svg.Editor.Skia](svg-editor-skia)
