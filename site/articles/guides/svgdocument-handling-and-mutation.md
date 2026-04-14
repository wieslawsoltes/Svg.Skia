---
title: "SvgDocument Handling and Mutation"
---

# SvgDocument Handling and Mutation

`SvgDocument` is the highest-level editable representation in the runtime. Use it when you want to inspect or mutate SVG DOM state instead of working at the lower `ShimSkiaSharp.SKPicture` command level.

## Loading from an existing document

If your code already has a parsed document, pass it directly to `SKSvg` or the Avalonia wrapper:

```csharp
using Svg;
using Svg.Skia;

var document = new SvgDocument
{
    Width = 80,
    Height = 40
};

using var svg = new SKSvg();
svg.FromSvgDocument(document);
```

```csharp
using Avalonia.Svg.Skia;
using Svg;

var source = SvgSource.LoadFromSvgDocument(new SvgDocument());
```

`SKSvg.SourceDocument` then exposes the current document instance backing the renderer.

## Full-document mutation and rebuild

For structural edits, broad CSS changes, or any change that touches the root element, mutate the document and rebuild from it:

```csharp
using System.Drawing;
using Svg;
using Svg.Skia;

using var svg = new SKSvg();
svg.FromSvg(
    "<svg width=\"80\" height=\"40\">" +
    "  <rect id=\"rect-a\" x=\"10\" y=\"8\" width=\"24\" height=\"12\" fill=\"red\" />" +
    "</svg>");

var document = svg.SourceDocument!;
var rect = (SvgRectangle)document.GetElementById("rect-a")!;
rect.Fill = new SvgColourServer(Color.BlueViolet);

svg.FromSvgDocument(document);
```

`FromSvgDocument(...)` reparses nothing. It recompiles the retained scene and refreshes the current `Picture` from the already-mutated DOM.

## Reparse the original source with new parameters

When the change is not a DOM edit but a different CSS string or entity dictionary, use `ReLoad(...)` instead of manually rebuilding the current document:

```csharp
using Svg.Model;
using Svg.Skia;

SKSvg.CacheOriginalStream = true;

using var svg = new SKSvg();
svg.Load("image.svg", new SvgParameters(null, ".accent { fill: red; }"));

svg.ReLoad(new SvgParameters(null, ".accent { fill: blue; }"));
```

Avalonia exposes the same pattern through `SvgSource.ReLoad(...)`.

## Ownership and cloning

`FromSvgDocument(...)` uses the `SvgDocument` instance you pass in. If the same document is shared across multiple views or editors, clone it before mutating:

```csharp
using Svg;
using Svg.Model.Services;
using Svg.Skia;

var sharedDocument = SvgService.FromSvg("<svg width=\"10\" height=\"10\" />");
var isolatedDocument = (SvgDocument)sharedDocument.DeepCopy();

using var svg = new SKSvg();
svg.FromSvgDocument(isolatedDocument);
```

That keeps one caller from mutating the DOM another caller still expects to be stable.

## When to stay at the DOM layer

Prefer `SvgDocument` mutation when:

- you need to add or remove elements,
- you want to edit authored attributes such as `fill`, `stroke`, `transform`, or `viewBox`,
- tooling needs DOM traversal, ids, or XML serialization,
- the same mutated document should later be saved back to SVG.

For localized retained-scene refresh after a small DOM edit, continue with [Retained Scene Graph Usage](retained-scene-graph-usage) and [Performance and Retained-Scene Refresh](performance-and-retained-scene-refresh).
