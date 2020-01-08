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
    public class Item
    {
        public string Name { get; set; }
        public SKSvg Svg { get; set; }
        public SKPicture Picture { get; set; }
    }

    [DesignTimeVisible(false)]
    public partial class MainPage : ContentPage
    {
        public List<Item> Items { get; set; }

        public MainPage()
        {
            InitializeComponent();

            try
            {
                Items = new List<Item>();
                var assembly = typeof(MainPage).GetTypeInfo().Assembly;
                var resourceNames = assembly.GetManifestResourceNames();
                foreach (var name in resourceNames)
                {
                    using (var stream = assembly.GetManifestResourceStream(name))
                    {
                        var item = new Item()
                        {
                            Name = name,
                            Svg = new SKSvg()
                        };
                        item.Picture = item.Svg.Load(stream);
                        Items.Add(item);
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex.Message);
                Debug.WriteLine(ex.StackTrace);
            }

            BindingContext = this;

            picker.SelectedItem = Items.FirstOrDefault();
            picker.SelectedIndexChanged += Picker_SelectedIndexChanged;
        }

        private void Picker_SelectedIndexChanged(object sender, EventArgs e)
        {
            skiaView.InvalidateSurface();
        }

        private void OnPaintSurface(object sender, SKPaintSurfaceEventArgs e)
        {
            var canvas = e.Surface.Canvas;
            var scale = (float)(e.Info.Width / skiaView.Width);
            canvas.Scale(scale);
            canvas.Clear(SKColors.White);
            if (Items.Count > 0 && picker.SelectedIndex >= 0)
            {
                canvas.DrawPicture(Items[picker.SelectedIndex].Picture);
            }
        }
    }
}
