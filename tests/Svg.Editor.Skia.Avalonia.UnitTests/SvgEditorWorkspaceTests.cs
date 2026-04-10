using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Interactivity;
using Svg;
using Svg.Editor.Avalonia;
using Svg.Editor.Skia;
using Svg.Editor.Skia.Avalonia;
using Svg.Skia;
using Svg.Transforms;
using Xunit;

namespace Svg.Editor.Skia.Avalonia.UnitTests;

public class SvgEditorWorkspaceTests
{
    [AvaloniaFact]
    public void SvgEditorSurface_CanBeConstructed()
    {
        var surface = new SvgEditorSurface();

        Assert.NotNull(surface);
    }

    [AvaloniaFact]
    public void SvgEditorWorkspace_LoadDocument_PopulatesSession()
    {
        const string svg = "<svg width=\"24\" height=\"24\"><rect id=\"rect1\" x=\"1\" y=\"1\" width=\"10\" height=\"10\" fill=\"red\" /></svg>";
        var path = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.svg");
        File.WriteAllText(path, svg);

        try
        {
            var workspace = new SvgEditorWorkspace();
            var host = new Window
            {
                Width = 1024,
                Height = 768,
                Content = workspace
            };

            host.Show();
            workspace.LoadDocument(path);

            Assert.NotNull(workspace.Document);
            Assert.NotNull(workspace.Session.Document);
            Assert.Equal(path, workspace.CurrentFile);
            Assert.NotEmpty(workspace.Session.Nodes);
            Assert.Contains(Path.GetFileName(path), workspace.WorkspaceTitle, StringComparison.Ordinal);

            host.Close();
        }
        finally
        {
            if (File.Exists(path))
                File.Delete(path);
        }
    }

