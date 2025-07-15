using System;
using System.Drawing;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Markup.Xaml;
using Avalonia.Svg.Skia;
using Avalonia.Interactivity;
using SkiaSharp;
using Svg;
using Svg.Skia;
using Svg.Model.Drawables;

namespace HitTestEditorSample;

public partial class MainWindow : Window
{
    private DrawableBase? _selectedDrawable;
    private SvgVisualElement? _selectedElement;
    private SKColor _boundsColor = SKColors.Red;

    public MainWindow()
    {
        InitializeComponent();
#if DEBUG
        this.AttachDevTools();
#endif
        SvgView.Path = "Assets/__tiger.svg";
        SvgView.SkSvg!.OnDraw += SvgView_OnDraw;
    }

    private void SvgView_OnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        var point = e.GetPosition(SvgView);
        if (SvgView.SkSvg is { } skSvg && SvgView.TryGetPicturePoint(point, out var pp))
        {
            _selectedDrawable = skSvg.HitTestDrawables(pp).FirstOrDefault();
            _selectedElement = skSvg.HitTestElements(pp).OfType<SvgVisualElement>().FirstOrDefault();
        }
        if (_selectedElement is { })
        {
            IdTextBox.Text = _selectedElement.ID;
            FillTextBox.Text = ColorTranslator.ToHtml((_selectedElement.Fill as SvgColourServer)?.Colour ?? System.Drawing.Color.Empty);
            StrokeTextBox.Text = ColorTranslator.ToHtml((_selectedElement.Stroke as SvgColourServer)?.Colour ?? System.Drawing.Color.Empty);
        }
        SvgView.InvalidateVisual();
    }

    private void ApplyButton_OnClick(object? sender, RoutedEventArgs e)
    {
        if (_selectedElement is null)
            return;
        _selectedElement.ID = IdTextBox.Text;
        try
        {
            var c = ColorTranslator.FromHtml(FillTextBox.Text);
            _selectedElement.Fill = new SvgColourServer(c);
        }
        catch
        {
        }
        try
        {
            var c = ColorTranslator.FromHtml(StrokeTextBox.Text);
            _selectedElement.Stroke = new SvgColourServer(c);
        }
        catch
        {
        }
        SvgView.InvalidateVisual();
    }

    private void SvgView_OnDraw(object? sender, SKSvgDrawEventArgs e)
    {
        if (_selectedDrawable is null)
            return;
        using var paint = new SKPaint
        {
            IsAntialias = true,
            Style = SKPaintStyle.Stroke,
            Color = _boundsColor
        };
        var rect = SvgView.SkSvg!.SkiaModel.ToSKRect(_selectedDrawable.TransformedBounds);
        e.Canvas.DrawRect(rect, paint);
    }
}
