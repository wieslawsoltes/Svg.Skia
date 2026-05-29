// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
using System;
using ShimSkiaSharp;
using Svg;

namespace Svg.Skia;

public interface ISKSvgNavigationHandler
{
    bool Navigate(SKSvgNavigationRequest request);
}

public sealed class SKSvgNavigationRequest
{
    public SKSvgNavigationRequest(
        Uri uri,
        string href,
        string? target,
        string? sourceElementId,
        SvgAnchor sourceElement,
        SKPoint picturePoint,
        SvgMouseButton button,
        int clickCount,
        string sessionId)
        : this(
            uri,
            href,
            target,
            sourceElementId,
            sourceElement,
            picturePoint,
            button,
            clickCount,
            sessionId,
            null,
            uri,
            null,
            false,
            null)
    {
    }

    public SKSvgNavigationRequest(
        Uri uri,
        string href,
        string? target,
        string? sourceElementId,
        SvgAnchor sourceElement,
        SKPoint picturePoint,
        SvgMouseButton button,
        int clickCount,
        string sessionId,
        Uri? baseUri,
        Uri? resolvedUri,
        string? fragment,
        bool isSameDocumentReference,
        string? show)
    {
        Uri = uri ?? throw new ArgumentNullException(nameof(uri));
        Href = href ?? throw new ArgumentNullException(nameof(href));
        Target = target;
        SourceElementId = sourceElementId;
        SourceElement = sourceElement ?? throw new ArgumentNullException(nameof(sourceElement));
        PicturePoint = picturePoint;
        Button = button;
        ClickCount = clickCount;
        SessionId = sessionId ?? string.Empty;
        BaseUri = baseUri;
        ResolvedUri = resolvedUri ?? uri;
        Fragment = fragment;
        IsSameDocumentReference = isSameDocumentReference;
        Show = show;
    }

    public Uri Uri { get; }

    public Uri ResolvedUri { get; }

    public Uri? BaseUri { get; }

    public string Href { get; }

    public string? Target { get; }

    public string? Fragment { get; }

    public bool IsSameDocumentReference { get; }

    public string? Show { get; }

    public string? SourceElementId { get; }

    public SvgAnchor SourceElement { get; }

    public SKPoint PicturePoint { get; }

    public SvgMouseButton Button { get; }

    public int ClickCount { get; }

    public string SessionId { get; }
}

public sealed class SKSvgViewerTransformChangedEventArgs : EventArgs
{
    public SKSvgViewerTransformChangedEventArgs(
        double oldScale,
        SKPoint oldTranslate,
        double newScale,
        SKPoint newTranslate)
    {
        OldScale = oldScale;
        OldTranslate = oldTranslate;
        NewScale = newScale;
        NewTranslate = newTranslate;
    }

    public double OldScale { get; }

    public SKPoint OldTranslate { get; }

    public double NewScale { get; }

    public SKPoint NewTranslate { get; }
}

public partial class SKSvg : ISKSvgJavaScriptViewerHost
{
    private double _currentScale = 1d;
    private SKPoint _currentTranslate;

    public event EventHandler<SKSvgViewerTransformChangedEventArgs>? ViewerTransformChanged;

    public bool IsZoomAndPanEnabled
    {
        get
        {
            lock (Sync)
            {
                return IsZoomAndPanEnabledLocked();
            }
        }
    }

    public double CurrentScale
    {
        get
        {
            lock (Sync)
            {
                if (!IsZoomAndPanEnabledLocked())
                {
                    return 1d;
                }

                return _currentScale;
            }
        }
        set
        {
            _ = ZoomTo(value);
        }
    }

    public SKPoint CurrentTranslate
    {
        get
        {
            lock (Sync)
            {
                if (!IsZoomAndPanEnabledLocked())
                {
                    return default;
                }

                return _currentTranslate;
            }
        }
        set
        {
            _ = PanTo(value);
        }
    }

    public SKMatrix ViewerTransform
    {
        get
        {
            lock (Sync)
            {
                return CreateViewerTransformLocked();
            }
        }
    }

    float ISKSvgJavaScriptViewerHost.CurrentTranslateX
    {
        get => CurrentTranslate.X;
        set => CurrentTranslate = new SKPoint(value, CurrentTranslate.Y);
    }

    float ISKSvgJavaScriptViewerHost.CurrentTranslateY
    {
        get => CurrentTranslate.Y;
        set => CurrentTranslate = new SKPoint(CurrentTranslate.X, value);
    }

    public bool ZoomTo(double scale)
    {
        if (!IsValidScale(scale))
        {
            return false;
        }

        return TrySetViewerTransform(scale, null);
    }

    public bool ZoomBy(double scaleFactor)
    {
        if (!IsValidScale(scaleFactor))
        {
            return false;
        }

        SKSvgViewerTransformChangedEventArgs? args;
        bool result;
        lock (Sync)
        {
            result = TrySetViewerTransformLocked(_currentScale * scaleFactor, _currentTranslate, out args);
        }

        RaiseViewerTransformChanged(args);
        return result;
    }

