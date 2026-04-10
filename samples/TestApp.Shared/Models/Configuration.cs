using System.Collections.Generic;

namespace TestApp.Models;

public sealed class Configuration
{
    public List<string>? Paths { get; set; }
    public string? Query { get; set; }
}
