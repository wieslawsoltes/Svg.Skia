// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
using System;
using System.Linq;
using Svg.Ast;
using Xunit;

namespace Svg.Ast.UnitTests.Diagnostics;

public class SvgAstBuilderDiagnosticTests
{
    [Fact]
    public void ParserDiagnostics_UsePreciseSpans()
    {
        var svg = "<svg width=\"100\"height=\"100\"></svg>";
        var expectedStart = svg.IndexOf("height", StringComparison.Ordinal);
        Assert.True(expectedStart >= 0);

        var source = SvgSourceText.FromString(svg);
        var document = SvgAstBuilder.Build(source);

        var diagnostic = Assert.Single(document.Diagnostics.Where(d =>
            d.Severity == SvgDiagnosticSeverity.Error &&
            d.Start == expectedStart));

        Assert.True(diagnostic.Length > 0);
    }

    [Fact]
    public void Reports_Unknown_Namespace_Prefix()
    {
        var svg = "<svg xmlns=\"http://www.w3.org/2000/svg\"><foo:rect width=\"10\" height=\"10\" /></svg>";

        var source = SvgSourceText.FromString(svg);
        var document = SvgAstBuilder.Build(source);

        Assert.Contains(document.Diagnostics, d => d.Code == "SVGASTNS001");
    }

    [Fact]
    public void Reports_Invalid_Presentation_Attribute()
    {
        var svg = "<svg xmlns=\"http://www.w3.org/2000/svg\"><rect foo=\"bar\" /></svg>";

        var source = SvgSourceText.FromString(svg);
        var document = SvgAstBuilder.Build(source);

        Assert.Contains(document.Diagnostics, d => d.Code == "SVGASTATTR001");
    }

    [Fact]
    public void Reports_Unresolved_Symbol_Reference()
    {
        var svg = "<svg xmlns=\"http://www.w3.org/2000/svg\"><rect width=\"10\" height=\"10\" fill=\"url(#missing)\" /></svg>";

        var source = SvgSourceText.FromString(svg);
        var document = SvgAstBuilder.Build(source);
        var validationDiagnostics = Svg.Ast.Validation.SvgValidator.Validate(document);
        Assert.Contains(validationDiagnostics, d => d.Code == "SVGASTREF001");
    }
}
