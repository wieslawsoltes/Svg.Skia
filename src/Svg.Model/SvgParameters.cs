using System.Collections.Generic;

namespace Svg.Model;

public class SvgParameters(Dictionary<string, string>? entities, string? style)
{
    public Dictionary<string, string>? Entities { get; } = entities;

    public string? Style { get; } = style;
}
