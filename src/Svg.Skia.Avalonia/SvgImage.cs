// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using Avalonia;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using Avalonia.Metadata;
using Avalonia.Platform;
using Avalonia.Visuals.Media.Imaging;
#if USE_PICTURE
using SP = Svg.Picture;
using SPA = Svg.Picture.Avalonia;
#else
using Avalonia.Data;
using Avalonia.Rendering.SceneGraph;
using Avalonia.Skia;
using SkiaSharp;
#endif

namespace Svg.Skia.Avalonia
{
    internal static class Extensions
    {
        public static T GetService<T>(this IServiceProvider sp)
            => (T)sp?.GetService(typeof(T))!;

        public static Uri GetContextBaseUri(this IServiceProvider ctx)
            => ctx.GetService<IUriContext>().BaseUri;
    }

    /// <summary>
    /// Represents a <see cref="SvgSource"/> type converter.
    /// </summary>
    public class SvgSourceTypeConverter : TypeConverter
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
            var svg = new SvgSource();
            if (uri.IsAbsoluteUri && uri.IsFile)
            {
#if USE_PICTURE
                var document = SKSvg.Open(uri.LocalPath);
                if (document != null)
                {
                    svg.Picture = SKSvg.ToModel(document);
                }
#else
                svg.Load(uri.LocalPath);
#endif
                return svg;
            }
            else
            {
                var assets = AvaloniaLocator.Current.GetService<IAssetLoader>();
#if USE_PICTURE
                var document = SKSvg.Open(assets.Open(uri, context.GetContextBaseUri()));
                if (document != null)
                {
                    svg.Picture = SKSvg.ToModel(document);
                }
#else
                svg.Load(assets.Open(uri, context.GetContextBaseUri()));
#endif
            }
            return svg;
        }
    }

#if USE_PICTURE
    /// <summary>
    /// Represents a Svg based image.
    /// </summary>
    [TypeConverter(typeof(SvgSourceTypeConverter))]
    public class SvgSource
    {
        public SP.Picture? Picture { get; set; }
    }
#else
    /// <summary>
    /// Represents a <see cref="SKPicture"/> based image.
    /// </summary>
    [TypeConverter(typeof(SvgSourceTypeConverter))]
    public class SvgSource : SKSvg
    {
    } 
#endif

#if !USE_PICTURE
    internal class SvgCustomDrawOperation : ICustomDrawOperation
    {
        private readonly SvgSource _svg;

        public SvgCustomDrawOperation(Rect bounds, SvgSource svg)
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
            if (_svg == null || _svg.Picture == null)
            {
                return;
            }
            var canvas = (context as ISkiaDrawingContextImpl)?.SkCanvas;
            if (canvas == null)
            {
                return;
            }
            canvas.Save();
            canvas.DrawPicture(_svg.Picture);
            canvas.Restore();
        }
    }
#endif

    /// <summary>
    /// An <see cref="IImage"/> that uses a <see cref="SvgSource"/> for content.
    /// </summary>
    public class SvgImage : AvaloniaObject, IImage, IAffectsRender
    {
        /// <summary>
        /// Defines the <see cref="Source"/> property.
        /// </summary>
        public static readonly StyledProperty<SvgSource> SourceProperty =
            AvaloniaProperty.Register<SvgImage, SvgSource>(nameof(Source));

        /// <inheritdoc/>
        public event EventHandler? Invalidated;

        /// <summary>
        /// Gets or sets the <see cref="SvgSource"/> content.
        /// </summary>
        [Content]
        public SvgSource Source
        {
            get => GetValue(SourceProperty);
            set => SetValue(SourceProperty, value);
        }

        /// <inheritdoc/>
        public Size Size =>
            Source?.Picture != null ? new Size(Source.Picture.CullRect.Width, Source.Picture.CullRect.Height) : default;

        private SP.Picture? _previousPicture = null;
        private SPA.AvaloniaPicture? _avaloniaPicture = null;

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
                _previousPicture = null;
                _avaloniaPicture?.Dispose();
                _avaloniaPicture = null;
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
#if USE_PICTURE
                try
                {
                    if (_avaloniaPicture == null || source.Picture != _previousPicture)
                    {
                        _previousPicture = source.Picture;
                        _avaloniaPicture?.Dispose();
                        _avaloniaPicture = SPA.AvaloniaPicture.Record(source.Picture);
                    }

                    if (_avaloniaPicture != null)
                    {
                        _avaloniaPicture.Draw(context);
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"{ex.Message}");
                    Debug.WriteLine($"{ex.StackTrace}");
                }
#else
                context.Custom(
                    new SvgCustomDrawOperation(
                        new Rect(0, 0, bounds.Width, bounds.Height),
                        source));
#endif
            }
        }

        /// <inheritdoc/>
        protected override void OnPropertyChanged<T>(AvaloniaPropertyChangedEventArgs<T> change)
        {
            base.OnPropertyChanged(change);
            if (change.Property == SourceProperty)
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
