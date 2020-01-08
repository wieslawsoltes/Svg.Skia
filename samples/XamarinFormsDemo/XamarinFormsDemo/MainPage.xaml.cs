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
        private List<SKSvg> Svgs = new List<SKSvg>();

        public MainPage()
        {
            InitializeComponent();

            try
            {
                var assembly = typeof(MainPage).GetTypeInfo().Assembly;
                var resourceNames = assembly.GetManifestResourceNames();
                foreach (var name in resourceNames)
                {
                    using (var stream = assembly.GetManifestResourceStream(name))
                    {
                        var svg = new SKSvg();
                        svg.Load(stream);
                        Svgs.Add(svg);
                    }
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
            canvas.DrawPicture(Svgs[1].Picture);
        }
    }
}
