namespace Svg.Picture
{
    public class AddRectPathCommand : PathCommand
    {
        public Rect Rect { get; set; }

        public AddRectPathCommand(Rect rect)
        {
            Rect = rect;
        }
    }
}
