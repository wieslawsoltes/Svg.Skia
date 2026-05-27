using System;

namespace Svg
{
    public abstract partial class SvgElement
    {
        internal TResult WithTemporaryParent<TResult>(SvgElement temporaryParent, Func<TResult> factory)
        {
            if (temporaryParent is null)
            {
                throw new ArgumentNullException(nameof(temporaryParent));
            }

            if (factory is null)
            {
                throw new ArgumentNullException(nameof(factory));
            }

            var originalParent = _parent;
            var originalDocument = OwnerDocument;
            var temporaryParentDocument = temporaryParent.OwnerDocument;
            using var originalComputedStyleScope = originalDocument?.BeginComputedStyleTemporaryParentScope();
            using var temporaryParentComputedStyleScope = ReferenceEquals(temporaryParentDocument, originalDocument)
                ? null
                : temporaryParentDocument?.BeginComputedStyleTemporaryParentScope();
            try
            {
                _parent = temporaryParent;
                return factory();
            }
            finally
            {
                _parent = originalParent;
            }
        }

        internal TResult WithUseInstanceStyleScope<TResult>(SvgUse useElement, Func<TResult> factory)
        {
            using var styleScope = (OwnerDocument ?? useElement.OwnerDocument)?.BeginUseInstanceStyleScope(this, useElement);
            return WithTemporaryParent(useElement, factory);
        }
    }
}
