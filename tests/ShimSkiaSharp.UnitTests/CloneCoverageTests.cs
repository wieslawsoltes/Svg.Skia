using System;
using System.Collections.Generic;
using System.Linq;
using ShimSkiaSharp;
using Xunit;

namespace ShimSkiaSharp.UnitTests;

public class CloneCoverageTests
{
    [Fact]
    public void AllEligibleTypesSupportCloneOrDeepClone()
    {
        var excluded = new HashSet<Type>
        {
            typeof(SKDrawable)
        };

        var types = typeof(SKPaint).Assembly
            .GetExportedTypes()
            .Where(type => type.Namespace == nameof(ShimSkiaSharp))
            .Where(type => type.IsClass)
            .Where(type => !excluded.Contains(type));

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
