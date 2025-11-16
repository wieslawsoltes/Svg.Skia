// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
using System;
using Svg.Ast;
using Svg.Ast.Emit;

namespace Svg.Model.Ast;

/// <summary>
/// Convenience helpers for creating legacy <see cref="SvgDocument"/> instances from AST documents.
/// </summary>
public static class SvgAstDomService
{
    private static readonly SvgAstEmissionPipeline s_pipeline =
        new(new[] { (ISvgAstEmissionStage)new SvgAstValidationStage() });

    /// <summary>
    /// Builds an <see cref="SvgDocument"/> from the supplied AST document.
    /// </summary>
    public static SvgDocument? CreateDocument(SvgAstDocument document, SvgAstEmissionOptions? emissionOptions = null)
    {
        if (document is null)
        {
            throw new ArgumentNullException(nameof(document));
        }

        var emitter = new SvgAstDomEmitter();
        var result = s_pipeline.Emit(document, emitter, emissionOptions);
        return result.Output;
    }
}
