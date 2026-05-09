using System.Text;
using Svg.Model;

namespace Uno.Svg.Skia;

internal static class SvgCacheKey
{
    public static string Create(string path, SvgParameters? parameters)
    {
        var builder = new StringBuilder(path.Trim());
        var css = parameters?.Css;
        if (!string.IsNullOrWhiteSpace(css))
        {
            builder.Append("|css:").Append(css.Trim());
        }

        if (parameters?.Entities is { Count: > 0 } entities)
        {
            foreach (var entity in entities.OrderBy(static pair => pair.Key, StringComparer.Ordinal))
            {
                builder.Append("|entity:")
                    .Append(entity.Key)
                    .Append('=')
                    .Append(entity.Value);
            }
        }

        if (parameters?.CurrentColor is { } currentColor)
        {
            builder.Append("|currentColor:").Append(currentColor.ToArgb().ToString("X8"));
        }

        return builder.ToString();
    }
}
