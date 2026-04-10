using System;

namespace Svg
{
    /// <summary>
    /// Svg.Custom exposes the URI-based lookup surface that the SVG sources already implement
    /// internally via <see cref="SvgElementIdManager"/>.
    ///
    /// The original submodule-side fix added these overloads directly to <see cref="SvgDocument"/>
    /// so retained-scene code could resolve references like <c>file.svg#id</c> without falling
    /// back to string-only lookups. Keeping the overloads in a partial class preserves the same
    /// forwarding behavior while avoiding a public-API delta in the externals/SVG checkout.
    /// </summary>
    public partial class SvgDocument
    {
        /// <summary>
        /// Retrieves the <see cref="SvgElement"/> with the specified URI reference.
        /// </summary>
        /// <param name="uri">A <see cref="Uri"/> containing the fragment or external reference to resolve.</param>
        /// <returns>An <see cref="SvgElement"/> if one exists with the specified reference; otherwise null.</returns>
        public virtual SvgElement GetElementById(Uri uri)
        {
            if (uri is null)
            {
                return null;
            }

            if (uri.IsAbsoluteUri)
            {
                if (string.IsNullOrWhiteSpace(uri.Fragment))
                {
                    return null;
                }
            }
            else if (uri.OriginalString.IndexOf('#') < 0)
            {
                return null;
            }

            return IdManager.GetElementById(uri);
        }

        /// <summary>
        /// Retrieves the <see cref="SvgElement"/> with the specified URI reference.
        /// </summary>
        /// <param name="uri">A <see cref="Uri"/> containing the fragment or external reference to resolve.</param>
        /// <returns>An <see cref="SvgElement"/> if one exists with the specified reference; otherwise null.</returns>
        public virtual TSvgElement GetElementById<TSvgElement>(Uri uri) where TSvgElement : SvgElement
        {
            return GetElementById(uri) as TSvgElement;
        }
    }
}
