namespace Svg
{
    public interface ISvgAnimationEventAttributes
    {
        string? OnBegin { get; set; }
        string? OnEnd { get; set; }
        string? OnRepeat { get; set; }
        string? OnLoad { get; set; }
    }
}
