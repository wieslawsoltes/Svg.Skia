using CodeGenerator;

var repoRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));

string ResolveGeneratedPath(string projectName) => Path.Combine(repoRoot, "src", projectName, "Generated");

if (args.Length == 0)
{
    return;
}

var target = args[0];

switch (target)
{
    case "avalonia":
        {
            var settings = new GeneratorSettings
            {
                ElementBaseType = "Avalonia.Controls.Control",
                BaseWriterType = "element",
                BasePath = ResolveGeneratedPath("SvgML.Avalonia"),
                TypeDefs = SvgTypeDefs.TypeDefs
            };

            new AvaloniaGenerator(settings).Generate();

            break;
        }

    case "maui":
        {
            var settings = new GeneratorSettings
            {
                ElementBaseType = "SkiaSharp.Views.Maui.Controls.SKCanvasView",
                BaseWriterType = "element",
                BasePath = ResolveGeneratedPath("SvgML.Maui"),
                TypeDefs = SvgTypeDefs.TypeDefs
            };

            new MauiGenerator(settings).Generate();

            break;
        }

    case "uno":
        {
            var settings = new GeneratorSettings
            {
                ElementBaseType = "Uno.WinUI.Graphics2DSK.SKCanvasElement",
                BaseWriterType = "element",
                BasePath = ResolveGeneratedPath("SvgML.Uno"),
                TypeDefs = SvgTypeDefs.TypeDefs
            };

            new UnoGenerator(settings).Generate();

            break;
        }
}
