using System;
using System.Collections.Generic;

namespace Svg
{
    /// <summary>
    /// Svg.Custom override of the upstream attribute collection.
    ///
    /// The upstream implementation returns the exact same deferred paint server instance when an
    /// inheritable property such as <c>fill="currentColor"</c> flows from a parent element to a
    /// child. That is usually fine for concrete colors, but it breaks once the deferred server has
    /// cached the parent's computed <c>color</c>: descendants that override <c>color</c> keep
    /// rendering with the parent's color instead of resolving <c>currentColor</c> for themselves.
    ///
    /// We keep the original inheritance logic intact and only clone inherited deferred
    /// <c>currentColor</c> paint servers. Direct properties, such as filter primitives with
    /// <c>lighting-color="currentColor"</c>, still resolve against the element that declared the
    /// property, while inherited paint still gets a fresh per-consumer resolution context.
    /// </summary>
    public sealed class SvgAttributeCollection : Dictionary<string, object>
    {
        private readonly SvgElement _owner;

        public SvgAttributeCollection(SvgElement owner)
        {
            _owner = owner;
        }

        public TAttributeType GetAttribute<TAttributeType>(string attributeName, TAttributeType defaultValue = default(TAttributeType))
        {
            if (ContainsKey(attributeName) && base[attributeName] != null)
                return (TAttributeType)base[attributeName];

            return defaultValue;
        }

        public TAttributeType GetInheritedAttribute<TAttributeType>(string attributeName, bool inherited, TAttributeType defaultValue = default(TAttributeType))
        {
            var inherit = false;

            if (ContainsKey(attributeName))
            {
                var result = (TAttributeType)base[attributeName];

                if (IsInheritValue(result))
                    inherit = true;
                else
                {
                    var deferred = result as SvgDeferredPaintServer;
                    if (deferred == null)
                    {
                        // Concrete values can be returned immediately. The bug we are correcting is
                        // specific to deferred paint servers whose final color depends on the
                        // consuming element's computed color.
                        return result;
                    }
                    else
                    {
                        // Deferred paint can itself resolve to "inherit". We keep that upstream
                        // behavior intact so explicit inherit and inherited currentColor still share
                        // one code path after this branch.
                        var server = SvgDeferredPaintServer.TryGet<SvgPaintServer>(deferred, _owner);
                        if (server == SvgPaintServer.Inherit)
                            inherit = true;
                        else
                        {
                            // Directly declared paint stays bound to the declaring element. That is
                            // important for cases like lighting-color="currentColor" on filter
                            // primitives, where resolving against the consuming shape would be wrong.
                            return result;
                        }
                    }
                }
            }

            if (inherited || inherit)
            {
                var parentAttribute = _owner.Parent?.Attributes.GetInheritedAttribute<object>(attributeName, inherited);
                if (parentAttribute != null)
                {
                    // Only inherited currentColor paint servers need to be cloned. The deep copy
                    // gives each consumer its own deferred-resolution/cache instance, so a child
                    // that overrides "color" no longer reuses the parent's already-resolved value.
                    return (TAttributeType)CloneInheritedCurrentColorPaintServer(parentAttribute);
                }
            }

            return defaultValue;
        }

        private static object CloneInheritedCurrentColorPaintServer(object value)
        {
            if (value is SvgDeferredPaintServer deferred
                && string.Equals(deferred.DeferredId, "currentColor", StringComparison.Ordinal))
            {
                // DeepCopy preserves the deferred "resolve later" semantics while severing the
                // shared instance that caused parent and child elements to cache the same color.
                return deferred.DeepCopy();
            }

            return value;
        }

        private bool IsInheritValue(object value)
        {
            return string.Equals(value?.ToString().Trim(), "inherit", StringComparison.OrdinalIgnoreCase);
        }

        public new object this[string attributeName]
        {
            get { return GetInheritedAttribute<object>(attributeName, true); }
            set
            {
                if (ContainsKey(attributeName))
                {
                    var oldVal = base[attributeName];
                    if (TryUnboxedCheck(oldVal, value))
                    {
                        base[attributeName] = value;
                        RemoveStaleRawTextDecoration(attributeName);
                        OnAttributeChanged(attributeName, value);
                    }
                }
                else
                {
                    base[attributeName] = value;
                    OnAttributeChanged(attributeName, value);
                }
            }
        }

        private void RemoveStaleRawTextDecoration(string attributeName)
        {
            if (string.Equals(attributeName, "text-decoration", StringComparison.Ordinal))
            {
                _owner.CustomAttributes.Remove(SvgStyleAttributeNames.RawTextDecorationAttributeKey);
            }
        }

        private bool TryUnboxedCheck(object a, object b)
        {
            if (IsValueType(a))
            {
                if (a is SvgUnit)
                    return UnboxAndCheck<SvgUnit>(a, b);
                else if (a is bool)
                    return UnboxAndCheck<bool>(a, b);
                else if (a is int)
                    return UnboxAndCheck<int>(a, b);
                else if (a is float)
                    return UnboxAndCheck<float>(a, b);
                else if (a is SvgViewBox)
                    return UnboxAndCheck<SvgViewBox>(a, b);
                else
                    return true;
            }
            else
                return a != b;
        }

        private bool UnboxAndCheck<T>(object a, object b)
        {
            return !((T)a).Equals((T)b);
        }

        private bool IsValueType(object obj)
        {
            return obj != null && obj.GetType().IsValueType;
        }

        public event EventHandler<AttributeEventArgs> AttributeChanged;

        private void OnAttributeChanged(string attribute, object value)
        {
            var handler = AttributeChanged;
            if (handler != null)
                handler(_owner, new AttributeEventArgs { Attribute = attribute, Value = value });
        }
    }

    public sealed class SvgCustomAttributeCollection : Dictionary<string, string>
    {
        private readonly SvgElement _owner;

        public SvgCustomAttributeCollection(SvgElement owner)
        {
            _owner = owner;
        }

        public new string this[string attributeName]
        {
            get { return base[attributeName]; }
            set
            {
                if (ContainsKey(attributeName))
                {
                    var oldVal = base[attributeName];
                    base[attributeName] = value;
                    if (oldVal != value)
                        OnAttributeChanged(attributeName, value);
                }
                else
                {
                    base[attributeName] = value;
                    OnAttributeChanged(attributeName, value);
                }
            }
        }

        public event EventHandler<AttributeEventArgs> AttributeChanged;

        private void OnAttributeChanged(string attribute, string value)
        {
            var handler = AttributeChanged;
            if (handler != null)
                handler(_owner, new AttributeEventArgs { Attribute = attribute, Value = value });
        }
    }
}
