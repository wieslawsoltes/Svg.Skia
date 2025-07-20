using System;
using System.IO;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Skia;
using Avalonia.Markup.Xaml;
using Svg;
using Svg.Skia;

namespace AnimationSample;

public partial class MainWindow : Window
{
    private readonly SKSvg _svg;
    private readonly SKSvgAnimator _animator;

    public MainWindow()
    {
        InitializeComponent();
#if DEBUG
        this.AttachDevTools();
#endif
        var pictureControl = this.FindControl<SKPictureControl>("PictureControl");
        var path = Path.Combine(AppContext.BaseDirectory, "Assets", "anim.svg");
        var doc = SvgDocument.Open<SvgDocument>(path);
        _svg = new SKSvg();
        _svg.FromSvgDocument(doc);
        pictureControl.Picture = _svg.Picture;

        _animator = new SKSvgAnimator(_svg, doc);
        _animator.Updated += (_, _) =>
        {
            pictureControl.Picture = _svg.Picture;
            pictureControl.InvalidateVisual();
        };
        _animator.Start();
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }
}
