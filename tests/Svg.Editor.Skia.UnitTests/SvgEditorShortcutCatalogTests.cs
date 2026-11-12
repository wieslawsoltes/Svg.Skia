using System;
using System.Collections.Generic;
using System.Linq;
using Svg.Editor.Core;
using Xunit;

namespace Svg.Editor.Skia.UnitTests;

public class SvgEditorShortcutCatalogTests
{
    private const SvgEditorShortcutModifiers Primary = SvgEditorShortcutModifiers.Primary;
    private const SvgEditorShortcutModifiers Shift = SvgEditorShortcutModifiers.Shift;
    private const SvgEditorShortcutModifiers Alt = SvgEditorShortcutModifiers.Alt;

    [Theory]
    [InlineData(SvgEditorShortcutKey.M, "tool-rectangle")]
    [InlineData(SvgEditorShortcutKey.L, "tool-ellipse")]
    [InlineData(SvgEditorShortcutKey.N, "tool-pencil")]
    [InlineData(SvgEditorShortcutKey.B, "tool-brush")]
    [InlineData(SvgEditorShortcutKey.R, "tool-rotate")]
    [InlineData(SvgEditorShortcutKey.E, "tool-free-transform")]
    [InlineData(SvgEditorShortcutKey.O, "tool-reflect")]
    [InlineData(SvgEditorShortcutKey.G, "tool-gradient")]
    [InlineData(SvgEditorShortcutKey.Q, "tool-lasso")]
    [InlineData(SvgEditorShortcutKey.Y, "tool-magicwand")]
    [InlineData(SvgEditorShortcutKey.C, "tool-scissors")]
    [InlineData(SvgEditorShortcutKey.Backslash, "tool-line")]
    [InlineData(SvgEditorShortcutKey.X, "activepaint")]
    public void ToolKeysSelectTheirExpectedAction(SvgEditorShortcutKey key, string expected)
        => Assert.Equal(expected, Resolve(key)?.CommandId);

    [Fact]
    public void OutlinePreviewCannotAccidentallyRedoAnEdit()
    {
        Assert.Equal("wireframe", Resolve(SvgEditorShortcutKey.Y, Primary)?.CommandId);
        Assert.Equal("redo", Resolve(SvgEditorShortcutKey.Z, Primary | Shift)?.CommandId);
        Assert.Equal("undo", Resolve(SvgEditorShortcutKey.Z, Primary)?.CommandId);
    }

    [Fact]
    public void FitAndActualSizeAreSeparateFromToolAndPaintShortcuts()
    {
        Assert.Equal("fit", Resolve(SvgEditorShortcutKey.Number0, Primary)?.CommandId);
        Assert.Equal("fitall", Resolve(SvgEditorShortcutKey.Number0, Primary | Alt)?.CommandId);
        Assert.Equal("zoom100", Resolve(SvgEditorShortcutKey.Number1, Primary)?.CommandId);
        Assert.Equal("swappaints", Resolve(SvgEditorShortcutKey.X, Shift)?.CommandId);
        Assert.Equal("tool-eraser", Resolve(SvgEditorShortcutKey.E, Shift)?.CommandId);
        Assert.Equal("tool-warp", Resolve(SvgEditorShortcutKey.R, Shift)?.CommandId);
        Assert.Equal("tool-blob-brush", Resolve(SvgEditorShortcutKey.B, Shift)?.CommandId);
        Assert.Equal("tool-join", Resolve(SvgEditorShortcutKey.J, Shift)?.CommandId);
        Assert.Equal("defaultpaints", Resolve(SvgEditorShortcutKey.D)?.CommandId);
        Assert.Equal("transformagain", Resolve(SvgEditorShortcutKey.D, Primary)?.CommandId);
        Assert.Equal("transparency", Resolve(SvgEditorShortcutKey.D, Primary | Shift)?.CommandId);
    }

    [Fact]
    public void SearchDoesNotStealPlaceOrPreferences()
    {
        Assert.Equal("image", Resolve(SvgEditorShortcutKey.P, Primary | Shift)?.CommandId);
        Assert.Equal("preferences", Resolve(SvgEditorShortcutKey.K, Primary)?.CommandId);
        Assert.Equal("commands", Resolve(SvgEditorShortcutKey.F12, Primary | Shift)?.CommandId);
        Assert.Equal("shortcutsettings", Resolve(SvgEditorShortcutKey.K, Primary | Alt | Shift)?.CommandId);
    }

