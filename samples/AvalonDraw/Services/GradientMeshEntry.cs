using System;
using Svg;
using Svg.Model;

namespace AvalonDraw.Services;

public class GradientMeshEntry : PropertyEntry
{
    public GradientMesh Mesh { get; }

    public GradientMeshEntry(GradientMesh mesh)
        : base("Mesh", mesh.ToString(), (_, __) => { })
    {
        Mesh = mesh;
    }

    public override void Apply(object target)
    {
        if (target is SvgVisualElement element)
            element.CustomAttributes["mesh"] = ToString();
    }

    public void UpdateValue()
    {
        Value = ToString();
    }

    public override string ToString()
    {
        return Mesh.ToString();
    }
}
