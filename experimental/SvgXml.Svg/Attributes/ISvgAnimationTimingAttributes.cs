namespace SvgXml.Svg
{
    public interface ISvgAnimationTimingAttributes
    {
        string? Begin { get; set; }
        string? Dur { get; set; }
        string? End { get; set; }
        string? Min { get; set; }
        string? Max { get; set; }
        string? Restart { get; set; }
        string? RepeatCount { get; set; }
        string? RepeatDur { get; set; }
        string? Fill { get; set; }
    }
}
