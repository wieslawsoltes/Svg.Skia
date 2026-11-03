---
title: "Avalonia Fonts and Default Settings"
---

# Avalonia fonts and default SVG settings

## Why browser SVG labels can become boxes

Svg.Skia shapes and renders text through SkiaSharp. SkiaSharp's font manager,
Avalonia's font collections, and the browser's CSS font system are separate.
WebAssembly normally has very few fonts visible to SkiaSharp, so a diagram can
render missing-glyph boxes even when an Avalonia `TextBlock` renders correctly.
Installing a CSS web font does not register it with either .NET font manager.

`Svg.Controls.Skia.Avalonia` installs `AvaloniaTypefaceProvider` in the default
`SvgSource` settings. It resolves fonts through Avalonia's `FontManager` and
creates native SkiaSharp typefaces from the selected font's stream. No reflection
or access to `SvgSource.s_skiaModel` is required.

## Register fonts before loading SVGs

For Inter, reference `Avalonia.Fonts.Inter` at the same version as Avalonia and
use the normal Avalonia setup:

```csharp
using Avalonia;

public static AppBuilder BuildAvaloniaApp() =>
    AppBuilder.Configure<App>()
        .WithInterFont();
```

Keep the application's existing platform startup, theme, and other builder calls.
Load SVGs after Avalonia has initialized its platform services and font collections.
`WithInterFont()` registers the `fonts:Inter` collection; it does not change
`FontManager.DefaultFontFamily`. The provider recognizes same-name registered
collections, so bare `Inter` can resolve independently of the application's default
font. A collection registered under a different key must be requested explicitly
as `fonts:YourCollectionKey#Family Name` or with an `avares:` resource reference.
Collection URI keys are case-sensitive; use the registered spelling.

An SVG can then request the registered family:

```xml
<svg xmlns="http://www.w3.org/2000/svg" width="520" height="100">
  <text x="16" y="38" font-family="Inter, sans-serif" font-size="24">
    Embedded font: Zażółć gęślą jaźń
  </text>
  <text x="16" y="76" font-family="Inter" font-size="24"
        font-weight="700" font-style="italic">
    Bold italic diagram label
  </text>
</svg>
```

For application-owned font files, mark them as Avalonia resources and use
Avalonia's embedded-font family syntax. The family after `#` must be the font's
internal family name, not its filename:

```xml
<ItemGroup>
  <AvaloniaResource Include="Assets/Fonts/**" />
</ItemGroup>
```

```xml
<text font-family="avares://MyApp/Assets/Fonts#My App Font"
      font-size="20">Application font</text>
```

Fonts must include the characters used in the diagram. The bridge cannot supply
glyphs missing from an embedded font, and it does not download fonts.

## Resolution and fallback policy

The default provider runs before SkiaSharp's normal typeface providers.
Document-local fonts retain their existing precedence. Insert a custom application
provider at index zero to give it priority over the Avalonia bridge.

The provider preserves the requested weight, style, and stretch. It accepts
registered and embedded families on all platforms. Unknown concrete family names
return no match, allowing subsequent SVG family names and providers to be tried.
For example, `Missing Font, Inter, sans-serif` can select Inter. Use a concrete
family name or an explicit `avares:`/`fonts:` family reference; aliases that
resolve to a different family name through private Avalonia font-manager mappings
are not guaranteed to be recognized by the bridge.

In WebAssembly, unspecified and CSS generic families such as `serif`,
`sans-serif`, `monospace`, and `system-ui` can use Avalonia's configured
default font. This is a usable browser fallback, not a guarantee that one embedded
font reproduces the typography of every generic category. Bundle and request
specific serif or monospace fonts when that distinction matters. On desktop,
the parameterless provider leaves generic fallback to the existing SkiaSharp providers.

To deliberately make Inter the application's default font, configure Avalonia
during startup as well as registering the font collection:

```csharp
AppBuilder.Configure<App>()
    .WithInterFont()
    .With(new Avalonia.Media.FontManagerOptions
    {
        DefaultFamilyName = "fonts:Inter#Inter"
    });
```

The provider's generic fallback follows this font-manager default, not an inherited
`TextBlock.FontFamily` or the first registered collection. If only upright font
data exists, the SVG renderer synthesizes italic/oblique using Avalonia's Skia
shear convention. Existing explicit skew and native italic faces are preserved.

To choose the policy explicitly, replace the automatic provider during startup:

