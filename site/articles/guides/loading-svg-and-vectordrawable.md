---
title: "Loading SVG, SvgDocument, and VectorDrawable"
---

# Loading SVG, SvgDocument, and VectorDrawable

`SKSvg` exposes several load surfaces because the repository supports both "parse from source" and "render an already-owned document" workflows.

## Choose the right load API

- Use `Load(path)` when the source is a file or resource path and you want auto-detection for `.svg`, `.svgz`, and Android `VectorDrawable` XML.
- Use `Load(stream, parameters, baseUri)` when the input already lives in memory or when external assets need a base URI for resolution.
- Use `Load(XmlReader)` when an upstream XML pipeline is already creating readers.
- Use `FromSvg(string)` when raw markup is already in memory.
- Use `FromSvgDocument(SvgDocument)` when your code already parsed or generated a `SvgDocument`.
- Use `LoadVectorDrawable(...)` or `FromVectorDrawable(...)` when the source is explicitly Android drawable XML.

## Files, streams, base URIs, and XML readers

```csharp
using System;
using System.IO;
using System.Xml;
using Svg.Skia;

using var svgFromFile = new SKSvg();
svgFromFile.Load("image.svg");

using var stream = File.OpenRead("image.svg");
using var svgFromStream = new SKSvg();
svgFromStream.Load(stream, parameters: null, baseUri: new Uri("file:///Users/me/Assets/"));

using var reader = XmlReader.Create(new StringReader("<svg width=\"16\" height=\"16\" />"));
using var svgFromReader = new SKSvg();
svgFromReader.Load(reader);
```

## Reusable factory helpers

If you prefer one-shot helpers, `SKSvg` exposes factory methods:

- `CreateFromFile(...)`
- `CreateFromStream(...)`
- `CreateFromSvg(...)`
- `CreateFromSvgDocument(...)`
- `CreateFromVectorDrawable(...)`

## Raw SVG strings and parsed documents

When parsing is already done elsewhere, loading from a `SvgDocument` avoids reparsing text and makes the document available immediately through `SKSvg.SourceDocument`.

```csharp
using Svg;
using Svg.Model.Services;
using Svg.Skia;

var svgText = "<svg width=\"16\" height=\"16\"><circle cx=\"8\" cy=\"8\" r=\"8\" fill=\"red\" /></svg>";

using var svgFromText = new SKSvg();
svgFromText.FromSvg(svgText);

var document = SvgService.FromSvg(svgText);
using var svgFromDocument = new SKSvg();
svgFromDocument.FromSvgDocument(document);
```

If you plan to mutate the DOM later, `FromSvgDocument(...)` is the most direct starting point because your code already owns the document instance.

## Reloading with different CSS or entity parameters

`ReLoad(...)` reparses the original input using a new `SvgParameters` value. This is useful when the source stays the same but the CSS or XML entities change.

```csharp
using Svg.Model;
using Svg.Skia;

SKSvg.CacheOriginalStream = true;

using var svg = new SKSvg();
svg.Load("image.svg", new SvgParameters(null, ".accent { fill: red; }"));

svg.ReLoad(new SvgParameters(null, ".accent { fill: blue; }"));
```

`ReLoad(...)` requires `SKSvg.CacheOriginalStream = true` when the original source came from a stream.

## Avalonia resource and document loading

The Skia-backed Avalonia package exposes both path-based loading and document-based loading:

```csharp
using Avalonia.Svg.Skia;
using Svg;

var source = SvgSource.Load("avares://MyAssembly/Assets/Icon.svg", baseUri: null);
var documentSource = SvgSource.LoadFromSvgDocument(new SvgDocument());
```

Use `SvgSource.LoadFromSvgDocument(...)` when an editor or tool already owns a `SvgDocument` and the control should render that DOM directly.

## VectorDrawable handling

Use these APIs when the source is Android XML:

```csharp
using Svg.Skia;

using var svg = new SKSvg();
svg.LoadVectorDrawable("icon.xml");
```

Or convert raw XML directly:

```csharp
using Svg.Skia;

using var svg = new SKSvg();
svg.FromVectorDrawable(vectorDrawableXml);
```

`Load(path)` also auto-detects `VectorDrawable` XML from the file contents, so the explicit methods are mainly for callers that want a fixed code path.

The repository tests cover:

- width and viewport validation,
- clip-path behavior,
- group transforms,
- supported and unsupported Android attributes,
- equivalence between representative VectorDrawable and SVG examples.
