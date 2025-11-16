// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

namespace Svg.Ast;

/// <summary>
/// Represents a diagnostic produced by the SVG AST builder.
/// </summary>
public sealed class SvgDiagnostic
{
    public SvgDiagnostic(string code, string message, SvgDiagnosticSeverity severity, int start, int length)
    {
        Code = code;
        Message = message;
        Severity = severity;
        Start = start;
        Length = length;
    }

    public string Code { get; }

    public string Message { get; }

    public SvgDiagnosticSeverity Severity { get; }

    public int Start { get; }

    public int Length { get; }
}
