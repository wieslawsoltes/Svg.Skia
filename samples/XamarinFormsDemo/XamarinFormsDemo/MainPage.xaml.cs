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
        private Assembly assembly;

        public List<Item> Items { get; set; }

        public MainPage()
        {
            InitializeComponent();

            Items = new List<Item>();

            assembly = typeof(MainPage).GetTypeInfo().Assembly;

            var resourceNames = assembly.GetManifestResourceNames();
            foreach (var name in resourceNames)
            {
                var item = new Item()
                {
                    Name = name,
                    Svg = new SKSvg()
                };
                Items.Add(item);
            }

            BindingContext = this;

            listView.SelectedItem = Items.FirstOrDefault();
            listView.ItemSelected += ListView_ItemSelected;
        }

        private void ListView_ItemSelected(object sender, SelectedItemChangedEventArgs e)
        {
            try
            {
                if (listView.SelectedItem is Item item)
                {
                    var picture = item.Picture;
                    if (picture == null)
                    {
                        using (var stream = assembly.GetManifestResourceStream(item.Name))
                        {
                            item.Picture = item.Svg.Load(stream);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex.Message);
                Debug.WriteLine(ex.StackTrace);
            }
            skiaView.InvalidateSurface();
        }

        private void OnPaintSurface(object sender, SKPaintSurfaceEventArgs e)
        {
            var canvas = e.Surface.Canvas;
            var scale = (float)(e.Info.Width / skiaView.Width);
            canvas.Scale(scale);
            canvas.Clear(SKColors.White);
            if (Items.Count > 0 && listView.SelectedItem is Item item)
            {
                var picture = item.Picture;
                if (picture != null)
                {
                    canvas.DrawPicture(picture);
                }
            }
        }
    }
}
