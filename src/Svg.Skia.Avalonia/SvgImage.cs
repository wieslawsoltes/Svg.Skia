// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
using System;
using System.ComponentModel;
using System.Globalization;
using Avalonia;
using Avalonia.Data;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using Avalonia.Metadata;
using Avalonia.Platform;
using Avalonia.Rendering.SceneGraph;
using Avalonia.Skia;
using Avalonia.Visuals.Media.Imaging;
using SkiaSharp;

namespace Svg.Skia.Avalonia
{
    internal static class Extensions
    {
        public static T GetService<T>(this IServiceProvider sp) => (T)sp?.GetService(typeof(T))!;

        public static Uri GetContextBaseUri(this IServiceProvider ctx) => ctx.GetService<IUriContext>().BaseUri;
    }

    internal class SvgCustomDrawOperation : ICustomDrawOperation
    {
        private readonly ISvg _svg;

        public SvgCustomDrawOperation(Rect bounds, ISvg svg)
        {
            _svg = svg;
            Bounds = bounds;
        }

        public void Dispose()
        {
        }

        public Rect Bounds { get; }

        public bool HitTest(Point p) => false;

        public bool Equals(ICustomDrawOperation other) => false;

        public void Render(IDrawingContextImpl context)
        {
            var canvas = (context as ISkiaDrawingContextImpl)?.SkCanvas;
            if (canvas != null)
            {
                canvas.Save();
                canvas.DrawPicture(_svg.Picture);
                canvas.Restore();
            }
        }
    }

    /// <summary>
    /// Represents a <see cref="SKPicture"/> image.
    /// </summary>
    [TypeConverter(typeof(SvgTypeConverter))]
    public interface ISvg : IDisposable
    {
        /// <summary>
        /// Gets or sets picture.
        /// </summary>
        SKPicture? Picture { get; set; }
    }

    public class SvgSkia : SKSvg, ISvg
    {
    }

    /// <inheritdoc/>
    public class SvgTypeConverter : TypeConverter
    {
        /// <inheritdoc/>
        public override bool CanConvertFrom(ITypeDescriptorContext context, Type sourceType)
        {
            return sourceType == typeof(string);
        }

        /// <inheritdoc/>
        public override object ConvertFrom(ITypeDescriptorContext context, CultureInfo culture, object value)
        {
            var s = (string)value;
            var uri = s.StartsWith("/")
                ? new Uri(s, UriKind.Relative)
                : new Uri(s, UriKind.RelativeOrAbsolute);

            var svg = new SvgSkia();
            if (uri.IsAbsoluteUri && uri.IsFile)
            {
                svg.Load(uri.LocalPath);
                return svg;
            }
            else
            {
                var assets = AvaloniaLocator.Current.GetService<IAssetLoader>();
                svg.Load(assets.Open(uri, context.GetContextBaseUri()));
            }
            return svg;
        }
    }

    /// <summary>
    /// An <see cref="IImage"/> that uses a <see cref="ISvg"/> for content.
    /// </summary>
    public class SvgImage : AvaloniaObject, IImage, IAffectsRender
    {
        /// <summary>
        /// Defines the <see cref="Source"/> property.
        /// </summary>
        public static readonly StyledProperty<ISvg> SourceProperty =
            AvaloniaProperty.Register<SvgImage, ISvg>(nameof(Source));

        /// <inheritdoc/>
        public event EventHandler? Invalidated;

        /// <summary>
        /// Gets or sets the <see cref="ISvg"/> content.
        /// </summary>
        [Content]
        public ISvg Source
        {
            get => GetValue(SourceProperty);
            set => SetValue(SourceProperty, value);
        }

        /// <inheritdoc/>
        public Size Size =>
            Source?.Picture != null ? new Size(Source.Picture.CullRect.Width, Source.Picture.CullRect.Height) : default;

        /// <inheritdoc/>
        void IImage.Draw(
            DrawingContext context,
            Rect sourceRect,
            Rect destRect,
            BitmapInterpolationMode bitmapInterpolationMode)
        {
            var source = Source;
            if (source == null || source.Picture == null)
            {
                return;
            }

            var bounds = source.Picture.CullRect;
            var scale = Matrix.CreateScale(
                destRect.Width / sourceRect.Width,
                destRect.Height / sourceRect.Height);
            var translate = Matrix.CreateTranslation(
                -sourceRect.X + destRect.X - bounds.Top,
                -sourceRect.Y + destRect.Y - bounds.Left);

            using (context.PushClip(destRect))
            using (context.PushPreTransform(translate * scale))
            {
                context.Custom(
                    new SvgCustomDrawOperation(
                        new Rect(0, 0, bounds.Width, bounds.Height),
                        source));
            }
        }

        /// <inheritdoc/>
        protected override void OnPropertyChanged<T>(AvaloniaProperty<T> property, Optional<T> oldValue, BindingValue<T> newValue, BindingPriority priority)
        {
            base.OnPropertyChanged(property, oldValue, newValue, priority);

            if (property == SourceProperty)
            {
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
