// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Threading;

namespace Svg.Ast.Emit;

/// <summary>
/// Coordinates emission stages and executes an emitter against a document.
/// </summary>
public sealed class SvgAstEmissionPipeline
{
    private readonly ImmutableArray<ISvgAstEmissionStage> _stages;

    /// <summary>
    /// Initializes a new instance of the <see cref="SvgAstEmissionPipeline"/> class.
    /// </summary>
    public SvgAstEmissionPipeline(IEnumerable<ISvgAstEmissionStage>? stages = null)
    {
        _stages = stages switch
        {
            null => ImmutableArray<ISvgAstEmissionStage>.Empty,
            ImmutableArray<ISvgAstEmissionStage> immutable => immutable,
            _ => ImmutableArray.CreateRange(stages)
        };
    }

    private SvgAstEmissionPipeline(ImmutableArray<ISvgAstEmissionStage> stages)
    {
        _stages = stages;
    }

    /// <summary>
    /// Gets the configured stages.
    /// </summary>
    public ImmutableArray<ISvgAstEmissionStage> Stages => _stages;

    /// <summary>
    /// Returns a pipeline with an additional stage appended to the end of the sequence.
    /// </summary>
    public SvgAstEmissionPipeline WithStage(ISvgAstEmissionStage stage)
    {
        if (stage is null)
        {
            throw new ArgumentNullException(nameof(stage));
        }

        return new SvgAstEmissionPipeline(_stages.Add(stage));
    }

    /// <summary>
    /// Executes the pipeline and emitter.
    /// </summary>
    public SvgAstEmissionResult<TResult> Emit<TResult>(
        SvgAstDocument document,
        ISvgAstEmitter<TResult> emitter,
        SvgAstEmissionOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        if (document is null)
        {
            throw new ArgumentNullException(nameof(document));
        }

        if (emitter is null)
        {
            throw new ArgumentNullException(nameof(emitter));
        }

        var effectiveOptions = options ?? SvgAstEmissionOptions.Default;
        var context = new SvgAstEmissionContext(document, effectiveOptions, cancellationToken);

        foreach (var stage in _stages)
        {
            context.ThrowIfCancellationRequested();
            stage.Execute(context);
        }

        var output = emitter.Emit(context);
        var emissionDiagnostics = context.CollectDiagnostics();
        var diagnosticsBuilder = ImmutableArray.CreateBuilder<SvgDiagnostic>(
            document.Diagnostics.Length + emissionDiagnostics.Length);
        diagnosticsBuilder.AddRange(document.Diagnostics);
        diagnosticsBuilder.AddRange(emissionDiagnostics);
        var diagnostics = diagnosticsBuilder.ToImmutable();
        var symbolTable = context.PeekSymbolTable();

        return new SvgAstEmissionResult<TResult>(document, output, diagnostics, symbolTable);
    }
}
