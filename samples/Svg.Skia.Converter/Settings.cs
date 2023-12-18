using System.IO;

namespace Svg.Skia.Converter;

public class Settings
{
    public FileInfo[]? InputFiles { get; set; }
    public DirectoryInfo? InputDirectory { get; set; }
    public FileInfo[]? OutputFiles { get; set; }
    public DirectoryInfo? OutputDirectory { get; set; }
    public string? Pattern { get; set; }
    public string Format { get; set; } = "png";
    public int Quality { get; set; } = 100;
    public string Background { get; set; } = "#00FFFFFF";
    public float Scale { get; set; } = 1f;
    public float ScaleX { get; set; } = 1f;
    public float ScaleY { get; set; } = 1f;
    public string? SystemLanguage { get; set; }
    public bool Quiet { get; set; }
}
