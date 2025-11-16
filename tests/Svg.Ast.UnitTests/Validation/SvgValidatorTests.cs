// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
using System.Linq;
using Svg.Ast.Validation;
using Xunit;

namespace Svg.Ast.UnitTests.Validation;

public class SvgValidatorTests
{
    [Fact]
    public void Reports_Duplicate_Ids()
    {
        var svg = """
            <svg xmlns="http://www.w3.org/2000/svg">
              <rect id="dup" width="10" height="10" />
              <rect id="dup" width="5" height="5" />
            </svg>
            """;

        var document = SvgAstBuilder.Build(SvgSourceText.FromString(svg));
        var diagnostics = SvgValidator.Validate(document);

        Assert.Contains(diagnostics, d => d.Code == "SVGASTID001");
    }

    [Fact]
    public void Reports_Unresolved_References()
    {
        var svg = """
            <svg xmlns="http://www.w3.org/2000/svg">
              <rect width="10" height="10" fill="url(#missing)" />
            </svg>
            """;

        var document = SvgAstBuilder.Build(SvgSourceText.FromString(svg));
        var diagnostics = SvgValidator.Validate(document);

        Assert.Contains(diagnostics, d => d.Code == "SVGASTREF001");
    }

    [Fact]
    public void Reports_Negative_Lengths()
    {
        var svg = """
            <svg xmlns="http://www.w3.org/2000/svg">
              <rect width="-5" height="10" />
            </svg>
            """;

        var document = SvgAstBuilder.Build(SvgSourceText.FromString(svg));
        var diagnostics = SvgValidator.Validate(document);

        Assert.Contains(diagnostics, d => d.Code == "SVGASTVAL002");
    }
}
