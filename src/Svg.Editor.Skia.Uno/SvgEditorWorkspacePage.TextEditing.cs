using System.Globalization;
using System.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Svg;
using Svg.Model.Drawables;
using Windows.Foundation;
using Shim = ShimSkiaSharp;
using SK = SkiaSharp;

namespace Svg.Editor.Skia.Uno;

public partial class SvgEditorWorkspacePage
{
    private const string TextBoxLeftAttribute = "data-svgskia-text-box-left";
    private const string TextBoxTopAttribute = "data-svgskia-text-box-top";
    private const string TextBoxWidthAttribute = "data-svgskia-text-box-width";
    private const string TextBoxHeightAttribute = "data-svgskia-text-box-height";
    private const string TextBoxModeAttribute = "data-svgskia-text-box-mode";
    private const string TextContentAttribute = "data-svgskia-text-content";
    private const float DefaultTextFontSize = 16f;
    private const float DefaultTextBoxWidth = 220f;
    private const float DefaultTextBoxHeight = 40f;
    private const float MinimumTextAreaWidth = 48f;
    private const float MinimumTextAreaHeight = 24f;
    private const double TextCreationThresholdPixels = 6.0;

    private SvgTextBase? _inlineTextElement;
    private string _inlineTextOriginalContent = string.Empty;
    private bool _inlineTextIsNew;
    private bool _inlineTextIsArea;
    private bool _inlineTextCommitInProgress;

    private bool IsInlineTextEditing => _inlineTextElement is not null && InlineTextEditor is { Visibility: Visibility.Visible };

    protected void OnCanvasDoubleTapped(object sender, DoubleTappedRoutedEventArgs e)
    {
        if (_document is null || IsInlineTextEditing)
        {
            return;
        }

        var viewPoint = e.GetPosition(EditorSvg);
        var text = GetVisualHits(viewPoint).OfType<SvgTextBase>().FirstOrDefault();
        if (text is null)
        {
            return;
        }

        SelectElement(text);
        if (StartInlineTextEdit(text))
        {
            e.Handled = true;
        }
    }

    protected void OnInlineTextEditorLostFocus(object sender, RoutedEventArgs e)
    {
        if (_inlineTextCommitInProgress)
        {
            return;
        }

        CommitInlineTextEdit();
    }

    protected void OnInlineTextEditorKeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (!IsInlineTextEditing)
        {
            return;
        }

        if (e.Key == Windows.System.VirtualKey.Escape)
        {
            CancelInlineTextEdit();
            e.Handled = true;
            return;
        }

