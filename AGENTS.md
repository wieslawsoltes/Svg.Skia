# Project Guidelines for Codex Agents

This repository contains the Svg.Skia library and associated tests. The following rules apply when creating pull requests with the Codex agent.

## References
- SVG 1.1 specification: https://www.w3.org/TR/SVG11/
- Avalonia UI code: https://github.com/AvaloniaUI/Avalonia
- Avalonia UI documentation: https://github.com/AvaloniaUI/avalonia-docs
- Free .NET decompiler (ILSpy): https://github.com/icsharpcode/ILSpy

## Workflow
1. After cloning the repo run `git submodule update --init --recursive` to fetch external dependencies.
2. Follow the coding conventions enforced by `.editorconfig`. Run `dotnet format --no-restore` before committing to ensure style compliance.
3. Build the solution using `dotnet build Svg.Skia.slnx -c Release`.
4. Run all tests with `dotnet test Svg.Skia.slnx -c Release`.

## W3C Chrome Overrides
- Files under `tests/Svg.Skia.UnitTests/ChromeReference/W3C/` must be generated from real Google Chrome captures, not copied from the legacy W3C PNG set.
- Use `node scripts/capture_w3c_chrome_overrides.mjs` to regenerate the checked Chrome override set. Pass specific test names as comma-separated arguments to limit capture scope, for example `node scripts/capture_w3c_chrome_overrides.mjs masking-path-04-b,linking-a-09-b`.
- The capture script serves the repository over HTTP and screenshots a `480x360` Chrome-hosted iframe, which matches the standalone viewport policy used by `W3CTestSuiteTests`.
- Never capture or manually inspect W3C fixtures in Chrome via `file://` URLs. Chrome treats `file:` URLs as unique security origins, so fixtures that load linked resources, fonts, nested documents, or iframe-hosted content can fail with errors like `Unsafe attempt to load URL file:///... 'file:' URLs are treated as unique security origins.`
- If you hit that warning, stop and rerun the fixture through HTTP instead of trying to work around it in-place. Prefer `node scripts/capture_w3c_chrome_overrides.mjs`, or serve the repo root with a local HTTP server and open the fixture as `http://127.0.0.1:<port>/externals/W3C_SVG_11_TestSuite/W3C_SVG_11_TestSuite/svg/<name>.svg`.
- When debugging manually with a harness page or iframe, keep both the parent page and the target SVG on the same HTTP origin. Do not mix an HTTP harness with `file://` fixture URLs or vice versa.
- When a Chrome override exists, keep `W3CTestSuiteTests` pointed at that override instead of the W3C reference PNG.
- Do not reintroduce footer exclusion regions for W3C comparisons. Prefer Chrome overrides plus narrowly-scoped per-test thresholds only when the library is visually aligned with Chrome but still differs at the pixel-raster level.
- If a W3C fixture depends on JavaScript, DOM APIs, or browser-only runtime behavior that Svg.Skia does not implement, keep that row skipped with an explicit reason instead of manufacturing a fake baseline.

## Testing Workflow
- For W3C work, first run focused rows with `dotnet test tests/Svg.Skia.UnitTests/Svg.Skia.UnitTests.csproj -f net10.0 -c Release --no-restore --filter "FullyQualifiedName~W3CTestSuiteTests.Tests"`.
- When refreshing baselines, rerun the focused W3C subset that changed before running the full W3C suite.
- Before committing renderer or baseline changes, run:
  - `dotnet format Svg.Skia.slnx --no-restore`
  - `dotnet build Svg.Skia.slnx -c Release`
  - `dotnet test Svg.Skia.slnx -c Release`
- Keep unrelated local state files, especially `samples/TestApp/TestApp.json`, out of commits unless the task explicitly requires them.

## Commit Guidelines
- Use concise commit messages summarizing your change in under 72 characters.
- Additional description lines may follow the summary if necessary.

## Pull Request Guidelines
- The pull request description should briefly explain what was changed and reference any relevant specification sections or documentation when appropriate.
