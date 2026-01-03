// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
using System;
using System.Collections.Generic;
using System.Linq;

namespace ShimSkiaSharp.Editing;

public static class SKPictureEditingExtensions
{
    public static IEnumerable<TCommand> FindCommands<TCommand>(this SKPicture picture)
        where TCommand : CanvasCommand
    {
        if (picture is null)
        {
            throw new ArgumentNullException(nameof(picture));
        }

        return picture.Commands?.OfType<TCommand>() ?? Enumerable.Empty<TCommand>();
    }

    public static int ReplaceCommands(this SKPicture picture, Func<CanvasCommand, CanvasCommand?> replace)
    {
        if (picture is null)
        {
            throw new ArgumentNullException(nameof(picture));
        }

        if (replace is null)
        {
            throw new ArgumentNullException(nameof(replace));
        }

        var commands = picture.Commands;
        if (commands is null)
        {
            return 0;
        }

        var count = 0;
        for (var i = 0; i < commands.Count; i++)
        {
            var original = commands[i];
            var next = replace(original);
            if (next is null)
            {
                commands.RemoveAt(i);
                count++;
                i--;
                continue;
            }

            if (!ReferenceEquals(original, next))
            {
                commands[i] = next;
                count++;
            }
        }

        return count;
    }

    public static int UpdatePaints(
        this SKPicture picture,
        Func<SKPaint, bool> predicate,
        Action<SKPaint> update,
        EditMode mode = EditMode.InPlace)
    {
        if (picture is null)
        {
            throw new ArgumentNullException(nameof(picture));
        }

        if (predicate is null)
        {
            throw new ArgumentNullException(nameof(predicate));
        }

        if (update is null)
        {
            throw new ArgumentNullException(nameof(update));
        }

        var commands = picture.Commands;
        if (commands is null)
        {
            return 0;
        }

        var count = 0;
        var context = mode == EditMode.CloneOnWrite ? new CloneContext() : null;
        var visited = new HashSet<SKPaint>(ReferenceEqualityComparer<SKPaint>.Instance);

        for (var i = 0; i < commands.Count; i++)
        {
            var command = commands[i];
            if (!TryGetPaint(command, out var paint) || paint is null || !predicate(paint))
            {
                continue;
            }

            if (mode == EditMode.CloneOnWrite)
            {
                var cloned = ClonePaint(context!, paint);
                if (!ReferenceEquals(cloned, paint))
                {
                    command = ReplacePaint(command, cloned);
                    commands[i] = command;
                }

                if (visited.Add(paint))
                {
                    update(cloned);
                    count++;
                }
            }
            else if (visited.Add(paint))
            {
                update(paint);
                count++;
            }
        }

        return count;
    }

    public static int UpdatePaths(
        this SKPicture picture,
        Func<SKPath, bool> predicate,
        Action<SKPath> update,
        EditMode mode = EditMode.InPlace)
    {
        if (picture is null)
        {
            throw new ArgumentNullException(nameof(picture));
        }

        if (predicate is null)
        {
            throw new ArgumentNullException(nameof(predicate));
        }

        if (update is null)
        {
            throw new ArgumentNullException(nameof(update));
        }

        var commands = picture.Commands;
        if (commands is null)
        {
            return 0;
        }

        var count = 0;
        var context = mode == EditMode.CloneOnWrite ? new CloneContext() : null;
        var visited = new HashSet<SKPath>(ReferenceEqualityComparer<SKPath>.Instance);

        for (var i = 0; i < commands.Count; i++)
        {
            var command = commands[i];
            if (TryGetPath(command, out var path) && path is { } && predicate(path))
            {
                if (mode == EditMode.CloneOnWrite)
                {
                    var cloned = ClonePath(context!, path);
                    if (!ReferenceEquals(cloned, path))
                    {
                        command = ReplacePath(command, cloned);
                        commands[i] = command;
                    }

                    if (visited.Add(path))
                    {
                        update(cloned);
                        count++;
                    }
                }
                else if (visited.Add(path))
                {
                    update(path);
                    count++;
                }
            }

            if (command is ClipPathCanvasCommand clipCommand && clipCommand.ClipPath is { })
            {
                var originalClipPath = clipCommand.ClipPath;
                if (!ClipPathContainsMatch(originalClipPath, predicate))
                {
                    continue;
                }

                if (mode == EditMode.CloneOnWrite)
                {
                    var cloned = CloneClipPath(context!, originalClipPath);
                    if (!ReferenceEquals(cloned, originalClipPath))
                    {
                        command = clipCommand with { ClipPath = cloned };
                        commands[i] = command;
                    }

                    count += UpdateClipPathPaths(originalClipPath, cloned, predicate, update, visited);
                }
                else
                {
                    count += UpdateClipPathPaths(originalClipPath, originalClipPath, predicate, update, visited);
                }
            }
        }

        return count;
    }

