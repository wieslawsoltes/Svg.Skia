# Svg.Ast Review Notes

## Findings

1. **Diagnostic Span Accuracy**
   - File: `src/Svg.Ast/SvgAstBuilder.cs` (~330)
   - Issue: `ReportDiagnostic` uses `node.Span` for every parser diagnostic emitted by `Microsoft.Language.Xml`. As a result, all errors/warnings point to the entire element (start of `<svg>` etc.) rather than the actual token. Need to forward the diagnostic’s own span (`diagnostic.Span` or equivalent) when capturing errors.

2. **Metadata-driven Attribute Warnings**
   - File: `src/Svg.Ast/SvgMetadata.cs` overrides + generated tables, consumed via `SvgAstBuilder.ValidateAttribute`.
   - Issue: Metadata doesn’t include fundamental attributes like `width`, `height`, `viewBox`, `xlink:href`, etc., so valid SVG files trigger warnings such as `Attribute 'width' is not valid on '<svg>' elements.` The RO metadata needs to be fleshed out before enabling these warnings; otherwise diagnostics are noisy and misleading.

3. **SvgAst CLI Render Feedback**
   - File: `tools/SvgAst.Cli/Program.cs` (`render` subcommand).
   - Issue: The render command ignores `document.Diagnostics` and `renderResult.Diagnostics`, emitting only "No picture produced" for failures. Diagnostics should be printed and a non-zero exit code returned on fatal parse/emission errors.
   - Status: Addressed by `samples/SvgAstPlayground` which now surfaces renderer diagnostics, skips PNG output when errors occur, and sets a non-zero exit code for fatal parse/emission failures.

## Next Steps

### Fix 1 – Diagnostic Spans
- Update `SvgAstBuilder.CollectParserDiagnostics` to pass the parser diagnostic’s own span when calling `ReportDiagnostic`.
- Ensure span coordinates map to source text indices by using the diagnostic’s `Span` property (if available) or `diagnostic.GetSpan()`.
- Add tests validating that parser errors point to the correct offsets.
