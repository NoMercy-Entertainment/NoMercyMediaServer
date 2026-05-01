using Microsoft.Extensions.DependencyInjection;
using NoMercy.Encoder.BuildingBlocks;
using NoMercy.Encoder.Commands;

namespace NoMercy.Tests.Encoder.BuildingBlocks;

/// <summary>
/// Verifies that encoder building-block interfaces can be replaced by a
/// downstream registrant (plugin or host) simply by adding a later
/// AddTransient/AddSingleton call.  MS DI returns the last registration
/// when GetRequiredService is called, so no special hook is needed.
/// </summary>
public class BuildingBlockReplacementTests
{
    // ------------------------------------------------------------------ //
    // Test 1 — plugin registers a fake IFontExtractor after the defaults  //
    // ------------------------------------------------------------------ //

    [Fact]
    public void Plugin_RegistersFakeFontExtractor_DefaultIsReplaced()
    {
        ServiceCollection services = new();

        // Register only the building block under test — no full encoder
        // stack needed; we just prove the last-registration-wins contract.
        services.AddTransient<IFontExtractor, ProductionFontExtractor>();
        services.AddTransient<IFontExtractor, FakeFontExtractor>();

        ServiceProvider provider = services.BuildServiceProvider();
        IFontExtractor resolved = provider.GetRequiredService<IFontExtractor>();

        resolved.Should().BeOfType<FakeFontExtractor>();
    }

    // ------------------------------------------------------------------ //
    // Test 2 — without a plugin, the production type is resolved          //
    // ------------------------------------------------------------------ //

    [Fact]
    public void NoPlugin_DefaultBuildingBlockResolved()
    {
        ServiceCollection services = new();

        services.AddTransient<IFontExtractor, ProductionFontExtractor>();

        ServiceProvider provider = services.BuildServiceProvider();
        IFontExtractor resolved = provider.GetRequiredService<IFontExtractor>();

        resolved.Should().BeOfType<ProductionFontExtractor>();
    }

    // ------------------------------------------------------------------ //
    // Minimal stubs — no real FFmpeg calls                                //
    // ------------------------------------------------------------------ //

    private static FfmpegCommand StubCommand() =>
        new(Executable: "ffmpeg", Arguments: [], WorkingDirectory: null);

    private sealed class ProductionFontExtractor : IFontExtractor
    {
        public FfmpegCommand BuildExtractionCommand(
            string ffmpegPath,
            string inputPath,
            string outputDirectory
        ) => StubCommand();

        public Task WriteFontManifestAsync(string outputDirectory, CancellationToken ct) =>
            Task.CompletedTask;
    }

    private sealed class FakeFontExtractor : IFontExtractor
    {
        public FfmpegCommand BuildExtractionCommand(
            string ffmpegPath,
            string inputPath,
            string outputDirectory
        ) => StubCommand();

        public Task WriteFontManifestAsync(string outputDirectory, CancellationToken ct) =>
            Task.CompletedTask;
    }
}
