---
title: "Svg.Skia"
layout: simple
og_type: website
---

<div class="svgskia-hero">
  <div class="svgskia-hero-copy">
    <div class="svgskia-eyebrow"><i class="bi bi-bezier2" aria-hidden="true"></i> SVG Rendering Toolkit</div>
    <h1>Svg.Skia</h1>
    <p class="lead"><strong>Svg.Skia</strong> loads SVG 1.1 and Android VectorDrawable content, builds a reusable picture model, and turns it into <code>SKPicture</code> output, Avalonia visuals, generated C# code, and conversion artifacts.</p>
    <div class="svgskia-hero-actions">
      <a class="btn btn-primary btn-lg" href="articles/getting-started/overview"><i class="bi bi-signpost-split" aria-hidden="true"></i> Choose a Package</a>
      <a class="btn btn-outline-secondary btn-lg" href="api"><i class="bi bi-braces" aria-hidden="true"></i> Browse API</a>
      <a class="btn btn-outline-secondary btn-lg" href="https://github.com/wieslawsoltes/Svg.Skia"><i class="bi bi-github" aria-hidden="true"></i> GitHub Repository</a>
    </div>
    <div class="svgskia-pill-row">
      <span class="svgskia-pill">SVG 1.1 subset</span>
      <span class="svgskia-pill">SKPicture output</span>
      <span class="svgskia-pill">Avalonia integration</span>
      <span class="svgskia-pill">Editor packages</span>
      <span class="svgskia-pill">Build-time generation</span>
      <span class="svgskia-pill">VectorDrawable import</span>
    </div>
  </div>
  <div class="svgskia-hero-media svgskia-hero-media--showcase">
    <div class="svgskia-showcase-shell">
      <div class="svgskia-showcase-head">
        <span class="svgskia-showcase-eyebrow">Repository SVG samples</span>
        <strong>Actual SVG assets used by the samples, source generator, and rendering pipeline.</strong>
      </div>
      <div class="svgskia-showcase-grid">
        <figure class="svgskia-showcase-card">
          <div class="svgskia-showcase-preview">
            <img src="images/hero-tiger.svg" alt="Tiger SVG sample asset" />
          </div>
          <figcaption>Complex paths and layered fills</figcaption>
        </figure>
        <figure class="svgskia-showcase-card">
          <div class="svgskia-showcase-preview">
            <img src="images/hero-camera.svg" alt="Digital camera SVG sample asset" />
          </div>
          <figcaption>Icon and UI-style artwork</figcaption>
        </figure>
      </div>
    </div>
  </div>
</div>

## Package Surface

<div class="svgskia-package-grid">
  <a class="svgskia-package-card" href="articles/packages/svg-skia">
    <span class="svgskia-package-title"><i class="bi bi-box-seam" aria-hidden="true"></i> Svg.Skia</span>
    <p>The core renderer that loads markup, builds a picture model, and emits <code>SKPicture</code> output.</p>
  </a>
  <a class="svgskia-package-card" href="articles/packages/svg-controls-skia-avalonia">
    <span class="svgskia-package-title"><i class="bi bi-window" aria-hidden="true"></i> Avalonia Packages</span>
    <p>Two Avalonia stacks: a Skia-backed renderer and a pure Avalonia picture pipeline.</p>
  </a>
  <a class="svgskia-package-card" href="articles/packages/svg-sourcegenerator-skia">
    <span class="svgskia-package-title"><i class="bi bi-filetype-cs" aria-hidden="true"></i> Generated Code</span>
    <p>Compile SVG assets into C# or source-generated pictures for static assets and AOT-friendly workflows.</p>
  </a>
  <a class="svgskia-package-card" href="articles/editor">
    <span class="svgskia-package-title"><i class="bi bi-pencil-square" aria-hidden="true"></i> Editor Packages</span>
    <p>Reusable SVG editing services, the UnoDraw workspace with a direct Skia canvas, and existing Avalonia panels and workspace.</p>
  </a>
  <a class="svgskia-package-card" href="articles/guides/cli-conversion">
    <span class="svgskia-package-title"><i class="bi bi-terminal" aria-hidden="true"></i> Tooling</span>
    <p>Batch-convert SVG and VectorDrawable input into png, jpg, webp, pdf, xps, or raw generated code.</p>
  </a>
</div>

## Documentation Sections

<div class="svgskia-link-grid svgskia-link-grid--wide">
  <a class="svgskia-link-card" href="articles/getting-started">
    <span class="svgskia-link-card-title"><i class="bi bi-signpost-split" aria-hidden="true"></i> Getting Started</span>
    <p>Install packages, pick the right stack, and render your first SVG in code or XAML.</p>
  </a>
  <a class="svgskia-link-card" href="articles/concepts">
    <span class="svgskia-link-card-title"><i class="bi bi-diagram-3" aria-hidden="true"></i> Concepts</span>
    <p>Understand the package layout, asset pipeline, picture model, and rebuild workflow.</p>
  </a>
  <a class="svgskia-link-card" href="articles/editor">
    <span class="svgskia-link-card-title"><i class="bi bi-layout-three-columns" aria-hidden="true"></i> Editor</span>
    <p>Embed the UnoDraw workspace, the Avalonia editor, or the shared document, geometry and history components.</p>
  </a>
  <a class="svgskia-link-card" href="articles/guides">
    <span class="svgskia-link-card-title"><i class="bi bi-journal-code" aria-hidden="true"></i> Guides</span>
    <p>Follow focused walkthroughs for rendering, exporting, hit testing, conversion, and code generation.</p>
  </a>
  <a class="svgskia-link-card" href="articles/xaml">
    <span class="svgskia-link-card-title"><i class="bi bi-filetype-xaml" aria-hidden="true"></i> XAML Usage</span>
    <p>Wire up <code>Svg</code>, <code>SvgImage</code>, <code>SvgResource</code>, and <code>SKPictureImage</code> in Avalonia.</p>
  </a>
  <a class="svgskia-link-card" href="articles/advanced">
    <span class="svgskia-link-card-title"><i class="bi bi-speedometer2" aria-hidden="true"></i> Advanced</span>
    <p>Review VectorDrawable support, trimming and NativeAOT notes, repository tests, and packaging expectations.</p>
  </a>
  <a class="svgskia-link-card" href="articles/reference">
    <span class="svgskia-link-card-title"><i class="bi bi-collection" aria-hidden="true"></i> Reference</span>
    <p>Package maps, sample inventory, generated API coverage, licensing, and the Lunet docs pipeline.</p>
  </a>
  <a class="svgskia-link-card" href="api">
    <span class="svgskia-link-card-title"><i class="bi bi-braces-asterisk" aria-hidden="true"></i> API Documentation</span>
    <p>Browse generated API pages for the renderer, Avalonia packages, picture model, and source-generator assemblies.</p>
  </a>
</div>

## Start Here

- Use [Getting Started Overview](articles/getting-started/overview) if you are choosing between the core renderer, the Avalonia packages, and the CLI.
- Use [Quickstart: Loading and Rendering](articles/getting-started/quickstart-loading-and-rendering) when you want a minimal `SKSvg` example first.
- Use [XAML Overview](articles/xaml/overview) when the target app is Avalonia.
- Use [Editor Overview](articles/editor) when you want the reusable `Svg.Editor.*` package stack.
- Use [API Coverage Index](articles/reference/api-coverage-index) when you want to know which assemblies are included in generated docs.
