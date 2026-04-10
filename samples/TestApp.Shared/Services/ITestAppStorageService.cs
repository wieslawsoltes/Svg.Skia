using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace TestApp.Services;

public interface ITestAppStorageService
{
    Task<Stream?> OpenConfigurationReadStreamAsync(CancellationToken cancellationToken = default);

    Task<Stream?> OpenConfigurationWriteStreamAsync(string suggestedFileName, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<string>> PickSvgPathsAsync(CancellationToken cancellationToken = default);

    Task<TestAppSaveStreamResult?> OpenExportWriteStreamAsync(string suggestedFileName, CancellationToken cancellationToken = default);
}

public sealed class TestAppSaveStreamResult
{
    public TestAppSaveStreamResult(Stream stream, string name)
    {
        Stream = stream;
        Name = name;
    }

    public Stream Stream { get; }

    public string Name { get; }
}

internal sealed class NullTestAppStorageService : ITestAppStorageService
{
    public static NullTestAppStorageService Instance { get; } = new();

    public Task<Stream?> OpenConfigurationReadStreamAsync(CancellationToken cancellationToken = default)
        => Task.FromResult<Stream?>(null);

    public Task<Stream?> OpenConfigurationWriteStreamAsync(string suggestedFileName, CancellationToken cancellationToken = default)
        => Task.FromResult<Stream?>(null);

    public Task<IReadOnlyList<string>> PickSvgPathsAsync(CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<string>>(System.Array.Empty<string>());

    public Task<TestAppSaveStreamResult?> OpenExportWriteStreamAsync(string suggestedFileName, CancellationToken cancellationToken = default)
        => Task.FromResult<TestAppSaveStreamResult?>(null);
}
