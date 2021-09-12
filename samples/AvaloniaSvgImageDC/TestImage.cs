using System.Text;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Svg;

namespace AvaloniaSvgImageDC
{
    public partial class TestImage : Control
    {
        private readonly SvgImage img;

        public TestImage()
        {
            string data = @"
<svg xmlns='http://www.w3.org/2000/svg' viewBox='-40 -40 80 80'>
        <circle r='39'/>
            <path fill='#fff' d='M0,38a38,38 0 0 1 0,-76a19,19 0 0 1 0,38a19,19 0 0 0 0,38'/>
        <circle r='5' cy='19' fill='#fff'/>
        <circle r='5' cy='-19'/>
        </svg>
";
            var tmp = System.IO.Path.GetTempFileName();
            System.IO.File.WriteAllBytes(tmp, Encoding.ASCII.GetBytes(data));
            img = new SvgImage();
            img.Source = SvgSource.Load(tmp, null);
        }

        public override void Render(DrawingContext context)
        {
            base.Render(context);

            context.DrawImage(img, new Rect(new Point(0, 0), img.Size));
        }
    }
}
