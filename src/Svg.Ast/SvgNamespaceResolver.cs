// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
using System;
using System.Collections.Generic;

namespace Svg.Ast;

/// <summary>
/// Tracks namespace prefix scopes for SVG documents.
/// </summary>
public sealed class SvgNamespaceResolver
{
    private readonly Stack<Dictionary<string, string>> _scopes = new();

    public SvgNamespaceResolver()
    {
        PushScope();
        Declare(string.Empty, SvgNamespaces.SvgNamespace);
        Declare("xml", SvgNamespaces.XmlNamespace);
        Declare("xlink", SvgNamespaces.XLinkNamespace);
    }

    public void PushScope()
    {
        _scopes.Push(new Dictionary<string, string>(StringComparer.Ordinal));
    }

    public void PopScope()
    {
        if (_scopes.Count > 1)
        {
            _scopes.Pop();
        }
    }

    public void Declare(string? prefix, string namespaceUri)
    {
        var key = prefix ?? string.Empty;
        _scopes.Peek()[key] = namespaceUri;
    }

    public bool TryResolve(string? prefix, out string namespaceUri)
    {
        var key = prefix ?? string.Empty;
        foreach (var scope in _scopes)
        {
            if (scope.TryGetValue(key, out var value))
            {
                namespaceUri = value;
                return true;
            }
        }

        namespaceUri = SvgNamespaces.SvgNamespace;
        return false;
    }

    public string Resolve(string? prefix)
        => TryResolve(prefix, out var ns) ? ns : ns;
}
