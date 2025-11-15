// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
using System.Threading;
using ShimSkiaSharp;
using Svg.Ast;
using Svg.Ast.Emit;

namespace Svg.Model.Ast;

/// <summary>
/// High-level entry points for rendering <see cref="SvgAstDocument"/> instances to Skia pictures.
/// </summary>
public static class SvgAstRenderService
{
    private static readonly SvgAstEmissionPipeline s_pipeline =
        new SvgAstEmissionPipeline(new[] { (ISvgAstEmissionStage)new SvgAstValidationStage() });

    /// <summary>
    /// Renders the specified AST into an <see cref="SKPicture"/>.
    /// </summary>
    public static SvgAstEmissionResult<SKPicture?> Render(
        SvgAstDocument document,
        SvgAstRenderOptions? renderOptions = null,
        SvgAstEmissionOptions? emissionOptions = null,
        CancellationToken cancellationToken = default)
    {
        var emitter = new SvgAstSkiaEmitter(renderOptions);
        return s_pipeline.Emit(document, emitter, emissionOptions, cancellationToken);
    }

    /// <summary>
    /// Convenience helper that returns the rendered picture without diagnostics.
    /// </summary>
    public static SKPicture? ToPicture(
        SvgAstDocument document,
        SvgAstRenderOptions? renderOptions = null,
        SvgAstEmissionOptions? emissionOptions = null,
        CancellationToken cancellationToken = default)
    {
        var result = Render(document, renderOptions, emissionOptions, cancellationToken);
        return result.Output;
    }
}
