// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
using System;
using System.Collections.Immutable;

namespace Svg.Ast.Emit;

/// <summary>
/// Represents the outcome of running the emission pipeline.
/// </summary>
public sealed class SvgAstEmissionResult<TResult>
{
    internal SvgAstEmissionResult(
        SvgAstDocument document,
        TResult output,
        ImmutableArray<SvgDiagnostic> diagnostics,
        SvgSymbolTable? symbolTable)
    {
        Document = document ?? throw new ArgumentNullException(nameof(document));
        Output = output;
        Diagnostics = diagnostics;
        SymbolTable = symbolTable;
    }

    /// <summary>
    /// Gets the document that was processed.
    /// </summary>
    public SvgAstDocument Document { get; }

    /// <summary>
    /// Gets the emitted output.
    /// </summary>
    public TResult Output { get; }

    /// <summary>
    /// Gets diagnostics collected during parsing and emission.
    /// </summary>
    public ImmutableArray<SvgDiagnostic> Diagnostics { get; }

    /// <summary>
    /// Gets the symbol table that was produced when <see cref="SvgAstEmissionOptions.BuildSymbolTable"/> is enabled.
    /// </summary>
    public SvgSymbolTable? SymbolTable { get; }
}
