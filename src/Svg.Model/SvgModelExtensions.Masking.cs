using System;
using System.Collections.Generic;
using System.Globalization;
using Svg.Model.Drawables.Elements;
using Svg.Model.Primitives;

namespace Svg.Model
{
    public static partial class SvgModelExtensions
    {
        internal static bool CanDraw(SvgVisualElement svgVisualElement, Attributes ignoreAttributes)
        {
            var visible = svgVisualElement.Visible;
            var ignoreDisplay = ignoreAttributes.HasFlag(Attributes.Display);
            var display = ignoreDisplay || !string.Equals(svgVisualElement.Display, "none", StringComparison.OrdinalIgnoreCase);
            return visible && display;
        }

        private static SvgFillRule ToFillRule(SvgVisualElement svgVisualElement, SvgClipRule? svgClipPathClipRule)
        {
            var svgClipRule = svgClipPathClipRule ?? svgVisualElement.ClipRule;
            return svgClipRule == SvgClipRule.EvenOdd ? SvgFillRule.EvenOdd : SvgFillRule.NonZero;
        }

        private static SvgClipRule? GetSvgClipRule(SvgClipPath svgClipPath)
        {
            TryGetAttribute(svgClipPath, "clip-rule", out var clipRuleString);

            return clipRuleString switch
            {
                "nonzero" => SvgClipRule.NonZero,
                "evenodd" => SvgClipRule.EvenOdd,
                // TODO: SvgClipRule.Inherit
                "inherit" => SvgClipRule.Inherit,
                _ => null
            };
        }

