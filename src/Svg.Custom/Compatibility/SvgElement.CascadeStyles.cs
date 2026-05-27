#nullable enable

using System.Collections.Generic;

namespace Svg;

public abstract partial class SvgElement
{
    internal void AddCompatibilityStyle(string name, string value, int specificity)
    {
        if (specificity == StyleSpecificity_PresAttribute)
        {
            specificity = 1;
        }

        if (!_styles.TryGetValue(name, out var rules) || rules is null)
        {
            rules = new SortedDictionary<int, string>();
            _styles[name] = rules;
        }

        rules[specificity] = value;
    }
}
