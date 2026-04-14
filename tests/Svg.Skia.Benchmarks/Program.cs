using BenchmarkDotNet.Running;

namespace Svg.Skia.Benchmarks;

public static class Program
{
    public static void Main(string[] args)
    {
        if (SvgLoadPipelineProfiler.TryRun(args))
        {
            return;
        }

        BenchmarkSwitcher.FromAssembly(typeof(Program).Assembly).Run(args, new SvgBenchmarkConfig());
    }
}
