namespace Svg.Model.Primitives
{
    public class PathClip
    {
        public SKPath? Path { get; set; }
        public SKMatrix? Transform { get; set; }
        public ClipPath? Clip { get; set; }
    }
}
