using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.IO;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Platform;
using Avalonia.Svg.Skia;
using SkiaSharp;
using Svg;
using Svg.Skia;
using Svg.Model.Drawables;
using Svg.Model.Services;

namespace HitTestEditorSample;

public partial class MainWindow : Window
{
    private DrawableBase? _selectedDrawable;
    private SvgVisualElement? _selectedElement;
    private SvgDocument? _document;

    private ObservableCollection<PropertyEntry> Properties { get; } = new();

    private readonly SKColor _boundsColor = SKColors.Red;

    public MainWindow()
    {
        InitializeComponent();
#if DEBUG
        this.AttachDevTools();
#endif
        DataContext = this;
        LoadDocument("Assets/__tiger.svg");
    }

    private void LoadDocument(string path)
    {
        // try load from Avalonia resources first
        var uri = new Uri($"avares://HitTestEditorSample/{path}");

        if (SvgView.SkSvg is { } skSvg)
            skSvg.OnDraw -= SvgView_OnDraw;

        if (AssetLoader.Exists(uri))
        {
            using var stream = AssetLoader.Open(uri);
            _document = SvgService.Open(stream);
            SvgView.Path = uri.ToString();
        }
        else if (File.Exists(path))
        {
            _document = SvgService.Open(path);
            SvgView.Path = path;
        }
        else
        {
            _document = null;
            SvgView.Path = null;
            Console.WriteLine($"SVG document '{path}' not found.");
        }

        if (SvgView.SkSvg is { } skSvg2)
            skSvg2.OnDraw += SvgView_OnDraw;
    }

    private async void OpenMenuItem_Click(object? sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Filters = new()
            {
                new FileDialogFilter { Name = "Svg", Extensions = { "svg", "svgz" } },
                new FileDialogFilter { Name = "All", Extensions = { "*" } }
            }
        };
        var result = await dialog.ShowAsync(this);
        var file = result?.FirstOrDefault();
        if (!string.IsNullOrEmpty(file))
        {
            LoadDocument(file);
            Properties.Clear();
            _selectedDrawable = null;
            _selectedElement = null;
            SvgView.InvalidateVisual();
        }
    }

    private async void SaveMenuItem_Click(object? sender, RoutedEventArgs e)
    {
        if (_document is null)
            return;
        var dialog = new SaveFileDialog
        {
            Filters = new()
            {
                new FileDialogFilter { Name = "Svg", Extensions = { "svg" } },
                new FileDialogFilter { Name = "All", Extensions = { "*" } }
            },
            DefaultExtension = "svg"
        };
        var path = await dialog.ShowAsync(this);
        if (!string.IsNullOrEmpty(path))
        {
            _document.Write(path);
        }
    }

    private void LoadProperties(SvgVisualElement element)
    {
        Properties.Clear();
        var props = element.GetType()
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.CanRead && p.CanWrite && p.GetCustomAttribute<SvgAttributeAttribute>() != null);
        foreach (var prop in props)
        {
            var converter = TypeDescriptor.GetConverter(prop.PropertyType);
            var value = prop.GetValue(element);
            string? str = null;
            if (value is not null)
            {
                try
                {
                    str = converter.ConvertToInvariantString(value);
                }
                catch (Exception)
                {
                    str = value.ToString();
                }
            }
            Properties.Add(new PropertyEntry(prop.GetCustomAttribute<SvgAttributeAttribute>()!.Name, prop, str));
        }
    }

    private void SvgView_OnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        var point = e.GetPosition(SvgView);
        if (SvgView.SkSvg is { } skSvg && SvgView.TryGetPicturePoint(point, out var pp))
        {
            _selectedDrawable = skSvg.HitTestDrawables(pp).FirstOrDefault();
            _selectedElement = skSvg.HitTestElements(pp).OfType<SvgVisualElement>().FirstOrDefault();
            if (_selectedElement is { })
            {
                LoadProperties(_selectedElement);
            }
            SvgView.InvalidateVisual();
        }
    }

    private void ApplyButton_OnClick(object? sender, RoutedEventArgs e)
    {
        if (_selectedElement is null || _document is null)
            return;
        foreach (var entry in Properties)
        {
            entry.Apply(_selectedElement);
        }
        SvgView.SkSvg!.FromSvgDocument(_document);
        Properties.Clear();
        _selectedDrawable = null;
        _selectedElement = null;
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

    public class PropertyEntry
    {
        public string Name { get; }
        public PropertyInfo Property { get; }
        public string? Value { get; set; }

        private readonly TypeConverter _converter;

        public PropertyEntry(string name, PropertyInfo property, string? value)
        {
            Name = name;
            Property = property;
            _converter = TypeDescriptor.GetConverter(property.PropertyType);
            Value = value;
        }

        public void Apply(object target)
        {
            try
            {
                var converted = _converter.ConvertFromInvariantString(Value);
                Property.SetValue(target, converted);
            }
            catch
            {
            }
        }
    }
}