    [AvaloniaFact]
    public void SvgEditorWorkspace_LoadDocument_ClearsPreviousSelectionState()
    {
        const string firstSvg = "<svg width=\"24\" height=\"24\"><rect id=\"rect1\" x=\"1\" y=\"1\" width=\"10\" height=\"10\" fill=\"red\" /></svg>";
        const string secondSvg = "<svg width=\"24\" height=\"24\"><circle id=\"circle1\" cx=\"8\" cy=\"8\" r=\"4\" fill=\"blue\" /></svg>";
        var firstPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}-first.svg");
        var secondPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}-second.svg");
        File.WriteAllText(firstPath, firstSvg);
        File.WriteAllText(secondPath, secondSvg);

        try
        {
            var workspace = new SvgEditorWorkspace();
            var host = new Window
            {
                Width = 1024,
                Height = 768,
                Content = workspace
            };

            host.Show();
            workspace.LoadDocument(firstPath);

            var previousElement = Assert.IsType<SvgRectangle>(workspace.Document!.Children.OfType<SvgRectangle>().Single());
            SetPrivateField(workspace, "_selectedSvgElement", previousElement);
            SetPrivateField(workspace, "_selectedElement", previousElement);
            var multiSelected = GetPrivateField<IList>(workspace, "_multiSelected");
            multiSelected.Add(previousElement);
            workspace.Session.SetSelectedElementIds(new[] { previousElement.ID });

            workspace.LoadDocument(secondPath);

            Assert.Null(GetPrivateField<SvgElement?>(workspace, "_selectedSvgElement"));
            Assert.Null(GetPrivateField<SvgVisualElement?>(workspace, "_selectedElement"));
            Assert.Empty(GetPrivateField<IList>(workspace, "_multiSelected").Cast<object>());
            Assert.Empty(workspace.Session.SelectedElementIds);
            Assert.NotNull(workspace.Document);
            Assert.Contains(workspace.Document!.Children.OfType<SvgCircle>(), element => element.ID == "circle1");

            host.Close();
        }
        finally
        {
            if (File.Exists(firstPath))
                File.Delete(firstPath);

            if (File.Exists(secondPath))
                File.Delete(secondPath);
        }
    }

    [AvaloniaFact]
    public void SvgEditorWorkspace_NewMenuItem_PreservesUndoHistory()
    {
        const string svg = "<svg width=\"24\" height=\"24\"><rect id=\"rect1\" x=\"1\" y=\"1\" width=\"10\" height=\"10\" fill=\"red\" /></svg>";
        var path = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.svg");
        File.WriteAllText(path, svg);

        try
        {
            var workspace = new SvgEditorWorkspace();
            var host = new Window
            {
                Width = 1024,
                Height = 768,
                Content = workspace
            };

            host.Show();
            workspace.LoadDocument(path);

            InvokePrivateMenuHandler(workspace, "NewMenuItem_Click");

            Assert.True(workspace.Session.UndoCount > 0);

            InvokePrivateMenuHandler(workspace, "UndoMenuItem_Click");

            Assert.NotNull(workspace.Document);
            Assert.Contains(workspace.Document!.Children.OfType<SvgRectangle>(), element => element.ID == "rect1");

            host.Close();
        }
        finally
        {
            if (File.Exists(path))
                File.Delete(path);
        }
    }

    [AvaloniaFact]
    public void SvgEditorWorkspace_LoadDocument_ResolvesAvaresResourceFromLoadedAssembly()
    {
        var workspace = new SvgEditorWorkspace();
        var host = new Window
        {
            Width = 1024,
            Height = 768,
            Content = workspace
        };

        host.Show();
        workspace.LoadDocument("Assets/embedded-test.svg");

        Assert.NotNull(workspace.Document);
        Assert.Equal("Assets/embedded-test.svg", workspace.CurrentFile);
        Assert.NotEmpty(workspace.Session.Nodes);

        host.Close();
    }

    [AvaloniaFact]
    public void SvgEditorWorkspace_UsesHostProvidedTitlePrefix()
    {
        const string svg = "<svg width=\"24\" height=\"24\"><rect id=\"rect1\" x=\"1\" y=\"1\" width=\"10\" height=\"10\" fill=\"red\" /></svg>";
        var path = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.svg");
        File.WriteAllText(path, svg);

        try
        {
            var workspace = new SvgEditorWorkspace
            {
                WorkspaceTitlePrefix = "HostApp"
            };

            var host = new Window
            {
                Width = 1024,
                Height = 768,
                Content = workspace
            };

            host.Show();
            workspace.LoadDocument(path);

            Assert.Equal($"HostApp - {Path.GetFileName(path)}", workspace.WorkspaceTitle);

            host.Close();
        }
        finally
        {
            if (File.Exists(path))
                File.Delete(path);
        }
    }

    [AvaloniaFact]
    public async Task SvgEditorWorkspace_OpenDocumentAsync_UsesInjectedFileDialogService()
    {
        const string svg = "<svg width=\"16\" height=\"16\"><circle id=\"circle1\" cx=\"8\" cy=\"8\" r=\"4\" fill=\"red\" /></svg>";
        var path = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.svg");
        File.WriteAllText(path, svg);

        try
        {
            var workspace = new SvgEditorWorkspace
            {
                FileDialogService = new FakeFileDialogService { OpenSvgDocumentPath = path }
            };

            var host = new Window
            {
                Width = 1024,
                Height = 768,
                Content = workspace
            };

            host.Show();
            await workspace.OpenDocumentAsync();

            Assert.NotNull(workspace.Document);
            Assert.Equal(path, workspace.CurrentFile);
            Assert.NotEmpty(workspace.Session.Nodes);

            host.Close();
        }
        finally
        {
            if (File.Exists(path))
                File.Delete(path);
        }
    }

    [AvaloniaFact]
    public void SvgEditorWorkspace_LoadDocument_PopulatesLayersWithRetainedSceneNodes()
    {
        const string svg = "<svg width=\"64\" height=\"32\">" +
                           "<g id=\"layer-a\" data-layer=\"true\" data-name=\"Layer A\">" +
                           "<rect id=\"rect1\" x=\"4\" y=\"5\" width=\"20\" height=\"10\" fill=\"red\" />" +
                           "</g>" +
                           "</svg>";
        var path = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.svg");
        File.WriteAllText(path, svg);

        try
        {
            var workspace = new SvgEditorWorkspace();
            var host = new Window
            {
                Width = 1024,
                Height = 768,
                Content = workspace
            };

            host.Show();
            workspace.LoadDocument(path);

            var layer = Assert.Single(workspace.Layers);
            var sceneNode = Assert.IsType<SvgSceneNode>(layer.SceneNode);
            Assert.Equal("layer-a", sceneNode.ElementId);
            Assert.True(sceneNode.TransformedBounds.Width > 0);

            host.Close();
        }
        finally
        {
            if (File.Exists(path))
                File.Delete(path);
        }
    }

    [AvaloniaFact]
    public void SvgEditorWorkspace_PathTool_UsesRetainedSceneNodeForEditContext()
    {
        const string svg = "<svg width=\"64\" height=\"64\"><g transform=\"translate(12,8)\"><path id=\"path1\" d=\"M 0 0 L 10 0\" /></g></svg>";
        var path = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.svg");
        File.WriteAllText(path, svg);

        try
        {
            var workspace = new SvgEditorWorkspace();
            var host = new Window
            {
                Width = 1024,
                Height = 768,
                Content = workspace
            };

            host.Show();
            workspace.LoadDocument(path);

            var element = Assert.IsType<SvgPath>(workspace.Document!.Children.OfType<SvgGroup>().Single().Children.Single());
            SetPrivateField(workspace, "_selectedSvgElement", element);
            SetPrivateField(workspace, "_selectedElement", element);

            var updateSelectedSceneState = typeof(SvgEditorWorkspace).GetMethod("UpdateSelectedSceneState", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.NotNull(updateSelectedSceneState);
            updateSelectedSceneState!.Invoke(workspace, null);

            InvokePrivateMenuHandler(workspace, "PathToolButton_Click");

            var selectedSceneNode = GetPrivateField<SvgSceneNode?>(workspace, "_selectedSceneNode");
            var pathService = GetPrivateField<PathService>(workspace, "_pathService");

            Assert.NotNull(selectedSceneNode);
            Assert.Same(selectedSceneNode, pathService.EditSceneNode);
            Assert.Equal(new ShimSkiaSharp.SKPoint(12, 8), pathService.PathMatrix.MapPoint(new ShimSkiaSharp.SKPoint(0, 0)));

            host.Close();
        }
        finally
        {
            if (File.Exists(path))
                File.Delete(path);
        }
    }

    [AvaloniaFact]
    public async Task SvgEditorWorkspace_ExportSelectedElementAsync_UsesRetainedSceneNodeWhenDrawableIsMissing()
    {
        const string svg = "<svg width=\"64\" height=\"64\"><g transform=\"translate(12,8)\"><rect id=\"rect1\" x=\"1\" y=\"2\" width=\"10\" height=\"6\" fill=\"red\" /></g></svg>";
        var path = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.svg");
        var exportPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.png");
        File.WriteAllText(path, svg);

        try
        {
            var workspace = new SvgEditorWorkspace
            {
                FileDialogService = new FakeFileDialogService { SaveElementPngPath = exportPath }
            };
            var host = new Window
            {
                Width = 1024,
                Height = 768,
                Content = workspace
            };

            host.Show();
            workspace.LoadDocument(path);

            var element = Assert.IsType<SvgRectangle>(workspace.Document!.Children.OfType<SvgGroup>().Single().Children.Single());
            SetPrivateField(workspace, "_selectedSvgElement", element);
            SetPrivateField(workspace, "_selectedElement", element);

            var updateSelectedSceneState = typeof(SvgEditorWorkspace).GetMethod("UpdateSelectedSceneState", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.NotNull(updateSelectedSceneState);
            updateSelectedSceneState!.Invoke(workspace, null);

            var selectedSceneNode = GetPrivateField<SvgSceneNode?>(workspace, "_selectedSceneNode");
            Assert.NotNull(selectedSceneNode);

            await workspace.ExportSelectedElementAsync();

            Assert.True(File.Exists(exportPath));
            Assert.True(new FileInfo(exportPath).Length > 0);

            host.Close();
        }
        finally
        {
            if (File.Exists(path))
                File.Delete(path);

            if (File.Exists(exportPath))
                File.Delete(exportPath);
        }
    }

    [AvaloniaFact]
    public void SvgEditorWorkspace_AlignSelected_UsesRetainedSceneBoundsWhenDrawablesAreMissing()
    {
        const string svg = "<svg width=\"64\" height=\"64\">" +
                           "<rect id=\"rect1\" x=\"10\" y=\"10\" width=\"10\" height=\"10\" fill=\"red\" />" +
                           "<rect id=\"rect2\" x=\"30\" y=\"10\" width=\"10\" height=\"10\" fill=\"blue\" />" +
                           "</svg>";
        var path = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.svg");
        File.WriteAllText(path, svg);

        try
        {
            var workspace = new SvgEditorWorkspace();
            var host = new Window
            {
                Width = 1024,
                Height = 768,
                Content = workspace
            };

            host.Show();
            workspace.LoadDocument(path);

            var elements = workspace.Document!.Children.OfType<SvgRectangle>().Cast<SvgVisualElement>().ToArray();
            Assert.Equal(2, elements.Length);

            var multiSelected = GetPrivateField<IList>(workspace, "_multiSelected");
            multiSelected.Add(elements[0]);
            multiSelected.Add(elements[1]);

            var updateSelectedSceneState = typeof(SvgEditorWorkspace).GetMethod("UpdateSelectedSceneState", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.NotNull(updateSelectedSceneState);
            updateSelectedSceneState!.Invoke(workspace, null);

            var multiSceneNodes = GetPrivateField<IList>(workspace, "_multiSceneNodes");
            Assert.Equal(2, multiSceneNodes.Count);

            InvokePrivateMenuHandler(workspace, "AlignLeftMenuItem_Click");

            var translation = Assert.Single(elements[1].Transforms!.OfType<SvgTranslate>());
            Assert.Equal(-20f, translation.X);
            Assert.Equal(0f, translation.Y);

            host.Close();
        }
        finally
        {
            if (File.Exists(path))
                File.Delete(path);
        }
    }

    [AvaloniaFact]
    public void SvgEditorWorkspace_FlipSelected_UsesRetainedSceneBoundsWhenDrawablesAreMissing()
    {
        const string svg = "<svg width=\"64\" height=\"64\"><rect id=\"rect1\" x=\"10\" y=\"10\" width=\"10\" height=\"10\" fill=\"red\" /></svg>";
        var path = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.svg");
        File.WriteAllText(path, svg);

        try
        {
            var workspace = new SvgEditorWorkspace();
            var host = new Window
            {
                Width = 1024,
                Height = 768,
                Content = workspace
            };

            host.Show();
            workspace.LoadDocument(path);

            var element = Assert.IsType<SvgRectangle>(workspace.Document!.Children.Single());
            SetPrivateField(workspace, "_selectedSvgElement", element);
            SetPrivateField(workspace, "_selectedElement", element);

            var updateSelectedSceneState = typeof(SvgEditorWorkspace).GetMethod("UpdateSelectedSceneState", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.NotNull(updateSelectedSceneState);
            updateSelectedSceneState!.Invoke(workspace, null);

            var selectedSceneNode = GetPrivateField<SvgSceneNode?>(workspace, "_selectedSceneNode");
            Assert.NotNull(selectedSceneNode);

            InvokePrivateMenuHandler(workspace, "FlipHMenuItem_Click");

            var transforms = element.Transforms;
            Assert.NotNull(transforms);
            Assert.Contains(transforms, transform => transform is SvgTranslate translate && translate.X == 15f && translate.Y == 15f);
            Assert.Contains(transforms, transform => transform is SvgScale scale && scale.X == -1f && scale.Y == 1f);
            Assert.Contains(transforms, transform => transform is SvgTranslate translate && translate.X == -15f && translate.Y == -15f);

            host.Close();
        }
        finally
        {
            if (File.Exists(path))
                File.Delete(path);
        }
    }

    private sealed class FakeFileDialogService : ISvgEditorFileDialogService
    {
        public string? OpenSvgDocumentPath { get; init; }
        public string? SaveElementPngPath { get; init; }

        public Task<string?> OpenSvgDocumentAsync(TopLevel? owner)
            => Task.FromResult(OpenSvgDocumentPath);

        public Task<string?> SaveSvgDocumentAsync(TopLevel? owner, string? currentFile)
            => Task.FromResult<string?>(null);

        public Task<string?> SaveElementPngAsync(TopLevel? owner, string? currentFile)
            => Task.FromResult(SaveElementPngPath);

        public Task<string?> SavePdfAsync(TopLevel? owner, string? currentFile)
            => Task.FromResult<string?>(null);

        public Task<string?> SaveXpsAsync(TopLevel? owner, string? currentFile)
            => Task.FromResult<string?>(null);

        public Task<string?> OpenImageAsync(TopLevel? owner)
            => Task.FromResult<string?>(null);
    }

    private static void InvokePrivateMenuHandler(SvgEditorWorkspace workspace, string methodName)
    {
        var method = typeof(SvgEditorWorkspace).GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(method);
        method!.Invoke(workspace, new object?[] { null, new RoutedEventArgs() });
    }

    private static T GetPrivateField<T>(object instance, string fieldName)
    {
        var field = instance.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(field);
        var value = field!.GetValue(instance);
        return value is null ? default! : (T)value;
    }

    private static void SetPrivateField<T>(object instance, string fieldName, T value)
    {
        var field = instance.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(field);
        field!.SetValue(instance, value);
    }
}
