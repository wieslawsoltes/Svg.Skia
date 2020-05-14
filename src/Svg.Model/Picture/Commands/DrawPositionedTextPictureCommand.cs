namespace Svg.Model
{
    public class DrawPositionedTextPictureCommand : PictureCommand
    {
        public string Text;
        public Point[] Points;
        public Paint Paint;

        public DrawPositionedTextPictureCommand(string text, Point[] points, Paint paint)
        {
            Text = text;
            Points = points;
            Paint = paint;
        }
    }
}
