#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;

namespace Svg;

public partial class SvgDocument
{
    private List<SvgCssStyleSource>? _compatibilityStyleSources;

    internal void SetCompatibilityStyleSources(IEnumerable<SvgCssStyleSource> styles)
    {
        _compatibilityStyleSources = styles is null
            ? null
            : styles.Select(style => new SvgCssStyleSource(style.Content, style.BaseUri)).ToList();
    }

    internal void CopyCompatibilityStyleSourcesTo(SvgDocument target)
    {
        target.SetCompatibilityStyleSources(_compatibilityStyleSources ?? Enumerable.Empty<SvgCssStyleSource>());
    }

    internal void ApplyCompatibilityStyles(SvgElement? scopeRoot = null)
    {
        if (_compatibilityStyleSources is { Count: > 0 })
        {
            SvgCssCompatibilityProcessor.Apply(this, _compatibilityStyleSources, new SvgElementFactory(), scopeRoot);
        }

        ApplyInlineStyles(scopeRoot);

        if (scopeRoot is null)
        {
            FlushStyles(children: true);
        }
        else
        {
            scopeRoot.FlushStyles(children: true);
        }
    }

    private void ApplyInlineStyles(SvgElement? scopeRoot)
    {
        var parser = new SvgInlineStyleAttributeParser();
        foreach (var element in EnumerateScope(scopeRoot))
        {
            if (element.CustomAttributes.TryGetValue("style", out var styleText) &&
                !string.IsNullOrWhiteSpace(styleText))
            {
                parser.ApplyStyles(element, styleText);
            }
        }
    }

    private IEnumerable<SvgElement> EnumerateScope(SvgElement? scopeRoot)
    {
        if (scopeRoot is null)
        {
            yield return this;
            foreach (var descendant in Descendants())
            {
                yield return descendant;
            }

            yield break;
        }

        yield return scopeRoot;
        foreach (var descendant in scopeRoot.Descendants())
        {
            yield return descendant;
        }
    }
}