    private static bool TryGetPaint(CanvasCommand command, out SKPaint? paint)
    {
        switch (command)
        {
            case DrawImageCanvasCommand drawImage:
                paint = drawImage.Paint;
                return true;
            case DrawPathCanvasCommand drawPath:
                paint = drawPath.Paint;
                return true;
            case DrawTextBlobCanvasCommand drawTextBlob:
                paint = drawTextBlob.Paint;
                return true;
            case DrawTextCanvasCommand drawText:
                paint = drawText.Paint;
                return true;
            case DrawTextOnPathCanvasCommand drawTextOnPath:
                paint = drawTextOnPath.Paint;
                return true;
            case SaveLayerCanvasCommand saveLayer:
                paint = saveLayer.Paint;
                return true;
            default:
                paint = null;
                return false;
        }
    }

    private static CanvasCommand ReplacePaint(CanvasCommand command, SKPaint? paint)
    {
        return command switch
        {
            DrawImageCanvasCommand drawImage => drawImage with { Paint = paint },
            DrawPathCanvasCommand drawPath => drawPath with { Paint = paint },
            DrawTextBlobCanvasCommand drawTextBlob => drawTextBlob with { Paint = paint },
            DrawTextCanvasCommand drawText => drawText with { Paint = paint },
            DrawTextOnPathCanvasCommand drawTextOnPath => drawTextOnPath with { Paint = paint },
            SaveLayerCanvasCommand saveLayer => saveLayer with { Paint = paint },
            _ => command
        };
    }

    private static bool TryGetPath(CanvasCommand command, out SKPath? path)
    {
        switch (command)
        {
            case DrawPathCanvasCommand drawPath:
                path = drawPath.Path;
                return true;
            case DrawTextOnPathCanvasCommand drawTextOnPath:
                path = drawTextOnPath.Path;
                return true;
            default:
                path = null;
                return false;
        }
    }

    private static CanvasCommand ReplacePath(CanvasCommand command, SKPath? path)
    {
        return command switch
        {
            DrawPathCanvasCommand drawPath => drawPath with { Path = path },
            DrawTextOnPathCanvasCommand drawTextOnPath => drawTextOnPath with { Path = path },
            _ => command
        };
    }

    private static SKPaint ClonePaint(CloneContext context, SKPaint paint)
    {
        if (context.TryGet(paint, out SKPaint existing))
        {
            return existing;
        }

        return paint.DeepClone(context);
    }

    private static SKPath ClonePath(CloneContext context, SKPath path)
    {
        if (context.TryGet(path, out SKPath existing))
        {
            return existing;
        }

        return path.DeepClone(context);
    }

    private static ClipPath CloneClipPath(CloneContext context, ClipPath clipPath)
    {
        if (context.TryGet(clipPath, out ClipPath existing))
        {
            return existing;
        }

        return clipPath.DeepClone(context);
    }

    private static int UpdateClipPathPaths(
        ClipPath original,
        ClipPath target,
        Func<SKPath, bool> predicate,
        Action<SKPath> update,
        HashSet<SKPath> visited)
    {
        var count = 0;
        if (original.Clips is { } originalClips && target.Clips is { })
        {
            var targetClips = target.Clips;
            var clipCount = Math.Min(originalClips.Count, targetClips.Count);
            for (var i = 0; i < clipCount; i++)
            {
                var originalClip = originalClips[i];
                var targetClip = targetClips[i];
                if (originalClip.Path is { } path && predicate(path))
                {
                    if (visited.Add(path) && targetClip.Path is { } targetPath)
                    {
                        update(targetPath);
                        count++;
                    }
                }

                if (originalClip.Clip is { } originalNested && targetClip.Clip is { } targetNested)
                {
                    count += UpdateClipPathPaths(originalNested, targetNested, predicate, update, visited);
                }
            }
        }

        if (original.Clip is { } originalNestedClip && target.Clip is { } targetNestedClip)
        {
            count += UpdateClipPathPaths(originalNestedClip, targetNestedClip, predicate, update, visited);
        }

        return count;
    }

    private static bool ClipPathContainsMatch(ClipPath clipPath, Func<SKPath, bool> predicate)
    {
        if (clipPath.Clips is { })
        {
            foreach (var pathClip in clipPath.Clips)
            {
                if (pathClip.Path is { } path && predicate(path))
                {
                    return true;
                }

                if (pathClip.Clip is { } nested && ClipPathContainsMatch(nested, predicate))
                {
                    return true;
                }
            }
        }

        return clipPath.Clip is { } clip && ClipPathContainsMatch(clip, predicate);
    }

    private sealed class ReferenceEqualityComparer<T> : IEqualityComparer<T>
    {
        public static readonly ReferenceEqualityComparer<T> Instance = new();

        public bool Equals(T? x, T? y) => ReferenceEquals(x, y);

        public int GetHashCode(T obj) => System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(obj);
    }
}
