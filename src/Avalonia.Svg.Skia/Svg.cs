using System;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Metadata;
using Svg.Skia;

namespace Avalonia.Svg.Skia
{
    /// <summary>
    /// Svg control.
    /// </summary>
    public class Svg : Control, IAffectsRender
    {
        private readonly Uri _baseUri;
        private SKSvg? _svg;

        /// <summary>
        /// Defines the <see cref="Path"/> property.
        /// </summary>
        public static readonly StyledProperty<string?> PathProperty =
            AvaloniaProperty.Register<Svg, string?>(nameof(Path));

        /// <inheritdoc/>
        public event EventHandler? Invalidated;

        /// <summary>
        /// Gets or sets the Svg path.
        /// </summary>
        [Content]
        public string? Path
        {
            get => GetValue(PathProperty);
            set => SetValue(PathProperty, value);
        }

        static Svg()
        {
            AffectsMeasure<Svg>(PathProperty);
            AffectsArrange<Svg>(PathProperty);
            AffectsRender<Svg>(PathProperty);   
        }
  
        /// <summary>
        /// Initializes a new instance of the <see cref="Svg"/> class.
        /// </summary>
        /// <param name="baseUri">The base URL for the XAML context.</param>
        public Svg(Uri baseUri)
        {
            _baseUri = baseUri;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="Svg"/> class.
        /// </summary>
        /// <param name="serviceProvider">The XAML service provider.</param>
        public Svg(IServiceProvider serviceProvider)
        {
            _baseUri = serviceProvider.GetContextBaseUri();
        }

        protected override Size MeasureOverride(Size availableSize)
        {
            return _svg?.Picture is { } 
                ? new Size(_svg.Picture.CullRect.Width, _svg.Picture.CullRect.Height) 
                : default;
        }

        protected override Size ArrangeOverride(Size finalSize)
        {
            return finalSize;
        }

        public override void Render(DrawingContext context)
        {
            var source = _svg;
            if (source?.Picture is null)
            {
                return;
            }

            var bounds = source.Picture.CullRect;
            var scale = Matrix.CreateScale(
                Bounds.Width / bounds.Width,
                Bounds.Height / bounds.Height);
            var translate = Matrix.CreateTranslation(
                -bounds.Left + Bounds.X - bounds.Top,
                -bounds.Top + Bounds.Y - bounds.Left);
            using (context.PushClip(Bounds))
            using (context.PushPreTransform(translate * scale))
            {
                context.Custom(
                    new SvgCustomDrawOperation(
                        new Rect(0, 0, bounds.Width, bounds.Height),
                        source));
            }
        }

        /// <inheritdoc/>
        protected override void OnPropertyChanged<T>(AvaloniaPropertyChangedEventArgs<T> change)
        {
            base.OnPropertyChanged(change);
            if (change.Property == PathProperty)
            {
                _svg?.Dispose();

                var path = Path;
                if (path is not null)
                {
                    _svg = SvgSource.Load<SvgSource>(path, _baseUri);
                }

                RaiseInvalidated(EventArgs.Empty);
            }
        }

        /// <summary>
        /// Raises the <see cref="Invalidated"/> event.
        /// </summary>
        /// <param name="e">The event args.</param>
        protected void RaiseInvalidated(EventArgs e) => Invalidated?.Invoke(this, e);
    }
}
