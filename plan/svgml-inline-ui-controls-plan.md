# SvgML Inline UI Controls Plan

Superseded by `svgml-foreignobject-inline-controls-update.md`.

The initial implementation plan used a custom `InlineUIContainer` element as the public inline native-control host. The current design removes that custom element and uses SVG `foreignObject` as the single idiomatic public API for Avalonia, Uno, and MAUI.
