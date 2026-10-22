using System.IO.Compression;
using System.Text;
using System.Text.Json;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using SkiaSharp;
using Svg.Editor.Svg;
using Svg.Skia;
using Windows.ApplicationModel.DataTransfer;
using Windows.Storage;
using Windows.Storage.Pickers;

namespace Svg.Editor.Skia.Uno;

public sealed partial class SvgEditorWorkspace
{
    private static readonly string RecoveryDirectory = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "UnoDraw", "Recovery");
    private bool _writingRecovery;

    private void InitializePicker(object picker)
    {
        if (HostWindow is { } window)
            WinRT.Interop.InitializeWithWindow.Initialize(picker, WinRT.Interop.WindowNative.GetWindowHandle(window));
    }

    public async Task OpenPathAsync(string path, bool isExample = false)
    {
        try
        {
            var file = await StorageFile.GetFileFromPathAsync(Path.GetFullPath(path));
            await OpenFileAsync(file, isExample);
        }
        catch (Exception exception)
        {
            await ShowErrorAsync($"Could not open {Path.GetFileName(path)}.\n{exception.Message}");
        }
    }

    private async Task OpenPickerAsync()
    {
        var picker = new FileOpenPicker { SuggestedStartLocation = PickerLocationId.DocumentsLibrary };
        picker.FileTypeFilter.Add(".svg");
        picker.FileTypeFilter.Add(".svgz");
        InitializePicker(picker);
        foreach (var file in await picker.PickMultipleFilesAsync())
            await OpenFileAsync(file);
    }

    private async Task OpenFileAsync(StorageFile file, bool isExample = false)
    {
        var existing = Documents.FirstOrDefault(document => document.File?.Path == file.Path && !string.IsNullOrEmpty(file.Path));
        if (existing is not null)
        {
            DocumentTabs.SelectedItem = DocumentTabs.TabItems.OfType<TabViewItem>().First(tab => ReferenceEquals(tab.Tag, existing));
            return;
        }
        StatusText.Text = $"Opening {file.Name}…";
        using var stream = await file.OpenStreamForReadAsync();
        using var expanded = file.FileType.Equals(".svgz", StringComparison.OrdinalIgnoreCase)
            ? new GZipStream(stream, CompressionMode.Decompress, leaveOpen: true) : null;
        using var reader = new StreamReader(expanded ?? stream);
        var xml = await reader.ReadToEndAsync();
        var document = new EditorDocument { Name = isExample ? "Welcome.svg" : file.Name, File = isExample ? null : file };
        try
        {
            await Task.Run(() => document.Controller.LoadSvg(xml, file.Path));
            if (isExample)
                document.Controller.Session.CurrentFile = null;
            AddDocument(document);
        }
        catch
        {
            document.Dispose();
            throw;
        }
    }

    private async Task<bool> SaveAsync(EditorDocument document, bool saveAs = false)
    {
        if (ReferenceEquals(document, _current))
            CommitInspectorEdits();
        await document.SaveGate.WaitAsync();
        try
        {
            return await SaveCoreAsync(document, saveAs);
        }
        finally
        {
            document.SaveGate.Release();
        }
    }

    private async Task<bool> SaveCoreAsync(EditorDocument document, bool saveAs)
    {
        var file = document.File;
        if (file is null || saveAs)
        {
            var picker = new FileSavePicker
            {
                SuggestedFileName = Path.GetFileNameWithoutExtension(document.Name),
                DefaultFileExtension = ".svg",
                SuggestedStartLocation = PickerLocationId.DocumentsLibrary
            };
            picker.FileTypeChoices.Add("SVG document", new List<string> { ".svg" });
            picker.FileTypeChoices.Add("Compressed SVG", new List<string> { ".svgz" });
            InitializePicker(picker);
            file = await picker.PickSaveFileAsync();
            if (file is null)
                return false;
        }
        var xml = document.Controller.GetSvg();
        if (document.Controller.Document?.BaseUri is { } originalUri && !string.IsNullOrEmpty(file.Path))
            xml = SvgResourcePathService.Rebase(xml, originalUri, new Uri(Path.GetFullPath(file.Path)));
        using var data = new MemoryStream();
        if (file.FileType.Equals(".svgz", StringComparison.OrdinalIgnoreCase))
        {
            using var gzip = new GZipStream(data, CompressionLevel.Optimal, leaveOpen: true);
            using var writer = new StreamWriter(gzip, new UTF8Encoding(false));
            await writer.WriteAsync(xml);
        }
        else
        {
            var bytes = Encoding.UTF8.GetBytes(xml);
            await data.WriteAsync(bytes);
        }
        await WriteFileAsync(file, data.ToArray());
        document.File = file;
        document.Name = file.Name;
        // Rebase current/history references and accept only the snapshot that was written.
        // Newer edits made while I/O awaited must remain dirty.
        if (!string.IsNullOrEmpty(file.Path))
            document.Controller.AcceptSave(xml, file.Path);
        else if (document.Controller.GetSvg() == xml)
            document.Controller.MarkSaved();
        if (!document.Controller.IsDirty)
            DeleteRecovery(document);
        RefreshWorkspace();
        StatusText.Text = $"Saved {file.Name}";
        return true;
    }

    private static async Task WriteFileAsync(StorageFile file, byte[] bytes)
    {
        if (!string.IsNullOrEmpty(file.Path))
        {
            var temporary = file.Path + ".unodraw-" + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                await File.WriteAllBytesAsync(temporary, bytes);
                File.Move(temporary, file.Path, overwrite: true);
                return;
            }
            finally
            {
                if (File.Exists(temporary))
                    File.Delete(temporary);
            }
        }
        using var stream = await file.OpenStreamForWriteAsync();
        stream.SetLength(0);
        await stream.WriteAsync(bytes);
        await stream.FlushAsync();
    }

    private async Task<bool> ConfirmSaveAsync(EditorDocument document)
    {
        if (ReferenceEquals(document, _current))
            CommitInspectorEdits();
        // Finish an already-running save before inspecting the checkpoint or disposing its
        // controller. Close operations disable new commands while this barrier is awaited.
        await document.SaveGate.WaitAsync();
        document.SaveGate.Release();
        if (!document.Controller.IsDirty)
            return true;
        var dialog = MakeDialog($"Save {document.Name}?", new TextBlock
        {
            Text = "This document has unsaved changes. You can save it before closing.",
            TextWrapping = TextWrapping.Wrap
        }, "Save", "Cancel");
        dialog.SecondaryButtonText = "Discard changes";
        var result = await ShowDialogAsync(dialog);
        if (result == ContentDialogResult.Secondary)
            return true;
        if (result != ContentDialogResult.Primary || !await SaveAsync(document))
            return false;
        // A later edit can arrive while the save writes its snapshot.
        return !document.Controller.IsDirty || await ConfirmSaveAsync(document);
    }

    public async Task<bool> ConfirmCloseAsync()
    {
        if (_closing || _disposed)
            return _disposed;
        _closing = true;
        try
        {
            CommitInspectorEdits();
            WorkspaceRoot.IsHitTestVisible = false;
            Canvas.CancelGesture();
            foreach (var document in Documents.ToArray())
                if (!await ConfirmSaveAsync(document))
                    return false;
            foreach (var document in Documents)
                DeleteRecovery(document);
            return true;
        }
        catch (Exception exception)
        {
            await ShowErrorAsync(exception.Message);
            return false;
        }
        finally
        {
            WorkspaceRoot.IsHitTestVisible = true;
            _closing = false;
        }
    }

    private async Task ExportAsync() => await ExportAsync(false);

    private async Task ExportAsync(bool selectionOnly)
    {
        if (_current is not { } document)
            return;
        if (selectionOnly && document.Controller.SelectedElements.Count == 0)
            throw new InvalidOperationException("Select one or more objects to export.");
        var scale = NumberInput("Scale", 1, 0.01, 16);
        var transparent = new CheckBox { Content = "Transparent background", IsChecked = true };
        var content = new StackPanel { Spacing = 16, MinWidth = 300 };
        content.Children.Add(new TextBlock { Text = "SVG keeps editable vectors. PDF and XPS preserve vector output. Image formats use the scale below.", TextWrapping = TextWrapping.Wrap });
        content.Children.Add(scale);
        content.Children.Add(transparent);
        if (await ShowDialogAsync(MakeDialog("Export artwork", content, "Choose destination")) != ContentDialogResult.Primary)
            return;
        var picker = new FileSavePicker
        {
            SuggestedFileName = Path.GetFileNameWithoutExtension(document.Name),
            DefaultFileExtension = ".png",
            SuggestedStartLocation = PickerLocationId.DocumentsLibrary
        };
        foreach (var (name, extensions) in new[]
        {
            ("PNG image", new[] { ".png" }), ("JPEG image", new[] { ".jpg" }),
            ("WebP image", new[] { ".webp" }), ("PDF document", new[] { ".pdf" }),
            ("XPS document", new[] { ".xps" }), ("Editable SVG", new[] { ".svg" })
        })
            picker.FileTypeChoices.Add(name, extensions);
        InitializePicker(picker);
        var file = await picker.PickSaveFileAsync();
        if (file is null)
            return;
        var xml = selectionOnly ? document.Controller.CopySelection() : document.Controller.GetSvg();
        var originalUri = document.Controller.Document?.BaseUri;
        if (file.FileType.Equals(".svg", StringComparison.OrdinalIgnoreCase))
        {
            if (originalUri is not null && !string.IsNullOrEmpty(file.Path))
                xml = SvgResourcePathService.Rebase(xml, originalUri, new Uri(Path.GetFullPath(file.Path)));
            await WriteFileAsync(file, Encoding.UTF8.GetBytes(xml));
            return;
        }
        var factor = (float)scale.Value;
        var background = transparent.IsChecked == true ? SKColors.Transparent : SKColors.White;
        var bytes = await Task.Run(() =>
        {
            using var renderer = new SKSvg();
            renderer.Load(new MemoryStream(Encoding.UTF8.GetBytes(xml)), null, originalUri);
            using var output = new MemoryStream();
            var picture = renderer.Picture ?? throw new InvalidOperationException("The document does not contain renderable artwork.");
            var bounds = picture.CullRect;
            if (bounds.Width * factor * bounds.Height * factor > 100_000_000)
                throw new InvalidOperationException("Choose a smaller export scale; this image exceeds 100 million pixels.");
            var success = file.FileType.ToLowerInvariant() switch
            {
                ".pdf" => picture.ToPdf(output, SKColors.White, factor, factor),
                ".xps" => picture.ToXps(output, SKColors.White, factor, factor),
                _ => ExportImage(picture, output, file.FileType, background, factor)
            };
            if (!success)
                throw new InvalidOperationException("The selected export format could not be encoded on this platform.");
            return output.ToArray();
        });
        await WriteFileAsync(file, bytes);
        StatusText.Text = $"Exported {file.Name}";
    }

    private static bool ExportImage(SKPicture picture, Stream stream, string extension, SKColor background, float scale)
    {
        using var colorSpace = SKColorSpace.CreateSrgb();
        var format = extension.ToLowerInvariant() switch
        {
            ".jpg" or ".jpeg" => SKEncodedImageFormat.Jpeg,
            ".webp" => SKEncodedImageFormat.Webp,
            _ => SKEncodedImageFormat.Png
        };
        if (format == SKEncodedImageFormat.Jpeg)
            background = SKColors.White;
        return picture.ToImage(stream, background, format, 95, scale, scale, SKColorType.Rgba8888, SKAlphaType.Premul, colorSpace);
    }

    private async Task PlaceImageAsync()
    {
        var picker = new FileOpenPicker { SuggestedStartLocation = PickerLocationId.PicturesLibrary };
        foreach (var type in new[] { ".png", ".jpg", ".jpeg", ".webp", ".svg" })
            picker.FileTypeFilter.Add(type);
        InitializePicker(picker);
        if (await picker.PickSingleFileAsync() is { } file)
            await PlaceImageFileAsync(file);
    }

    private async Task PlaceImageFileAsync(StorageFile file)
    {
        if (Controller is not { } controller)
            return;
        using var input = await file.OpenStreamForReadAsync();
        using var memory = new MemoryStream();
        await input.CopyToAsync(memory);
        var bytes = memory.ToArray();
        var mime = file.FileType.ToLowerInvariant() switch
        {
            ".svg" => "image/svg+xml",
            ".jpg" or ".jpeg" => "image/jpeg",
            ".webp" => "image/webp",
            _ => "image/png"
        };
        using var bitmap = mime == "image/svg+xml" ? null : SKBitmap.Decode(bytes);
        var width = bitmap?.Width ?? 320;
        var height = bitmap?.Height ?? 240;
        var ratio = Math.Min(1f, 600f / Math.Max(width, height));
        var image = new SvgImage
        {
            X = new SvgUnit(40),
            Y = new SvgUnit(40),
            Width = new SvgUnit(width * ratio),
            Height = new SvgUnit(height * ratio),
            Href = $"data:{mime};base64,{Convert.ToBase64String(bytes)}"
        };
        controller.AddElement(image, "Place image");
        SetTool(global::Svg.Editor.Core.SvgEditorToolKind.Select);
    }

    private void Workspace_DragOver(object sender, DragEventArgs e)
    {
        if (e.DataView.Contains(StandardDataFormats.StorageItems))
        {
            e.AcceptedOperation = DataPackageOperation.Copy;
            e.DragUIOverride.Caption = "Open SVG or place image";
        }
    }

    private async void Workspace_Drop(object sender, DragEventArgs e)
    {
        try
        {
            if (!e.DataView.Contains(StandardDataFormats.StorageItems))
                return;
            foreach (var file in (await e.DataView.GetStorageItemsAsync()).OfType<StorageFile>())
            {
                if (file.FileType.Equals(".svg", StringComparison.OrdinalIgnoreCase) || file.FileType.Equals(".svgz", StringComparison.OrdinalIgnoreCase))
                    await OpenFileAsync(file);
                else if (new[] { ".png", ".jpg", ".jpeg", ".webp" }.Contains(file.FileType.ToLowerInvariant()))
                    await PlaceImageFileAsync(file);
            }
        }
        catch (Exception exception)
        {
            await ShowErrorAsync(exception.Message);
        }
    }

    private sealed record RecoveryRecord(string Name, string? Path, string Svg);

    private async Task SaveRecoveryAsync()
    {
        if (_writingRecovery || _disposed)
            return;
        _writingRecovery = true;
        try
        {
            foreach (var document in Documents.ToArray())
            {
                var controller = document.Controller;
                if (!controller.IsDirty)
                {
                    DeleteRecovery(document);
                    document.RecoveredRevision = -1;
                    continue;
                }
                if (controller.IsEditing || document.RecoveredRevision == controller.DocumentRevision)
                    continue;
                Directory.CreateDirectory(RecoveryDirectory);
                var revision = controller.DocumentRevision;
                var record = new RecoveryRecord(document.Name, document.File?.Path, controller.GetSvg());
                var path = Path.Combine(RecoveryDirectory, document.RecoveryId + ".json");
                await File.WriteAllTextAsync(path + ".tmp", JsonSerializer.Serialize(record));
                if (_disposed || !Documents.Contains(document) || !controller.IsDirty)
                {
                    File.Delete(path + ".tmp");
                    continue;
                }
                File.Move(path + ".tmp", path, true);
                document.RecoveredRevision = revision;
            }
        }
        catch (Exception exception)
        {
            StatusText.Text = $"Recovery copy could not be saved: {exception.Message}";
        }
        finally
        {
            _writingRecovery = false;
        }
    }

    private static void DeleteRecovery(EditorDocument document)
    {
        var path = Path.Combine(RecoveryDirectory, document.RecoveryId + ".json");
        if (File.Exists(path))
            File.Delete(path);
    }

    public async Task OfferRecoveryAsync()
    {
        if (!Directory.Exists(RecoveryDirectory))
            return;
        var files = Directory.GetFiles(RecoveryDirectory, "*.json");
        if (files.Length == 0)
            return;
        var dialog = MakeDialog("Recover your work", new TextBlock
        {
            Text = $"Found {files.Length} recovery document(s) from an earlier session.",
            TextWrapping = TextWrapping.Wrap
        }, "Restore", "Later");
        dialog.SecondaryButtonText = "Discard copies";
        var result = await ShowDialogAsync(dialog);
        if (result == ContentDialogResult.None)
            return;
        foreach (var file in files)
        {
            try
            {
                if (result == ContentDialogResult.Primary)
                {
                    var record = JsonSerializer.Deserialize<RecoveryRecord>(await File.ReadAllTextAsync(file));
                    if (record is null)
                        continue;
                    var document = new EditorDocument { Name = Path.GetFileNameWithoutExtension(record.Name) + " recovered.svg" };
                    if (Guid.TryParse(Path.GetFileNameWithoutExtension(file), out var recoveryId))
                        document.RecoveryId = recoveryId;
                    document.Controller.LoadSvg(record.Svg, record.Path);
                    document.Controller.MarkUnsaved();
                    // Recovery is intentionally a new document and never overwrites its source.
                    AddDocument(document);
                }
                if (result == ContentDialogResult.Secondary)
                    File.Delete(file);
            }
            catch (Exception exception)
            {
                StatusText.Text = $"Could not recover {Path.GetFileName(file)}: {exception.Message}";
            }
        }
    }
}
