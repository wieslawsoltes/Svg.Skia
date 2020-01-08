using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using SkiaSharp;
using SkiaSharp.Views.Forms;
using Svg.Skia;
using Xamarin.Forms;

namespace XamarinFormsDemo
{
    // Learn more about making custom code visible in the Xamarin.Forms previewer
    // by visiting https://aka.ms/xamarinforms-previewer
    [DesignTimeVisible(false)]
    public partial class MainPage : ContentPage
    {
        private SKSvg _svg = new SKSvg();

        public MainPage()
        {
            InitializeComponent();

            try
            {
                var assembly = typeof(MainPage).GetTypeInfo().Assembly;
                //var resourceNames = assembly.GetManifestResourceNames();
                using (var stream = assembly.GetManifestResourceStream("XamarinFormsDemo.__tiger.svg"))
                { 
                    _svg = new SKSvg();
                    _svg.Load(stream);
                }
            }
            catch (Exception ex)
            {
            }
        }

        private void OnPaintSurface(object sender, SKPaintSurfaceEventArgs e)
        {
            var canvas = e.Surface.Canvas;
            var scale = (float)(e.Info.Width / skiaView.Width);
            canvas.Scale(scale);
            canvas.Clear(SKColors.White);
            canvas.DrawPicture(_svg.Picture);
        }
    }
}
