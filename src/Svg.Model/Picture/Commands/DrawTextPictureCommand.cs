namespace Svg.Model
{
    public class DrawTextPictureCommand : PictureCommand
    {
        public string Text;
        public float X;
        public float Y;
        public Paint Paint;

        public DrawTextPictureCommand(string text, float x, float y, Paint paint)
        {
            Text = text;
            X = x;
            Y = y;
            Paint = paint;
        }
    }
}
