---
title: "Svg.Custom"
---

# Svg.Custom

`Svg.Custom` packages the SVG DOM and parser used by the rest of the repository. It is the layer that understands SVG documents as `SvgDocument`, `SvgElement`, and related DOM types before the repository-specific model and rendering layers take over.

## Install

```bash
dotnet add package Svg.Custom
```

## Choose this package when

- you need to parse or inspect SVG documents directly,
- you want to modify the SVG DOM before rendering,
- you want the exact DOM package used by `Svg.Model` and `Svg.Skia`,
- you are building tooling that should stay at the SVG-document level.

## Important packaging note

Unlike most packages in this repository, `Svg.Custom` is packaged under the `MS-PL` license because it vendors and republishes the upstream SVG implementation it is built from.

If you depend on `Svg.Custom` directly, review that license decision alongside the rest of your dependency policy.

## Namespace and role

| Package | Namespace | Role |
| --- | --- | --- |
| `Svg.Custom` | `Svg` | Parsed document model and parser |

Most applications do not need to reference `Svg.Custom` explicitly because higher layers bring it in transitively. Direct references make sense when you want DOM access in application or tooling code.

## Basic usage

```csharp
using Svg;
using Svg.Skia;

var document = SvgDocument.Open("Assets/icon.svg");

if (document is not null)
{
    Console.WriteLine($"{document.Width} x {document.Height}");

    using var renderer = new SKSvg();
    renderer.FromSvgDocument(document);
}
```

That pattern is useful when a document should be parsed once, inspected or modified, and only then rendered.

## What this package adds in this repository

The repository wraps the vendored SVG sources with project-level concerns such as:

- the package identity used by the repo,
- trimming and AOT annotations for supported target frameworks,
- embedded SVG 1.1 DTD resources,
- analyzer and generator integration used by the vendored code path,
- repository-local SVG 1.1 animation object-model types,
- typed `pointer-events` support used by the shared hit-test and interaction layers.

The rendering behavior still lives above this package, not inside it.

## Animation DOM coverage

`Svg.Custom` now includes the repository's SVG 1.1 animation object-model overlay in the `Svg` namespace.

That surface includes:

- `SvgAnimationElement`
- `SvgAnimationAttributeElement`
- `SvgAnimationValueElement`
- `SvgAnimate`
- `SvgSet`
- `SvgAnimateMotion`
- `SvgAnimateColor`
- `SvgAnimateTransform`
- `SvgMPath`

The DOM layer also adds typed enums and converters for common animation attributes such as `attributeType`, `restart`, `fill`, `calcMode`, `additive`, `accumulate`, and transform `type`.

This package does not execute animation by itself. It only parses and stores the DOM. Runtime evaluation lives in [Svg.Skia](svg-skia).

## Good use cases

- validating raw SVG input before rendering,
- rewriting document structure or attributes,
- custom importers that output `SvgDocument`,
- applications that already understand the upstream `Svg` DOM and want to pair it with `Svg.Skia`.

## When not to choose `Svg.Custom`

- Choose [Svg.Skia](svg-skia) when you want a renderer, not just the DOM.
- Choose [Svg.Model](svg-model) when your work starts after parsing and focuses on the intermediate drawables or model.
- Choose a UI package when the main goal is Avalonia integration.

## Related docs

- [Source Formats and Assets](../concepts/source-formats-and-assets)
- [Interaction and Animation](../guides/interaction-and-animation)
- [Svg.Skia](svg-skia)
- [Svg.Model](svg-model)