        internal static void GetClipPath(SvgVisualElement svgVisualElement, Rect skBounds, HashSet<Uri> uris, ClipPath? clipPath, SvgClipRule? svgClipPathClipRule)
        {
            if (clipPath is null)
            {
                return;
            }

            if (!CanDraw(svgVisualElement, Attributes.None))
            {
                return;
            }

            switch (svgVisualElement)
            {
                case SvgPath svgPath:
                    {
                        var fillRule = ToFillRule(svgPath, svgClipPathClipRule);
                        var skPath = svgPath.PathData?.ToPath(fillRule);
                        if (skPath is null)
                        {
                            break;
                        }

                        var pathClip = new PathClip
                        {
                            Path = skPath,
                            Transform = ToMatrix(svgPath.Transforms),
                            Clip = new ClipPath
                            {
                                Clip = new ClipPath()
                            }
                        };
                        clipPath.Clips?.Add(pathClip);

                        GetSvgVisualElementClipPath(svgPath, skPath.Bounds, uris, pathClip.Clip);
                    }
                    break;

                case SvgRectangle svgRectangle:
                    {
                        var fillRule = ToFillRule(svgRectangle, svgClipPathClipRule);
                        var skPath = svgRectangle.ToPath(fillRule, skBounds);
                        if (skPath is null)
                        {
                            break;
                        }

                        var pathClip = new PathClip
                        {
                            Path = skPath,
                            Transform = ToMatrix(svgRectangle.Transforms),
                            Clip = new ClipPath
                            {
                                Clip = new ClipPath()
                            }
                        };
                        clipPath.Clips?.Add(pathClip);

                        GetSvgVisualElementClipPath(svgRectangle, skPath.Bounds, uris, pathClip.Clip);
                    }
                    break;

                case SvgCircle svgCircle:
                    {
                        var fillRule = ToFillRule(svgCircle, svgClipPathClipRule);
                        var skPath = svgCircle.ToPath(fillRule, skBounds);
                        if (skPath is null)
                        {
                            break;
                        }

                        var pathClip = new PathClip
                        {
                            Path = skPath,
                            Transform = ToMatrix(svgCircle.Transforms),
                            Clip = new ClipPath
                            {
                                Clip = new ClipPath()
                            }
                        };
                        clipPath.Clips?.Add(pathClip);

                        GetSvgVisualElementClipPath(svgCircle, skPath.Bounds, uris, pathClip.Clip);
                    }
                    break;

                case SvgEllipse svgEllipse:
                    {
                        var fillRule = ToFillRule(svgEllipse, svgClipPathClipRule);
                        var skPath = svgEllipse.ToPath(fillRule, skBounds);
                        if (skPath is null)
                        {
                            break;
                        }

                        var pathClip = new PathClip
                        {
                            Path = skPath,
                            Transform = ToMatrix(svgEllipse.Transforms),
                            Clip = new ClipPath
                            {
                                Clip = new ClipPath()
                            }
                        };
                        clipPath.Clips?.Add(pathClip);

                        GetSvgVisualElementClipPath(svgEllipse, skPath.Bounds, uris, pathClip.Clip);
                    }
                    break;

                case SvgLine svgLine:
                    {
                        var fillRule = ToFillRule(svgLine, svgClipPathClipRule);
                        var skPath = svgLine.ToPath(fillRule, skBounds);
                        if (skPath is null)
                        {
                            break;
                        }

                        var pathClip = new PathClip
                        {
                            Path = skPath,
                            Transform = ToMatrix(svgLine.Transforms),
                            Clip = new ClipPath
                            {
                                Clip = new ClipPath()
                            }
                        };
                        clipPath.Clips?.Add(pathClip);

                        GetSvgVisualElementClipPath(svgLine, skPath.Bounds, uris, pathClip.Clip);
                    }
                    break;

                case SvgPolyline svgPolyline:
                    {
                        var fillRule = ToFillRule(svgPolyline, svgClipPathClipRule);
                        var skPath = svgPolyline.Points?.ToPath(fillRule, false, skBounds);
                        if (skPath is null)
                        {
                            break;
                        }

                        var pathClip = new PathClip
                        {
                            Path = skPath,
                            Transform = ToMatrix(svgPolyline.Transforms),
                            Clip = new ClipPath
                            {
                                Clip = new ClipPath()
                            }
                        };
                        clipPath.Clips?.Add(pathClip);

                        GetSvgVisualElementClipPath(svgPolyline, skPath.Bounds, uris, pathClip.Clip);
                    }
                    break;

                case SvgPolygon svgPolygon:
                    {
                        var fillRule = ToFillRule(svgPolygon, svgClipPathClipRule);
                        var skPath = svgPolygon.Points?.ToPath(fillRule, true, skBounds);
                        if (skPath is null)
                        {
                            break;
                        }

                        var pathClip = new PathClip
                        {
                            Path = skPath,
                            Transform = ToMatrix(svgPolygon.Transforms),
                            Clip = new ClipPath
                            {
                                Clip = new ClipPath()
                            }
                        };
                        clipPath.Clips?.Add(pathClip);

                        GetSvgVisualElementClipPath(svgPolygon, skPath.Bounds, uris, pathClip.Clip);
                    }
                    break;

                case SvgUse svgUse:
                    {
                        if (HasRecursiveReference(svgUse, (e) => e.ReferencedElement, new HashSet<Uri>()))
                        {
                            break;
                        }

                        var svgReferencedVisualElement = GetReference<SvgVisualElement>(svgUse, svgUse.ReferencedElement);
                        if (svgReferencedVisualElement is null || svgReferencedVisualElement is SvgSymbol)
                        {
                            break;
                        }

                        if (!CanDraw(svgReferencedVisualElement, Attributes.None))
                        {
                            break;
                        }

                        // TODO: GetClipPath
                        GetClipPath(svgReferencedVisualElement, skBounds, uris, clipPath, svgClipPathClipRule);

                        if (clipPath.Clips is { } && clipPath.Clips.Count > 0)
                        {
                            // TODO: clipPath.Clips
                            var lastClip = clipPath.Clips[clipPath.Clips.Count - 1];
                            if (lastClip.Clip is { })
                            {
                                GetSvgVisualElementClipPath(svgUse, skBounds, uris, lastClip.Clip);
                            }
                        }
                    }
                    break;

                case SvgText svgText:
                    {
                        // TODO: Get path from SvgText.
                    }
                    break;
            }
        }

        private static void GetClipPath(SvgElementCollection svgElementCollection, Rect skBounds, HashSet<Uri> uris, ClipPath? clipPath, SvgClipRule? svgClipPathClipRule)
        {
            foreach (var svgElement in svgElementCollection)
            {
                if (svgElement is SvgVisualElement visualChild)
                {
                    if (!CanDraw(visualChild, Attributes.None))
                    {
                        continue;
                    }
                    GetClipPath(visualChild, skBounds, uris, clipPath, svgClipPathClipRule);
                }
            }
        }

