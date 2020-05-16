namespace Svg.Model
{
    public class SavePictureCommand : PictureCommand
    {
        public int Count;

        public SavePictureCommand(int count)
        {
            Count = count;
        }
    }
}
