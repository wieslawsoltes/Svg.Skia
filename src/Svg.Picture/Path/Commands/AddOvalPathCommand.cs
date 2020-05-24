namespace Svg.Picture
{
    public class AddOvalPathCommand : PathCommand
    {
        public Rect Rect { get; set; }

        public AddOvalPathCommand(Rect rect)
        {
            Rect = rect;
        }
    }
}
