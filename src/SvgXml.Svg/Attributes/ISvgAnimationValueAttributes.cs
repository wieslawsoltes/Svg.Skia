namespace Svg
{
    public interface ISvgAnimationValueAttributes
    {
        string? CalcMode { get; set; }
        string? Values { get; set; }
        string? KeyTimes { get; set; }
        string? KeySplines { get; set; }
        string? From { get; set; }
        string? To { get; set; }
        string? By { get; set; }
    }
}
