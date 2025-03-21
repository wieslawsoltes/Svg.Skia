using System;
using System.Collections.Generic;
using System.Diagnostics;
using ShimSkiaSharp;

namespace Svg.Model.Drawables.Elements;

public sealed class ImageDrawable : DrawableBase
{
    public SKImage? Image { get; set; }
    public FragmentDrawable? FragmentDrawable { get; set; }
    public SKRect SrcRect { get; set; }
    public SKRect DestRect { get; set; }
    public SKMatrix FragmentTransform { get; set; }

    private ImageDrawable(IAssetLoader assetLoader, HashSet<Uri>? references)
        : base(assetLoader, references)
    {
    }

    public static ImageDrawable Create(SvgImage svgImage, SKRect skViewport, DrawableBase? parent, IAssetLoader assetLoader, HashSet<Uri>? references, DrawAttributes ignoreAttributes = DrawAttributes.None)
    {
        var drawable = new ImageDrawable(assetLoader, references)
        {
            Element = svgImage,
            Parent = parent,
            IgnoreAttributes = ignoreAttributes
        };

        drawable.IsDrawable = drawable.CanDraw(svgImage, drawable.IgnoreAttributes) && drawable.HasFeatures(svgImage, drawable.IgnoreAttributes);

        if (!drawable.IsDrawable)
        {
            return drawable;
        }

        var width = svgImage.Width.ToDeviceValue(UnitRenderingType.Horizontal, svgImage, skViewport);
        var height = svgImage.Height.ToDeviceValue(UnitRenderingType.Vertical, svgImage, skViewport);
        var x = svgImage.Location.X.ToDeviceValue(UnitRenderingType.Horizontal, svgImage, skViewport);
        var y = svgImage.Location.Y.ToDeviceValue(UnitRenderingType.Vertical, svgImage, skViewport);
        var location = new SKPoint(x, y);

        if (width <= 0f || height <= 0f || svgImage.Href is null)
        {
            drawable.IsDrawable = false;
            return drawable;
        }

        var uri = SvgExtensions.GetImageUri(svgImage.Href, svgImage.OwnerDocument);
        if (references is { } && references.Contains(uri))
        {
            drawable.IsDrawable = false;
            return drawable;
        }

        var image = SvgExtensions.GetImage(svgImage.Href, svgImage.OwnerDocument, assetLoader);
        var skImage = image as SKImage;
        var svgFragment = image as SvgFragment;
        if (skImage is null && svgFragment is null)
        {
            drawable.IsDrawable = false;
            return drawable;
        }

        drawable.SrcRect = default;

        if (skImage is { })
        {
            drawable.SrcRect = SKRect.Create(0f, 0f, skImage.Width, skImage.Height);
        }

        if (svgFragment is { })
        {
            var skSize = SvgExtensions.GetDimensions(svgFragment);
            drawable.SrcRect = SKRect.Create(0f, 0f, skSize.Width, skSize.Height);
        }

        var destClip = SKRect.Create(location.X, location.Y, width, height);
        drawable.DestRect = SvgExtensions.CalculateRect(svgImage.AspectRatio, drawable.SrcRect, destClip);
        drawable.Clip = destClip;

        var skClipRect = SvgExtensions.GetClipRect(svgImage.Clip, destClip);
        if (skClipRect is { })
        {
            drawable.Clip = skClipRect;
        }

        if (skImage is { })
        {
            drawable.Image = skImage;
        }

        if (svgFragment is { })
        {
            drawable.FragmentDrawable = FragmentDrawable.Create(svgFragment, skViewport, drawable, assetLoader, references, ignoreAttributes);
        }

        drawable.Initialize();

        return drawable;
    }

    private void Initialize()
    {
        if (Element is not SvgImage svgImage)
        {
            return;
        }
        
        IsAntialias = SvgExtensions.IsAntialias(svgImage);

        GeometryBounds = default(SKRect);

        if (Image is { })
        {
            GeometryBounds = DestRect;
        }

        if (FragmentDrawable is { })
        {
            GeometryBounds = DestRect;
        }

        Transform = SvgExtensions.ToMatrix(svgImage.Transforms);
        FragmentTransform = SKMatrix.CreateIdentity();

        if (FragmentDrawable is { })
        {
            var dx = DestRect.Left;
            var dy = DestRect.Top;
            var sx = DestRect.Width / SrcRect.Width;
            var sy = DestRect.Height / SrcRect.Height;
            var skTranslationMatrix = SKMatrix.CreateTranslation(dx, dy);
            var skScaleMatrix = SKMatrix.CreateScale(sx, sy);
            FragmentTransform = FragmentTransform.PreConcat(skTranslationMatrix);
            FragmentTransform = FragmentTransform.PreConcat(skScaleMatrix);
            // TODO: FragmentTransform
        }

        Fill = null;
        Stroke = null;
    }

    public override void OnDraw(SKCanvas canvas, DrawAttributes ignoreAttributes, DrawableBase? until)
    {
        if (until is { } && this == until)
        {
            return;
        }

        if (Image is { })
        {
            var skImagePaint = new SKPaint
            {
                IsAntialias = true,
                FilterQuality = SKFilterQuality.High
            };
            canvas.DrawImage(Image, SrcRect, DestRect, skImagePaint);
        }

        if (FragmentDrawable is { })
        {
            canvas.Save();

            canvas.SetMatrix(FragmentTransform);

            FragmentDrawable.Draw(canvas, ignoreAttributes, until, true);

            canvas.Restore();
        }
    }

    public override void PostProcess(SKRect? viewport, SKMatrix totalMatrix)
    {
        base.PostProcess(viewport, totalMatrix);

        FragmentDrawable?.PostProcess(viewport, TotalTransform);
    }
}
