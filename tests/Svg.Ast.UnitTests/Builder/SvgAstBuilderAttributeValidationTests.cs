// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
using Svg.Ast;
using Xunit;

namespace Svg.Ast.UnitTests.Builder;

public class SvgAstBuilderAttributeValidationTests
{
    [Fact]
    public void Does_Not_Warn_For_Common_Core_Attributes()
    {
        var svg = """
            <svg xmlns="http://www.w3.org/2000/svg"
                 width="100"
                 height="100"
                 viewBox="0 0 100 100">
              <defs>
                <linearGradient id="grad" x1="0%" x2="100%" y1="0%" y2="0%">
                  <stop offset="0%" stop-color="#fff" />
                  <stop offset="100%" stop-color="#000" />
                </linearGradient>
              </defs>
              <rect width="100" height="100" rx="4" fill="url(#grad)" />
            </svg>
            """;

        var document = SvgAstBuilder.Build(SvgSourceText.FromString(svg));

        Assert.DoesNotContain(document.Diagnostics, d => d.Code == "SVGASTATTR001");
    }
}
