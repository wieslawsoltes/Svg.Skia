using System.Collections.Generic;

namespace TestApp.Models
{
    public class Configuration
    {
        public List<string>? Paths { get; set; }
        public string? SelectedPath { get; set; }
        public string? Query { get; set; }
    }
}
