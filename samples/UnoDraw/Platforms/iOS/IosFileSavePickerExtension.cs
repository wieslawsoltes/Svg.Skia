using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using CoreGraphics;
using UIKit;
using Uno.Extensions.Storage.Pickers;
using Windows.Storage;
using Windows.Storage.Pickers;

namespace UnoDraw.iOS;

/// <summary>
/// Saves through a security-scoped folder selected in Files. Native prompts can be
/// presented while an editor ContentDialog is open, including brush-library export.
/// </summary>
public sealed class IosFileSavePickerExtension : IFileSavePickerExtension
{
    private readonly FileSavePicker _picker;

    public IosFileSavePickerExtension(object owner)
    {
        _picker = owner as FileSavePicker ?? throw new ArgumentException("A file save picker is required.", nameof(owner));
    }

    public async Task<StorageFile?> PickSaveFileAsync(CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        var choices = _picker.FileTypeChoices.SelectMany(pair => pair.Value.Select(extension => new FileChoice(pair.Key, extension)))
            .Where(choice => choice.Extension.StartsWith(".", StringComparison.Ordinal) && choice.Extension.Length > 1)
            .GroupBy(choice => choice.Extension, StringComparer.OrdinalIgnoreCase).Select(group => group.First()).ToArray();
        if (choices.Length == 0)
            throw new InvalidOperationException("Choose at least one file format before saving.");
        var folderPicker = new FolderPicker { SuggestedStartLocation = _picker.SuggestedStartLocation };
        folderPicker.FileTypeFilter.Add("*");
        var folder = await folderPicker.PickSingleFolderAsync();
        token.ThrowIfCancellationRequested();
        if (folder is null)
            return null;

        // Uno's iOS DefaultFileExtension getter throws. The shared save workflow
        // provides ordered FileTypeChoices, including a single choice for exports.
        var choice = choices[0];
        if (choices.Length > 1)
        {
            var selected = await PromptAsync("Choose file format", "Your file keeps the selected format's extension.", UIAlertControllerStyle.ActionSheet,
                (alert, complete) =>
                {
                    foreach (var item in choices.OrderByDescending(value => ReferenceEquals(value, choice)))
                        alert.AddAction(UIAlertAction.Create($"{item.Name} ({item.Extension})", UIAlertActionStyle.Default, _ => complete(item.Extension)));
                    alert.AddAction(UIAlertAction.Create("Cancel", UIAlertActionStyle.Cancel, _ => complete(null)));
                }, token);
            if (selected is null)
                return null;
            choice = choices.First(value => value.Extension == selected);
        }

        var suggested = _picker.SuggestedFileName;
        if (string.IsNullOrWhiteSpace(suggested))
            suggested = "Untitled";
        suggested = RemoveKnownExtension(suggested, choices);
        var problem = string.Empty;
        while (true)
        {
            var name = await PromptAsync("Save file", $"{folder.Name}\n{choice.Name} ({choice.Extension})" + (problem.Length == 0 ? string.Empty : "\n\n" + problem),
                UIAlertControllerStyle.Alert, (alert, complete) =>
                {
                    alert.AddTextField(field =>
                    {
                        field.Text = suggested;
                        field.Placeholder = "File name";
                        field.AutocorrectionType = UITextAutocorrectionType.No;
                        field.AutocapitalizationType = UITextAutocapitalizationType.None;
                        field.ClearButtonMode = UITextFieldViewMode.WhileEditing;
                    });
                    alert.AddAction(UIAlertAction.Create("Cancel", UIAlertActionStyle.Cancel, _ => complete(null)));
                    var save = UIAlertAction.Create("Save", UIAlertActionStyle.Default, _ => complete(alert.TextFields?[0].Text ?? string.Empty));
                    alert.AddAction(save);
                    alert.PreferredAction = save;
                }, token);
            if (name is null)
                return null;
            suggested = name.Trim();
            var stem = RemoveKnownExtension(suggested, choices);
            var fileName = stem + choice.Extension;
            if (stem.Length == 0 || stem is "." or ".." || stem.Any(character => char.IsControl(character) || character is '/' or '\\' or ':')
                || Encoding.UTF8.GetByteCount(fileName) > 255)
            {
                problem = "Enter a file name without slashes, colons or control characters (up to 255 bytes including its extension).";
                continue;
            }
            token.ThrowIfCancellationRequested();
            try
            {
                var existing = await folder.TryGetItemAsync(fileName);
                token.ThrowIfCancellationRequested();
                if (existing is StorageFile file)
                {
                    var replace = await PromptAsync("Replace existing file?", $"{fileName} already exists in {folder.Name}.", UIAlertControllerStyle.Alert,
                        (alert, complete) =>
                        {
                            alert.AddAction(UIAlertAction.Create("Choose another name", UIAlertActionStyle.Cancel, _ => complete(null)));
                            alert.AddAction(UIAlertAction.Create("Replace", UIAlertActionStyle.Destructive, _ => complete("replace")));
                        }, token);
                    if (replace is not null)
                        return file; // Do not truncate until the shared save operation writes successfully.
                    problem = string.Empty;
                    continue;
                }
                if (existing is not null)
                {
                    problem = "A folder already uses that name. Choose another file name.";
                    continue;
                }
                // Fail instead of replacing if another process creates this name between
                // the existence check and creation. The next attempt asks before replacing.
                return await folder.CreateFileAsync(fileName, CreationCollisionOption.FailIfExists);
            }
            catch (IOException exception)
            {
                problem = "The file could not be created: " + exception.Message;
            }
            catch (UnauthorizedAccessException exception)
            {
                problem = "The selected folder is not writable: " + exception.Message;
            }
        }
    }

