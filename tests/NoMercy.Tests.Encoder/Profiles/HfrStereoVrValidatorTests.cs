using NoMercy.Encoder.Analysis;
using NoMercy.Encoder.Codecs;
using NoMercy.Encoder.Profiles;
using AudioOutput = NoMercy.Encoder.Profiles.AudioOutput;
using CodecProfile = NoMercy.Encoder.Profiles.CodecProfile;
using Container = NoMercy.Encoder.Profiles.Container;
using EncodingProfile = NoMercy.Encoder.Profiles.EncodingProfile;
using RateControlMode = NoMercy.Encoder.Profiles.RateControlMode;
using StreamPolicy = NoMercy.Encoder.Profiles.StreamPolicy;
using VideoOutput = NoMercy.Encoder.Profiles.VideoOutput;

namespace NoMercy.Tests.Encoder.Profiles;

/// <summary>
/// Covers the three source-aware validator rules added in Task 6:
///   - LevelFrameRateCapExceeded (HFR)
///   - StereoscopicSourceUnsupported (3D)
///   - SphericalMetadataWillBeStripped (VR)
/// </summary>
public class HfrStereoVrValidatorTests
{
    // ------------------------------------------------------------------
    // Helpers
    // ------------------------------------------------------------------

    private static MediaInfo BuildSourceMedia(
        int width = 1920,
        int height = 1080,
        double fps = 30.0,
        string? stereoMode = null,
        string? sphericalProjection = null
    ) =>
        new(
            FilePath: "/media/test.mkv",
            Format: "matroska",
            Duration: TimeSpan.FromHours(2),
            OverallBitRateKbps: 8000,
            FileSizeBytes: 7_200_000_000,
            VideoStreams:
            [
                new(
                    Index: 0,
                    Codec: "h264",
                    Width: width,
                    Height: height,
                    FrameRate: fps,
                    BitDepth: 8,
                    PixelFormat: "yuv420p",
                    ColorPrimaries: null,
                    ColorTransfer: null,
                    ColorSpace: null,
                    IsDefault: true,
                    BitRateKbps: 8000,
                    AverageFrameRate: fps
                ),
            ],
            AudioStreams: [],
            SubtitleStreams: [],
            Chapters: [],
            StereoMode: stereoMode,
            SphericalProjection: sphericalProjection
        );

    private static EncodingProfile BuildTranscodeProfile(
        VideoCodecType codec = VideoCodecType.H264,
        string level = "4.2",
        int width = 1920,
        int height = 1080
    ) =>
        new(
            Id: Ulid.NewUlid(),
            Name: "Test",
            Container: Container.Mkv,
            Video: new(
                Policy: StreamPolicy.Transcode,
                Codec: codec,
                Width: width,
                Height: height,
                RateControl: RateControlMode.Crf,
                Crf: 23,
                BitrateKbps: 0,
                MaxBitrateKbps: null,
                BufferSizeKbps: null,
                Preset: "medium",
                CodecProfile: CodecProfile.High,
                Level: level,
                Tune: null,
                BitDepth: 8,
                PixelFormat: null,
                KeyframeIntervalSeconds: 2,
                ConvertHdrToSdr: false,
                SegmentNameTemplate: "video/video",
                PlaylistNameTemplate: "video/video"
            ),
            Audio: [],
            Subtitles: []
        );

    private static EncodingProfile BuildCopyProfile() =>
        new(
            Id: Ulid.NewUlid(),
            Name: "Copy",
            Container: Container.Mkv,
            Video: new(
                Policy: StreamPolicy.Copy,
                Codec: VideoCodecType.H264,
                Width: 0,
                Height: null,
                RateControl: RateControlMode.Crf,
                Crf: 0,
                BitrateKbps: 0,
                MaxBitrateKbps: null,
                BufferSizeKbps: null,
                Preset: null,
                CodecProfile: CodecProfile.Auto,
                Level: null,
                Tune: null,
                BitDepth: 8,
                PixelFormat: null,
                KeyframeIntervalSeconds: 0,
                ConvertHdrToSdr: false,
                SegmentNameTemplate: "video/video",
                PlaylistNameTemplate: "video/video"
            ),
            Audio: [],
            Subtitles: []
        );

    // ------------------------------------------------------------------
    // HFR — Level cap exceeded at 240fps 1080p with H.264 Level 4.2
    // ------------------------------------------------------------------