        internal static void GetClipPathClipPath(SvgClipPath svgClipPath, Rect skBounds, HashSet<Uri> uris, ClipPath? clipPath)
        {
            if (clipPath is null)
            {
                return;
            }

            var svgClipPathRef = svgClipPath.GetUriElementReference<SvgClipPath>("clip-path", uris);
            if (svgClipPathRef?.Children is null)
            {
                return;
            }

            GetClipPath(svgClipPathRef, skBounds, uris, clipPath);

            var skMatrix = Matrix.CreateIdentity();

            if (svgClipPathRef.ClipPathUnits == SvgCoordinateUnits.ObjectBoundingBox)
            {
                var skScaleMatrix = Matrix.CreateScale(skBounds.Width, skBounds.Height);
                skMatrix = skMatrix.PostConcat(skScaleMatrix);

                var skTranslateMatrix = Matrix.CreateTranslation(skBounds.Left, skBounds.Top);
                skMatrix = skMatrix.PostConcat(skTranslateMatrix);
            }

            var skTransformsMatrix = ToMatrix(svgClipPathRef.Transforms);
            skMatrix = skMatrix.PostConcat(skTransformsMatrix);

            // TODO: clipPath.Transform
            clipPath.Transform = skMatrix;
        }

        internal static void GetClipPath(SvgClipPath svgClipPath, Rect skBounds, HashSet<Uri> uris, ClipPath? clipPath)
        {
            if (clipPath is null)
            {
                return;
            }

            GetClipPathClipPath(svgClipPath, skBounds, uris, clipPath.Clip);

            var clipPathClipRule = GetSvgClipRule(svgClipPath);

            GetClipPath(svgClipPath.Children, skBounds, uris, clipPath, clipPathClipRule);

            var skMatrix = Matrix.CreateIdentity();

            if (svgClipPath.ClipPathUnits == SvgCoordinateUnits.ObjectBoundingBox)
            {
                var skScaleMatrix = Matrix.CreateScale(skBounds.Width, skBounds.Height);
                skMatrix = skMatrix.PostConcat(skScaleMatrix);

                var skTranslateMatrix = Matrix.CreateTranslation(skBounds.Left, skBounds.Top);
                skMatrix = skMatrix.PostConcat(skTranslateMatrix);
            }

            var skTransformsMatrix = ToMatrix(svgClipPath.Transforms);
            skMatrix = skMatrix.PostConcat(skTransformsMatrix);

            // TODO: clipPath.Transform
            clipPath.Transform = skMatrix;

            if (clipPath.Clips is { } && clipPath.Clips.Count == 0)
            {
                var pathClip = new PathClip
                {
                    Path = new Path(),
                    Transform = Matrix.CreateIdentity(),
                    Clip = default
                };
                clipPath.Clips.Add(pathClip);
            }
        }

        internal static void GetSvgVisualElementClipPath(SvgVisualElement svgVisualElement, Rect skBounds, HashSet<Uri> uris, ClipPath clipPath)
        {
            if (svgVisualElement?.ClipPath is null)
            {
                return;
            }

            if (HasRecursiveReference(svgVisualElement, (e) => e.ClipPath, uris))
            {
                return;
            }

            var svgClipPath = GetReference<SvgClipPath>(svgVisualElement, svgVisualElement.ClipPath);
            if (svgClipPath?.Children is null)
            {
                return;
            }

            GetClipPath(svgClipPath, skBounds, uris, clipPath);
        }

        internal static Rect? GetClipRect(string clip, Rect skRectBounds)
        {
            if (!string.IsNullOrEmpty(clip) && clip.StartsWith("rect(", StringComparison.Ordinal))
            {
                clip = clip.Trim();
                var offsets = new List<float>();
                foreach (var o in clip.Substring(5, clip.Length - 6).Split(','))
                {
                    offsets.Add(float.Parse(o.Trim(), NumberStyles.Any, CultureInfo.InvariantCulture));
                }

                var skClipRect = Rect.Create(
                    skRectBounds.Left + offsets[3],
                    skRectBounds.Top + offsets[0],
                    skRectBounds.Width - (offsets[3] + offsets[1]),
                    skRectBounds.Height - (offsets[2] + offsets[0]));
                return skClipRect;
            }
            return default;
        }

        internal static MaskDrawable? GetSvgElementMask(SvgElement svgElement, Rect skBounds, HashSet<Uri> uris, IAssetLoader assetLoader)
        {
            var svgMaskRef = svgElement.GetUriElementReference<SvgMask>("mask", uris);
            if (svgMaskRef?.Children is null)
            {
                return default;
            }
            var maskDrawable = MaskDrawable.Create(svgMaskRef, skBounds, null, assetLoader, Attributes.None);
            return maskDrawable;
        }
    }
}