    public bool PanTo(SKPoint translate)
    {
        if (!IsValidTranslate(translate))
        {
            return false;
        }

        return TrySetViewerTransform(null, translate);
    }

    public bool PanBy(SKPoint delta)
    {
        if (!IsValidTranslate(delta))
        {
            return false;
        }

        SKSvgViewerTransformChangedEventArgs? args;
        bool result;
        lock (Sync)
        {
            var translate = new SKPoint(_currentTranslate.X + delta.X, _currentTranslate.Y + delta.Y);
            result = TrySetViewerTransformLocked(_currentScale, translate, out args);
        }

        RaiseViewerTransformChanged(args);
        return result;
    }

    public bool SetViewerTransform(double scale, SKPoint translate)
    {
        if (!IsValidScale(scale) || !IsValidTranslate(translate))
        {
            return false;
        }

        return TrySetViewerTransform(scale, translate);
    }

    public bool ResetViewerTransform()
    {
        return TrySetViewerTransform(1d, default(SKPoint));
    }

    public SKPoint PictureToViewerPoint(SKPoint picturePoint)
    {
        return ViewerTransform.MapPoint(picturePoint);
    }

    public bool TryGetViewerPicturePoint(SKPoint viewerPoint, out SKPoint picturePoint)
    {
        return TryGetPicturePoint(viewerPoint, ViewerTransform, out picturePoint);
    }

    private bool TrySetViewerTransform(double? scale, SKPoint? translate)
    {
        SKSvgViewerTransformChangedEventArgs? args;
        bool result;
        lock (Sync)
        {
            result = TrySetViewerTransformLocked(
                scale ?? _currentScale,
                translate ?? _currentTranslate,
                out args);
        }

        RaiseViewerTransformChanged(args);
        return result;
    }

    private bool TrySetViewerTransformLocked(double scale, SKPoint translate, out SKSvgViewerTransformChangedEventArgs? args)
    {
        args = null;
        if (!IsValidScale(scale) || !IsValidTranslate(translate))
        {
            return false;
        }

        if (!IsZoomAndPanEnabledLocked() && !IsIdentityViewerTransform(scale, translate))
        {
            return false;
        }

        var oldScale = _currentScale;
        var oldTranslate = _currentTranslate;
        if (oldScale.Equals(scale) && oldTranslate.Equals(translate))
        {
            return true;
        }

        _currentScale = scale;
        _currentTranslate = translate;
        args = new SKSvgViewerTransformChangedEventArgs(oldScale, oldTranslate, scale, translate);
        return true;
    }

    private SKMatrix CreateViewerTransformLocked()
    {
        if (!IsZoomAndPanEnabledLocked() || IsIdentityViewerTransform(_currentScale, _currentTranslate))
        {
            return SKMatrix.Identity;
        }

        return SKMatrix.CreateTranslation(_currentTranslate.X, _currentTranslate.Y)
            .PreConcat(SKMatrix.CreateScale((float)_currentScale, (float)_currentScale));
    }

    private void ApplyViewerTransform(SkiaSharp.SKCanvas canvas)
    {
        var transform = ViewerTransform;
        if (transform.IsIdentity)
        {
            return;
        }

        var skTransform = SkiaModel.ToSKMatrix(transform);
        canvas.Concat(in skTransform);
    }

