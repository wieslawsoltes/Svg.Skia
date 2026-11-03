using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Avalonia.Headless.XUnit;
using SkiaSharp;
using Svg.Model.Services;
using Svg.Skia;
using Svg.Skia.TypefaceProviders;
using Xunit;

namespace Avalonia.Svg.Skia.UnitTests;

[CollectionDefinition("SvgSource default settings", DisableParallelization = true)]
public sealed class SvgSourceSettingsCollection
{
}

[Collection("SvgSource default settings")]
public class SvgSourceSettingsTests
{
    private const string SampleSvg = """
        <svg xmlns="http://www.w3.org/2000/svg" width="240" height="40">
          <text x="4" y="28" font-family="Issue565ApplicationFont, sans-serif" font-size="20">Application fonts</text>
        </svg>
        """;

    private const string ShapeSvg = """
        <svg xmlns="http://www.w3.org/2000/svg" width="10" height="10">
          <rect width="10" height="10" fill="red" />
        </svg>
        """;

    [AvaloniaFact]
    public void DefaultSettings_IncludeAvaloniaProviderBeforeSkiaProviders()
    {
        var providers = SvgSource.DefaultSettings.TypefaceProviders;

        Assert.NotNull(providers);
        Assert.IsType<AvaloniaTypefaceProvider>(providers[0]);
        Assert.Single(providers.OfType<AvaloniaTypefaceProvider>());
        Assert.Contains(providers, provider => provider is FontManagerTypefaceProvider);
        Assert.Contains(providers, provider => provider is DefaultTypefaceProvider);
    }

