
using CodeGenerator;

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
                BasePath = "../SvgML/Generated",
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
                BasePath = "../SvgML.Maui/Generated",
                TypeDefs = SvgTypeDefs.TypeDefs
            };

            new MauiGenerator(settings).Generate();

            break;
        }
}
