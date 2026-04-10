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

            return GetValue(attributeName);
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

            return SetValue(attributeName, context, culture, value);
        }

        public virtual bool ClearAnimationValue(string attributeName)
        {
            if (attributeName is null)
            {
                throw new ArgumentNullException(nameof(attributeName));
            }

            return Attributes.Remove(attributeName);
        }
    }
}
