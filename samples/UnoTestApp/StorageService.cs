using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using TestApp.Services;
using Windows.Storage;
using Windows.Storage.Pickers;

namespace UnoTestApp;

internal sealed class StorageService : ITestAppStorageService
{
    public async Task<Stream?> OpenConfigurationReadStreamAsync(CancellationToken cancellationToken = default)
    {
        var picker = new FileOpenPicker();
        picker.FileTypeFilter.Add(".json");

        var file = await picker.PickSingleFileAsync();
        return file is null ? null : await file.OpenStreamForReadAsync();
    }

    public async Task<Stream?> OpenConfigurationWriteStreamAsync(string suggestedFileName, CancellationToken cancellationToken = default)
    {
        var picker = new FileSavePicker
        {
            SuggestedFileName = Path.GetFileNameWithoutExtension(suggestedFileName),
            SuggestedStartLocation = PickerLocationId.DocumentsLibrary,
            DefaultFileExtension = ".json"
        };
        picker.FileTypeChoices.Add("Json", new List<string> { ".json" });

        var file = await picker.PickSaveFileAsync();
        return file is null ? null : await file.OpenStreamForWriteAsync();
    }

    public async Task<IReadOnlyList<string>> PickSvgPathsAsync(CancellationToken cancellationToken = default)
    {
        var picker = new FileOpenPicker();
        picker.FileTypeFilter.Add(".svg");
        picker.FileTypeFilter.Add(".svgz");

        var files = await picker.PickMultipleFilesAsync();
        return files.Select(file => file.Path).ToList();
    }

    public async Task<TestAppSaveStreamResult?> OpenExportWriteStreamAsync(string suggestedFileName, CancellationToken cancellationToken = default)
    {
        var picker = new FileSavePicker
        {
            SuggestedFileName = Path.GetFileNameWithoutExtension(suggestedFileName),
            SuggestedStartLocation = PickerLocationId.DocumentsLibrary,
            DefaultFileExtension = ".png"
        };
        picker.FileTypeChoices.Add("PNG image", new List<string> { ".png" });
        picker.FileTypeChoices.Add("JPEG image", new List<string> { ".jpg", ".jpeg" });
        picker.FileTypeChoices.Add("C#", new List<string> { ".cs" });
        picker.FileTypeChoices.Add("PDF document", new List<string> { ".pdf" });
        picker.FileTypeChoices.Add("XPS document", new List<string> { ".xps" });

        var file = await picker.PickSaveFileAsync();
        return file is null ? null : new TestAppSaveStreamResult(await file.OpenStreamForWriteAsync(), file.Name);
    }
}
