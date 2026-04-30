namespace NoMercy.Tests.Encoder.BuildingBlocks.Drm;

using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using NoMercy.Encoder.BuildingBlocks.Drm;
using NoMercy.Encoder.Composition;
using NoMercy.Encoder.Infrastructure;
using NoMercy.Storage;

public class CencDrmProcessorTests
{
    // ── Helpers ──────────────────────────────────────────────────────────────

    private static byte[] FakeKeyId() =>
        Enumerable.Range(0, 16).Select(i => (byte)(0x10 + i)).ToArray();

    private static byte[] FakeKey() =>
        Enumerable.Range(0, 16).Select(i => (byte)(0xA0 + i)).ToArray();

    private static CencKeyEntry SdEntry() => new("SD", FakeKeyId(), FakeKey());

    private static DrmConfig CencConfig(IReadOnlyList<CencKeyEntry>? keys = null) =>
        new(
            DrmMethod.Cenc,
            KeyUri: "https://license.example/widevine",
            CencKeys: keys ?? [SdEntry()]
        );

    private static IStorage MakeStorage() => Mock.Of<IStorage>();

    private static CencDrmProcessor BuildSut(
        string? packagerPath = "/fake/packager",
        IProcessRunner? runner = null
    )
    {
        EncoderOptions opts = new()
        {
            FfmpegPathOverride = "/fake/ffmpeg",
            ShakaPackagerPathOverride = packagerPath,
        };
        IProcessRunner processRunner =
            runner
            ?? Mock.Of<IProcessRunner>(r =>
                r.RunAsync(
                    It.IsAny<string>(),
                    It.IsAny<string[]>(),
                    It.IsAny<string>(),
                    It.IsAny<CancellationToken>()
                ) == Task.FromResult(new ProcessResult(0, "", "", TimeSpan.Zero))
            );
        return new CencDrmProcessor(
            opts,
            processRunner,
            NullLogger<CencDrmProcessor>.Instance,
            MakeStorage()
        );
    }

    // ── BuildPackagerArguments (pure, no I/O) ─────────────────────────────

    [Fact]
    public void BuildPackagerArguments_SingleKey_ContainsRawKeyFlag()
    {
        CencKeyEntry entry = SdEntry();
        CencStreamDescriptor descriptor = new(
            "/out/video.mp4",
            "video",
            "/out/video_enc.mp4",
            "SD"
        );

        List<string> args = CencDrmProcessor.BuildPackagerArguments(
            [descriptor],
            [entry],
            "/out/manifest.mpd"
        );

        args.Should().Contain("--enable_raw_key_encryption");
        args.Should().Contain("--mpd_output");
        args.Should().Contain("/out/manifest.mpd");
    }

    [Fact]
    public void BuildPackagerArguments_KeysFlag_HasCorrectHexFormat()
    {
        CencKeyEntry entry = new("HD", new byte[16], new byte[16]);
        CencStreamDescriptor descriptor = new("/in.mp4", "video", "/out.mp4", "HD");

        List<string> args = CencDrmProcessor.BuildPackagerArguments(
            [descriptor],
            [entry],
            "/manifest.mpd"
        );

        // --keys must be followed by the label:key_id:key triple
        int keysIndex = args.IndexOf("--keys");
        keysIndex.Should().BeGreaterThanOrEqualTo(0, "--keys flag must be present");

        string keySpec = args[keysIndex + 1];
        keySpec.Should().StartWith("label=HD:");
        keySpec.Should().Contain(":key_id=");
        keySpec.Should().Contain(":key=");
        // 16 zero bytes → 32 zero hex chars
        keySpec.Should().Contain("key_id=00000000000000000000000000000000");
        keySpec.Should().Contain(":key=00000000000000000000000000000000");
    }

    [Fact]
    public void BuildPackagerArguments_StreamSpec_ForwardSlashes()
    {
        CencStreamDescriptor descriptor = new(
            @"C:\out\video.mp4",
            "video",
            @"C:\out\video_enc.mp4",
            "SD"
        );

        List<string> args = CencDrmProcessor.BuildPackagerArguments(
            [descriptor],
            [SdEntry()],
            "/manifest.mpd"
        );

        string spec = args[0];
        spec.Should().NotContain("\\", "shaka-packager requires forward slashes");
        spec.Should().StartWith("in=C:/out/video.mp4");
    }

    [Fact]
    public void BuildPackagerArguments_MultipleKeys_EmitsOneKeysEntryEach()
    {
        List<CencKeyEntry> keys =
        [
            new("SD", FakeKeyId(), FakeKey()),
            new("HD", FakeKeyId(), FakeKey()),
        ];
        CencStreamDescriptor descriptor = new("/v.mp4", "video", "/v_enc.mp4", "SD");

        List<string> args = CencDrmProcessor.BuildPackagerArguments(
            [descriptor],
            keys,
            "/manifest.mpd"
        );

        int count = args.Count(a => a == "--keys");
        count.Should().Be(2, "one --keys flag per key entry");
    }

    // ── PrepareAsync — must be a no-op for CENC ───────────────────────────

