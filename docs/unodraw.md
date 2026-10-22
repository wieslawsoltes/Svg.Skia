# UnoDraw

UnoDraw migrates the AvalonDraw application to Uno Platform while retaining the repository's Svg.Skia renderer and reusable SVG editing libraries.

## Architecture

- SVG remains the editable document format; the existing SVG DOM, scene graph, resource loading, filters, text and paint pipeline remain authoritative.
- A framework-independent editor controller coordinates selection, commands, history and document mutations through `Svg.Editor.Core`, `Svg.Editor.Svg` and `Svg.Editor.Skia`.
- The Uno workspace hosts a custom `SKCanvasElement` that draws cached Svg.Skia pictures directly into the Uno Skia compositor surface. Hardware acceleration follows the active Uno desktop backend; software fallback remains possible on unsupported hosts.
- Pointer interaction updates overlays and viewport transforms without reparsing SVG for pan and zoom. Completed edits invalidate the document rendering cache.
- A modern tool rail, document outline, contextual inspector, command search and keyboard shortcuts expose the editing model.

## Migration work

This document and the pull request will track the implemented migration, verified capabilities, build commands and remaining advanced editor work. The existing reusable Avalonia libraries remain available to their consumers while the application is renamed to UnoDraw.

## References

- [Uno SKCanvasElement](https://platform.uno/docs/articles/controls/SKCanvasElement.html)
- [Uno Skia Desktop](https://platform.uno/docs/articles/features/using-skia-desktop.html)
- [SVG 1.1](https://www.w3.org/TR/SVG11/)
