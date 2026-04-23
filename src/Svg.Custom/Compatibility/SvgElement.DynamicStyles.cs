#nullable enable

using System.Collections.Generic;

namespace Svg;

public abstract partial class SvgElement
{
    internal void RestoreCompatibilityStyleState(SvgCompatibilityStyleSnapshot snapshot)
    {
        foreach (var name in SvgStyleAttributeNames.All)
        {
            _ = Attributes.Remove(name);
            CustomAttributes.Remove(name);
            _styles.Remove(name);
        }

        CustomAttributes.Remove(SvgStyleAttributeNames.RawTextDecorationAttributeKey);

        if (string.IsNullOrWhiteSpace(snapshot.InlineStyleText))
        {
            CustomAttributes.Remove("style");
        }
        else
        {
            CustomAttributes["style"] = snapshot.InlineStyleText;
        }

        foreach (var attribute in snapshot.PresentationAttributes)
        {
            AddStyle(attribute.Key, attribute.Value, StyleSpecificity_PresAttribute);
        }
    }
}
