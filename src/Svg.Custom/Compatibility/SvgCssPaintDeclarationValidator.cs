#nullable enable

using System;

namespace Svg;

internal static class SvgCssPaintDeclarationValidator
{
    public static bool ShouldIgnoreInvalidPaintDeclaration(SvgElement element, string name, string value)
    {
        if (!IsPaintProperty(name) ||
            ContainsVarFunction(value) ||
            IsCssWideKeyword(value))
        {
            return false;
        }

        try
        {
            _ = SvgPaintServerFactory.Create(value, element.OwnerDocument);
            return false;
        }
        catch (Exception ex) when (IsPaintConversionFailure(ex))
        {
            return true;
        }
    }

    private static bool IsPaintProperty(string name)
    {
        return name.Equals("color", StringComparison.OrdinalIgnoreCase) ||
               name.Equals("fill", StringComparison.OrdinalIgnoreCase) ||
               name.Equals("flood-color", StringComparison.OrdinalIgnoreCase) ||
               name.Equals("lighting-color", StringComparison.OrdinalIgnoreCase) ||
               name.Equals("stop-color", StringComparison.OrdinalIgnoreCase) ||
               name.Equals("stroke", StringComparison.OrdinalIgnoreCase);
    }

    private static bool ContainsVarFunction(string value)
    {
        return value.IndexOf("var(", StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private static bool IsCssWideKeyword(string value)
    {
        var trimmed = value.Trim();
        return trimmed.Equals("initial", StringComparison.OrdinalIgnoreCase) ||
               trimmed.Equals("inherit", StringComparison.OrdinalIgnoreCase) ||
               trimmed.Equals("unset", StringComparison.OrdinalIgnoreCase) ||
               trimmed.Equals("revert", StringComparison.OrdinalIgnoreCase) ||
               trimmed.Equals("revert-layer", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsPaintConversionFailure(Exception exception)
    {
        return exception is ArgumentException or FormatException or InvalidCastException or NotSupportedException;
    }
}
