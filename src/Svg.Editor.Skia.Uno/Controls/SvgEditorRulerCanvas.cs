using System.Collections.Specialized;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Svg.Editor.Skia.Uno.Models;
using Uno.WinUI.Graphics2DSK;
using Windows.Foundation;
using SK = SkiaSharp;

namespace Svg.Editor.Skia.Uno.Controls;

public sealed class SvgEditorRulerCanvas : SKCanvasElement
{
    private static readonly SK.SKColor s_defaultBackgroundColor = new(250, 250, 251);

    public static readonly DependencyProperty OrientationProperty =
        DependencyProperty.Register(
            nameof(Orientation),
            typeof(Orientation),
            typeof(SvgEditorRulerCanvas),
            new PropertyMetadata(Orientation.Horizontal, OnVisualPropertyChanged));

    public static readonly DependencyProperty MarksProperty =
        DependencyProperty.Register(
            nameof(Marks),
            typeof(IEnumerable<RulerMark>),
            typeof(SvgEditorRulerCanvas),
            new PropertyMetadata(null, OnMarksPropertyChanged));

    public static readonly DependencyProperty MarkersProperty =
        DependencyProperty.Register(
            nameof(Markers),
            typeof(IEnumerable<RulerMarker>),
            typeof(SvgEditorRulerCanvas),
            new PropertyMetadata(null, OnMarkersPropertyChanged));

    public Orientation Orientation
    {
        get => (Orientation)GetValue(OrientationProperty);
        set => SetValue(OrientationProperty, value);
    }

    public IEnumerable<RulerMark>? Marks
    {
        get => (IEnumerable<RulerMark>?)GetValue(MarksProperty);
        set => SetValue(MarksProperty, value);
    }

    public IEnumerable<RulerMarker>? Markers
    {
        get => (IEnumerable<RulerMarker>?)GetValue(MarkersProperty);
        set => SetValue(MarkersProperty, value);
    }

    protected override void RenderOverride(SK.SKCanvas canvas, Size area)
    {
        var marks = (Marks ?? Array.Empty<RulerMark>())
            .OrderBy(mark => mark.Position)
            .ToArray();
        var markers = (Markers ?? Array.Empty<RulerMarker>())
            .OrderBy(marker => Math.Min(marker.Start, marker.End))
            .ToArray();

        canvas.Clear(s_defaultBackgroundColor);

        DrawSelectionMarkers(canvas, area, markers);
        DrawMinorTicks(canvas, area, marks);
        DrawMajorTicks(canvas, area, marks);
    }

    private static void OnVisualPropertyChanged(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs dependencyPropertyChangedEventArgs)
    {
        if (dependencyObject is SvgEditorRulerCanvas canvas)
        {
            canvas.Invalidate();
        }
    }

    private static void OnMarksPropertyChanged(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs dependencyPropertyChangedEventArgs)
    {
        if (dependencyObject is not SvgEditorRulerCanvas canvas)
        {
            return;
        }

        canvas.AttachSubscription(
            dependencyPropertyChangedEventArgs.OldValue as INotifyCollectionChanged,
            dependencyPropertyChangedEventArgs.NewValue as INotifyCollectionChanged,
            isMarks: true);
        canvas.Invalidate();
    }

    private static void OnMarkersPropertyChanged(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs dependencyPropertyChangedEventArgs)
    {
        if (dependencyObject is not SvgEditorRulerCanvas canvas)
        {
            return;
        }

        canvas.AttachSubscription(
            dependencyPropertyChangedEventArgs.OldValue as INotifyCollectionChanged,
            dependencyPropertyChangedEventArgs.NewValue as INotifyCollectionChanged,
            isMarks: false);
        canvas.Invalidate();
    }

    private void AttachSubscription(INotifyCollectionChanged? oldCollection, INotifyCollectionChanged? newCollection, bool isMarks)
    {
        if (oldCollection is not null)
        {
            oldCollection.CollectionChanged -= OnBoundCollectionChanged;
        }

        if (newCollection is not null)
        {
            newCollection.CollectionChanged += OnBoundCollectionChanged;
        }

    }

