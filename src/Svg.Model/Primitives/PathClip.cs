namespace Svg.Model.Primitives
{
    public class PathClip
    {
        public Path? Path { get; set; }
        public Matrix? Transform { get; set; }
        public ClipPath? Clip { get; set; }
    }
}
