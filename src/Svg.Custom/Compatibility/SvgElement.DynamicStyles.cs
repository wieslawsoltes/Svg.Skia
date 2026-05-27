#nullable enable

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace Svg;

public abstract partial class SvgElement
{
    internal SvgCompatibilityStyleSnapshot CreateCompatibilityStyleSnapshot(SvgCompatibilityElementStyleState? compatibilityStyleState)
    {
        SvgCompatibilityStyleSnapshot? snapshot = null;

        if (compatibilityStyleState?.HasPresentationAttributes == true)
        {
            compatibilityStyleState.AddPresentationAttributesTo(ref snapshot);
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
            : CreateInlineStyleTextFromPendingRules();

        if (inlineStyleText.Length == 0 && snapshot is null)
        {
            return SvgCompatibilityStyleSnapshot.Empty;
        }

        snapshot ??= new SvgCompatibilityStyleSnapshot(inlineStyleText);
        snapshot.InlineStyleText = inlineStyleText;
        return snapshot;
    }

    private string CreateInlineStyleTextFromPendingRules()
    {
        StringBuilder? builder = null;
        foreach (var style in _styles)
        {
            if (!SvgStyleAttributeNames.Contains(style.Key))
            {
                continue;
            }

            if (style.Value.TryGetValue(StyleSpecificity_InlineStyle, out var inlineValue))
            {
                AppendInlineStyleRule(ref builder, style.Key, inlineValue, important: false);
            }

            if (style.Value.TryGetValue(
                    StyleSpecificity_InlineStyle + SvgCssDeclarationPriority.ImportantSpecificityOffset,
                    out var importantInlineValue))
            {
                AppendInlineStyleRule(ref builder, style.Key, importantInlineValue, important: true);
            }
        }

        return builder?.ToString() ?? string.Empty;
    }

    private static void AppendInlineStyleRule(ref StringBuilder? builder, string name, string value, bool important)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return;
        }

        builder ??= new StringBuilder();
        builder.Append(name);
        builder.Append(':');
        builder.Append(value);
        if (important)
        {
            builder.Append(" !important");
        }

        builder.Append(';');
    }

    internal void RestoreCompatibilityStyleState(SvgCompatibilityStyleSnapshot snapshot)
    {
        SvgCssVariableResolver.ClearCustomProperties(this);

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

internal sealed class SvgCompatibilityElementStyleState
{
    private string? _presentationAttributeName;
    private string? _presentationAttributeValue;
    private List<KeyValuePair<string, string>>? _presentationAttributes;
    private bool _styleStateCandidateTracked;
    private bool _styleRestoreCandidateTracked;

    public bool HasPresentationAttributes =>
        _presentationAttributeName is not null ||
        _presentationAttributes?.Count > 0;

    public bool PreservePresentationAttribute(string name, string value)
    {
        if (_presentationAttributeName is null)
        {
            _presentationAttributeName = name;
            _presentationAttributeValue = value;
            return true;
        }

        if (string.Equals(_presentationAttributeName, name, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (_presentationAttributes is not null)
        {
            for (var i = 0; i < _presentationAttributes.Count; i++)
            {
                if (string.Equals(_presentationAttributes[i].Key, name, StringComparison.OrdinalIgnoreCase))
                {
                    return false;
                }
            }
        }

        (_presentationAttributes ??= new List<KeyValuePair<string, string>>(2))
            .Add(new KeyValuePair<string, string>(name, value));
        return false;
    }

    public bool MarkStateCandidate()
    {
        if (_styleStateCandidateTracked)
        {
            return false;
        }

        _styleStateCandidateTracked = true;
        return true;
    }

    public bool MarkRestoreCandidate()
    {
        if (_styleRestoreCandidateTracked)
        {
            return false;
        }

        _styleRestoreCandidateTracked = true;
        return true;
    }

    public void AddPresentationAttributesTo(ref SvgCompatibilityStyleSnapshot? snapshot)
    {
        AddCompatibilityStyleSnapshotValue(
            ref snapshot,
            _presentationAttributeName!,
            _presentationAttributeValue);

        if (_presentationAttributes is not { Count: > 0 })
        {
            return;
        }

        for (var i = 0; i < _presentationAttributes.Count; i++)
        {
            AddCompatibilityStyleSnapshotValue(
                ref snapshot,
                _presentationAttributes[i].Key,
                _presentationAttributes[i].Value);
        }
    }

    public SvgCompatibilityElementStyleState CloneRawPresentationState()
    {
        return new SvgCompatibilityElementStyleState
        {
            _presentationAttributeName = _presentationAttributeName,
            _presentationAttributeValue = _presentationAttributeValue,
            _presentationAttributes = _presentationAttributes is null
                ? null
                : new List<KeyValuePair<string, string>>(_presentationAttributes)
        };
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
