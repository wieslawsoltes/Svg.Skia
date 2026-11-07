using System;
using System.Globalization;
using System.Linq;
using System.Xml;
using Svg.Editor.Core;
using Xunit;

namespace Svg.Editor.Skia.UnitTests;

public class SvgEditorShortcutProfileTests
{
    private const SvgEditorShortcutModifiers Primary = SvgEditorShortcutModifiers.Primary;
    private const SvgEditorShortcutModifiers Shift = SvgEditorShortcutModifiers.Shift;
    private const SvgEditorShortcutModifiers Alt = SvgEditorShortcutModifiers.Alt;

    [Fact]
    public void EditingADraftDoesNotChangeTheActiveProfile()
    {
        var active = new SvgEditorShortcutProfile();
        var draft = active.Clone();
        Assert.True(draft.TryAssign("duplicate", SvgEditorShortcutKey.D, Primary | Alt, out var error), error);
        Assert.Equal("duplicate", draft.Resolve(SvgEditorShortcutKey.D, Primary | Alt, SvgEditorShortcutContext.Canvas)?.CommandId);
        Assert.Null(active.Resolve(SvgEditorShortcutKey.D, Primary | Alt, SvgEditorShortcutContext.Canvas));
        Assert.Empty(active.GetShortcutText("duplicate"));
        Assert.Equal("Ctrl+Alt+D", draft.GetShortcutText("duplicate"));
    }

    [Fact]
    public void ConflictsNeverOverwriteAnExistingCommandOrPartiallyMutateTheProfile()
    {
        var profile = new SvgEditorShortcutProfile();
        var before = profile.Serialize();
        Assert.False(profile.TryAssign("save", SvgEditorShortcutKey.O, Primary, out var error));
        Assert.Contains("open", error);
        Assert.Equal(before, profile.Serialize());
        Assert.Equal("open", profile.FindShortcut(SvgEditorShortcutKey.O, Primary)?.CommandId);
        Assert.Equal("save", profile.FindShortcut(SvgEditorShortcutKey.S, Primary)?.CommandId);
    }

    [Fact]
    public void ClearingACommandFreesAllItsAliasesForExplicitReassignment()
    {
        var profile = new SvgEditorShortcutProfile();
        profile.Clear("zoomin");
        Assert.Null(profile.FindShortcut(SvgEditorShortcutKey.Equals, Primary));
        Assert.Null(profile.FindShortcut(SvgEditorShortcutKey.Equals, Primary | Shift));
        Assert.Null(profile.FindShortcut(SvgEditorShortcutKey.Add, Primary));
        Assert.Empty(profile.GetShortcutText("zoomin"));
        Assert.True(profile.IsCustomized("zoomin"));
        Assert.True(profile.TryAssign("duplicate", SvgEditorShortcutKey.Equals, Primary, out _));
        Assert.False(profile.TryResetCommand("zoomin", out var error));
        Assert.Contains("duplicate", error);
        Assert.Equal("duplicate", profile.FindShortcut(SvgEditorShortcutKey.Equals, Primary)?.CommandId);
        profile.Clear("duplicate");
        Assert.True(profile.TryResetCommand("zoomin", out error), error);
        Assert.Equal("zoomin", profile.FindShortcut(SvgEditorShortcutKey.Add, Primary)?.CommandId);
    }

    [Fact]
    public void SwappedDefaultsRoundTripWithoutDependingOnXmlOrder()
    {
        var profile = new SvgEditorShortcutProfile { Name = "Drawing workstation" };
        profile.Clear("new");
        profile.Clear("open");
        Assert.True(profile.TryAssign("new", SvgEditorShortcutKey.O, Primary, out _));
        Assert.True(profile.TryAssign("open", SvgEditorShortcutKey.N, Primary, out _));
        var restored = SvgEditorShortcutProfile.Parse(profile.Serialize());
        Assert.Equal("new", restored.FindShortcut(SvgEditorShortcutKey.O, Primary)?.CommandId);
        Assert.Equal("open", restored.FindShortcut(SvgEditorShortcutKey.N, Primary)?.CommandId);
        Assert.Equal("Drawing workstation", restored.Name);
        Assert.Equal(profile.Serialize(), restored.Serialize());
    }

