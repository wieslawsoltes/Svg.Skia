using System;

namespace Svg.Model;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct | AttributeTargets.Constructor | AttributeTargets.Method | AttributeTargets.Property | AttributeTargets.Field | AttributeTargets.Event, Inherited = false)]
public sealed class PreserveAttribute : Attribute
{
    public bool AllMembers { get; set; }
    public bool Conditional { get; set; }
}

