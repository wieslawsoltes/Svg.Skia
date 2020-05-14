namespace Svg.Model
{
    public class DrawTextOnPathPictureCommand : PictureCommand
    {
        public string Text;
        public Path Path;
        public float HOffset;
        public float VOffset;
        public Paint Paint;

        public DrawTextOnPathPictureCommand(string text, Path path, float hOffset, float vOffset, Paint paint)
        {
            Text = text;
            Path = path;
            HOffset = hOffset;
            VOffset = vOffset;
            Paint = paint;
        }
    }
}
