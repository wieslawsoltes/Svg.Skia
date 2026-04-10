using System;

namespace Svg.Skia;

public sealed class SvgAnimationClockChangedEventArgs : EventArgs
{
    internal SvgAnimationClockChangedEventArgs(TimeSpan time)
    {
        Time = time;
    }

    public TimeSpan Time { get; }
}

public sealed class SvgAnimationClock
{
    private TimeSpan _currentTime;

    public TimeSpan CurrentTime => _currentTime;

    public event EventHandler<SvgAnimationClockChangedEventArgs>? TimeChanged;

    public void Reset()
    {
        Seek(TimeSpan.Zero);
    }

    public void Seek(TimeSpan time)
    {
        if (time < TimeSpan.Zero)
        {
            time = TimeSpan.Zero;
        }

        if (_currentTime == time)
        {
            return;
        }

        _currentTime = time;
        TimeChanged?.Invoke(this, new SvgAnimationClockChangedEventArgs(time));
    }

    public void AdvanceBy(TimeSpan delta)
    {
        TimeSpan next;
        if (delta >= TimeSpan.Zero)
        {
            next = delta >= TimeSpan.MaxValue - _currentTime
                ? TimeSpan.MaxValue
                : _currentTime + delta;
        }
        else
        {
            next = delta <= -_currentTime
                ? TimeSpan.Zero
                : _currentTime + delta;
        }

        Seek(next);
    }
}
