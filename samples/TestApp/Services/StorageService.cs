using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Platform.Storage;
using Avalonia.VisualTree;

namespace TestApp.Services;

internal sealed class StorageService : ITestAppStorageService
{
    private static FilePickerFileType All { get; } = new("All")
    {
        Patterns = new[] { "*.*" },
        MimeTypes = new[] { "*/*" }
    };

    private static FilePickerFileType Json { get; } = new("Json")
    {
        Patterns = new[] { "*.json" },
        AppleUniformTypeIdentifiers = new[] { "public.json" },
        MimeTypes = new[] { "application/json" }
    };

    private static FilePickerFileType CSharp { get; } = new("C#")
    {
        Patterns = new[] { "*.cs" },
        AppleUniformTypeIdentifiers = new[] { "public.csharp-source" },
        MimeTypes = new[] { "text/plain" }
    };

    private static FilePickerFileType ImagePng { get; } = new("PNG image")
    {
        Patterns = new[] { "*.png" },
        AppleUniformTypeIdentifiers = new[] { "public.png" },
        MimeTypes = new[] { "image/png" }
    };

    private static FilePickerFileType ImageJpg { get; } = new("JPEG image")
    {
        Patterns = new[] { "*.jpg", "*.jpeg" },
        AppleUniformTypeIdentifiers = new[] { "public.jpeg" },
        MimeTypes = new[] { "image/jpeg" }
    };

    private static FilePickerFileType Pdf { get; } = new("PDF document")
    {
        Patterns = new[] { "*.pdf" },
        AppleUniformTypeIdentifiers = new[] { "com.adobe.pdf" },
        MimeTypes = new[] { "application/pdf" }
    };

    private static FilePickerFileType Xps { get; } = new("XPS document")
    {
        Patterns = new[] { "*.xps" },
        AppleUniformTypeIdentifiers = new[] { "com.microsoft.xps" },
        MimeTypes = new[] { "application/oxps", "application/vnd.ms-xpsdocument" }
    };

    public async Task<Stream?> OpenConfigurationReadStreamAsync(CancellationToken cancellationToken = default)
    {
        var storageProvider = GetStorageProvider();
        if (storageProvider is null)
        {
            return null;
        }

        var result = await storageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Open configuration",
            FileTypeFilter = new[] { Json, All },
            AllowMultiple = false
        });

        var file = result.FirstOrDefault();
        return file is null ? null : await file.OpenReadAsync();
    }

    public async Task<Stream?> OpenConfigurationWriteStreamAsync(string suggestedFileName, CancellationToken cancellationToken = default)
    {
        var storageProvider = GetStorageProvider();
        if (storageProvider is null)
        {
            return null;
        }

        var file = await storageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Save configuration",
            FileTypeChoices = new[] { Json, All },
            SuggestedFileName = Path.GetFileNameWithoutExtension(suggestedFileName),
            DefaultExtension = "json",
            ShowOverwritePrompt = true
        });

        return file is null ? null : await file.OpenWriteAsync();
    }

    public async Task<IReadOnlyList<string>> PickSvgPathsAsync(CancellationToken cancellationToken = default)
    {
        var storageProvider = GetStorageProvider();
        if (storageProvider is null)
        {
            return Array.Empty<string>();
        }

        var files = await storageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Open svg files",
            AllowMultiple = true,
            FileTypeFilter = new[]
            {
                new FilePickerFileType("Svg Files")
                {
                    Patterns = new[] { "*.svg", "*.svgz" },
                    AppleUniformTypeIdentifiers = new[] { "public.svg-image" },
                    MimeTypes = new[] { "image/svg+xml", "application/gzip" }
                },
                All
            }
        });

        return files
            .Select(file => file.TryGetLocalPath())
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .Cast<string>()
            .ToList();
    }

    public async Task<TestAppSaveStreamResult?> OpenExportWriteStreamAsync(string suggestedFileName, CancellationToken cancellationToken = default)
    {
        var storageProvider = GetStorageProvider();
        if (storageProvider is null)
        {
            return null;
        }

        var file = await storageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Export svg",
            FileTypeChoices = new[] { ImagePng, ImageJpg, CSharp, Pdf, Xps, All },
            SuggestedFileName = Path.GetFileNameWithoutExtension(suggestedFileName),
            DefaultExtension = "png",
            ShowOverwritePrompt = true
        });

        return file is null ? null : new TestAppSaveStreamResult(await file.OpenWriteAsync(), file.Name);
    }

    private static IStorageProvider? GetStorageProvider()
    {
        if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime { MainWindow: { } window })
        {
            return window.StorageProvider;
        }

        if (Application.Current?.ApplicationLifetime is ISingleViewApplicationLifetime { MainView: { } mainView } &&
            mainView is Visual visual)
        {
            var topLevel = TopLevel.GetTopLevel(visual);
            if (topLevel is not null)
            {
                return topLevel.StorageProvider;
            }
        }

        return null;
    }
}
