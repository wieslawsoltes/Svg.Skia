// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Threading;

namespace Svg.Ast.Emit;

/// <summary>
/// Provides state that is shared between emission stages and emitters.
/// </summary>
public sealed class SvgAstEmissionContext
{
    private readonly Dictionary<Type, object> _services = new();
    private readonly List<SvgDiagnostic> _diagnostics = new();
    private SvgSymbolTable? _symbolTable;

    internal SvgAstEmissionContext(
        SvgAstDocument document,
        SvgAstEmissionOptions options,
        CancellationToken cancellationToken)
    {
        Document = document ?? throw new ArgumentNullException(nameof(document));
        Options = options ?? SvgAstEmissionOptions.Default;
        CancellationToken = cancellationToken;
    }

    /// <summary>
    /// Gets the document being processed.
    /// </summary>
    public SvgAstDocument Document { get; }

    /// <summary>
    /// Gets the options controlling the pipeline behavior.
    /// </summary>
    public SvgAstEmissionOptions Options { get; }

    /// <summary>
    /// Gets the cancellation token that stages should observe.
    /// </summary>
    public CancellationToken CancellationToken { get; }

    /// <summary>
    /// Gets the optional items provided in <see cref="SvgAstEmissionOptions"/>.
    /// </summary>
    public IImmutableDictionary<string, object?> Items => Options.Items;

    /// <summary>
    /// Gets diagnostics reported during emission.
    /// </summary>
    public IReadOnlyList<SvgDiagnostic> Diagnostics => _diagnostics;

    /// <summary>
    /// Gets the current symbol table if it has already been computed.
    /// </summary>
    public SvgSymbolTable? SymbolTable => _symbolTable;

    /// <summary>
    /// Gets a value indicating whether the symbol table can be created.
    /// </summary>
    public bool IsSymbolTableEnabled => Options.BuildSymbolTable;

    /// <summary>
    /// Reports a diagnostic produced during emission.
    /// </summary>
    public void ReportDiagnostic(SvgDiagnostic diagnostic)
    {
        if (diagnostic is null)
        {
            throw new ArgumentNullException(nameof(diagnostic));
        }

        _diagnostics.Add(diagnostic);
    }

    /// <summary>
    /// Retrieves the symbol table, throwing if the option is disabled.
    /// </summary>
    public SvgSymbolTable GetSymbolTable()
    {
        if (!Options.BuildSymbolTable)
        {
            throw new InvalidOperationException("Symbol table building is disabled through SvgAstEmissionOptions.");
        }

        return _symbolTable ??= SvgSymbolTable.Build(Document);
    }

    /// <summary>
    /// Attempts to retrieve the current symbol table if available.
    /// </summary>
    public bool TryGetSymbolTable(out SvgSymbolTable? symbolTable)
    {
        if (_symbolTable is null)
        {
            symbolTable = null;
            return false;
        }

        symbolTable = _symbolTable;
        return true;
    }

    /// <summary>
    /// Adds or retrieves a service instance scoped to this emission context.
    /// </summary>
    public TService GetOrAddService<TService>(Func<TService> factory)
        where TService : class
    {
        if (factory is null)
        {
            throw new ArgumentNullException(nameof(factory));
        }

        if (_services.TryGetValue(typeof(TService), out var existing))
        {
            return (TService)existing;
        }

        var created = factory();
        if (created is null)
        {
            throw new InvalidOperationException($"The factory for {typeof(TService)} returned null.");
        }

        _services[typeof(TService)] = created;
        return created;
    }

    /// <summary>
    /// Attempts to retrieve a previously registered service.
    /// </summary>
    public bool TryGetService<TService>(out TService? service)
        where TService : class
    {
        if (_services.TryGetValue(typeof(TService), out var existing))
        {
            service = (TService)existing;
            return true;
        }

        service = null;
        return false;
    }

    /// <summary>
    /// Registers or replaces a service instance scoped to this emission context.
    /// </summary>
    public void SetService<TService>(TService service)
        where TService : class
    {
        if (service is null)
        {
            throw new ArgumentNullException(nameof(service));
        }

        _services[typeof(TService)] = service;
    }

    /// <summary>
    /// Throws if cancellation has been requested.
    /// </summary>
    public void ThrowIfCancellationRequested() => CancellationToken.ThrowIfCancellationRequested();

    internal ImmutableArray<SvgDiagnostic> CollectDiagnostics() =>
        _diagnostics.Count == 0 ? ImmutableArray<SvgDiagnostic>.Empty : ImmutableArray.CreateRange(_diagnostics);

    internal SvgSymbolTable? PeekSymbolTable() => _symbolTable;
}