    [AvaloniaTheory]
    [InlineData("svg")]
    [InlineData("stream")]
    [InlineData("document")]
    [InlineData("file")]
    [InlineData("lazy-file")]
    [InlineData("async-file")]
    public async Task Load_UsesConfiguredSettingsAndTypefaceProviders(string sourceKind)
    {
        using var scope = new DefaultSettingsScope();
        var provider = new RecordingTypefaceProvider();
        SvgSource.ConfigureDefaultSettings(settings =>
        {
            settings.EnableTextSelectionRendering = false;
            settings.JavaScriptTimeoutMilliseconds = 565;
            settings.TypefaceProviders!.Insert(0, provider);
        });

        var path = Path.Combine(Path.GetTempPath(), $"svg-settings-{Guid.NewGuid():N}.svg");
        try
        {
            using var source = await LoadSource(sourceKind, path);

            Assert.NotNull(source.Picture);
            Assert.NotNull(source.Svg);
            Assert.False(source.Svg.Settings.EnableTextSelectionRendering);
            Assert.Equal(565, source.Svg.Settings.JavaScriptTimeoutMilliseconds);
            Assert.Same(provider, source.Svg.Settings.TypefaceProviders![0]);
            Assert.NotSame(SvgSource.DefaultSettings.TypefaceProviders, source.Svg.Settings.TypefaceProviders);
            Assert.Contains("Issue565ApplicationFont", provider.RequestedFamilies);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [AvaloniaTheory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Load_EmbeddedResource_UsesConfiguredDefaults(bool asynchronous)
    {
        using var scope = new DefaultSettingsScope();
        SvgSource.ConfigureDefaultSettings(settings => settings.EnableTextSelectionRendering = false);
        const string path = "avares://Svg.Controls.Skia.Avalonia.UnitTests/Assets/Icon.svg";

        using var source = asynchronous ? await SvgSource.LoadAsync(path) : SvgSource.Load(path);

        Assert.NotNull(source.Picture);
        Assert.False(source.Svg!.Settings.EnableTextSelectionRendering);
    }

    [AvaloniaFact]
    public void DefaultSettings_PreserveLegacySharedSettingsCompatibility()
    {
        using var scope = new DefaultSettingsScope();
        var legacySettings = SvgSource.s_skiaModel.Settings;
        legacySettings.EnableTextSelectionRendering = false;

        using var source = SvgSource.LoadFromSvg(ShapeSvg);

        Assert.Same(legacySettings, SvgSource.DefaultSettings);
        Assert.False(source.Svg!.Settings.EnableTextSelectionRendering);

        SvgSource.ConfigureDefaultSettings(settings => settings.EnableTextSelectionRendering = true);

        Assert.Same(legacySettings, SvgSource.DefaultSettings);
        Assert.True(legacySettings.EnableTextSelectionRendering);
        Assert.False(source.Svg.Settings.EnableTextSelectionRendering);
    }

    [AvaloniaFact]
    public void ConfigureDefaultSettings_NullCallback_Throws()
    {
        Assert.Throws<ArgumentNullException>("configure", () => SvgSource.ConfigureDefaultSettings(null!));
    }

    [AvaloniaFact]
    public void ConfigureDefaultSettings_ThrowingCallback_DoesNotPublishPartialChanges()
    {
        using var scope = new DefaultSettingsScope();
        var providers = SvgSource.DefaultSettings.TypefaceProviders;
        var originalSelectionRendering = SvgSource.DefaultSettings.EnableTextSelectionRendering;
        var originalTimeout = SvgSource.DefaultSettings.JavaScriptTimeoutMilliseconds;
        var failure = new InvalidOperationException("Configuration failed.");

        var exception = Assert.Throws<InvalidOperationException>(() => SvgSource.ConfigureDefaultSettings(settings =>
        {
            settings.EnableTextSelectionRendering = !originalSelectionRendering;
            settings.JavaScriptTimeoutMilliseconds = originalTimeout + 1;
            settings.TypefaceProviders!.Clear();
            throw failure;
        }));

        Assert.Same(failure, exception);
        Assert.Equal(originalSelectionRendering, SvgSource.DefaultSettings.EnableTextSelectionRendering);
        Assert.Equal(originalTimeout, SvgSource.DefaultSettings.JavaScriptTimeoutMilliseconds);
        Assert.Same(providers, SvgSource.DefaultSettings.TypefaceProviders);
        Assert.NotEmpty(providers!);
    }

    [AvaloniaFact]
    public void ConfigureDefaultSettings_CapturedDraft_CannotMutatePublishedSettings()
    {
        using var scope = new DefaultSettingsScope();
        var provider = new RecordingTypefaceProvider();
        SKSvgSettings? draft = null;
        SvgSource.ConfigureDefaultSettings(settings =>
        {
            draft = settings;
            settings.EnableTextSelectionRendering = false;
            settings.TypefaceProviders = new List<ITypefaceProvider> { provider };
        });

        Assert.NotNull(draft);
        draft.EnableTextSelectionRendering = true;
        draft.TypefaceProviders!.Clear();

        using var source = SvgSource.LoadFromSvg(ShapeSvg);

        Assert.False(SvgSource.DefaultSettings.EnableTextSelectionRendering);
        Assert.Same(provider, Assert.Single(SvgSource.DefaultSettings.TypefaceProviders!));
        Assert.False(source.Svg!.Settings.EnableTextSelectionRendering);
        Assert.Same(provider, Assert.Single(source.Svg.Settings.TypefaceProviders!));
    }

    [AvaloniaFact]
    public void Sources_KeepIndependentSettingsAndProviderLists()
    {
        using var scope = new DefaultSettingsScope();
        var initialProvider = new RecordingTypefaceProvider();
        SvgSource.ConfigureDefaultSettings(settings =>
        {
            settings.EnableTextSelectionRendering = false;
            settings.TypefaceProviders = new List<ITypefaceProvider> { initialProvider };
        });
        using var first = SvgSource.LoadFromSvg(ShapeSvg);
        using var second = SvgSource.LoadFromSvg(ShapeSvg);
        first.Svg!.Settings.EnableTextSelectionRendering = true;
        first.Svg.Settings.TypefaceProviders!.Clear();

        var replacementProvider = new RecordingTypefaceProvider();
        SvgSource.ConfigureDefaultSettings(settings =>
        {
            settings.EnableTextSelectionRendering = true;
            settings.TypefaceProviders = new List<ITypefaceProvider> { replacementProvider };
        });
        using var subsequent = SvgSource.LoadFromSvg(ShapeSvg);

        Assert.Empty(first.Svg.Settings.TypefaceProviders);
        Assert.False(second.Svg!.Settings.EnableTextSelectionRendering);
        Assert.Same(initialProvider, Assert.Single(second.Svg.Settings.TypefaceProviders!));
        Assert.True(subsequent.Svg!.Settings.EnableTextSelectionRendering);
        Assert.Same(replacementProvider, Assert.Single(subsequent.Svg.Settings.TypefaceProviders!));
        Assert.NotSame(SvgSource.DefaultSettings.TypefaceProviders, subsequent.Svg.Settings.TypefaceProviders);
    }

    [AvaloniaFact]
    public void Clone_PreservesSourceSettingsAfterDefaultsChange()
    {
        using var scope = new DefaultSettingsScope();
        var provider = new RecordingTypefaceProvider();
        SvgSource.ConfigureDefaultSettings(settings =>
        {
            settings.EnableTextSelectionRendering = false;
            settings.TypefaceProviders = new List<ITypefaceProvider> { provider };
        });
        using var source = SvgSource.LoadFromSvg(ShapeSvg);
        SvgSource.ConfigureDefaultSettings(settings =>
        {
            settings.EnableTextSelectionRendering = true;
            settings.TypefaceProviders!.Clear();
        });

        using var clone = source.Clone();

        Assert.False(clone.Svg!.Settings.EnableTextSelectionRendering);
        Assert.Same(provider, Assert.Single(clone.Svg.Settings.TypefaceProviders!));
        Assert.NotSame(source.Svg!.Settings.TypefaceProviders, clone.Svg.Settings.TypefaceProviders);
        clone.Svg.Settings.TypefaceProviders!.Clear();
        Assert.Same(provider, Assert.Single(source.Svg.Settings.TypefaceProviders!));
    }

    [AvaloniaTheory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Reload_AppliesUpdatedDefaults(bool asynchronous)
    {
        using var scope = new DefaultSettingsScope();
        SvgSource.ConfigureDefaultSettings(settings => settings.EnableTextSelectionRendering = true);
        using var source = SvgSource.LoadFromSvg(ShapeSvg);
        SvgSource.ConfigureDefaultSettings(settings => settings.EnableTextSelectionRendering = false);

        Assert.True(source.Svg!.Settings.EnableTextSelectionRendering);

        if (asynchronous)
        {
            await source.ReLoadAsync(null);
        }
        else
        {
            source.ReLoad(null);
        }

        Assert.NotNull(source.Picture);
        Assert.False(source.Svg!.Settings.EnableTextSelectionRendering);
    }

    [AvaloniaFact]
    public void ConfigureDefaultSettings_NullProviderList_IsSupported()
    {
        using var scope = new DefaultSettingsScope();
        SvgSource.ConfigureDefaultSettings(settings => settings.TypefaceProviders = null);

        using var source = SvgSource.LoadFromSvg(ShapeSvg);

        Assert.NotNull(source.Picture);
        Assert.Null(source.Svg!.Settings.TypefaceProviders);
    }

    private static async Task<SvgSource> LoadSource(string sourceKind, string path)
    {
        switch (sourceKind)
        {
            case "svg":
                return SvgSource.LoadFromSvg(SampleSvg);
            case "stream":
                using (var stream = new MemoryStream(Encoding.UTF8.GetBytes(SampleSvg)))
                {
                    return SvgSource.LoadFromStream(stream);
                }
            case "document":
                return SvgSource.LoadFromSvgDocument(SvgService.FromSvg(SampleSvg)!);
            case "file":
                File.WriteAllText(path, SampleSvg);
                return SvgSource.Load(path);
            case "lazy-file":
                File.WriteAllText(path, SampleSvg);
                return new SvgSource(default(Uri)) { Path = path };
            case "async-file":
                File.WriteAllText(path, SampleSvg);
                return await SvgSource.LoadAsync(path);
            default:
                throw new ArgumentOutOfRangeException(nameof(sourceKind));
        }
    }

    private sealed class DefaultSettingsScope : IDisposable
    {
        private readonly SKSvgSettings _settings = SvgSource.DefaultSettings.Clone();

        public void Dispose()
        {
            SvgSource.ConfigureDefaultSettings(settings => _settings.CopyTo(settings));
        }
    }

    private sealed class RecordingTypefaceProvider : ITypefaceProvider
    {
        public List<string> RequestedFamilies { get; } = new();

        public SKTypeface? FromFamilyName(
            string fontFamily,
            SKFontStyleWeight fontWeight,
            SKFontStyleWidth fontWidth,
            SKFontStyleSlant fontStyle)
        {
            RequestedFamilies.Add(fontFamily);
            return null;
        }
    }
}
