using System.Collections.Generic;

namespace Svg.Model;

public readonly record struct SvgParameters(Dictionary<string, string>? Entities, string? Css);
