using System;
using System.Collections.Generic;
using System.Linq;
using SkiaSharp;

namespace Svg.Editor.Skia;

public sealed partial class SvgEditorController
{
    private string? _activeArtboardId;

    /// <summary>Returns named views in picture coordinates, or the implicit root artboard.</summary>
    public IReadOnlyList<SvgEditorArtboard> GetArtboards()
    {
        ThrowIfDisposed();
        if (Document is null || Renderer.Picture is not { } picture)
            return Array.Empty<SvgEditorArtboard>();
        var boards = SvgEditorArtboardService.Read(Document);
        if (boards.Count == 0)
            return new[] { new SvgEditorArtboard(string.Empty, "Artboard 1", picture.CullRect, true) };
        var transform = ArtboardRootTransform();
        return boards.Select(board => new SvgEditorArtboard(board.Id, board.Name, transform.MapRect(board.Bounds))).ToArray();
    }

    /// <summary>Returns the persisted root-user-unit rectangles used by numeric editors.</summary>
    public IReadOnlyList<SvgEditorArtboard> GetArtboardsInRootCoordinates()
    {
        ThrowIfDisposed();
        if (Document is null || Renderer.Picture is not { } picture)
            return Array.Empty<SvgEditorArtboard>();
        var boards = SvgEditorArtboardService.Read(Document);
        if (boards.Count > 0)
            return boards;
        var bounds = ArtboardRootTransform().TryInvert(out var inverse) ? inverse.MapRect(picture.CullRect)
            : Document.ViewBox.Width > 0 && Document.ViewBox.Height > 0
                ? SKRect.Create(Document.ViewBox.MinX, Document.ViewBox.MinY, Document.ViewBox.Width, Document.ViewBox.Height)
                : picture.CullRect;
        return new[] { new SvgEditorArtboard(string.Empty, "Artboard 1", bounds, true) };
    }

    public SvgEditorArtboard? ActiveArtboard
    {
        get
        {
            var boards = GetArtboards();
            if (boards.Count == 0)
                return null;
            foreach (var board in boards)
            if (board.Id == _activeArtboardId)
                return board;
            return boards[0];
        }
    }

    public string? ActiveArtboardId => ActiveArtboard?.Id;

    /// <summary>Changes only the editing target, without marking the SVG dirty.</summary>
    public void ActivateArtboard(string id)
    {
        ThrowIfDisposed();
        if (id is null)
            throw new ArgumentNullException(nameof(id));
        if (!GetArtboards().Any(board => board.Id == id))
            throw new InvalidOperationException("The artboard is no longer in this document.");
        if (_activeArtboardId == id)
            return;
        _activeArtboardId = id;
        NotifyChanged();
    }

    public bool ActivateArtboardAt(SKPoint picturePoint)
    {
        if (!IsFinite(picturePoint.X) || !IsFinite(picturePoint.Y))
            throw new ArgumentOutOfRangeException(nameof(picturePoint));
        var boards = GetArtboards();
        for (var index = boards.Count - 1; index >= 0; index--)
            if (boards[index].Bounds.Contains(picturePoint))
            { ActivateArtboard(boards[index].Id); return true; }
        return false;
    }

    public SKRect GetArtboardsBounds()
    {
        var boards = GetArtboards();
        if (boards.Count == 0)
            return SKRect.Empty;
        var bounds = boards[0].Bounds;
        for (var index = 1; index < boards.Count; index++)
            bounds = SKRect.Union(bounds, boards[index].Bounds);
        return bounds;
    }

    public string AddArtboard(SKRect pictureBounds, string? name = null)
        => AddArtboardInRootCoordinates(ArtboardInRootCoordinates(pictureBounds), name);

    public string AddArtboardInRootCoordinates(SKRect bounds, string? name = null)
    {
        EnsureArtboardsEditable();
        SvgEditorArtboardService.ValidateBounds(bounds);
        var boards = MaterializeArtboards();
        var id = CreateId("artboard");
        boards.Add(new SvgEditorArtboard(id, name ?? NextArtboardName(boards), bounds));
        CommitArtboards("Create artboard", boards, id, true);
        return id;
    }

    /// <summary>Edits one view. The original overload still crops the SVG root viewport.</summary>
    public void SetArtboardBounds(string id, SKRect pictureBounds)
    {
        EnsureArtboardsEditable();
        SvgEditorArtboardService.ValidateBounds(pictureBounds);
        if (id == string.Empty && SvgEditorArtboardService.Read(Document).Count == 0)
        { SetArtboardBounds(pictureBounds); return; }
        SetArtboardBoundsInRootCoordinates(id, ArtboardInRootCoordinates(pictureBounds));
    }

    public void SetArtboardBoundsInRootCoordinates(string id, SKRect bounds)
    {
        EnsureArtboardsEditable();
        SvgEditorArtboardService.ValidateBounds(bounds);
        if (id == string.Empty && SvgEditorArtboardService.Read(Document).Count == 0)
        { SetArtboardBounds(ArtboardRootTransform().MapRect(bounds)); return; }
        var boards = SvgEditorArtboardService.Read(Document).ToList();
        var index = FindArtboard(boards, id);
        if (boards[index].Bounds == bounds)
            return;
        boards[index] = new SvgEditorArtboard(id, boards[index].Name, bounds);
        CommitArtboards("Change artboard bounds", boards, id, true);
    }

    public void RenameArtboard(string id, string name)
    {
        EnsureArtboardsEditable();
        SvgEditorArtboardService.ValidateName(name);
        var implicitRoot = GetArtboards()[0].IsImplicit;
        var boards = MaterializeArtboards();
        var index = id == string.Empty && implicitRoot ? 0 : FindArtboard(boards, id);
        if (boards[index].Name == name && !implicitRoot)
            return;
        boards[index] = new SvgEditorArtboard(boards[index].Id, name, boards[index].Bounds);
        CommitArtboards("Rename artboard", boards, boards[index].Id, false);
    }