    private void OnBoundCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        Invalidate();
    }

    private void DrawSelectionMarkers(SK.SKCanvas canvas, Size area, IReadOnlyList<RulerMarker> markers)
    {
        if (markers.Count == 0)
        {
            return;
        }

        using var fill = new SK.SKPaint
        {
            IsAntialias = true,
            Style = SK.SKPaintStyle.Fill,
            Color = new SK.SKColor(13, 153, 255, 34)
        };
        using var stroke = new SK.SKPaint
        {
            IsAntialias = true,
            Style = SK.SKPaintStyle.Stroke,
            Color = new SK.SKColor(13, 153, 255),
            StrokeWidth = 1f
        };
        using var labelFill = new SK.SKPaint
        {
            IsAntialias = true,
            Style = SK.SKPaintStyle.Fill,
            Color = new SK.SKColor(13, 153, 255)
        };
        using var labelText = new SK.SKPaint
        {
            IsAntialias = true,
            Color = SK.SKColors.White
        };
        using var labelFont = CreateFont(10f, SK.SKFontStyleWeight.Medium);

        foreach (var marker in markers)
        {
            var start = (float)Math.Min(marker.Start, marker.End);
            var end = (float)Math.Max(marker.Start, marker.End);
            var center = marker.Center is { } value ? (float)value : (start + end) / 2f;

            if (Orientation == Orientation.Horizontal)
            {
                var band = new SK.SKRoundRect(new SK.SKRect(start, (float)area.Height - 5f, end, (float)area.Height - 1f), 2f, 2f);
                canvas.DrawRoundRect(band, fill);
                canvas.DrawLine(start, 0f, start, (float)area.Height, stroke);
                canvas.DrawLine(end, 0f, end, (float)area.Height, stroke);
                canvas.DrawLine(center, (float)area.Height - 10f, center, (float)area.Height, stroke);

                if (!string.IsNullOrWhiteSpace(marker.Label) && end - start > 34f)
                {
                    DrawMarkerLabel(canvas, marker.Label!, labelFill, labelText, labelFont, center, 8f, horizontal: true);
                }
            }
            else
            {
                var band = new SK.SKRoundRect(new SK.SKRect((float)area.Width - 5f, start, (float)area.Width - 1f, end), 2f, 2f);
                canvas.DrawRoundRect(band, fill);
                canvas.DrawLine(0f, start, (float)area.Width, start, stroke);
                canvas.DrawLine(0f, end, (float)area.Width, end, stroke);
                canvas.DrawLine((float)area.Width - 10f, center, (float)area.Width, center, stroke);

                if (!string.IsNullOrWhiteSpace(marker.Label) && end - start > 34f)
                {
                    DrawMarkerLabel(canvas, marker.Label!, labelFill, labelText, labelFont, center, 8f, horizontal: false);
                }
            }
        }
    }

    private void DrawMinorTicks(SK.SKCanvas canvas, Size area, IReadOnlyList<RulerMark> marks)
    {
        if (marks.Count < 2)
        {
            return;
        }

        using var tick = new SK.SKPaint
        {
            IsAntialias = false,
            Style = SK.SKPaintStyle.Stroke,
            StrokeWidth = 1f,
            Color = new SK.SKColor(178, 184, 194)
        };

        for (var index = 0; index < marks.Count - 1; index++)
        {
            var start = marks[index].Position;
            var end = marks[index + 1].Position;
            var delta = end - start;
            if (Math.Abs(delta) < double.Epsilon)
            {
                continue;
            }

            for (var division = 1; division < 5; division++)
            {
                var position = (float)(start + ((delta / 5.0) * division));
                if (Orientation == Orientation.Horizontal)
                {
                    canvas.DrawLine(position, (float)area.Height - 8f, position, (float)area.Height - 1f, tick);
                }
                else
                {
                    canvas.DrawLine((float)area.Width - 8f, position, (float)area.Width - 1f, position, tick);
                }
            }
        }
    }

    private void DrawMajorTicks(SK.SKCanvas canvas, Size area, IReadOnlyList<RulerMark> marks)
    {
        if (marks.Count == 0)
        {
            return;
        }

        using var tick = new SK.SKPaint
        {
            IsAntialias = false,
            Style = SK.SKPaintStyle.Stroke,
            StrokeWidth = 1f,
            Color = new SK.SKColor(156, 163, 175)
        };
        using var label = new SK.SKPaint
        {
            IsAntialias = true,
            Color = new SK.SKColor(107, 114, 128)
        };
        using var accentLabel = new SK.SKPaint
        {
            IsAntialias = true,
            Color = new SK.SKColor(13, 153, 255)
        };
        using var labelFont = CreateFont(10f, SK.SKFontStyleWeight.Medium);

        foreach (var mark in marks)
        {
            var position = (float)mark.Position;
            var currentLabel = mark.IsAccent ? accentLabel : label;

            if (Orientation == Orientation.Horizontal)
            {
                canvas.DrawLine(position, (float)area.Height - (float)mark.TickSize, position, (float)area.Height, tick);
                canvas.DrawText(mark.Label, position + 4f, 11f, labelFont, currentLabel);
            }
            else
            {
                canvas.DrawLine((float)area.Width - (float)mark.TickSize, position, (float)area.Width, position, tick);
                canvas.Save();
                canvas.Translate(10f, position - 4f);
                canvas.RotateDegrees(-90f);
                canvas.DrawText(mark.Label, 0f, 0f, labelFont, currentLabel);
                canvas.Restore();
            }
        }
    }

    private static void DrawMarkerLabel(SK.SKCanvas canvas, string label, SK.SKPaint fill, SK.SKPaint text, SK.SKFont font, float position, float offset, bool horizontal)
    {
        var width = Math.Max(18f, font.MeasureText(label, text) + 10f);
        const float height = 18f;

        if (horizontal)
        {
            var rect = new SK.SKRoundRect(new SK.SKRect(position - (width / 2f), offset, position + (width / 2f), offset + height), 5f, 5f);
            canvas.DrawRoundRect(rect, fill);
            canvas.DrawText(label, rect.Rect.Left + 5f, rect.Rect.MidY + 4f, font, text);
        }
        else
        {
            var rect = new SK.SKRoundRect(new SK.SKRect(offset, position - (width / 2f), offset + height, position + (width / 2f)), 5f, 5f);
            canvas.DrawRoundRect(rect, fill);
            canvas.Save();
            canvas.Translate(rect.Rect.MidX + 3f, rect.Rect.Bottom - 5f);
            canvas.RotateDegrees(-90f);
            canvas.DrawText(label, 0f, 0f, font, text);
            canvas.Restore();
        }
    }

    private static SK.SKFont CreateFont(float size, SK.SKFontStyleWeight weight)
    {
        return new SK.SKFont(SK.SKTypeface.FromFamilyName("SF Pro Text", weight, SK.SKFontStyleWidth.Normal, SK.SKFontStyleSlant.Upright), size, 1f, 0f);
    }
}
