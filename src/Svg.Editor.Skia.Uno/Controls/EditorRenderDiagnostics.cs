using System.Diagnostics;
using System.Threading;

namespace Svg.Editor.Skia.Uno;

/// <summary>The independently recorded editor surfaces.</summary>
public enum EditorRenderPhase
{
    Document,
    Interaction,
    Ruler,
    Navigator
}

/// <summary>Managed work inside a paint callback, excluding native rasterization and GPU presentation.</summary>
public sealed record EditorRenderMeasurement(string Phase, long Count, double TotalMilliseconds,
    double MaxMilliseconds, long AllocatedBytes);

/// <summary>A passive snapshot; reading it does not schedule an editor frame.</summary>
public sealed record EditorRenderSnapshot(bool Enabled, long Timestamp, long StopwatchFrequency,
    long ThreadAllocatedBytes, int Gen0Collections, int Gen1Collections, int Gen2Collections,
    IReadOnlyList<EditorRenderMeasurement> Phases);

/// <summary>
/// Opt-in diagnostics for browser and native profiling. Disabled in normal use.
/// Timing covers only the callback's managed/native calls; deferred composition,
/// texture transfers and GPU execution must be measured by the host separately.
/// </summary>
public static class EditorRenderDiagnostics
{
    private struct Counter
    {
        public long Count;
        public long Ticks;
        public long MaximumTicks;
        public long AllocatedBytes;
    }

    private static readonly object s_gate = new();
    private static readonly Counter[] s_counters = new Counter[4];
    private static bool s_enabled;
    private static int s_generation;

    public static bool Enabled
    {
        get => Volatile.Read(ref s_enabled);
        set => Volatile.Write(ref s_enabled, value);
    }

    /// <summary>Creates an allocation-free scope, or a no-op scope when disabled.</summary>
    public static MeasurementScope Measure(EditorRenderPhase phase)
    {
        if (!Enabled)
            return default;
        ArgumentOutOfRangeException.ThrowIfNegative((int)phase);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual((int)phase, s_counters.Length);
        return new MeasurementScope(phase, Volatile.Read(ref s_generation));
    }

    /// <summary>
    /// Copies the counters. Reset also excludes callbacks that started before the
    /// reset, so a diagnostic workload cannot inherit a partially measured frame.
    /// </summary>
    public static EditorRenderSnapshot Snapshot(bool reset = false)
    {
        lock (s_gate)
        {
            var phases = new EditorRenderMeasurement[s_counters.Length];
            for (var index = 0; index < phases.Length; index++)
            {
                ref var counter = ref s_counters[index];
                phases[index] = new EditorRenderMeasurement(((EditorRenderPhase)index).ToString(), counter.Count,
                    counter.Ticks * 1000d / Stopwatch.Frequency,
                    counter.MaximumTicks * 1000d / Stopwatch.Frequency, counter.AllocatedBytes);
            }

            var snapshot = new EditorRenderSnapshot(Enabled, Stopwatch.GetTimestamp(), Stopwatch.Frequency,
                GC.GetAllocatedBytesForCurrentThread(), GC.CollectionCount(0), GC.CollectionCount(1),
                GC.CollectionCount(2), phases);
            if (reset)
            {
                Array.Clear(s_counters);
                Interlocked.Increment(ref s_generation);
            }
            return snapshot;
        }
    }

    public readonly struct MeasurementScope : IDisposable
    {
        private readonly EditorRenderPhase _phase;
        private readonly int _generation;
        private readonly long _started;
        private readonly long _allocatedBytes;

        internal MeasurementScope(EditorRenderPhase phase, int generation)
        {
            _phase = phase;
            _generation = generation;
            _allocatedBytes = GC.GetAllocatedBytesForCurrentThread();
            _started = Stopwatch.GetTimestamp();
        }

        public void Dispose()
        {
            if (_started == 0)
                return;
            var ticks = Stopwatch.GetTimestamp() - _started;
            var allocatedBytes = GC.GetAllocatedBytesForCurrentThread() - _allocatedBytes;
            lock (s_gate)
            {
                if (_generation != s_generation)
                    return;
                ref var counter = ref s_counters[(int)_phase];
                counter.Count++;
                counter.Ticks += ticks;
                counter.MaximumTicks = Math.Max(counter.MaximumTicks, ticks);
                counter.AllocatedBytes += allocatedBytes;
            }
        }
    }
}