    [Fact]
    public void ResetDefaultsRestoresAliasesAndDropsCustomCommands()
    {
        var profile = new SvgEditorShortcutProfile();
        profile.Clear("zoomin");
        Assert.True(profile.TryAssign("duplicate", SvgEditorShortcutKey.D, Primary | Alt, out _));
        profile.ResetDefaults();
        Assert.Equal(0, profile.OverrideCount);
        Assert.Equal("zoomin", profile.FindShortcut(SvgEditorShortcutKey.Add, Primary)?.CommandId);
        Assert.Null(profile.FindShortcut(SvgEditorShortcutKey.D, Primary | Alt));
        Assert.Equal(SvgEditorShortcutCatalog.Bindings.Count, profile.Bindings.Count);
    }

    [Fact]
    public void RebindingAnApplicationActionToALetterCannotCaptureTextOrControlFocus()
    {
        var profile = new SvgEditorShortcutProfile();
        profile.Clear("tool-rectangle");
        Assert.True(profile.TryAssign("save", SvgEditorShortcutKey.M, SvgEditorShortcutModifiers.None, out var error), error);
        Assert.Equal("save", profile.Resolve(SvgEditorShortcutKey.M, SvgEditorShortcutModifiers.None, SvgEditorShortcutContext.Canvas)?.CommandId);
        Assert.Null(profile.Resolve(SvgEditorShortcutKey.M, SvgEditorShortcutModifiers.None, SvgEditorShortcutContext.TextInput));
        Assert.Null(profile.Resolve(SvgEditorShortcutKey.M, SvgEditorShortcutModifiers.None, SvgEditorShortcutContext.OtherControl));
        var restored = SvgEditorShortcutProfile.Parse(profile.Serialize());
        Assert.Null(restored.Resolve(SvgEditorShortcutKey.M, SvgEditorShortcutModifiers.None, SvgEditorShortcutContext.TextInput));
    }

    [Theory]
    [InlineData("copy", SvgEditorShortcutKey.C, Primary)]
    [InlineData("selectall", SvgEditorShortcutKey.A, Primary)]
    [InlineData("undo", SvgEditorShortcutKey.Z, Primary)]
    [InlineData("redo", SvgEditorShortcutKey.Z, Primary | Shift)]
    public void CustomApplicationBindingsCannotStealNativeEditing(string original, SvgEditorShortcutKey key, SvgEditorShortcutModifiers modifiers)
    {
        var profile = new SvgEditorShortcutProfile();
        profile.Clear(original);
        Assert.True(profile.TryAssign("new", key, modifiers, out var error), error);
        Assert.Equal("new", profile.Resolve(key, modifiers, SvgEditorShortcutContext.Canvas)?.CommandId);
        Assert.Null(profile.Resolve(key, modifiers, SvgEditorShortcutContext.TextInput));
    }

    [Theory]
    [InlineData(SvgEditorShortcutKey.Space, SvgEditorShortcutModifiers.None)]
    [InlineData(SvgEditorShortcutKey.Space, Primary)]
    [InlineData(SvgEditorShortcutKey.Tab, SvgEditorShortcutModifiers.None)]
    [InlineData(SvgEditorShortcutKey.Escape, SvgEditorShortcutModifiers.None)]
    [InlineData(SvgEditorShortcutKey.Left, Primary)]
    [InlineData(SvgEditorShortcutKey.Backspace, Primary)]
    [InlineData(SvgEditorShortcutKey.F10, SvgEditorShortcutModifiers.None)]
    [InlineData(SvgEditorShortcutKey.F10, Shift)]
    [InlineData(SvgEditorShortcutKey.M, Alt)]
    public void NavigationCompositionAndMenuKeysCannotBeAssignedToUnrelatedCommands(SvgEditorShortcutKey key, SvgEditorShortcutModifiers modifiers)
    {
        var profile = new SvgEditorShortcutProfile();
        Assert.False(profile.TryAssign("duplicate", key, modifiers, out var error));
        Assert.False(string.IsNullOrWhiteSpace(error));
        Assert.Equal(0, profile.OverrideCount);
    }

