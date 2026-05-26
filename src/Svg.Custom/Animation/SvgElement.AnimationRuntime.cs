using System;
using System.ComponentModel;
using System.Globalization;

namespace Svg
{
    public abstract partial class SvgElement
    {
        public virtual object GetAnimationValue(string attributeName)
        {
            if (attributeName is null)
            {
                throw new ArgumentNullException(nameof(attributeName));
            }

            var normalized = NormalizeAnimationAttributeName(attributeName, out _, out _);
            var value = GetValue(normalized);
            if (value is not null)
            {
                return value;
            }

            if (TryGetAttribute(attributeName, out var rawValue))
            {
                return rawValue;
            }

            return !string.Equals(normalized, attributeName, StringComparison.Ordinal) &&
                   TryGetAttribute(normalized, out rawValue)
                ? rawValue
                : null;
        }

        public virtual bool TrySetAnimationValue(string attributeName, object value)
        {
            return TrySetAnimationValue(attributeName, OwnerDocument, CultureInfo.InvariantCulture, value);
        }

        public virtual bool TrySetAnimationValue(string attributeName, ITypeDescriptorContext context, CultureInfo culture, object value)
        {
            if (attributeName is null)
            {
                throw new ArgumentNullException(nameof(attributeName));
            }

            context ??= OwnerDocument;
            culture ??= CultureInfo.InvariantCulture;

            var normalized = NormalizeAnimationAttributeName(attributeName, out var namespaceName, out var customAttributeName);
            var stringValue = Convert.ToString(value, culture) ?? string.Empty;
            var isStyleMutation = string.Equals(normalized, "style", StringComparison.Ordinal);

            if (IsHrefAttribute(normalized, namespaceName))
            {
                SetCompatibilityHrefAttributeValue(namespaceName, stringValue);
            }

            if (isStyleMutation)
            {
                (context as SvgDocument ?? OwnerDocument)?.TrackCompatibilityStyleStateCandidate(this);
            }

            if (SetValue(normalized, context, culture, value))
            {
                InvalidateAnimationAttributeChange(normalized);
                return true;
            }

            var document = context as SvgDocument ?? OwnerDocument;
            if (document is not null &&
                SvgElementFactory.SetPropertyValue(this, namespaceName, normalized, stringValue, document, isStyle: false))
            {
                InvalidateAnimationAttributeChange(normalized);
                return true;
            }

            CustomAttributes[customAttributeName] = stringValue;
            InvalidateAnimationAttributeChange(normalized);
            return true;
        }

        public virtual bool ClearAnimationValue(string attributeName)
        {
            if (attributeName is null)
            {
                throw new ArgumentNullException(nameof(attributeName));
            }

            var normalized = NormalizeAnimationAttributeName(attributeName, out var namespaceName, out var customAttributeName);
            var isStyleMutation = string.Equals(normalized, "style", StringComparison.Ordinal);
            var removed = Attributes.Remove(normalized);
            if (!string.Equals(normalized, attributeName, StringComparison.Ordinal))
            {
                removed |= Attributes.Remove(attributeName);
            }

            removed |= CustomAttributes.Remove(customAttributeName);
            removed |= CustomAttributes.Remove(attributeName);
            if (!string.Equals(normalized, customAttributeName, StringComparison.Ordinal))
            {
                removed |= CustomAttributes.Remove(normalized);
            }

            if (namespaceName.Length != 0)
            {
                removed |= CustomAttributes.Remove(string.Concat(namespaceName, ":", normalized));
            }

            if (IsHrefAttribute(normalized, namespaceName))
            {
                ClearCompatibilityHrefAttributeValue(namespaceName);
                removed = true;
            }

            if (isStyleMutation)
            {
                OwnerDocument?.TrackCompatibilityStyleStateCandidate(this);
            }

            if (removed)
            {
                InvalidateAnimationAttributeChange(normalized);
            }

            return removed;
        }

        private string NormalizeAnimationAttributeName(string attributeName, out string namespaceName, out string customAttributeName)
        {
            namespaceName = string.Empty;
            customAttributeName = attributeName;

            if (attributeName.StartsWith("xlink:", StringComparison.Ordinal))
            {
                namespaceName = SvgNamespaces.XLinkNamespace;
                customAttributeName = attributeName;
                return attributeName.Substring("xlink:".Length);
            }

            if (attributeName.StartsWith(SvgNamespaces.XLinkNamespace + ":", StringComparison.Ordinal))
            {
                namespaceName = SvgNamespaces.XLinkNamespace;
                customAttributeName = attributeName;
                return attributeName.Substring(SvgNamespaces.XLinkNamespace.Length + 1);
            }

            if (attributeName.StartsWith("xml:", StringComparison.Ordinal))
            {
                namespaceName = SvgNamespaces.XmlNamespace;
                customAttributeName = attributeName;
                return attributeName.Substring("xml:".Length);
            }

            if (attributeName.StartsWith(SvgNamespaces.XmlNamespace + ":", StringComparison.Ordinal))
            {
                namespaceName = SvgNamespaces.XmlNamespace;
                customAttributeName = attributeName;
                return attributeName.Substring(SvgNamespaces.XmlNamespace.Length + 1);
            }

            var colonIndex = attributeName.IndexOf(':');
            if (colonIndex > 0 && colonIndex < attributeName.Length - 1)
            {
                var prefix = attributeName.Substring(0, colonIndex);
                if (TryResolveAnimationAttributeNamespace(prefix, out namespaceName))
                {
                    return attributeName.Substring(colonIndex + 1);
                }

                namespaceName = string.Empty;
            }

            return attributeName;
        }

        private bool TryResolveAnimationAttributeNamespace(string prefix, out string namespaceName)
        {
            for (SvgElement current = this; current is not null; current = current.Parent as SvgElement)
            {
                if (current.Namespaces.TryGetValue(prefix, out namespaceName))
                {
                    return true;
                }
            }

            var document = this as SvgDocument ?? OwnerDocument;
            if (document is not null && document.Namespaces.TryGetValue(prefix, out namespaceName))
            {
                return true;
            }

            namespaceName = string.Empty;
            return false;
        }

        private static bool IsHrefAttribute(string attributeName, string namespaceName)
        {
            return string.Equals(attributeName, "href", StringComparison.Ordinal) &&
                   (namespaceName.Length == 0 ||
                    string.Equals(namespaceName, SvgNamespaces.XLinkNamespace, StringComparison.Ordinal));
        }

        private void InvalidateAnimationAttributeChange(string attributeName)
        {
            if (string.Equals(attributeName, "class", StringComparison.Ordinal) ||
                string.Equals(attributeName, "style", StringComparison.Ordinal))
            {
                OwnerDocument?.ReapplyCompatibilityStylesAfterSelectorMutation();
                return;
            }

            OwnerDocument?.InvalidateComputedStyleCache();
        }
    }
}
