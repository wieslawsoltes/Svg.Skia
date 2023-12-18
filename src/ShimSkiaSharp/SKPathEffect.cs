namespace ShimSkiaSharp;

public abstract record SKPathEffect
{
    public static SKPathEffect CreateDash(float[] intervals, float phase) 
        => new DashPathEffect(intervals, phase);
}

public record DashPathEffect(float[]? Intervals, float Phase) : SKPathEffect;