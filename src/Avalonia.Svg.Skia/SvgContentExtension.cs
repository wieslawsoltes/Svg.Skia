using System;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace Avalonia.Svg.Skia
{
    /// <summary>
    /// Svg content markup extension.
    /// </summary>
    public class SvgContentExtension : MarkupExtension
    {
        /// <summary>
        /// Initialises a new instance of an <see cref="SvgContentExtension"/>.
        /// </summary>
        /// <param name="path">The resource or file path.</param>
        public SvgContentExtension(string path) => Path = path;

        /// <summary>
        /// Gets or sets resource or file path.
        /// </summary>
        public string Path { get; }

        /// <inheritdoc/>
        public override object ProvideValue(IServiceProvider serviceProvider)
        {
            var path = Path;
            var context = (IUriContext)serviceProvider.GetService(typeof(IUriContext))!;
            var baseUri = context.BaseUri;
            var source = SvgSource.Load(path, baseUri);
            return new Image {Source = new SvgImage {Source = source}};
        }
    }
}
