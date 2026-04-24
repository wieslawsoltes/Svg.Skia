#nullable enable

using System;
using System.Collections.Generic;
using System.Globalization;

namespace Svg;

public abstract partial class SvgElement
{
    internal SvgCompatibilityStyleSnapshot CreateCompatibilityStyleSnapshot()
    {
        Dictionary<string, string>? presentationAttributes = null;

        foreach (var style in _styles)
        {
            if (!SvgStyleAttributeNames.Contains(style.Key))
            {
                continue;
            }

            if (style.Value.TryGetValue(StyleSpecificity_PresAttribute, out var presentationValue))
            {
                AddCompatibilityStyleSnapshotValue(ref presentationAttributes, style.Key, presentationValue);
            }
        }

        foreach (var attribute in Attributes)
        {
            if (SvgStyleAttributeNames.Contains(attribute.Key) &&
                attribute.Value is not null)
            {
                AddCompatibilityStyleSnapshotValue(
                    ref presentationAttributes,
                    attribute.Key,
                    Convert.ToString(attribute.Value, CultureInfo.InvariantCulture));
            }
        }

        foreach (var attribute in CustomAttributes)
        {
            if (SvgStyleAttributeNames.Contains(attribute.Key))
            {
                AddCompatibilityStyleSnapshotValue(ref presentationAttributes, attribute.Key, attribute.Value);
            }
        }

        var inlineStyleText = CustomAttributes.TryGetValue("style", out var styleText)
            ? styleText ?? string.Empty
            : string.Empty;

        if (inlineStyleText.Length == 0 && presentationAttributes is null)
        {
            return SvgCompatibilityStyleSnapshot.Empty;
        }

        return new SvgCompatibilityStyleSnapshot(
            inlineStyleText,
            presentationAttributes ?? SvgCompatibilityStyleSnapshot.Empty.PresentationAttributes);
    }

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

    private static void AddCompatibilityStyleSnapshotValue(
        ref Dictionary<string, string>? presentationAttributes,
        string name,
        string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return;
        }

        presentationAttributes ??= new Dictionary<string, string>(System.StringComparer.OrdinalIgnoreCase);
        if (!presentationAttributes.ContainsKey(name))
        {
            presentationAttributes[name] = value!;
        }
    }
}
