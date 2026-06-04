using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;
using BenchmarkDotNet.Running;

namespace Svg.Skia.Benchmarks;

public static class Program
{
    public static void Main(string[] args)
    {
        var artifactsPath = SvgBenchmarkConfig.ResolveArtifactsPath(args);

        using var runGate = SvgBenchmarkRunGate.Enter();
        Console.WriteLine($"Svg.Skia benchmark lock: {runGate.LockPath}");
        Console.WriteLine($"Svg.Skia benchmark artifacts: {artifactsPath}");
        Console.WriteLine();

        if (SvgLoadPipelineProfiler.TryRun(args, artifactsPath))
        {
            return;
        }

        BenchmarkSwitcher.FromAssembly(typeof(Program).Assembly).Run(args, new SvgBenchmarkConfig(artifactsPath));
    }
}

internal sealed class SvgBenchmarkRunGate : IDisposable
{
    private const string LockPathEnvironmentVariable = "SVG_SKIA_BENCHMARK_LOCK_PATH";
    private static readonly TimeSpan WaitMessageInterval = TimeSpan.FromSeconds(10);
    private readonly FileStream _lockStream;

    private SvgBenchmarkRunGate(string lockPath, FileStream lockStream)
    {
        LockPath = lockPath;
        _lockStream = lockStream;
    }

    public string LockPath { get; }

    public static SvgBenchmarkRunGate Enter()
    {
        var lockPath = ResolveLockPath();
        var lockDirectory = Path.GetDirectoryName(lockPath);
        if (!string.IsNullOrEmpty(lockDirectory))
        {
            Directory.CreateDirectory(lockDirectory);
        }

        var lastMessage = DateTimeOffset.MinValue;
        while (true)
        {
            try
            {
                var lockStream = new FileStream(lockPath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
                WriteLockOwner(lockStream);
                return new SvgBenchmarkRunGate(lockPath, lockStream);
            }
            catch (IOException)
            {
                var now = DateTimeOffset.UtcNow;
                if (now - lastMessage >= WaitMessageInterval)
                {
                    Console.WriteLine($"Waiting for Svg.Skia benchmark lock: {lockPath}");
                    lastMessage = now;
                }

                Thread.Sleep(TimeSpan.FromSeconds(1));
            }
        }
    }

    public void Dispose()
    {
        _lockStream.Dispose();
    }

    private static string ResolveLockPath()
    {
        var lockPath = Environment.GetEnvironmentVariable(LockPathEnvironmentVariable);
        if (string.IsNullOrWhiteSpace(lockPath))
        {
            lockPath = Path.Combine(Path.GetTempPath(), "svg-skia-benchmark.lock");
        }

        return Path.GetFullPath(lockPath);
    }

    private static void WriteLockOwner(FileStream lockStream)
    {
        lockStream.SetLength(0);
        using var writer = new StreamWriter(lockStream, Encoding.UTF8, 1024, leaveOpen: true);
        writer.WriteLine($"pid={Environment.ProcessId}");
        writer.WriteLine($"process={Process.GetCurrentProcess().ProcessName}");
        writer.WriteLine($"startedUtc={DateTimeOffset.UtcNow:O}");
        writer.Flush();
        lockStream.Position = 0;
    }
}