    [Fact]
    public void LevelFrameRateCapExceeded_HFR240fps1080p_LevelTooLow()
    {
        // 1920 × 1080 × 240 = 497,664,000 luma samples/sec
        // H.264 Level 4.2 allows 133,693,440 — far below
        MediaInfo source = BuildSourceMedia(width: 1920, height: 1080, fps: 240.0);
        EncodingProfile profile = BuildTranscodeProfile(codec: VideoCodecType.H264, level: "4.2");

        ProfileValidationResult result = ProfileValidator.ValidateWithSource(profile, source);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().HaveCount(1);
        result.Errors[0].Should().Contain("Level 4.2 cap exceeded");
        result.Errors[0].Should().Contain("Raise level to");
    }

    // ------------------------------------------------------------------
    // HFR — Standard 30fps 1080p with H.264 Level 4.2 passes
    // ------------------------------------------------------------------

    [Fact]
    public void LevelFrameRateCapExceeded_StandardContent_AllowsLevel()
    {
        // 1920 × 1080 × 30 = 62,208,000 luma samples/sec
        // H.264 Level 4.2 allows 133,693,440 — passes
        MediaInfo source = BuildSourceMedia(width: 1920, height: 1080, fps: 30.0);
        EncodingProfile profile = BuildTranscodeProfile(codec: VideoCodecType.H264, level: "4.2");

        ProfileValidationResult result = ProfileValidator.ValidateWithSource(profile, source);

        result.IsValid.Should().BeTrue();
        result.Errors.Should().BeEmpty();
    }

    // ------------------------------------------------------------------
    // HFR — 4K 120fps with HEVC Level 5.1 passes; Level 4.0 fails
    // ------------------------------------------------------------------

    [Fact]
    public void LevelFrameRateCapExceeded_4K120fps_HevcLevel51_Passes()
    {
        // 3840 × 2160 × 120 = 995,328,000 — exceeds L5.1 (534,773,760)
        // but L5.2 (1,069,547,520) fits
        MediaInfo source = BuildSourceMedia(width: 3840, height: 2160, fps: 120.0);
        EncodingProfile profile = BuildTranscodeProfile(
            codec: VideoCodecType.H265,
            level: "5.2",
            width: 3840,
            height: 2160
        );

        ProfileValidationResult result = ProfileValidator.ValidateWithSource(profile, source);

        result.IsValid.Should().BeTrue();
        result.Errors.Should().BeEmpty();
    }

    [Fact]
    public void LevelFrameRateCapExceeded_4K120fps_HevcLevel40_Fails()
    {
        // 3840 × 2160 × 120 = 995,328,000 — L4.0 allows 66,846,720
        MediaInfo source = BuildSourceMedia(width: 3840, height: 2160, fps: 120.0);
        EncodingProfile profile = BuildTranscodeProfile(
            codec: VideoCodecType.H265,
            level: "4.0",
            width: 3840,
            height: 2160
        );

        ProfileValidationResult result = ProfileValidator.ValidateWithSource(profile, source);

        result.IsValid.Should().BeFalse();
        result.Errors[0].Should().Contain("Level 4.0 cap exceeded");
    }

    // ------------------------------------------------------------------
    // HFR — copy policy skips the check (level is irrelevant for copy)
    // ------------------------------------------------------------------

    [Fact]
    public void LevelFrameRateCapExceeded_CopyPolicy_Skipped()
    {
        MediaInfo source = BuildSourceMedia(fps: 240.0);
        EncodingProfile profile = BuildCopyProfile();

        ProfileValidationResult result = ProfileValidator.ValidateWithSource(profile, source);

        // Copy policy: rule does not apply, no error about level cap
        result.Errors.Should().NotContain(e => e.Contains("cap exceeded"));
    }

    // ------------------------------------------------------------------
    // 3D — Transcode profile with StereoMode source → rejected
    // ------------------------------------------------------------------

    [Fact]
    public void StereoscopicSourceUnsupported_Transcode_Rejected()
    {
        MediaInfo source = BuildSourceMedia(stereoMode: "side_by_side_left");
        EncodingProfile profile = BuildTranscodeProfile();

        ProfileValidationResult result = ProfileValidator.ValidateWithSource(profile, source);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().HaveCount(1);
        result.Errors[0].Should().Contain("3D source detected");
        result.Errors[0].Should().Contain("side_by_side_left");
    }

