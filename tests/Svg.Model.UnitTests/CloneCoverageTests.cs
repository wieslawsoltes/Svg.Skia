using System;
using System.Linq;
using ShimSkiaSharp;
using Svg.Model.Drawables;
using Xunit;

namespace Svg.Model.UnitTests;

public class CloneCoverageTests
{
    [Fact]
    public void AllDrawableTypesSupportCloneOrDeepClone()
    {
        var types = typeof(DrawableBase).Assembly
            .GetTypes()
            .Where(type => typeof(SKDrawable).IsAssignableFrom(type))
            .Where(type => type.IsClass && !type.IsAbstract);

        var missing = types
            .Where(type => !SupportsClone(type))
            .Select(type => type.FullName)
            .ToList();

        Assert.True(missing.Count == 0, $"Missing clone support: {string.Join(", ", missing)}");
    }

    private static bool SupportsClone(Type type)
    {
        if (typeof(ICloneable).IsAssignableFrom(type))
        {
            return true;
        }

        return type.GetInterfaces().Any(HasDeepCloneInterface);
    }

    private static bool HasDeepCloneInterface(Type type)
        => type.IsGenericType && type.GetGenericTypeDefinition() == typeof(IDeepCloneable<>);
}