    [Fact]
    public void GuideAndAnchorChordsKeepTheirDistinctCanvasAndDocumentActions()
    {
        Assert.Equal("guides", Resolve(SvgEditorShortcutKey.Semicolon, Primary)?.CommandId);
        Assert.Equal("lockguides", Resolve(SvgEditorShortcutKey.Semicolon, Primary | Alt)?.CommandId);
        Assert.Equal("smartguides", Resolve(SvgEditorShortcutKey.U, Primary)?.CommandId);
        Assert.Equal("guide-bounds", Resolve(SvgEditorShortcutKey.Number5, Primary)?.CommandId);
        Assert.Equal("tool-add-anchor", Resolve(SvgEditorShortcutKey.Equals, Shift)?.CommandId);
        Assert.Equal("tool-add-anchor", Resolve(SvgEditorShortcutKey.Equals)?.CommandId);
        Assert.Equal("tool-add-anchor", Resolve(SvgEditorShortcutKey.Add)?.CommandId);
        Assert.Equal("+", SvgEditorShortcutCatalog.GetShortcutText("tool-add-anchor"));
        Assert.Equal("tool-delete-anchor", Resolve(SvgEditorShortcutKey.Minus)?.CommandId);
        Assert.Equal("tool-anchor", Resolve(SvgEditorShortcutKey.C, Shift)?.CommandId);
        Assert.Equal("tool-shape-builder", Resolve(SvgEditorShortcutKey.M, Shift)?.CommandId);
        Assert.Equal("tool-symbol-sprayer", Resolve(SvgEditorShortcutKey.S, Shift)?.CommandId);
        Assert.Null(Resolve(SvgEditorShortcutKey.Equals, Shift, SvgEditorShortcutContext.TextInput));
    }

    [Fact]
    public void EscapeCanCancelAnUnfinishedGestureWithoutRequiringASelection()
    {
        Assert.Equal("cancel-action", Resolve(SvgEditorShortcutKey.Escape)?.CommandId);
        Assert.Equal("deselect", Resolve(SvgEditorShortcutKey.A, Primary | Shift)?.CommandId);
        Assert.Null(Resolve(SvgEditorShortcutKey.Escape, context: SvgEditorShortcutContext.TextInput));
        Assert.Null(Resolve(SvgEditorShortcutKey.Escape, context: SvgEditorShortcutContext.OtherControl));
    }

    [Theory]
    [InlineData(SvgEditorShortcutKey.C, Primary)]
    [InlineData(SvgEditorShortcutKey.X, Primary)]
    [InlineData(SvgEditorShortcutKey.V, Primary)]
    [InlineData(SvgEditorShortcutKey.Z, Primary)]
    [InlineData(SvgEditorShortcutKey.Z, Primary | Shift)]
    [InlineData(SvgEditorShortcutKey.Y, Primary)]
    [InlineData(SvgEditorShortcutKey.A, Primary)]
    [InlineData(SvgEditorShortcutKey.A, Primary | Shift)]
    [InlineData(SvgEditorShortcutKey.T, Primary)]
    [InlineData(SvgEditorShortcutKey.M, SvgEditorShortcutModifiers.None)]
    [InlineData(SvgEditorShortcutKey.Tab, SvgEditorShortcutModifiers.None)]
    [InlineData(SvgEditorShortcutKey.Space, SvgEditorShortcutModifiers.None)]
    [InlineData(SvgEditorShortcutKey.Delete, SvgEditorShortcutModifiers.None)]
    [InlineData(SvgEditorShortcutKey.Backspace, SvgEditorShortcutModifiers.None)]
    [InlineData(SvgEditorShortcutKey.Enter, SvgEditorShortcutModifiers.None)]
    public void TextEditorsKeepTheirOwnEditingAndCompositionKeys(SvgEditorShortcutKey key, SvgEditorShortcutModifiers modifiers)
        => Assert.Null(Resolve(key, modifiers, SvgEditorShortcutContext.TextInput));

