# Svg.Editor.Skia.Uno

Reusable Uno Platform editor UI components for the Svg.Skia editor stack.

## Included controls

- `SvgEditorShell`: composed full editor shell
- `SvgEditorTopBar`: document chrome and session actions
- `SvgEditorUtilityRail`: left utility rail
- `SvgEditorSidebar`: pages, layers, and assets panel
- `SvgEditorStagePanel`: stage header, rulers, hints, and floating tool tray
- `SvgEditorInspectorPanel`: design/prototype inspector shell
- `SvgEditorOverlayCanvas`: Skia selection and marquee overlay

## Supporting types

- `ISvgEditorShellViewModel`: contract for shell state binding
- `EditorToolDefinition`
- `EditorObjectNode`
- `RulerMark`

## Resource dictionary

Merge `ms-appx:///Svg.Editor.Skia.Uno/Themes/EditorThemeResources.xaml` to reuse the default editor brushes and styles outside the composed shell.