        if (e.Key == Windows.System.VirtualKey.Enter && IsPrimaryCommandModifierPressed())
        {
            CommitInlineTextEdit();
            e.Handled = true;
        }
    }

    protected void OnInlineTextEditorTextChanged(object sender, TextChangedEventArgs e)
    {
        if (IsInlineTextEditing)
        {
            LayoutInlineTextEditor();
        }
    }

    private bool StartInlineTextEdit(SvgTextBase text, bool isNew = false, bool selectAll = true)
    {
        if (InlineTextEditor is not TextBox editor)
        {
            return false;
        }

        if (IsInlineTextEditing && !ReferenceEquals(_inlineTextElement, text))
        {
            CommitInlineTextEdit();
        }

        _inlineTextElement = text;
        _inlineTextOriginalContent = GetEditableTextContent(text);
        _inlineTextIsNew = isNew;
        _inlineTextIsArea = IsAreaText(text);

        editor.AcceptsReturn = true;
        editor.TextWrapping = _inlineTextIsArea ? TextWrapping.Wrap : TextWrapping.NoWrap;
        editor.Text = _inlineTextOriginalContent;
        editor.Visibility = Visibility.Visible;
        editor.IsTabStop = true;

        LayoutInlineTextEditor();
        DispatcherQueue.TryEnqueue(() =>
        {
            LayoutInlineTextEditor();
            editor.Focus(FocusState.Programmatic);
            if (selectAll)
            {
                editor.SelectAll();
            }
            else
            {
                editor.Select(editor.Text?.Length ?? 0, 0);
            }
        });

        return true;
    }

    private void CommitInlineTextEdit()
    {
        if (!IsInlineTextEditing || InlineTextEditor is not TextBox editor || _inlineTextElement is null)
        {
            return;
        }

        _inlineTextCommitInProgress = true;
        try
        {
            var element = _inlineTextElement;
            var isNew = _inlineTextIsNew;
            var isArea = _inlineTextIsArea;
            var content = editor.Text ?? string.Empty;

            HideInlineTextEditor();

            if (isNew && !isArea && string.IsNullOrWhiteSpace(content))
            {
                RemoveElementFromParent(element);
                SelectElement(null);
                RefreshDocumentVisual(rebuildOutline: true, reloadProperties: true);
                return;
            }

            ApplyTextContentLayout(element, content);
            RefreshDocumentVisual(rebuildOutline: true, reloadProperties: true);
        }
        finally
        {
            _inlineTextCommitInProgress = false;
        }
    }

    private void CancelInlineTextEdit()
    {
        if (!IsInlineTextEditing || _inlineTextElement is null)
        {
            return;
        }

        var element = _inlineTextElement;
        var isNew = _inlineTextIsNew;
        var originalContent = _inlineTextOriginalContent;

        HideInlineTextEditor();

        if (isNew)
        {
            RemoveElementFromParent(element);
            SelectElement(null);
        }
        else
        {
            ApplyTextContentLayout(element, originalContent);
        }

        RefreshDocumentVisual(rebuildOutline: true, reloadProperties: true);
    }

    private void HideInlineTextEditor()
    {
        if (InlineTextEditor is TextBox editor)
        {
            editor.Visibility = Visibility.Collapsed;
            editor.Text = string.Empty;
        }

        _inlineTextElement = null;
        _inlineTextOriginalContent = string.Empty;
        _inlineTextIsNew = false;
        _inlineTextIsArea = false;
    }

    private void LayoutInlineTextEditor()
    {
        if (!IsInlineTextEditing || InlineTextEditor is not TextBox editor || _inlineTextElement is null)
        {
            return;
        }

        if (!TryGetInlineTextEditorRect(_inlineTextElement, out var hostRect))
        {
            return;
        }

        Canvas.SetLeft(editor, hostRect.X);
        Canvas.SetTop(editor, hostRect.Y);
        editor.Width = Math.Max(24.0, hostRect.Width);
        editor.Height = Math.Max(28.0, hostRect.Height);
    }

    private bool TryGetInlineTextEditorRect(SvgTextBase text, out Rect hostRect)
    {
        hostRect = default;

        if (_inlineTextIsArea && TryGetTextPictureBounds(text, out var pictureBounds))
        {
            return TryMapPictureRectToCanvasHost(pictureBounds, out hostRect);
        }

        if (!TryGetTextAnchorPicturePoint(text, out var anchorPicture)
            || !TryMapPicturePointToCanvasHost(anchorPicture, out var anchorHost))
        {
            return false;
        }

        var fontSize = GetTextFontSize(text);
        var metrics = GetTextFontMetrics(text);
        var lineHeight = GetTextLineHeight(text, fontSize, metrics);
        var widthPicture = Math.Max(
            DefaultTextBoxWidth,
            MeasureLongestLineWidth(text, NormalizeTextContent(InlineTextEditor?.Text ?? string.Empty)) + (fontSize * 0.75f));
        var lineCount = Math.Max(1, CountLogicalLines(InlineTextEditor?.Text ?? string.Empty));
        var heightPicture = Math.Max(lineHeight * lineCount, DefaultTextBoxHeight);
        var scale = GetCanvasScale();
        var topOffset = Math.Max(fontSize * 0.8f, -metrics.Ascent) * scale;

        hostRect = new Rect(
            anchorHost.X,
            anchorHost.Y - topOffset,
            Math.Max(96.0, widthPicture * scale),
            Math.Max(28.0, heightPicture * scale));

        return true;
    }

    private void UpdateTextCreationPreview(SvgTextBase text, Shim.SKPoint start, Shim.SKPoint current)
    {
        var threshold = (float)(TextCreationThresholdPixels / Math.Max(GetCanvasScale(), 0.0001f));
        var dx = current.X - start.X;
        var dy = current.Y - start.Y;

        if (Math.Abs(dx) > threshold || Math.Abs(dy) > threshold)
        {
            var left = Math.Min(start.X, current.X);
            var top = Math.Min(start.Y, current.Y);
            var right = Math.Max(start.X, current.X);
            var bottom = Math.Max(start.Y, current.Y);
            var rect = new SK.SKRect(
                left,
                top,
                Math.Max(left + MinimumTextAreaWidth, right),
                Math.Max(top + MinimumTextAreaHeight, bottom));
            SetTextBoxRect(text, rect);
            text.CustomAttributes[TextBoxModeAttribute] = "area";
            SetTextAnchor(text, rect.Left, rect.Top + GetTextBaselineOffset(text));
        }
        else
        {
            SetPointTextBounds(text, start.X, start.Y, string.Empty);
            SetTextAnchor(text, start.X, start.Y);
        }
    }

    private void CompleteTextCreationAndEdit(SvgTextBase text)
    {
        _isCreating = false;
        _newElement = null;
        _freehandPoints.Clear();
        RefreshDocumentVisual(rebuildOutline: true, reloadProperties: true);
        StartInlineTextEdit(text, isNew: true);
    }

    private void ResizeTextBox(SvgTextBase text, int handle, float dx, float dy)
    {
        if (!TryGetTextBoxRect(text, out var rect))
        {
            return;
        }

        var left = rect.Left;
        var top = rect.Top;
        var right = rect.Right;
        var bottom = rect.Bottom;

        switch (handle)
        {
            case 0:
                left += dx;
                top += dy;
                break;
            case 1:
                top += dy;
                break;
            case 2:
                right += dx;
                top += dy;
                break;
            case 3:
                right += dx;
                break;
            case 4:
                right += dx;
                bottom += dy;
                break;
            case 5:
                bottom += dy;
                break;
            case 6:
                left += dx;
                bottom += dy;
                break;
            case 7:
                left += dx;
                break;
            default:
                return;
        }

        if (right - left < MinimumTextAreaWidth)
        {
            if (handle is 0 or 6 or 7)
            {
                left = right - MinimumTextAreaWidth;
            }
            else
            {
                right = left + MinimumTextAreaWidth;
            }
        }

        if (bottom - top < MinimumTextAreaHeight)
        {
            if (handle is 0 or 1 or 2)
            {
                top = bottom - MinimumTextAreaHeight;
            }
            else
            {
                bottom = top + MinimumTextAreaHeight;
            }
        }

        var updatedRect = new SK.SKRect(left, top, right, bottom);
        SetTextBoxRect(text, updatedRect);
        SetTextAnchor(text, updatedRect.Left, updatedRect.Top + GetTextBaselineOffset(text));
        ApplyTextContentLayout(text, GetEditableTextContent(text));
    }

    private void ApplyTextContentLayout(SvgTextBase text, string content)
    {
        var normalizedContent = NormalizeTextContent(content);
        var hasTextRect = TryGetTextBoxRect(text, out var textRect);
        var isArea = IsAreaText(text) && hasTextRect && textRect.Width >= MinimumTextAreaWidth;
        var fontSize = GetTextFontSize(text);
        var metrics = GetTextFontMetrics(text);
        var baselineOffset = Math.Max(fontSize * 0.8f, -metrics.Ascent);
        var lineHeight = GetTextLineHeight(text, fontSize, metrics);
        var anchorX = text.X.Count > 0 ? text.X[0].Value : 0f;
        var anchorY = text.Y.Count > 0 ? text.Y[0].Value : baselineOffset;
        var lines = LayoutTextLines(text, normalizedContent, isArea ? textRect.Width : null);

        if (isArea)
        {
            anchorX = textRect.Left;
            anchorY = textRect.Top + baselineOffset;
            SetTextBoxRect(text, textRect);
        }

        SetTextAnchor(text, anchorX, anchorY);

        if (!isArea && lines.Count <= 1)
        {
            text.Text = normalizedContent;
        }
        else
        {
            text.Text = null;
            var baseline = anchorY;
            foreach (var line in lines)
            {
                var span = new SvgTextSpan
                {
                    X = new SvgUnitCollection { new SvgUnit(SvgUnitType.User, anchorX) },
                    Y = new SvgUnitCollection { new SvgUnit(SvgUnitType.User, baseline) },
                    Text = string.IsNullOrEmpty(line) ? " " : line
                };
                text.Children.Add(span);
                baseline += lineHeight;
            }
        }

        text.CustomAttributes[TextContentAttribute] = EncodeEditableTextContent(normalizedContent);

        if (!isArea)
        {
            SetPointTextBounds(text, anchorX, anchorY, normalizedContent);
        }
    }

    private void SetPointTextBounds(SvgTextBase text, float anchorX, float anchorY, string content)
    {
        var normalizedContent = NormalizeTextContent(content);
        var fontSize = GetTextFontSize(text);
        var metrics = GetTextFontMetrics(text);
        var lineHeight = GetTextLineHeight(text, fontSize, metrics);
        var lines = LayoutTextLines(text, normalizedContent, null);
        var width = Math.Max(1f, MeasureLongestLineWidth(text, normalizedContent));
        var top = anchorY + metrics.Ascent;
        var bottom = anchorY + metrics.Descent + Math.Max(0, lines.Count - 1) * lineHeight;
        SetTextBoxRect(text, new SK.SKRect(anchorX, top, anchorX + width, bottom));
        text.CustomAttributes.Remove(TextBoxModeAttribute);
    }

    private List<string> LayoutTextLines(SvgTextBase text, string content, float? boxWidth)
    {
        var normalizedContent = NormalizeTextContent(content);
        var paragraphs = normalizedContent.Split('\n');
        var lines = new List<string>();

        foreach (var paragraph in paragraphs)
        {
            if (boxWidth is > 1f)
            {
                lines.AddRange(WrapParagraph(text, paragraph, boxWidth.Value));
            }
            else
            {
                lines.Add(paragraph);
            }
        }

        if (lines.Count == 0)
        {
            lines.Add(string.Empty);
        }

        return lines;
    }

    private List<string> WrapParagraph(SvgTextBase text, string paragraph, float boxWidth)
    {
        if (string.IsNullOrEmpty(paragraph))
        {
            return [string.Empty];
        }

        if (MeasureTextWidth(text, paragraph) <= boxWidth)
        {
            return [paragraph];
        }

        var lines = new List<string>();
        var current = new StringBuilder();

        foreach (var ch in paragraph)
        {
            var candidate = current.ToString() + ch;
            if (current.Length == 0 || MeasureTextWidth(text, candidate) <= boxWidth)
            {
                current.Append(ch);
                continue;
            }

            lines.Add(current.ToString().TrimEnd());
            current.Clear();
            if (!char.IsWhiteSpace(ch))
            {
                current.Append(ch);
            }
        }

        if (current.Length > 0)
        {
            lines.Add(current.ToString().TrimEnd());
        }

        return lines.Count == 0 ? [paragraph] : lines;
    }

    private string GetEditableTextContent(SvgTextBase text)
    {
        if (text.CustomAttributes.TryGetValue(TextContentAttribute, out var encoded)
            && !string.IsNullOrWhiteSpace(encoded))
        {
            return DecodeEditableTextContent(encoded);
        }

        var spans = text.Children.OfType<SvgTextSpan>().ToList();
        if (spans.Count > 0)
        {
            return string.Join("\n", spans.Select(span => span.Text ?? string.Empty));
        }

        return text.Text ?? string.Empty;
    }

    private bool TryGetTextBoxRect(SvgTextBase text, out SK.SKRect rect)
    {
        rect = default;
        if (TryParseTextBoxAttribute(text, TextBoxLeftAttribute, out var left)
            && TryParseTextBoxAttribute(text, TextBoxTopAttribute, out var top)
            && TryParseTextBoxAttribute(text, TextBoxWidthAttribute, out var width)
            && TryParseTextBoxAttribute(text, TextBoxHeightAttribute, out var height)
            && width >= 0f
            && height >= 0f)
        {
            rect = new SK.SKRect(left, top, left + width, top + height);
            return true;
        }

        return false;
    }

    private bool HasTextBoxRect(SvgTextBase text)
    {
        return TryGetTextBoxRect(text, out _);
    }

    private static bool IsAreaText(SvgTextBase text)
    {
        return text.CustomAttributes.TryGetValue(TextBoxModeAttribute, out var mode)
            && string.Equals(mode, "area", StringComparison.OrdinalIgnoreCase);
    }

    private void SetTextBoxRect(SvgTextBase text, SK.SKRect rect)
    {
        text.CustomAttributes[TextBoxLeftAttribute] = rect.Left.ToString(CultureInfo.InvariantCulture);
        text.CustomAttributes[TextBoxTopAttribute] = rect.Top.ToString(CultureInfo.InvariantCulture);
        text.CustomAttributes[TextBoxWidthAttribute] = rect.Width.ToString(CultureInfo.InvariantCulture);
        text.CustomAttributes[TextBoxHeightAttribute] = rect.Height.ToString(CultureInfo.InvariantCulture);
    }

    private static bool TryParseTextBoxAttribute(SvgTextBase text, string key, out float value)
    {
        value = 0f;
        return text.CustomAttributes.TryGetValue(key, out var raw)
            && float.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out value);
    }

    private bool TryGetTextPictureBounds(SvgTextBase text, out SK.SKRect pictureBounds)
    {
        pictureBounds = default;

        if (EditorSvg.SkSvg?.Drawable is not DrawableBase root)
        {
            return false;
        }

        var drawable = FindDrawable(root, text);
        if (drawable is null)
        {
            return false;
        }

        if (TryGetTextBoxRect(text, out var localRect))
        {
            var mappedTopLeft = drawable.TotalTransform.MapPoint(new Shim.SKPoint(localRect.Left, localRect.Top));
            var mappedTopRight = drawable.TotalTransform.MapPoint(new Shim.SKPoint(localRect.Right, localRect.Top));
            var mappedBottomLeft = drawable.TotalTransform.MapPoint(new Shim.SKPoint(localRect.Left, localRect.Bottom));
            var mappedBottomRight = drawable.TotalTransform.MapPoint(new Shim.SKPoint(localRect.Right, localRect.Bottom));
            pictureBounds = SK.SKRect.Create(
                Math.Min(Math.Min(mappedTopLeft.X, mappedTopRight.X), Math.Min(mappedBottomLeft.X, mappedBottomRight.X)),
                Math.Min(Math.Min(mappedTopLeft.Y, mappedTopRight.Y), Math.Min(mappedBottomLeft.Y, mappedBottomRight.Y)),
                Math.Max(Math.Max(mappedTopLeft.X, mappedTopRight.X), Math.Max(mappedBottomLeft.X, mappedBottomRight.X)) - Math.Min(Math.Min(mappedTopLeft.X, mappedTopRight.X), Math.Min(mappedBottomLeft.X, mappedBottomRight.X)),
                Math.Max(Math.Max(mappedTopLeft.Y, mappedTopRight.Y), Math.Max(mappedBottomLeft.Y, mappedBottomRight.Y)) - Math.Min(Math.Min(mappedTopLeft.Y, mappedTopRight.Y), Math.Min(mappedBottomLeft.Y, mappedBottomRight.Y)));
            return true;
        }

        pictureBounds = new SK.SKRect(
            drawable.TransformedBounds.Left,
            drawable.TransformedBounds.Top,
            drawable.TransformedBounds.Right,
            drawable.TransformedBounds.Bottom);
        return pictureBounds.Width > 0f || pictureBounds.Height > 0f;
    }

    private bool TryGetTextAnchorPicturePoint(SvgTextBase text, out Shim.SKPoint picturePoint)
    {
        picturePoint = default;

        var x = text.X.Count > 0 ? text.X[0].Value : 0f;
        var y = text.Y.Count > 0 ? text.Y[0].Value : 0f;

        if (EditorSvg.SkSvg?.Drawable is DrawableBase root
            && FindDrawable(root, text) is { } drawable)
        {
            picturePoint = drawable.TotalTransform.MapPoint(new Shim.SKPoint(x, y));
            return true;
        }

        picturePoint = new Shim.SKPoint(x, y);
        return true;
    }

    private bool TryMapPicturePointToCanvasHost(Shim.SKPoint picturePoint, out Point hostPoint)
    {
        hostPoint = default;
        if (!EditorSvg.TryGetViewPoint(picturePoint, out var localPoint)
            || !TryGetSvgOriginInCanvasHost(out var origin))
        {
            return false;
        }

        hostPoint = new Point(origin.X + localPoint.X, origin.Y + localPoint.Y);
        return true;
    }

    private bool TryMapPictureRectToCanvasHost(SK.SKRect pictureRect, out Rect hostRect)
    {
        hostRect = default;

        if (!TryGetSvgOriginInCanvasHost(out var origin))
        {
            return false;
        }

        var points = new[]
        {
            new Shim.SKPoint(pictureRect.Left, pictureRect.Top),
            new Shim.SKPoint(pictureRect.Right, pictureRect.Top),
            new Shim.SKPoint(pictureRect.Left, pictureRect.Bottom),
            new Shim.SKPoint(pictureRect.Right, pictureRect.Bottom)
        };

        var projected = new List<Point>(points.Length);
        foreach (var point in points)
        {
            if (!EditorSvg.TryGetViewPoint(point, out var localPoint))
            {
                return false;
            }

            projected.Add(new Point(origin.X + localPoint.X, origin.Y + localPoint.Y));
        }

        var left = projected.Min(static point => point.X);
        var top = projected.Min(static point => point.Y);
        var right = projected.Max(static point => point.X);
        var bottom = projected.Max(static point => point.Y);
        hostRect = new Rect(left, top, right - left, bottom - top);
        return true;
    }

    private void SetTextAnchor(SvgTextBase text, float x, float y)
    {
        if (text.X.Count == 0)
        {
            text.X.Add(new SvgUnit(SvgUnitType.User, x));
        }
        else
        {
            text.X[0] = new SvgUnit(text.X[0].Type, x);
        }

        if (text.Y.Count == 0)
        {
            text.Y.Add(new SvgUnit(SvgUnitType.User, y));
        }
        else
        {
            text.Y[0] = new SvgUnit(text.Y[0].Type, y);
        }
    }

    private float MeasureLongestLineWidth(SvgTextBase text, string content)
    {
        var normalizedContent = NormalizeTextContent(content);
        var lines = normalizedContent.Split('\n');
        var max = 0f;
        foreach (var line in lines)
        {
            max = Math.Max(max, MeasureTextWidth(text, line));
        }

        return max;
    }

    private float MeasureTextWidth(SvgTextBase text, string content)
    {
        if (string.IsNullOrEmpty(content))
        {
            return 0f;
        }

        var paint = CreateTextMeasurePaint(text);
        if (EditorSvg.SkSvg?.AssetLoader is { } assetLoader)
        {
            var bounds = default(Shim.SKRect);
            return assetLoader.MeasureText(content, paint, ref bounds);
        }

        return content.Length * GetTextFontSize(text) * 0.6f;
    }

    private Shim.SKFontMetrics GetTextFontMetrics(SvgTextBase text)
    {
        var paint = CreateTextMeasurePaint(text);
        return EditorSvg.SkSvg?.AssetLoader?.GetFontMetrics(paint)
            ?? new Shim.SKFontMetrics
            {
                Ascent = -GetTextFontSize(text) * 0.8f,
                Descent = GetTextFontSize(text) * 0.2f,
                Leading = GetTextFontSize(text) * 0.2f
            };
    }

    private Shim.SKPaint CreateTextMeasurePaint(SvgTextBase text)
    {
        var paint = new Shim.SKPaint
        {
            TextSize = GetTextFontSize(text),
            TextAlign = Shim.SKTextAlign.Left,
            LcdRenderText = true,
            SubpixelText = true,
            TextEncoding = Shim.SKTextEncoding.Utf16
        };

        paint.Typeface = Shim.SKTypeface.FromFamilyName(
            string.IsNullOrWhiteSpace(text.FontFamily) ? "Arial" : text.FontFamily,
            ToSkFontStyleWeight(text.FontWeight),
            Shim.SKFontStyleWidth.Normal,
            ToSkFontStyleSlant(text.FontStyle));

        return paint;
    }

    private static Shim.SKFontStyleWeight ToSkFontStyleWeight(SvgFontWeight fontWeight)
    {
        if (fontWeight.HasFlag(SvgFontWeight.W100))
        {
            return Shim.SKFontStyleWeight.Thin;
        }

        if (fontWeight.HasFlag(SvgFontWeight.W200))
        {
            return Shim.SKFontStyleWeight.ExtraLight;
        }

        if (fontWeight.HasFlag(SvgFontWeight.W300))
        {
            return Shim.SKFontStyleWeight.Light;
        }

        if (fontWeight.HasFlag(SvgFontWeight.W500))
        {
            return Shim.SKFontStyleWeight.Medium;
        }

        if (fontWeight.HasFlag(SvgFontWeight.W600))
        {
            return Shim.SKFontStyleWeight.SemiBold;
        }

        if (fontWeight.HasFlag(SvgFontWeight.W800))
        {
            return Shim.SKFontStyleWeight.ExtraBold;
        }

        if (fontWeight.HasFlag(SvgFontWeight.W900))
        {
            return Shim.SKFontStyleWeight.Black;
        }

        return fontWeight.HasFlag(SvgFontWeight.Bold) || fontWeight.HasFlag(SvgFontWeight.W700)
            ? Shim.SKFontStyleWeight.Bold
            : Shim.SKFontStyleWeight.Normal;
    }

    private static Shim.SKFontStyleSlant ToSkFontStyleSlant(SvgFontStyle fontStyle)
    {
        if (fontStyle.HasFlag(SvgFontStyle.Italic))
        {
            return Shim.SKFontStyleSlant.Italic;
        }

        return fontStyle.HasFlag(SvgFontStyle.Oblique)
            ? Shim.SKFontStyleSlant.Oblique
            : Shim.SKFontStyleSlant.Upright;
    }

    private static string NormalizeTextContent(string content)
    {
        return (content ?? string.Empty).Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n');
    }

    private static string EncodeEditableTextContent(string content)
    {
        return Convert.ToBase64String(Encoding.UTF8.GetBytes(content));
    }

    private static string DecodeEditableTextContent(string encoded)
    {
        try
        {
            return Encoding.UTF8.GetString(Convert.FromBase64String(encoded));
        }
        catch
        {
            return string.Empty;
        }
    }

    private static int CountLogicalLines(string content)
    {
        return Math.Max(1, NormalizeTextContent(content).Split('\n').Length);
    }

    private float GetTextFontSize(SvgTextBase text)
    {
        return text.FontSize == SvgUnit.None || text.FontSize == SvgUnit.Empty
            ? DefaultTextFontSize
            : Math.Max(1f, text.FontSize.Value);
    }

    private float GetTextLineHeight(SvgTextBase text, float fontSize, Shim.SKFontMetrics metrics)
    {
        if (text.CustomAttributes.TryGetValue(TextLineHeightAttribute, out var rawLineHeight)
            && !string.Equals(rawLineHeight, "Auto", StringComparison.OrdinalIgnoreCase)
            && float.TryParse(rawLineHeight, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed))
        {
            return Math.Max(fontSize, parsed);
        }

        var measured = metrics.Descent - metrics.Ascent + Math.Max(0f, metrics.Leading);
        return Math.Max(fontSize * 1.2f, measured);
    }

    private float GetTextBaselineOffset(SvgTextBase text)
    {
        var fontSize = GetTextFontSize(text);
        var metrics = GetTextFontMetrics(text);
        return Math.Max(fontSize * 0.8f, -metrics.Ascent);
    }

    private bool IsInlineTextEditorSource(DependencyObject? source)
    {
        if (InlineTextEditor is not TextBox editor || source is null)
        {
            return false;
        }

        var current = source;
        while (current is not null)
        {
            if (ReferenceEquals(current, editor))
            {
                return true;
            }

            current = VisualTreeHelper.GetParent(current);
        }

        return false;
    }

    private static void RemoveElementFromParent(SvgVisualElement element)
    {
        if (element.Parent is SvgElement parent)
        {
            parent.Children.Remove(element);
        }
    }
}