    [Theory]
    [InlineData(SvgEditorShortcutKey.Left)]
    [InlineData(SvgEditorShortcutKey.Up)]
    [InlineData(SvgEditorShortcutKey.Right)]
    [InlineData(SvgEditorShortcutKey.Down)]
    [InlineData(SvgEditorShortcutKey.Tab)]
    [InlineData(SvgEditorShortcutKey.Space)]
    [InlineData(SvgEditorShortcutKey.Enter)]
    [InlineData(SvgEditorShortcutKey.Backspace)]
    public void FocusedControlsKeepNavigationAndActivationKeys(SvgEditorShortcutKey key)
    {
        Assert.Null(Resolve(key, context: SvgEditorShortcutContext.OtherControl));
        Assert.Null(Resolve(key, Shift, SvgEditorShortcutContext.OtherControl));
        Assert.Null(Resolve(key, context: SvgEditorShortcutContext.DocumentTree));
    }

    [Fact]
    public void DeleteIsAvailableToObjectTreeWithoutTreatingBackspaceAsDeletion()
    {
        Assert.Equal("delete", Resolve(SvgEditorShortcutKey.Delete, context: SvgEditorShortcutContext.DocumentTree)?.CommandId);
        Assert.Null(Resolve(SvgEditorShortcutKey.Backspace, context: SvgEditorShortcutContext.DocumentTree));
        Assert.Null(Resolve(SvgEditorShortcutKey.Delete, context: SvgEditorShortcutContext.OtherControl));
    }

    [Fact]
    public void SavingIsAvailableWhileEditingAPropertyButNeverFromAnotherWorkspaceOrDialog()
    {
        Assert.Equal("save", Resolve(SvgEditorShortcutKey.S, Primary, SvgEditorShortcutContext.TextInput)?.CommandId);
        foreach (var shortcut in SvgEditorShortcutCatalog.Bindings)
            Assert.Null(Resolve(shortcut.Key, shortcut.Modifiers, SvgEditorShortcutContext.Blocked));
    }

