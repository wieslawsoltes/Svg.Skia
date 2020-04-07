using System;

namespace SvgValidated
{
    public interface ISvgClipable
    {
        Uri ClipPath { get; set; }
        SvgClipRule ClipRule { get; set; }
    }
}