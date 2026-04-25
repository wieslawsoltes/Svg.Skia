#nullable enable

using System;
using System.Collections.Generic;
using System.Globalization;

namespace Svg;

public abstract partial class SvgElement
{
    private string? _compatibilityPresentationAttributeName;
    private string? _compatibilityPresentationAttributeValue;
    private List<KeyValuePair<string, string>>? _compatibilityPresentationAttributes;
    private bool _compatibilityStyleStateCandidateTracked;
    private bool _compatibilityStyleRestoreCandidateTracked;

    internal bool PreserveCompatibilityPresentationAttribute(string name, string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        if (_compatibilityPresentationAttributeName is null)
        {
            _compatibilityPresentationAttributeName = name;
            _compatibilityPresentationAttributeValue = value!;
            return true;
        }

        if (string.Equals(_compatibilityPresentationAttributeName, name, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (_compatibilityPresentationAttributes is not null)
        {
            for (var i = 0; i < _compatibilityPresentationAttributes.Count; i++)
            {
                if (string.Equals(_compatibilityPresentationAttributes[i].Key, name, StringComparison.OrdinalIgnoreCase))
                {
                    return false;
                }
            }
        }

        (_compatibilityPresentationAttributes ??= new List<KeyValuePair<string, string>>(2))
            .Add(new KeyValuePair<string, string>(name, value!));
        return false;
    }

    internal bool MarkCompatibilityStyleStateCandidate()
    {
        if (_compatibilityStyleStateCandidateTracked)
        {
            return false;
        }

        _compatibilityStyleStateCandidateTracked = true;
        return true;
    }

    internal bool MarkCompatibilityStyleRestoreCandidate()
    {
        if (_compatibilityStyleRestoreCandidateTracked)
        {
            return false;
        }

        _compatibilityStyleRestoreCandidateTracked = true;
        return true;
    }

    internal void CopyCompatibilityRawStyleStateTo(SvgElement target)
    {
        target._compatibilityPresentationAttributeName = _compatibilityPresentationAttributeName;
        target._compatibilityPresentationAttributeValue = _compatibilityPresentationAttributeValue;
        target._compatibilityPresentationAttributes = _compatibilityPresentationAttributes is null
            ? null
            : new List<KeyValuePair<string, string>>(_compatibilityPresentationAttributes);
    }

    internal SvgCompatibilityStyleSnapshot CreateCompatibilityStyleSnapshot()
    {
        SvgCompatibilityStyleSnapshot? snapshot = null;

        if (_compatibilityPresentationAttributeName is not null)
        {
            AddCompatibilityStyleSnapshotValue(
                ref snapshot,
                _compatibilityPresentationAttributeName,
                _compatibilityPresentationAttributeValue);

            if (_compatibilityPresentationAttributes is { Count: > 0 })
            {
                for (var i = 0; i < _compatibilityPresentationAttributes.Count; i++)
                {
                    AddCompatibilityStyleSnapshotValue(
                        ref snapshot,
                        _compatibilityPresentationAttributes[i].Key,
                        _compatibilityPresentationAttributes[i].Value);
                }
            }
        }
        else
        {
            foreach (var style in _styles)
            {
                if (!SvgStyleAttributeNames.Contains(style.Key))
                {
                    continue;
                }

                if (style.Value.TryGetValue(StyleSpecificity_PresAttribute, out var presentationValue))
                {
                    AddCompatibilityStyleSnapshotValue(ref snapshot, style.Key, presentationValue);
                }
            }

            foreach (var attribute in Attributes)
            {
                if (SvgStyleAttributeNames.Contains(attribute.Key) &&
                    attribute.Value is not null)
                {
                    AddCompatibilityStyleSnapshotValue(
                        ref snapshot,
                        attribute.Key,
                        Convert.ToString(attribute.Value, CultureInfo.InvariantCulture));
                }
            }

            foreach (var attribute in CustomAttributes)
            {
                if (SvgStyleAttributeNames.Contains(attribute.Key))
                {
                    AddCompatibilityStyleSnapshotValue(ref snapshot, attribute.Key, attribute.Value);
                }
            }
        }

        var inlineStyleText = CustomAttributes.TryGetValue("style", out var styleText)
            ? styleText ?? string.Empty
            : string.Empty;

        if (inlineStyleText.Length == 0 && snapshot is null)
        {
            return SvgCompatibilityStyleSnapshot.Empty;
        }

        snapshot ??= new SvgCompatibilityStyleSnapshot(inlineStyleText);
        snapshot.InlineStyleText = inlineStyleText;
        return snapshot;
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

        snapshot.ApplyPresentationAttributesTo(this);
    }

    private static void AddCompatibilityStyleSnapshotValue(
        ref SvgCompatibilityStyleSnapshot? snapshot,
        string name,
        string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return;
        }

        snapshot ??= SvgCompatibilityStyleSnapshot.CreateEmpty();
        snapshot.AddPresentationAttributeIfAbsent(name, value);
    }
}
