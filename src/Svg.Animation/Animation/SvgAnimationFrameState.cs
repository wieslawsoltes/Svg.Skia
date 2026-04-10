using System;
using System.Collections.Generic;

namespace Svg.Skia;

internal sealed class SvgAnimationFrameAttributeState
{
    public SvgAnimationFrameAttributeState(string key, SvgElementAddress targetAddress, string attributeName, string value)
    {
        Key = key;
        TargetAddress = targetAddress;
        AttributeName = attributeName;
        Value = value;
    }

    public string Key { get; }

    public SvgElementAddress TargetAddress { get; }

    public string AttributeName { get; }

    public string Value { get; }

    public bool HasSameValue(SvgAnimationFrameAttributeState other)
    {
        return string.Equals(AttributeName, other.AttributeName, StringComparison.Ordinal) &&
               string.Equals(Value, other.Value, StringComparison.Ordinal) &&
               string.Equals(TargetAddress.Key, other.TargetAddress.Key, StringComparison.Ordinal);
    }
}

internal sealed class SvgAnimationFrameState
{
    private readonly Dictionary<string, SvgAnimationFrameAttributeState> _attributes;

    public SvgAnimationFrameState(TimeSpan time, int version, Dictionary<string, SvgAnimationFrameAttributeState> attributes)
    {
        Time = time;
        Version = version;
        _attributes = attributes ?? throw new ArgumentNullException(nameof(attributes));
    }

    public TimeSpan Time { get; }

    public int Version { get; }

    public int Count => _attributes.Count;

    public IEnumerable<SvgAnimationFrameAttributeState> Attributes => _attributes.Values;

    public bool TryGetAttribute(string key, out SvgAnimationFrameAttributeState attribute)
    {
        return _attributes.TryGetValue(key, out attribute!);
    }

    public bool IsEquivalentTo(SvgAnimationFrameState? other)
    {
        if (ReferenceEquals(this, other))
        {
            return true;
        }

        if (other is null || _attributes.Count != other._attributes.Count)
        {
            return false;
        }

        foreach (var pair in _attributes)
        {
            if (!other._attributes.TryGetValue(pair.Key, out var otherAttribute) ||
                !pair.Value.HasSameValue(otherAttribute))
            {
                return false;
            }
        }

        return true;
    }

    public int GetDirtyTargetCount(SvgAnimationFrameState? previous)
    {
        if (previous is null)
        {
            return _attributes.Count;
        }

        var dirtyCount = 0;

        foreach (var pair in _attributes)
        {
            if (!previous._attributes.TryGetValue(pair.Key, out var previousAttribute) ||
                !pair.Value.HasSameValue(previousAttribute))
            {
                dirtyCount++;
            }
        }

        foreach (var pair in previous._attributes)
        {
            if (!_attributes.ContainsKey(pair.Key))
            {
                dirtyCount++;
            }
        }

        return dirtyCount;
    }

    public IEnumerable<SvgAnimationFrameAttributeState> EnumerateDirtyAttributes(SvgAnimationFrameState? previous)
    {
        if (previous is null)
        {
            foreach (var attribute in _attributes.Values)
            {
                yield return attribute;
            }

            yield break;
        }

        foreach (var pair in _attributes)
        {
            if (!previous._attributes.TryGetValue(pair.Key, out var previousAttribute) ||
                !pair.Value.HasSameValue(previousAttribute))
            {
                yield return pair.Value;
            }
        }
    }

    public IEnumerable<string> EnumerateRemovedKeys(SvgAnimationFrameState? previous)
    {
        if (previous is null)
        {
            yield break;
        }

        foreach (var pair in previous._attributes)
        {
            if (!_attributes.ContainsKey(pair.Key))
            {
                yield return pair.Key;
            }
        }
    }

    public IEnumerable<SvgAnimationFrameAttributeState> EnumerateRemovedAttributes(SvgAnimationFrameState? previous)
    {
        if (previous is null)
        {
            yield break;
        }

        foreach (var pair in previous._attributes)
        {
            if (!_attributes.ContainsKey(pair.Key))
            {
                yield return pair.Value;
            }
        }
    }
}
