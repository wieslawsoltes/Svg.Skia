using System;

namespace SvgAstPlayground.ViewModels;

public sealed class TextSelectionRequestedEventArgs : EventArgs
{
    public TextSelectionRequestedEventArgs(int start, int length)
    {
        Start = start;
        Length = length;
    }

    public int Start { get; }

    public int Length { get; }
}