    [Fact]
    public async Task PrepareAsync_Cenc_ReturnsEmptySentinel()
    {
        CencDrmProcessor sut = BuildSut();
        DrmConfig config = CencConfig();

        DrmArtifact artifact = await sut.PrepareAsync(
            "/tmp/drm-test",
            config,
            CancellationToken.None
        );

        artifact.KeyInfoFilePath.Should().BeEmpty("CENC pre-encode prep is a no-op");
        artifact.KeyFilePath.Should().BeEmpty();
        artifact.Key.Should().BeEmpty();
        artifact.Iv.Should().BeEmpty();
        artifact.KeyUri.Should().Be(config.KeyUri);
    }

    [Fact]
    public async Task PrepareAsync_WrongMethod_ThrowsArgumentException()
    {
        CencDrmProcessor sut = BuildSut();
        DrmConfig wrongConfig = new(DrmMethod.Aes128, KeyUri: "http://k");

        Func<Task> act = () =>
            sut.PrepareAsync("/tmp/drm-test", wrongConfig, CancellationToken.None);

        await act.Should().ThrowAsync<ArgumentException>().WithMessage("*CENC only*");
    }

    // ── PackageAsync error paths ───────────────────────────────────────────

    [Fact]
    public async Task PackageAsync_WithMissingPackager_ThrowsClearError()
    {
        // ShakaPackagerPathOverride points to a non-existent file;
        // FfmpegPathOverride sibling also won't exist → GetShakaPackagerPath() throws.
        EncoderOptions opts = new()
        {
            FfmpegPathOverride = "/nonexistent/ffmpeg",
            ShakaPackagerPathOverride = null,
        };
        CencDrmProcessor sut = new(
            opts,
            Mock.Of<IProcessRunner>(),
            NullLogger<CencDrmProcessor>.Instance,
            MakeStorage()
        );

        Func<Task> act = () =>
            sut.PackageAsync(
                "/tmp",
                [new("/v.mp4", "video", "/v_enc.mp4", "SD")],
                CencConfig(),
                "/manifest.mpd",
                CancellationToken.None
            );

        // GetShakaPackagerPath throws InvalidOperationException when nothing is found.
        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*shaka-packager*");
    }

    [Fact]
    public async Task PackageAsync_NonZeroExitCode_ThrowsWithDetails()
    {
        Mock<IProcessRunner> runner = new();
        runner
            .Setup(r =>
                r.RunAsync(
                    It.IsAny<string>(),
                    It.IsAny<string[]>(),
                    It.IsAny<string>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(new ProcessResult(1, "", "Fatal: bad key", TimeSpan.Zero));

        CencDrmProcessor sut = BuildSut(runner: runner.Object);

        Func<Task> act = () =>
            sut.PackageAsync(
                "/tmp",
                [new("/v.mp4", "video", "/v_enc.mp4", "SD")],
                CencConfig(),
                "/tmp/manifest.mpd",
                CancellationToken.None
            );

        await act.Should()
            .ThrowAsync<InvalidOperationException>()
            .WithMessage("*shaka-packager exited with code 1*");
    }

    [Fact]
    public async Task PackageAsync_NoCencKeys_ThrowsArgumentException()
    {
        CencDrmProcessor sut = BuildSut();
        DrmConfig noKeys = new(DrmMethod.Cenc, KeyUri: "https://lic.example", CencKeys: []);

        Func<Task> act = () =>
            sut.PackageAsync(
                "/tmp",
                [new("/v.mp4", "video", "/v_enc.mp4", "SD")],
                noKeys,
                "/tmp/manifest.mpd",
                CancellationToken.None
            );

        await act.Should().ThrowAsync<ArgumentException>().WithMessage("*CencKeys*");
    }

    // ── CencStreamDescriptor ─────────────────────────────────────────────

    [Fact]
    public void CencStreamDescriptor_ToPackagerSpec_ReturnsCorrectFormat()
    {
        CencStreamDescriptor descriptor = new(
            "/out/video.mp4",
            "video",
            "/out/video_enc.mp4",
            "HD"
        );

        string spec = descriptor.ToPackagerSpec();

        spec.Should().Be("in=/out/video.mp4,stream=video,output=/out/video_enc.mp4,drm_label=HD");
    }

    // ── Integration (skipped unless packager binary is present) ──────────

    [Fact(Skip = "needs shaka-packager binary on PATH or ShakaPackagerPathOverride set")]
    public async Task PackageAsync_WithRealPackager_ProducesMpd()
    {
        // This test exercises the real shaka-packager binary end-to-end.
        // It is skipped in CI unless the binary is explicitly provided.
        // To run locally: set SHAKA_PACKAGER_PATH env or place `packager`
        // on PATH, then remove the Skip attribute temporarily.

        string? packagerPath = Environment.GetEnvironmentVariable("SHAKA_PACKAGER_PATH");
        if (string.IsNullOrWhiteSpace(packagerPath))
            return; // guard: Skip attribute already prevents this, but belt-and-braces

        string tmpDir = Path.Combine(Path.GetTempPath(), $"cenc-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tmpDir);

        try
        {
            // Real test would create a test mp4, run ffmpeg, then run packager.
            // Placeholder: just verify the path resolves.
            File.Exists(packagerPath).Should().BeTrue();
        }
        finally
        {
            Directory.Delete(tmpDir, recursive: true);
        }
    }
}