```csharp
using System.Collections.Generic;
using Avalonia.Svg.Skia;
using Svg.Skia.TypefaceProviders;

SvgSource.ConfigureDefaultSettings(settings =>
{
    var providers = settings.TypefaceProviders ??= new List<ITypefaceProvider>();
    for (var i = providers.Count - 1; i >= 0; i--)
    {
        if (providers[i] is AvaloniaTypefaceProvider)
            providers.RemoveAt(i);
    }

    // Use false to retain SkiaSharp's generic-family fallback on every platform.
    providers.Insert(0, new AvaloniaTypefaceProvider(useDefaultFontFallback: true));
});
```

The provider resolves Avalonia services lazily, so constructing it before
`AppBuilder` initialization is safe. Misses are not cached by the provider;
fonts registered later can be resolved by new SVG loads. Reload previously
rendered sources after registering additional fonts.

## Configure the defaults through the supported API

`SvgSource.DefaultSettings` exposes the settings copied to new sources.
`SvgSource.ConfigureDefaultSettings(Action<SKSvgSettings>)` is the preferred API:

```csharp
using Avalonia.Svg.Skia;

SvgSource.ConfigureDefaultSettings(settings =>
{
    settings.EnableTextSelectionRendering = false;
    settings.EnableBrokenImagePlaceholders = true;

    // For an application-specific provider:
    // settings.TypefaceProviders!.Insert(0, myTypefaceProvider);
});
```

The callback edits a copy. Its scalar and provider-list changes are published
together under the same lock used to copy settings for new sources. If the callback
throws, those changes are not published. Provider objects, color spaces, and other
service objects are shared references; mutating an object's internals is not rolled
back. Do not dispose shared objects while sources still use them.

The API applies to synchronous and asynchronous file/resource loads, SVG strings,
streams, documents, lazy XAML sources, and reloads. Each loaded source receives
its own settings object and provider list. Existing sources keep their settings
until `ReLoad(...)` or `ReLoadAsync(...)`; cloning a source preserves that
source's settings.

Direct edits to `DefaultSettings` are supported for startup configuration when
they cannot overlap loading or other configuration. Existing code using
`s_skiaModel.Settings` still accesses the same object for compatibility, but
new code should use the supported API.

## Standalone SKSvg in an Avalonia application

The automatic bridge belongs to `Svg.Controls.Skia.Avalonia`; standalone
`Svg.Skia.SKSvg` does not depend on Avalonia. Add the public provider explicitly
when using `SKSvg` directly inside an initialized Avalonia application:

```csharp
using Avalonia.Svg.Skia;
using Svg.Skia;

using var svg = new SKSvg();
svg.Settings.TypefaceProviders!.Insert(0, new AvaloniaTypefaceProvider());
svg.FromSvg("""
    <svg xmlns="http://www.w3.org/2000/svg" width="200" height="50">
      <text x="8" y="32" font-family="Inter" font-size="24">Hello, SVG!</text>
    </svg>
    """);
```

The provider caches font data against Avalonia's glyph-typeface lifetime and
returns a caller-owned native typeface for each request. Disposing one returned
typeface does not dispose Avalonia's font or invalidate another request.

## Troubleshooting

- Confirm that fonts are registered before SVG loading and that the SVG requests
  the correct internal family name.
- Check that the font contains every required character. Inter alone is not a
  replacement for CJK or emoji fonts.
- If using standalone `SKSvg`, add `AvaloniaTypefaceProvider` explicitly.
- If replacing `TypefaceProviders`, preserve the Avalonia provider when
  embedded-font support is required.
- Check custom provider order and whether each provider returns `null` for
  families it does not support.
- A platform typeface must expose a usable stream through Avalonia's
  `TryGetStream`. Unsupported or unavailable streams leave other providers
  available. Resolution failures are logged at Avalonia's `Debug` font level.
- Reload existing sources after changing defaults or registering more fonts.

## Browser regression sample

`samples/AvaloniaSvgFontBrowser` uses the real Avalonia browser runtime and the
modified library project. It embeds Inter, renders SVG and Avalonia text, and
exposes font and settings checks in the page.

Use the .NET 10 SDK with the `wasm-tools` workload:

```bash
dotnet workload install wasm-tools
dotnet run --project samples/AvaloniaSvgFontBrowser/AvaloniaSvgFontBrowser.csproj
```

Open the HTTP address printed by the development server. Check the diagnostics
and visually inspect regular, bold, italic, family-list, and generic labels.
Serve published WebAssembly files over HTTP; opening `index.html` through
a `file:` URL does not reproduce browser resource loading correctly.