    // ------------------------------------------------------------------
    // 3D — Copy profile with StereoMode source → allowed
    // ------------------------------------------------------------------

    [Fact]
    public void StereoscopicSourceUnsupported_StreamCopy_Allowed()
    {
        MediaInfo source = BuildSourceMedia(stereoMode: "side_by_side_left");
        EncodingProfile profile = BuildCopyProfile();

        ProfileValidationResult result = ProfileValidator.ValidateWithSource(profile, source);

        result.IsValid.Should().BeTrue();
        result.Errors.Should().BeEmpty();
    }

    // ------------------------------------------------------------------
    // 3D — Non-3D source with transcode profile → no error
    // ------------------------------------------------------------------

    [Fact]
    public void StereoscopicSourceUnsupported_No3DSource_NoError()
    {
        MediaInfo source = BuildSourceMedia();
        EncodingProfile profile = BuildTranscodeProfile();

        ProfileValidationResult result = ProfileValidator.ValidateWithSource(profile, source);

        result.Errors.Should().NotContain(e => e.Contains("3D source detected"));
    }

    // ------------------------------------------------------------------
    // VR — Transcode with SphericalProjection → warning, not error
    // ------------------------------------------------------------------

    [Fact]
    public void SphericalMetadataWillBeStripped_Warning_NotError()
    {
        MediaInfo source = BuildSourceMedia(sphericalProjection: "equirectangular");
        EncodingProfile profile = BuildTranscodeProfile();

        ProfileValidationResult result = ProfileValidator.ValidateWithSource(profile, source);

        result.IsValid.Should().BeTrue();
        result.Errors.Should().BeEmpty();
        result.Warnings.Should().HaveCount(1);
        result.Warnings[0].Should().Contain("equirectangular");
        result.Warnings[0].Should().Contain("will be stripped");
    }

    // ------------------------------------------------------------------
    // VR — Copy policy with SphericalProjection → no warning
    // ------------------------------------------------------------------

    [Fact]
    public void SphericalMetadataWillBeStripped_CopyPolicy_NoWarning()
    {
        MediaInfo source = BuildSourceMedia(sphericalProjection: "equirectangular");
        EncodingProfile profile = BuildCopyProfile();

        ProfileValidationResult result = ProfileValidator.ValidateWithSource(profile, source);

        result.IsValid.Should().BeTrue();
        result.Warnings.Should().BeEmpty();
    }

    // ------------------------------------------------------------------
    // VR — Non-VR source with transcode → no warning
    // ------------------------------------------------------------------

    [Fact]
    public void SphericalMetadataWillBeStripped_NoVrSource_NoWarning()
    {
        MediaInfo source = BuildSourceMedia();
        EncodingProfile profile = BuildTranscodeProfile();

        ProfileValidationResult result = ProfileValidator.ValidateWithSource(profile, source);

        result.Warnings.Should().NotContain(w => w.Contains("spherical") || w.Contains("VR"));
    }

    // ------------------------------------------------------------------
    // CodecLevelFpsCaps lookup sanity
    // ------------------------------------------------------------------

    [Fact]
    public void CodecLevelFpsCaps_Lookup_H264Level42_ReturnsCorrectCap()
    {
        CodecLevelFpsCaps.LevelCap? cap = CodecLevelFpsCaps.Lookup(VideoCodecType.H264, "4.2");

        cap.Should().NotBeNull();
        cap!.MaxLumaSamplesPerSec.Should().Be(133_693_440);
    }

    [Fact]
    public void CodecLevelFpsCaps_Lookup_UnknownLevel_ReturnsNull()
    {
        CodecLevelFpsCaps.LevelCap? cap = CodecLevelFpsCaps.Lookup(VideoCodecType.H264, "9.9");

        cap.Should().BeNull();
    }

    [Fact]
    public void CodecLevelFpsCaps_FindNextFit_ReturnsSmallestSufficientLevel()
    {
        // 1080p @ 240fps = 497,664,000 luma samples/sec
        // H.264 L5.1 = 251,658,240 (too low)
        // H.264 L5.2 = 530,841,600 (fits)
        long required = 1920L * 1080 * 240;
        CodecLevelFpsCaps.LevelCap? next = CodecLevelFpsCaps.FindNextFit(
            VideoCodecType.H264,
            required
        );

        next.Should().NotBeNull();
        next!.Level.Should().Be("5.2");
    }
}
