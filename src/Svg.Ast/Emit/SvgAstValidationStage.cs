// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
using System;
using Svg.Ast.Validation;

namespace Svg.Ast.Emit;

/// <summary>
/// Runs <see cref="SvgValidator"/> before emission to surface semantic diagnostics.
/// </summary>
public sealed class SvgAstValidationStage : ISvgAstEmissionStage
{
    public void Execute(SvgAstEmissionContext context)
    {
        if (context is null)
        {
            throw new ArgumentNullException(nameof(context));
        }

        context.ThrowIfCancellationRequested();

        var diagnostics = SvgValidator.Validate(context.Document);
        foreach (var diagnostic in diagnostics)
        {
            context.ReportDiagnostic(diagnostic);
        }
    }
}
