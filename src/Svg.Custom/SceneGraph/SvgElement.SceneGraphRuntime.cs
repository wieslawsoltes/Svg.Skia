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
    }
}
