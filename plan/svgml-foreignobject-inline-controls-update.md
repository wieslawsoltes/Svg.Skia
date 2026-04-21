# SvgML `foreignObject` Inline Controls Update

Update date: 2026-04-21

## Goal

Use SVG `foreignObject` as the idiomatic SvgML host for native inline controls on Avalonia, Uno, and .NET MAUI.

`InlineUIContainer` has been removed; `foreignObject` is the single native-control host element.

## SVG Basis

- MDN describes `foreignObject` as the SVG element for including content from another namespace, most commonly HTML in browsers: https://developer.mozilla.org/en-US/docs/Web/SVG/Reference/Element/foreignObject
- SVG 1.1 defines `x`, `y`, `width`, and `height` as the rectangular region into which the foreign content is placed: https://www.w3.org/TR/SVG11/extend.html#ForeignObjectElement
- SVG 1.1 also specifies that `x`/`y` default to `0`, and zero `width` or `height` disables rendering.

SvgML maps those rules onto native UI hosting:

- non-inline `foreignObject` uses its SVG rectangular bounds
- inline `foreignObject` reserves text-flow space from explicit `width`/`height` or the measured native control size
- missing non-inline `width`/`height` are written from the native desired size when a child control exists

## Architecture

### Shared model

`foreignObject` implements the hosted-control contract:

- `HostedControl` returns the platform-native child
- `GetHostedControlSize()` returns explicit SVG size when set, otherwise native desired size
- a generated stable mapping id is emitted when the author did not set `id`

When a `foreignObject` is authored inside SVG text, it serializes as a `tspan` placeholder with:

- generated or explicit id for retained-scene mapping
- invisible placeholder glyph
- `font-size` from resolved slot height
- `textLength` from resolved slot width
- `lengthAdjust="spacingAndGlyphs"`

This lets the SVG text engine reserve inline advance while the real native control is arranged by the platform overlay.

### Scene graph

`SvgForeignObject` is retained as a non-rendering scene node, but the scene compiler now assigns geometry bounds from `x`, `y`, `width`, and `height`.

This is required for non-inline hosted controls because the native child is not serialized into the SVG document and therefore does not contribute child geometry.

### Platform overlays

All platforms now use one hosted-control layout contract:

- enumerate hosted controls from the SvgML source tree
- detect inline placement by walking from the hosted element to an owning `text_base`
- compute inline slot positions from the source text tree
- transform SVG picture-space bounds into platform control coordinates
- measure and arrange the native child in that slot

Avalonia hosts controls as logical/visual children of the root `svg`.

Uno hosts controls through retained popups positioned from the transformed SVG slot.

MAUI hosts controls in an `AbsoluteLayout` overlay above the Skia drawing surface.

## XML Namespaces

SvgML assemblies expose the `SvgML` CLR namespace through `https://github.com/svgml` where the XAML stack supports public assembly-level XML namespace definitions.

Avalonia also keeps the existing `https://github.com/avaloniaui` mapping so SvgML elements can be used unprefixed in Avalonia markup that already has Avalonia as the default namespace.

Uno uses the WinUI/Uno-supported `using:SvgML` XAML namespace form because Uno's `XmlnsDefinitionAttribute` is not publicly usable by application/library code.

## Samples

The Avalonia, Uno, and MAUI demos now demonstrate inline controls using `foreignObject`.

Uno sample markup relies on measured native size because Uno XAML does not currently convert literal `SvgUnit` values for `foreignObject` attributes.