    [Fact]
    public void InvalidKeysModifiersAndCommandIdsAreRejectedWithoutMutation()
    {
        var profile = new SvgEditorShortcutProfile();
        Assert.False(profile.TryAssign("duplicate", (SvgEditorShortcutKey)9999, Primary, out _));
        Assert.False(profile.TryAssign("duplicate", SvgEditorShortcutKey.D, (SvgEditorShortcutModifiers)16, out _));
        Assert.False(profile.TryAssign("invalid command", SvgEditorShortcutKey.D, Primary, out _));
        Assert.Equal(0, profile.OverrideCount);
    }

    [Theory]
    [InlineData(SvgEditorShortcutKey.Number4, "4")]
    [InlineData(SvgEditorShortcutKey.Number5, "5")]
    [InlineData(SvgEditorShortcutKey.Number8, "8")]
    [InlineData(SvgEditorShortcutKey.Number9, "9")]
    public void AllNumberKeysCanBeAssignedAndRoundTrip(SvgEditorShortcutKey key, string label)
    {
        var profile = new SvgEditorShortcutProfile();
        Assert.True(profile.TryAssign("duplicate", key, Primary, out var error), error);
        var restored = SvgEditorShortcutProfile.Parse(profile.Serialize());
        Assert.Equal("duplicate", restored.Resolve(key, Primary, SvgEditorShortcutContext.Canvas)?.CommandId);
        Assert.Equal("Ctrl+" + label, restored.GetShortcutText("duplicate"));
    }

    [Fact]
    public void ImportRejectsConflictsDuplicateIdsUnsupportedVersionsAndAmbiguousDisabledEntries()
    {
        Assert.Throws<ArgumentException>(() => SvgEditorShortcutProfile.Parse("<editor-shortcuts version='1'><binding command='save' key='79' modifiers='1'/></editor-shortcuts>"));
        Assert.Throws<FormatException>(() => SvgEditorShortcutProfile.Parse("<editor-shortcuts version='1'><binding command='save' disabled='true'/><binding command='save' disabled='true'/></editor-shortcuts>"));
        Assert.Throws<FormatException>(() => SvgEditorShortcutProfile.Parse("<editor-shortcuts version='99'/>"));
        Assert.Throws<FormatException>(() => SvgEditorShortcutProfile.Parse("<editor-shortcuts version='1'><binding command='save' disabled='true' key='83'/></editor-shortcuts>"));
        Assert.Throws<FormatException>(() => SvgEditorShortcutProfile.Parse("<editor-shortcuts version='1'><binding command='save' key='83' modifiers='16'/></editor-shortcuts>"));
    }

    [Fact]
    public void ImportCannotExpandEntitiesOrAllocateAnUnboundedProfile()
    {
        Assert.Throws<XmlException>(() => SvgEditorShortcutProfile.Parse("<!DOCTYPE editor-shortcuts [<!ENTITY x 'expanded'>]><editor-shortcuts version='1' name='&x;'/>"));
        Assert.Throws<FormatException>(() => SvgEditorShortcutProfile.Parse(new string('x', SvgEditorShortcutProfile.MaximumCharacters + 1)));
        var entries = string.Concat(Enumerable.Range(0, SvgEditorShortcutProfile.MaximumOverrides + 1).Select(index => $"<binding command='custom-{index}' disabled='true'/>"));
        Assert.Throws<FormatException>(() => SvgEditorShortcutProfile.Parse("<editor-shortcuts version='1'>" + entries + "</editor-shortcuts>"));
    }

    [Fact]
    public void PersistenceIsDeterministicAcrossCulturesAndUsesTheHostPlatformForLabels()
    {
        var previous = CultureInfo.CurrentCulture;
        try
        {
            var profile = new SvgEditorShortcutProfile { Name = "Żółw — editor" };
            Assert.True(profile.TryAssign("duplicate", SvgEditorShortcutKey.D, Primary | Alt, out _));
            var expected = profile.Serialize();
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("ar-SA");
            Assert.Equal(expected, profile.Serialize());
            var restored = SvgEditorShortcutProfile.Parse(expected);
            Assert.Equal("Ctrl+Alt+D", restored.GetShortcutText("duplicate"));
            Assert.Equal("Cmd+Option+D", restored.GetShortcutText("duplicate", true));
        }
        finally { CultureInfo.CurrentCulture = previous; }
    }
}
