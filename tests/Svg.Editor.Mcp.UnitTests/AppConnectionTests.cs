using System.Text.Json.Nodes;
using Svg.Editor.Skia.Uno;
using UnoDraw;
using Xunit;

namespace Svg.Editor.Mcp.UnitTests;

public class AppConnectionTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task StopAndDisposeWaitForStdioRequestToUnwind(bool dispose)
    {
        var target = new Target();
        var canceled = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var drained = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        await using var connection = new UnoDrawMcpConnection(_ => target)
        {
            RunStandardInputOutput = async (_, cancellationToken) =>
            {
                using var registration = cancellationToken.Register(() => canceled.TrySetResult());
                await drained.Task;
                cancellationToken.ThrowIfCancellationRequested();
            }
        };

        await connection.StartAsync(new(EditorMcpConnectionMode.StandardInputOutput));
        var stop = dispose ? connection.DisposeAsync().AsTask() : connection.StopAsync();
        try
        {
            await canceled.Task.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.True(target.Disposed);
            Assert.False(stop.IsCompleted);
        }
        finally
        {
            drained.TrySetResult();
        }
        await stop.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.False(connection.State.IsConnected);
    }

    [Fact]
    public async Task RestartCannotCreateTargetUntilPreviousStdioRunDrains()
    {
        var targets = new List<Target>();
        var drained = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var canceled = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        await using var connection = new UnoDrawMcpConnection(_ =>
        {
            var target = new Target();
            targets.Add(target);
            return target;
        })
        {
            RunStandardInputOutput = async (_, cancellationToken) =>
            {
                using var registration = cancellationToken.Register(() => canceled.TrySetResult());
                await drained.Task;
            }
        };
        var settings = new EditorMcpConnectionSettings(EditorMcpConnectionMode.StandardInputOutput);
        await connection.StartAsync(settings);
        var restart = connection.StartAsync(settings);
        try
        {
            await canceled.Task.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.Single(targets);
            Assert.True(targets[0].Disposed);
            Assert.False(restart.IsCompleted);
        }
        finally
        {
            drained.TrySetResult();
        }
        await restart.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal(2, targets.Count);
    }

    [Fact]
    public async Task StopCancelsAnInProgressHostStartBeforeWaitingForGate()
    {
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var target = new Target();
        await using var connection = new UnoDrawMcpConnection(_ => target)
        {
            StartLocalHost = async (_, _, cancellationToken) =>
            {
                started.SetResult();
                await Task.Delay(Timeout.Infinite, cancellationToken);
                throw new InvalidOperationException("The canceled startup must not complete.");
            }
        };
        var start = connection.StartAsync(new(EditorMcpConnectionMode.LocalServer));
        await started.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await connection.StopAsync().WaitAsync(TimeSpan.FromSeconds(5));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => start);
        Assert.True(target.Disposed);
        Assert.False(connection.State.IsConnected);
    }

    private sealed class Target : IEditorMcpTarget, IDisposable
    {
        public bool Disposed { get; private set; }
        public event Action<string>? ResourceChanged { add { } remove { } }
        public ValueTask<JsonNode?> InvokeAsync(string operation, JsonObject arguments, CancellationToken cancellationToken)
            => ValueTask.FromResult<JsonNode?>(null);
        public void Dispose() => Disposed = true;
    }
}
