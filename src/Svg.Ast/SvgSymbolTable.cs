// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
using System;
using System.Collections.Generic;
using System.Collections.Immutable;

namespace Svg.Ast;

/// <summary>
/// Represents lookup tables for resolving ids and referenced elements.
/// </summary>
public sealed class SvgSymbolTable
{
    private static readonly string[] s_gradientNames = { "linearGradient", "radialGradient" };
    private static readonly string[] s_clipNames = { "clipPath" };
    private static readonly string[] s_maskNames = { "mask" };
    private static readonly string[] s_patternNames = { "pattern" };

    private SvgSymbolTable(
        ImmutableDictionary<string, SvgAstElement> ids,
        ImmutableDictionary<string, SvgAstElement> gradients,
        ImmutableDictionary<string, SvgAstElement> clipPaths,
        ImmutableDictionary<string, SvgAstElement> masks,
        ImmutableDictionary<string, SvgAstElement> patterns)
    {
        Ids = ids;
        Gradients = gradients;
        ClipPaths = clipPaths;
        Masks = masks;
        Patterns = patterns;
    }

    public ImmutableDictionary<string, SvgAstElement> Ids { get; }

    public ImmutableDictionary<string, SvgAstElement> Gradients { get; }

    public ImmutableDictionary<string, SvgAstElement> ClipPaths { get; }

    public ImmutableDictionary<string, SvgAstElement> Masks { get; }

    public ImmutableDictionary<string, SvgAstElement> Patterns { get; }

    public bool TryGetElementById(string id, out SvgAstElement element) => Ids.TryGetValue(id, out element!);

    public bool TryGetGradient(string id, out SvgAstElement element) => Gradients.TryGetValue(id, out element!);

    public bool TryGetClipPath(string id, out SvgAstElement element) => ClipPaths.TryGetValue(id, out element!);

    public bool TryGetMask(string id, out SvgAstElement element) => Masks.TryGetValue(id, out element!);

    public bool TryGetPattern(string id, out SvgAstElement element) => Patterns.TryGetValue(id, out element!);

    public static SvgSymbolTable Build(SvgAstDocument document)
    {
        var idBuilder = ImmutableDictionary.CreateBuilder<string, SvgAstElement>(StringComparer.Ordinal);
        var gradientBuilder = ImmutableDictionary.CreateBuilder<string, SvgAstElement>(StringComparer.Ordinal);
        var clipBuilder = ImmutableDictionary.CreateBuilder<string, SvgAstElement>(StringComparer.Ordinal);
        var maskBuilder = ImmutableDictionary.CreateBuilder<string, SvgAstElement>(StringComparer.Ordinal);
        var patternBuilder = ImmutableDictionary.CreateBuilder<string, SvgAstElement>(StringComparer.Ordinal);

        if (document.RootElement is { } root)
        {
            Traverse(root);
        }

        return new SvgSymbolTable(
            idBuilder.ToImmutable(),
            gradientBuilder.ToImmutable(),
            clipBuilder.ToImmutable(),
            maskBuilder.ToImmutable(),
            patternBuilder.ToImmutable());

        void Traverse(SvgAstElement element)
        {
            if (element.TryGetAttribute("id", out var idAttribute))
            {
                var idValue = NormalizeId(idAttribute.GetValueText());
                if (!string.IsNullOrEmpty(idValue) && !idBuilder.ContainsKey(idValue))
                {
                    idBuilder[idValue] = element;
                }
            }

            AddIfMatches(element, s_gradientNames, gradientBuilder);
            AddIfMatches(element, s_clipNames, clipBuilder);
            AddIfMatches(element, s_maskNames, maskBuilder);
            AddIfMatches(element, s_patternNames, patternBuilder);

            foreach (var child in element.Children)
            {
                if (child is SvgAstElement childElement)
                {
                    Traverse(childElement);
                }
            }
        }
    }

    private static void AddIfMatches(
        SvgAstElement element,
        string[] names,
        ImmutableDictionary<string, SvgAstElement>.Builder builder)
    {
        foreach (var name in names)
        {
            if (string.Equals(element.Name.LocalName, name, StringComparison.Ordinal))
            {
                if (element.TryGetAttribute("id", out var id) && !builder.ContainsKey(id.GetValueText()))
                {
                    var key = NormalizeId(id.GetValueText());
                    if (!string.IsNullOrEmpty(key))
                    {
                        builder[key] = element;
                    }
                }
                break;
            }
        }
    }

    private static string NormalizeId(string value)
    {
        return value.Trim().Trim('\"', '\'');
    }
}