    private static string RemoveKnownExtension(string name, IEnumerable<FileChoice> choices)
    {
        var extension = choices.Select(choice => choice.Extension).OrderByDescending(value => value.Length)
            .FirstOrDefault(value => name.EndsWith(value, StringComparison.OrdinalIgnoreCase));
        return extension is null ? name : name.Substring(0, name.Length - extension.Length);
    }

    private static async Task<string?> PromptAsync(string title, string message, UIAlertControllerStyle style,
        Action<UIAlertController, Action<string?>> configure, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        var presenter = Presenter();
        using var alert = UIAlertController.Create(title, message, style);
        var completion = new TaskCompletionSource<string?>(TaskCreationOptions.RunContinuationsAsynchronously);
        var finishing = 0;
        async void Complete(string? value, bool cancelled = false)
        {
            if (Interlocked.Exchange(ref finishing, 1) != 0)
                return;
            try
            {
                if (alert.PresentingViewController is not null)
                    await alert.DismissViewControllerAsync(true);
                if (cancelled)
                    completion.TrySetCanceled(token);
                else
                    completion.TrySetResult(value);
            }
            catch (Exception exception) { completion.TrySetException(exception); }
        }
        configure(alert, value => Complete(value));
        if (alert.PopoverPresentationController is { } popover && presenter.View is { } view)
        {
            popover.SourceView = view;
            popover.SourceRect = new CGRect(view.Bounds.X + view.Bounds.Width / 2, view.Bounds.Y + view.Bounds.Height / 2, 1, 1);
            popover.PermittedArrowDirections = 0;
        }
        await presenter.PresentViewControllerAsync(alert, true);
        using var registration = token.Register(() => presenter.BeginInvokeOnMainThread(() => Complete(null, true)));
        return await completion.Task;
    }

    private static UIViewController Presenter()
    {
        var window = UIApplication.SharedApplication.ConnectedScenes.OfType<UIWindowScene>()
            .Where(scene => scene.ActivationState == UISceneActivationState.ForegroundActive)
            .SelectMany(scene => scene.Windows).FirstOrDefault(value => value.IsKeyWindow);
        // Uno6.5 can also host a single UIKit window without an active scene.
#pragma warning disable CA1422
        window ??= UIApplication.SharedApplication.KeyWindow;
#pragma warning restore CA1422
        var controller = window?.RootViewController ?? throw new InvalidOperationException("The document window is not ready to save a file.");
        while (controller.PresentedViewController is { } presented && !presented.IsBeingDismissed)
            controller = presented;
        return controller;
    }

    private sealed record FileChoice(string Name, string Extension);
}
