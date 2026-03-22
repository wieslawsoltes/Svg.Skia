using Svg.Editor.Skia.Uno.Models;

namespace Svg.Editor.Skia.Uno;

public static class SvgEditorFramePresetCatalog
{
    public static IReadOnlyList<EditorFramePresetItem> CreateDefault()
    {
        return
        [
            new EditorFramePresetItem { Id = EditorFramePresetItem.CustomId, Name = "Custom", Category = "Current selection" },
            new EditorFramePresetItem { Id = "desktop", Name = "Desktop", Category = "Desktop", Width = 1440, Height = 1024 },
            new EditorFramePresetItem { Id = "wireframes", Name = "Wireframes", Category = "Desktop", Width = 1440, Height = 1024 },
            new EditorFramePresetItem { Id = "tv", Name = "TV", Category = "Desktop", Width = 1280, Height = 720 },
            new EditorFramePresetItem { Id = "iphone17", Name = "iPhone 17", Category = "Phones", Width = 402, Height = 874 },
            new EditorFramePresetItem { Id = "iphone16", Name = "iPhone 16", Category = "Phones", Width = 393, Height = 852 },
            new EditorFramePresetItem { Id = "iphone14plus", Name = "iPhone 14 Plus", Category = "Phones", Width = 428, Height = 926 },
            new EditorFramePresetItem { Id = "androidcompact", Name = "Android Compact", Category = "Phones", Width = 412, Height = 917 },
            new EditorFramePresetItem { Id = "androidmedium", Name = "Android Medium", Category = "Phones", Width = 700, Height = 840 },
            new EditorFramePresetItem { Id = "ipadmini", Name = "iPad mini 8.3", Category = "Tablets", Width = 744, Height = 1133 },
            new EditorFramePresetItem { Id = "surfacepro8", Name = "Surface Pro 8", Category = "Tablets", Width = 1440, Height = 960 },
            new EditorFramePresetItem { Id = "slide16x9", Name = "Slide 16:9", Category = "Presentation", Width = 1920, Height = 1080 },
            new EditorFramePresetItem { Id = "slide4x3", Name = "Slide 4:3", Category = "Presentation", Width = 1024, Height = 768 }
        ];
    }
}