    private bool IsZoomAndPanEnabledLocked()
    {
        if (SourceDocument is null ||
            !SourceDocument.TryGetAttribute("zoomAndPan", out var zoomAndPan))
        {
            return true;
        }

        return !string.Equals(zoomAndPan.Trim(), "disable", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsIdentityViewerTransform(double scale, SKPoint translate)
    {
        return scale.Equals(1d) && translate.Equals(default(SKPoint));
    }

    private static bool IsValidScale(double scale)
    {
        return !double.IsNaN(scale) && !double.IsInfinity(scale) && scale > 0d;
    }

    private static bool IsValidTranslate(SKPoint translate)
    {
        return !float.IsNaN(translate.X) &&
               !float.IsInfinity(translate.X) &&
               !float.IsNaN(translate.Y) &&
               !float.IsInfinity(translate.Y);
    }

    private void RaiseViewerTransformChanged(SKSvgViewerTransformChangedEventArgs? args)
    {
        if (args is not null)
        {
            ViewerTransformChanged?.Invoke(this, args);
        }
    }

    internal bool ActivateHyperlink(SvgElement? element, SvgPointerInput input)
    {
        if (FindNearestAnchor(element) is not { } anchor ||
            !anchor.TryGetEffectiveHrefString(out var href))
        {
            return false;
        }

        var trimmedHref = href.Trim();
        if (trimmedHref.Length == 0 ||
            !Uri.TryCreate(trimmedHref, UriKind.RelativeOrAbsolute, out var uri))
        {
            return false;
        }

        var baseUri = SourceDocument?.BaseUri ?? _originalBaseUri;
        var resolvedUri = ResolveNavigationUri(uri, trimmedHref, baseUri);
        var normalizedTarget = NormalizeTarget(anchor);
        var normalizedShow = string.IsNullOrWhiteSpace(anchor.Show) ? null : anchor.Show.Trim();
        var fragment = TryGetNavigationFragment(trimmedHref, resolvedUri);
        var sameDocumentReference = fragment is not null &&
            IsSameDocumentReference(trimmedHref, resolvedUri, baseUri);

        if (sameDocumentReference &&
            AllowsCurrentViewerDefaultAction(normalizedTarget, normalizedShow) &&
            TryActivateAnimationFragment(fragment))
        {
            return true;
        }

        if (Settings.NavigationHandler is not { } navigationHandler)
        {
            return false;
        }

        var request = new SKSvgNavigationRequest(
            uri,
            trimmedHref,
            normalizedTarget,
            string.IsNullOrWhiteSpace(anchor.ID) ? null : anchor.ID,
            anchor,
            input.PicturePoint,
            input.Button,
            input.ClickCount,
            input.SessionId,
            baseUri,
            resolvedUri,
            fragment,
            sameDocumentReference,
            normalizedShow);

        return navigationHandler.Navigate(request);
    }

    private bool TryActivateAnimationFragment(string? fragment)
    {
        if (string.IsNullOrWhiteSpace(fragment) || SourceDocument is null)
        {
            return false;
        }

        return SourceDocument.GetElementById(fragment) is SvgAnimationElement animation &&
               BeginAnimationElement(animation, TimeSpan.Zero);
    }

    private static Uri? ResolveNavigationUri(Uri uri, string href, Uri? baseUri)
    {
        if (baseUri is { } && Uri.TryCreate(baseUri, href, out var resolvedUri))
        {
            return resolvedUri;
        }

        return uri;
    }

    private static string? NormalizeTarget(SvgAnchor anchor)
    {
        if (!string.IsNullOrWhiteSpace(anchor.Target))
        {
            return anchor.Target.Trim();
        }

        if (string.Equals(anchor.Show?.Trim(), "new", StringComparison.OrdinalIgnoreCase))
        {
            return "_blank";
        }

        if (string.Equals(anchor.Show?.Trim(), "replace", StringComparison.OrdinalIgnoreCase))
        {
            return "_self";
        }

        return null;
    }

    private static bool AllowsCurrentViewerDefaultAction(string? target, string? show)
    {
        if (string.Equals(show, "new", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return string.IsNullOrWhiteSpace(target) ||
               string.Equals(target, "_self", StringComparison.OrdinalIgnoreCase);
    }

    private static string? TryGetNavigationFragment(string href, Uri? resolvedUri)
    {
        if (href.StartsWith("#", StringComparison.Ordinal) && href.Length > 1)
        {
            return Uri.UnescapeDataString(href.Substring(1));
        }

        var fragment = resolvedUri?.Fragment;
        if (!string.IsNullOrEmpty(fragment))
        {
            return Uri.UnescapeDataString(GetFragmentWithoutNumberSign(fragment, href));
        }

        return null;
    }

    private static bool IsSameDocumentReference(
        string href,
        Uri? resolvedUri,
        Uri? baseUri)
    {
        if (href.StartsWith("#", StringComparison.Ordinal))
        {
            return true;
        }

        if (resolvedUri is null || string.IsNullOrEmpty(resolvedUri.Fragment))
        {
            return false;
        }

        if (baseUri is null)
        {
            return false;
        }

        return IsSameDocumentUri(resolvedUri, baseUri);
    }

    private static bool IsSameDocumentUri(Uri resolvedUri, Uri baseUri)
    {
        if (!resolvedUri.IsAbsoluteUri || !baseUri.IsAbsoluteUri)
        {
            return false;
        }

        var resolvedBuilder = new UriBuilder(resolvedUri) { Fragment = string.Empty };
        var baseBuilder = new UriBuilder(baseUri) { Fragment = string.Empty };
        return Uri.Compare(
                resolvedBuilder.Uri,
                baseBuilder.Uri,
                UriComponents.AbsoluteUri,
                UriFormat.SafeUnescaped,
                StringComparison.OrdinalIgnoreCase) == 0;
    }

    private static string GetFragmentWithoutNumberSign(string? resolvedFragment, string href)
    {
        if (!string.IsNullOrEmpty(resolvedFragment))
        {
            return resolvedFragment![0] == '#'
                ? resolvedFragment.Substring(1)
                : resolvedFragment;
        }

        return href.StartsWith("#", StringComparison.Ordinal)
            ? href.Substring(1)
            : string.Empty;
    }

    private static SvgAnchor? FindNearestAnchor(SvgElement? element)
    {
        for (var current = element; current is not null; current = current.Parent)
        {
            if (current is SvgAnchor anchor)
            {
                return anchor;
            }
        }

        return null;
    }
}