    public string DuplicateArtboard(string id, SKPoint pictureOffset)
    {
        EnsureArtboardsEditable();
        if (!IsFinite(pictureOffset.X) || !IsFinite(pictureOffset.Y))
            throw new ArgumentOutOfRangeException(nameof(pictureOffset));
        var original = GetArtboards().FirstOrDefault(board => board.Id == id);
        if (original.Id is null)
            throw new InvalidOperationException("The artboard is no longer in this document.");
        var picture = original.Bounds;
        picture.Offset(pictureOffset);
        var rootBounds = ArtboardInRootCoordinates(picture);
        var boards = MaterializeArtboards();
        var index = id == string.Empty ? 0 : FindArtboard(boards, id);
        var copiedId = CreateId("artboard");
        var name = original.Name + " copy";
        var suffix = 2;
        while (boards.Any(board => board.Name == name))
            name = original.Name + " copy " + suffix++;
        if (name.Length > 256)
            name = NextArtboardName(boards);
        boards.Insert(index + 1, new SvgEditorArtboard(copiedId, name, rootBounds));
        CommitArtboards("Duplicate artboard", boards, copiedId, true);
        return copiedId;
    }

    /// <summary>Removes only a view; all artwork and the root viewport are retained.</summary>
    public void RemoveArtboard(string id)
    {
        EnsureArtboardsEditable();
        var boards = SvgEditorArtboardService.Read(Document).ToList();
        if (boards.Count <= 1)
            throw new InvalidOperationException("A document must retain at least one artboard.");
        var index = FindArtboard(boards, id);
        boards.RemoveAt(index);
        var active = _activeArtboardId == id ? boards[Math.Min(index, boards.Count - 1)].Id : ActiveArtboardId;
        CommitArtboards("Delete artboard", boards, active, false);
    }

    public void ReorderArtboard(string id, int index)
    {
        EnsureArtboardsEditable();
        var boards = SvgEditorArtboardService.Read(Document).ToList();
        if (index < 0 || index >= boards.Count)
            throw new ArgumentOutOfRangeException(nameof(index));
        var previous = FindArtboard(boards, id);
        if (index == previous)
            return;
        var board = boards[previous];
        boards.RemoveAt(previous);
        boards.Insert(index, board);
        CommitArtboards("Reorder artboards", boards, id, false);
    }

    private void CommitArtboards(string label, IReadOnlyList<SvgEditorArtboard> boards, string? activeId, bool expand)
    {
        var document = Document!;
        var previous = _activeArtboardId;
        var envelope = Renderer.Picture!.CullRect;
        if (expand)
        {
            var matrix = ArtboardRootTransformForEdit();
            foreach (var board in boards)
                envelope = SKRect.Union(envelope, matrix.MapRect(board.Bounds));
            SvgEditorArtboardService.ValidateBounds(envelope);
        }
        try
        {
            Execute(label, () =>
            {
                SvgEditorArtboardService.Write(document, boards);
                _activeArtboardId = activeId;
                // The nested edit joins this transaction. Enlarging the root viewport
                // uses the existing inverse-mapping crop operation; the original SVG
                // coordinates, object transforms, units and artwork remain intact.
                if (expand && envelope != Renderer.Picture!.CullRect)
                    SetArtboardBounds(envelope);
            });
        }
        catch { _activeArtboardId = previous; throw; }
    }

    private List<SvgEditorArtboard> MaterializeArtboards()
    {
        var boards = GetArtboardsInRootCoordinates().ToList();
        if (boards.Count == 1 && boards[0].IsImplicit)
            boards[0] = new SvgEditorArtboard(CreateId("artboard"), boards[0].Name, boards[0].Bounds);
        return boards;
    }

    private static int FindArtboard(IReadOnlyList<SvgEditorArtboard> boards, string id)
    {
        for (var index = 0; index < boards.Count; index++)
        if (boards[index].Id == id)
            return index;
        throw new InvalidOperationException("The artboard is no longer in this document.");
    }

    private static string NextArtboardName(IReadOnlyList<SvgEditorArtboard> boards)
    {
        var index = 1;
        while (boards.Any(board => board.Name == "Artboard " + index))
            index++;
        return "Artboard " + index;
    }

    private void EnsureArtboardsEditable()
    {
        ThrowIfDisposed();
        if (Document is null || Renderer.Picture is null)
            throw new InvalidOperationException("Open a document before editing artboards.");
        if (IsLocked(Document))
            throw new InvalidOperationException("Unlock the document before editing artboards.");
    }

    private SKMatrix ArtboardRootTransform()
    {
        var root = Document is null ? null : GetSceneNode(Document) ?? Renderer.RetainedSceneGraph?.Root;
        return root is null ? SKMatrix.Identity : ToMatrix(root.TotalTransform);
    }

    private SKMatrix ArtboardRootTransformForEdit()
    {
        var matrix = ArtboardRootTransform();
        if (Math.Abs(matrix.SkewX) > 0.0001 || Math.Abs(matrix.SkewY) > 0.0001 || !matrix.TryInvert(out _))
            throw new InvalidOperationException("Reset the root SVG rotation or skew before editing artboard bounds.");
        return matrix;
    }

    private SKRect ArtboardInRootCoordinates(SKRect pictureBounds)
    {
        EnsureArtboardsEditable();
        SvgEditorArtboardService.ValidateBounds(pictureBounds);
        ArtboardRootTransformForEdit().TryInvert(out var inverse);
        var bounds = inverse.MapRect(pictureBounds);
        SvgEditorArtboardService.ValidateBounds(bounds);
        return bounds;
    }
}
