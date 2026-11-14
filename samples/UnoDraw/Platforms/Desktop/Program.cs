using Uno.UI.Hosting;

namespace UnoDraw;

internal static class Program
{
    [STAThread]
    private static void Main(string[] args)
    {
        try
        {
            App.McpStartup = McpStartupArguments.Parse(args, Environment.GetEnvironmentVariable("UNODRAW_MCP_TOKEN"));
        }
        catch (ArgumentException error)
        {
            Console.Error.WriteLine(error.Message);
            Environment.ExitCode = 2;
            return;
        }
        if (App.McpStartup?.Mode == Svg.Editor.Skia.Uno.EditorMcpConnectionMode.StandardInputOutput)
        {
            App.McpStandardInput = Console.OpenStandardInput();
            App.McpStandardOutput = Console.OpenStandardOutput();
            // The MCP stream contains only protocol messages; host logs use stderr.
            Console.SetOut(Console.Error);
        }
        App.StartupFile = args.FirstOrDefault(argument =>
            argument.EndsWith(".svg", StringComparison.OrdinalIgnoreCase) ||
            argument.EndsWith(".svgz", StringComparison.OrdinalIgnoreCase));
        var host = UnoPlatformHostBuilder.Create()
            .App(() => new App())
            .UseX11()
            .UseLinuxFrameBuffer()
            .UseMacOS()
            .UseWin32()
            .Build();
        host.Run();
    }
}
