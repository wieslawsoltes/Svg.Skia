namespace Svg.Model
{
    public class RestorePictureCommand : PictureCommand
    {
        public int Count;

        public RestorePictureCommand(int count)
        {
            Count = count;
        }
    }
}
