namespace svgc;

internal class Settings
{
    public System.IO.FileInfo? InputFile { get; set; }
    public System.IO.FileInfo? OutputFile { get; set; }
    public System.IO.FileInfo? JsonFile { get; set; }
    public string Namespace { get; set; } = "Svg";
    public string Class { get; set; } = "Generated";
}
