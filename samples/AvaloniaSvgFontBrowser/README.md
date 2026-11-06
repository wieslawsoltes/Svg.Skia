# Avalonia SVG font browser regression

This .NET 10 WebAssembly application exercises issue #565 against the library
projects in this repository. It registers Inter using Avalonia's `WithInterFont()`;
the SVG typeface provider is installed automatically by `SvgSource`.

```sh
dotnet workload install wasm-tools
dotnet run --project samples/AvaloniaSvgFontBrowser/AvaloniaSvgFontBrowser.csproj -c Release
```

Open the HTTP URL printed by `dotnet run`. The left pane renders ordinary Avalonia
text and an SVG using the same text for each case. The right pane reports actual
WebAssembly checks for family and style selection, glyph coverage in the actual
recorded text commands, default-settings propagation, nonempty rendering, and
pixel equality with an explicit font reference. Named-family cases use the embedded
`avares://` Inter font; generic-family cases use Avalonia's actual default, which is
reported in the page. Registering Inter does not silently change that default.
Use **Run font checks again** to verify loading after disposal of
the preceding SVG sources.

Open the same URL with `?default=inter` to test the explicit application-default
configuration. In that mode, startup also sets
`FontManagerOptions.DefaultFamilyName = "fonts:Inter#Inter"`, so generic and omitted
SVG font families must render with Inter. The page identifies which setup is active.

Cases include regular, bold, and italic Inter, quoted CSS fallback lists, explicit
embedded-font URIs, generic font families, omitted `font-family`, and a Mermaid-like
family list. The comparison text includes Polish characters that must have real
glyphs rather than missing-glyph boxes.

The sample intentionally keeps the default rendering backend selection, and it
does not install fonts into SkiaSharp's system font manager. Browser checks must
run in the actual WebAssembly application, not against the HTML header fonts or a
standalone browser-native SVG.

## Automated Chromium validation

The path-filtered **Avalonia browser fonts** GitHub Actions workflow publishes this
application, runs both font-configuration modes in Chromium, checks each mode twice
(including SVG disposal/reload), and uploads screenshots, DOM results, and browser errors even when
a check fails. The same runner can be used locally after publishing:

```sh
dotnet publish samples/AvaloniaSvgFontBrowser/AvaloniaSvgFontBrowser.csproj -c Release -o artifacts/avalonia-browser-fonts/publish
npm install --no-save --package-lock=false playwright@1.64.1
npx playwright install --with-deps chromium
node scripts/validate_avalonia_browser_fonts.mjs --root artifacts/avalonia-browser-fonts/publish/wwwroot --output artifacts/avalonia-browser-fonts/results
```

The runner starts its own HTTP server on an available loopback port. It tests the
published WebAssembly assets and does not require a globally installed browser or
access to desktop system fonts.
