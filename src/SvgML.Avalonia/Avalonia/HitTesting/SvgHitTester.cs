using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Rendering;
using Avalonia.VisualTree;

namespace SvgML.Avalonia.HitTesting;

internal sealed class SvgHitTester : IHitTester
{
    private readonly IHitTester _inner;

    public SvgHitTester(IHitTester inner)
    {
        _inner = inner;
    }

    public IEnumerable<Visual> HitTest(Point point, Visual root, Func<Visual, bool>? filter)
    {
        foreach (var visual in _inner.HitTest(point, root, filter))
        {
            yield return visual;

            if (visual is svg svgRoot)
            {
                foreach (var hit in svgRoot.HitTestElements(point).OfType<Visual>())
                {
                    if (!ReferenceEquals(hit, visual) && (filter?.Invoke(hit) ?? true))
                    {
                        yield return hit;
                    }
                }
            }
        }
    }

    public Visual? HitTestFirst(Point point, Visual root, Func<Visual, bool>? filter)
    {
        foreach (var visual in HitTest(point, root, filter))
        {
            return visual;
        }

        return null;
    }
}