    [Theory]
    [InlineData(SvgEditorShortcutContext.Canvas)]
    [InlineData(SvgEditorShortcutContext.DocumentTree)]
    [InlineData(SvgEditorShortcutContext.OtherControl)]
    [InlineData(SvgEditorShortcutContext.TextInput)]
    public void DocumentTabNavigationWorksAcrossWorkspaceFocusWithoutTakingPlainTab(SvgEditorShortcutContext context)
    {
        Assert.Equal("next-document", Resolve(SvgEditorShortcutKey.Tab, Primary, context)?.CommandId);
        Assert.Equal("previous-document", Resolve(SvgEditorShortcutKey.Tab, Primary | Shift, context)?.CommandId);
        if (context != SvgEditorShortcutContext.Canvas)
        {
            Assert.Null(Resolve(SvgEditorShortcutKey.Tab, context: context));
            Assert.Null(Resolve(SvgEditorShortcutKey.Tab, Shift, context));
        }
        Assert.Null(Resolve(SvgEditorShortcutKey.Tab, Primary | Alt, context));
        Assert.Null(Resolve(SvgEditorShortcutKey.Tab, Primary, SvgEditorShortcutContext.Blocked));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void MacDocumentTabsUsePhysicalControlAndLeaveCommandTabToTheSystem(bool shift)
    {
        var expected = Primary | (shift ? Shift : SvgEditorShortcutModifiers.None);
        Assert.True(SvgEditorShortcutCatalog.TryNormalizeModifiersForKey(SvgEditorShortcutKey.Tab, true,
            true, shift, false, false, false, out var actual));
        Assert.Equal(expected, actual);
        Assert.False(SvgEditorShortcutCatalog.TryNormalizeModifiersForKey(SvgEditorShortcutKey.Tab, true,
            false, shift, false, true, false, out _));
        Assert.False(SvgEditorShortcutCatalog.TryNormalizeModifiersForKey(SvgEditorShortcutKey.Tab, true,
            true, shift, false, true, false, out _));
        Assert.False(SvgEditorShortcutCatalog.TryNormalizeModifiersForKey(SvgEditorShortcutKey.Tab, true,
            true, shift, true, false, false, out _));
        Assert.False(SvgEditorShortcutCatalog.TryNormalizeModifiersForKey(SvgEditorShortcutKey.Tab, true,
            true, shift, false, false, true, out _));
        Assert.False(SvgEditorShortcutCatalog.TryNormalizeModifiersForKey(SvgEditorShortcutKey.A, true,
            true, shift, false, false, false, out _));
        var id = shift ? "previous-document" : "next-document";
        Assert.Equal(shift ? "Ctrl+Shift+Tab" : "Ctrl+Tab", SvgEditorShortcutCatalog.GetShortcutText(id, true));
    }

    [Fact]
    public void AmbiguousAltGraphInputCannotInvokeAGlobalAltShortcutFromATextEditor()
    {
        // A host without right-Alt state can report Polish AltGr+E as Ctrl+Alt+E.
        // Export is valid on the canvas, but must not replace the typed character.
        Assert.Equal("export", Resolve(SvgEditorShortcutKey.E, Primary | Alt)?.CommandId);
        Assert.Null(Resolve(SvgEditorShortcutKey.E, Primary | Alt, SvgEditorShortcutContext.TextInput));
        Assert.Null(Resolve(SvgEditorShortcutKey.K, Primary | Alt | Shift, SvgEditorShortcutContext.TextInput));
        Assert.Equal("save", Resolve(SvgEditorShortcutKey.S, Primary, SvgEditorShortcutContext.TextInput)?.CommandId);
    }

    [Theory]
    [InlineData(SvgEditorShortcutKey.M, Alt)]
    [InlineData(SvgEditorShortcutKey.G, Primary | Alt)]
    [InlineData(SvgEditorShortcutKey.R, Primary | Shift)]
    [InlineData(SvgEditorShortcutKey.X, Alt)]
    [InlineData(SvgEditorShortcutKey.Y, Primary | Shift)]
    [InlineData(SvgEditorShortcutKey.Number0, Primary | Shift)]
    public void ExtraModifiersNeverFallThroughToADifferentAction(SvgEditorShortcutKey key, SvgEditorShortcutModifiers modifiers)
        => Assert.Null(Resolve(key, modifiers));

    [Theory]
    [InlineData(false, true, false, false, false, false, Primary)]
    [InlineData(true, false, false, false, true, false, Primary)]
    [InlineData(false, true, true, true, false, false, Primary | Shift | Alt)]
    [InlineData(true, false, true, true, true, false, Primary | Shift | Alt)]
    [InlineData(false, false, false, false, false, false, SvgEditorShortcutModifiers.None)]
    public void PhysicalKeysNormalizeToPortableGestures(bool commandPlatform, bool control, bool shift, bool alt,
        bool meta, bool altGraph, SvgEditorShortcutModifiers expected)
    {
        Assert.True(SvgEditorShortcutCatalog.TryNormalizeModifiers(commandPlatform, control, shift, alt, meta, altGraph, out var modifiers));
        Assert.Equal(expected, modifiers);
    }

    [Theory]
    [InlineData(false, true, false, true, false, true)]
    [InlineData(false, true, true, true, false, true)]
    [InlineData(false, false, false, false, true, false)]
    [InlineData(true, true, false, false, false, false)]
    [InlineData(true, true, true, true, true, false)]
    [InlineData(false, true, true, true, true, false)]
    public void AltGraphAndSystemChordsCannotRunEditorCommands(bool commandPlatform, bool control, bool shift, bool alt,
        bool meta, bool altGraph)
    {
        Assert.False(SvgEditorShortcutCatalog.TryNormalizeModifiers(commandPlatform, control, shift, alt, meta, altGraph, out var modifiers));
        Assert.Equal(SvgEditorShortcutModifiers.None, modifiers);
    }

    [Fact]
    public void TemporaryNavigationIsRestrictedToTheCanvas()
    {
        Assert.Equal(SvgEditorNavigationShortcut.Hand, SvgEditorShortcutCatalog.Navigation(SvgEditorShortcutModifiers.None, SvgEditorShortcutContext.Canvas));
        Assert.Equal(SvgEditorNavigationShortcut.ZoomIn, SvgEditorShortcutCatalog.Navigation(Primary, SvgEditorShortcutContext.Canvas));
        Assert.Equal(SvgEditorNavigationShortcut.ZoomOut, SvgEditorShortcutCatalog.Navigation(Primary | Alt, SvgEditorShortcutContext.Canvas));
        foreach (var context in new[] { SvgEditorShortcutContext.TextInput, SvgEditorShortcutContext.OtherControl, SvgEditorShortcutContext.DocumentTree, SvgEditorShortcutContext.Blocked })
        {
            Assert.Equal(SvgEditorNavigationShortcut.None, SvgEditorShortcutCatalog.Navigation(SvgEditorShortcutModifiers.None, context));
            Assert.Equal(SvgEditorNavigationShortcut.None, SvgEditorShortcutCatalog.Navigation(Primary, context));
            Assert.Equal(SvgEditorNavigationShortcut.None, SvgEditorShortcutCatalog.Navigation(Primary | Alt, context));
        }
    }

    [Fact]
    public void ZoomWorksWithMainAndNumericKeyboardsWithoutAliasingPlainToolKeys()
    {
        Assert.Equal("zoomin", Resolve(SvgEditorShortcutKey.Equals, Primary)?.CommandId);
        Assert.Equal("zoomin", Resolve(SvgEditorShortcutKey.Equals, Primary | Shift)?.CommandId);
        Assert.Equal("zoomin", Resolve(SvgEditorShortcutKey.Add, Primary)?.CommandId);
        Assert.Equal("zoomout", Resolve(SvgEditorShortcutKey.Minus, Primary)?.CommandId);
        Assert.Equal("zoomout", Resolve(SvgEditorShortcutKey.Subtract, Primary)?.CommandId);
        Assert.Equal("tool-add-anchor", Resolve(SvgEditorShortcutKey.Equals)?.CommandId);
        Assert.Equal("tool-delete-anchor", Resolve(SvgEditorShortcutKey.Minus)?.CommandId);
    }

    [Fact]
    public void RepeatingInputCannotTogglePanelsOrCreateDocumentsRepeatedly()
    {
        Assert.True(Resolve(SvgEditorShortcutKey.Left)!.Repeats);
        Assert.True(Resolve(SvgEditorShortcutKey.Left, Shift)!.Repeats);
        Assert.True(Resolve(SvgEditorShortcutKey.Equals, Primary)!.Repeats);
        Assert.False(Resolve(SvgEditorShortcutKey.N, Primary)!.Repeats);
        Assert.False(Resolve(SvgEditorShortcutKey.Tab)!.Repeats);
        Assert.False(Resolve(SvgEditorShortcutKey.Y, Primary)!.Repeats);
    }

    [Fact]
    public void LabelsAndDispatchUseTheSameUniqueGestureCatalog()
    {
        var shortcuts = SvgEditorShortcutCatalog.Bindings;
        Assert.Equal(shortcuts.Count, shortcuts.Select(shortcut => (shortcut.Key, shortcut.Modifiers)).Distinct().Count());
        foreach (var shortcut in shortcuts.Where(shortcut => shortcut.IsPreferred))
        {
            Assert.Same(shortcut, Resolve(shortcut.Key, shortcut.Modifiers));
            Assert.Equal(shortcut.GetDisplayText(), SvgEditorShortcutCatalog.GetShortcutText(shortcut.CommandId));
            Assert.Equal(shortcut.GetDisplayText(true), SvgEditorShortcutCatalog.GetShortcutText(shortcut.CommandId, true));
        }
        Assert.Equal("Ctrl+Shift+S", SvgEditorShortcutCatalog.GetShortcutText("saveas"));
        Assert.Equal("Cmd+Shift+S", SvgEditorShortcutCatalog.GetShortcutText("saveas", true));
        Assert.Equal("Cmd+Option+0", SvgEditorShortcutCatalog.GetShortcutText("fitall", true));
        Assert.Empty(SvgEditorShortcutCatalog.GetShortcutText("duplicate"));
        Assert.Empty(SvgEditorShortcutCatalog.GetShortcutText("unknown-command"));
        Assert.Throws<NotSupportedException>(() => ((IList<SvgEditorShortcut>)shortcuts).Clear());
    }

    private static SvgEditorShortcut? Resolve(SvgEditorShortcutKey key,
        SvgEditorShortcutModifiers modifiers = SvgEditorShortcutModifiers.None,
        SvgEditorShortcutContext context = SvgEditorShortcutContext.Canvas)
        => SvgEditorShortcutCatalog.Resolve(key, modifiers, context);
}
