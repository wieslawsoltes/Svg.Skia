// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using Avalonia.Markup.Xaml;
using Avalonia.Metadata;
using Avalonia.Platform;
using ShimSkiaSharp;
using Svg;
using Svg.Model;
using Svg.Model.Services;
using Svg.Skia;
using SM = Svg.Model;

namespace Avalonia.Svg;

/// <summary>
/// Represents a Svg based image.
/// </summary>
[TypeConverter(typeof(SvgSourceTypeConverter))]
public class SvgSource : MarkupExtension, ISupportInitialize
{
    private static readonly SM.ISvgAssetLoader s_assetLoader = new AvaloniaSvgAssetLoader();
    private Uri? _baseUri;
    private string? _path;
    private string? _css;
    private SvgParameters? _parameters;
    private SKPicture? _picture;
    private bool _isInitializing;
    private bool _isDirty;

    /// <summary>
    /// Initializes a new instance of the <see cref="SvgSource"/> class.
    /// </summary>
    public SvgSource()
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="SvgSource"/> class.
    /// </summary>
    /// <param name="baseUri">The base URL used to resolve relative SVG paths.</param>
    public SvgSource(Uri? baseUri)
    {
        _baseUri = baseUri;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="SvgSource"/> class.
    /// </summary>
    /// <param name="serviceProvider">The XAML service provider.</param>
    public SvgSource(IServiceProvider serviceProvider)
        : this(serviceProvider.GetContextBaseUri())
    {
    }

    /// <summary>
    /// Raised when the loaded picture changes.
    /// </summary>
    public event EventHandler? Invalidated;

    /// <summary>
    /// Gets or sets the SVG resource or file path.
    /// </summary>
    [Content]
    public string? Path
    {
        get => _path;
        set
        {
            if (string.Equals(_path, value, StringComparison.Ordinal))
            {
                return;
            }

            _path = value;
            QueueReload();
        }
    }

    /// <summary>
    /// Gets or sets the CSS applied when loading <see cref="Path"/>.
    /// </summary>
    public string? Css
    {
        get => _css;
        set
        {
            if (string.Equals(_css, value, StringComparison.Ordinal))
            {
                return;
            }

            _css = value;
            QueueReload();
        }
    }

    /// <summary>
    /// Gets or sets the loaded SVG picture.
    /// </summary>
    public SKPicture? Picture
    {
        get => _picture;
        set
        {
            if (ReferenceEquals(_picture, value))
            {
                return;
            }

            _picture = value;
            Invalidated?.Invoke(this, EventArgs.Empty);
        }
    }

    /// <inheritdoc/>
    public override object ProvideValue(IServiceProvider serviceProvider)
    {
        _baseUri ??= serviceProvider.GetContextBaseUri();
        Reload();
        return this;
    }

    /// <inheritdoc/>
    public void BeginInit()
    {
        _isInitializing = true;
    }

    /// <inheritdoc/>
    public void EndInit()
    {
        _isInitializing = false;
        Reload();
    }

    /// <summary>
    /// Loads svg picture from file or resource.
    /// </summary>
    /// <param name="path">The path to file or resource.</param>
    /// <param name="baseUri">The base uri.</param>
    /// <param name="parameters">The svg parameters.</param>
    /// <returns>The svg picture.</returns>
    public static SKPicture? LoadPicture(string path, Uri? baseUri, SvgParameters? parameters = null)
    {
        if (File.Exists(path))
        {
            var document = SvgService.Open(path, parameters);
            return CreateModel(document);
        }

        if (Uri.TryCreate(path, UriKind.Absolute, out var uriHttp) && (uriHttp.Scheme == "http" || uriHttp.Scheme == "https"))
        {
            try
            {
                var response = new HttpClient().GetAsync(uriHttp).Result;
                if (response.IsSuccessStatusCode)
                {
                    var stream = response.Content.ReadAsStreamAsync().Result;
                    var document = SvgService.Open(stream, parameters);
                    return CreateModel(document);
                }
            }
            catch (HttpRequestException e)
            {
                Debug.WriteLine("Failed to connect to " + uriHttp);
                Debug.WriteLine(e.ToString());
            }

            return default;
        }

        var uri = path.StartsWith("/") ? new Uri(path, UriKind.Relative) : new Uri(path, UriKind.RelativeOrAbsolute);
        if (uri.IsAbsoluteUri && uri.IsFile)
        {
            var document = SvgService.Open(uri.LocalPath, parameters);
            return CreateModel(document);
        }
        else
        {
            var stream = AssetLoader.Open(uri, baseUri);
            if (stream is null)
            {
                return default;
            }
            var document = SvgService.Open(stream, parameters);
            return CreateModel(document);
        }
    }

    /// <summary>
    /// Loads svg source from file or resource.
    /// </summary>
    /// <param name="path">The path to file or resource.</param>
    /// <param name="baseUri">The base uri.</param>
    /// <param name="parameters">The svg parameters.</param>
    /// <returns>The svg source.</returns>
    public static SvgSource Load(string path, Uri? baseUri, SvgParameters? parameters = null)
    {
        return new SvgSource(baseUri)
        {
            _path = path,
            _css = parameters?.Css,
            _parameters = parameters,
            _picture = LoadPicture(path, baseUri, parameters)
        };
    }

    /// <summary>
    /// Loads svg picture from stream.
    /// </summary>
    /// <param name="stream">The svg stream.</param>
    /// <param name="parameters">The svg parameters.</param>
    /// <returns>The svg picture.</returns>
    public static SKPicture? LoadPicture(Stream stream, SvgParameters? parameters = null)
    {
        var document = SvgService.Open(stream, parameters);
        return CreateModel(document);
    }

    /// <summary>
    /// Loads svg source from stream.
    /// </summary>
    /// <param name="stream">The svg stream.</param>
    /// <param name="parameters">The svg parameters.</param>
    /// <returns>The svg source.</returns>
    public static SvgSource Load(Stream stream, SvgParameters? parameters = null)
    {
        return new() { Picture = LoadPicture(stream, parameters) };
    }

    /// <summary>
    /// Loads svg picture from svg source.
    /// </summary>
    /// <param name="source">The svg source.</param>
    /// <returns>The svg picture.</returns>
    public static SKPicture? LoadPictureFromSvg(string source, SvgParameters? parameters = null)
    {
        var document = SvgService.FromSvg(source, parameters);
        return CreateModel(document);
    }

    /// <summary>
    /// Loads svg source from svg source.
    /// </summary>
    /// <param name="source">The svg source.</param>
    /// <returns>The svg source.</returns>
    public static SvgSource LoadFromSvg(string source)
    {
        return new() { Picture = LoadPictureFromSvg(source) };
    }

    private static SKPicture? CreateModel(SvgDocument? document)
    {
        return document is { } ? SvgSceneRuntime.CreateModel(document, s_assetLoader) : default;
    }

    /// <summary>
    /// Rebuilds the <see cref="SvgSource"/> from its underlying model, refreshing its associated picture.
    /// </summary>
    public void RebuildFromModel()
    {
        if (Picture is not { } picture)
        {
            return;
        }

        Picture = picture.DeepClone();
    }

    /// <summary>
    /// Creates a deep clone of this <see cref="SvgSource"/>.
    /// </summary>
    /// <returns>A new <see cref="SvgSource"/> instance.</returns>
    public SvgSource Clone()
    {
        return new SvgSource(_baseUri)
        {
            _path = _path,
            _css = _css,
            _parameters = _parameters,
            _picture = Picture?.DeepClone()
        };
    }

    private void Reload()
    {
        if (!_isDirty)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(_path))
        {
            Picture = null;
            _isDirty = false;
            return;
        }

        if (_baseUri is null && !File.Exists(_path))
        {
            var uri = _path.StartsWith("/")
                ? new Uri(_path, UriKind.Relative)
                : new Uri(_path, UriKind.RelativeOrAbsolute);
            if (!uri.IsAbsoluteUri)
            {
                Picture = null;
                return;
            }
        }

        var parameters = _parameters is { } existingParameters
            ? existingParameters with { Css = _css }
            : new SvgParameters(null, _css);
        Picture = LoadPicture(_path, _baseUri, parameters);
        _isDirty = false;
    }

    private void QueueReload()
    {
        _isDirty = true;
        if (!_isInitializing)
        {
            Reload();
        }
    }
}
