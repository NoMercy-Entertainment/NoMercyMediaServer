using Microsoft.Extensions.Logging.Abstractions;
using NoMercy.Encoder.Analysis;
using NoMercy.Encoder.Codecs;
using NoMercy.Encoder.Errors;
using NoMercy.Encoder.Pipeline;
using NoMercy.Encoder.Pipeline.Stages;
using CodecProfile = NoMercy.Encoder.Profiles.CodecProfile;
using Container = NoMercy.Encoder.Profiles.Container;
using EncodingProfile = NoMercy.Encoder.Profiles.EncodingProfile;
using RateControlMode = NoMercy.Encoder.Profiles.RateControlMode;
using StreamPolicy = NoMercy.Encoder.Profiles.StreamPolicy;

namespace NoMercy.Tests.Encoder.Pipeline.Stages;

public class ValidateStageTests
{
    private readonly ValidateStage _stage = new(NullLogger<ValidateStage>.Instance);
    private readonly EncodingContext _context = EncodingContext.Create();

    private static MediaInfo BuildMediaInfo() =>
        new(
            FilePath: "/movies/test.mkv",
            Format: "matroska",
            Duration: TimeSpan.FromHours(2),
            OverallBitRateKbps: 8000,
            FileSizeBytes: 7_200_000_000,
            VideoStreams:
            [
                new(
                    Index: 0,
                    Codec: "h264",
                    Width: 1920,
                    Height: 1080,
                    FrameRate: 24.0,
                    BitDepth: 8,
                    PixelFormat: "yuv420p",
                    ColorPrimaries: null,
                    ColorTransfer: null,
                    ColorSpace: null,
                    IsDefault: true,
                    BitRateKbps: 6000
                ),
            ],
            AudioStreams: [],
            SubtitleStreams: [],
            Chapters: []
        );

    private static EncodingProfile BuildValidProfile() =>
        new(
            Id: Ulid.NewUlid(),
            Name: "Test",
            Container: Container.HlsTs,
            Video: new(
                Policy: StreamPolicy.Transcode,
                Codec: VideoCodecType.H264,
                Width: 1920,
                Height: 1080,
                RateControl: RateControlMode.Crf,
                Crf: 23,
                BitrateKbps: 4000,
                MaxBitrateKbps: null,
                BufferSizeKbps: null,
                Preset: "medium",
                CodecProfile: CodecProfile.High,
                Level: "4.1",
                Tune: null,
                BitDepth: 8,
                PixelFormat: null,
                KeyframeIntervalSeconds: 2,
                ConvertHdrToSdr: false,
                SegmentNameTemplate: ":type:_:framesize:_:colorrange:/:type:_:framesize:_:colorrange:",
                PlaylistNameTemplate: ":type:_:framesize:_:colorrange:/:type:_:framesize:_:colorrange:"
            ),
            Audio:
            [
                new(
                    Policy: StreamPolicy.Transcode,
                    Codec: AudioCodecType.Aac,
                    BitrateKbps: 192,
                    Channels: 2,
                    SampleRateHz: 48000,
                    AllowedLanguages: ["en"],
                    DefaultLanguage: null,
                    Loudness: null,
                    Downmix: null,
                    SegmentNameTemplate: ":type:_:language:_:codec:/:type:_:language:_:codec:",
                    PlaylistNameTemplate: ":type:_:language:_:codec:/:type:_:language:_:codec:"
                ),
            ],
            Subtitles: []
        );

    private static EncodingProfile BuildInvalidProfile() =>
        new(
            Id: Ulid.NewUlid(),
            Name: "Invalid",
            Container: Container.HlsTs,
            Video: null,
            Audio:
            [
                new(
                    Policy: StreamPolicy.Transcode,
                    Codec: AudioCodecType.Aac,
                    BitrateKbps: 0,
                    Channels: 2,
                    SampleRateHz: 48000,
                    AllowedLanguages: [],
                    DefaultLanguage: null,
                    Loudness: null,
                    Downmix: null,
                    SegmentNameTemplate: ":type:_:language:_:codec:/:type:_:language:_:codec:",
                    PlaylistNameTemplate: ":type:_:language:_:codec:/:type:_:language:_:codec:"
                ),
            ],
            Subtitles: []
        );

    // ------------------------------------------------------------------
    // Valid profile → success, passes input through
    // ------------------------------------------------------------------

    [Fact]
    public async Task ValidProfile_ReturnsSuccess_WithPassthrough()
    {
        EncodingProfile profile = BuildValidProfile();
        MediaInfo media = BuildMediaInfo();
        ValidateInput input = new(media, profile);

        StageResult result = await _stage.ExecuteAsync(input, _context, default);

        result.Should().BeOfType<StageSuccess<ValidateInput>>();
        StageSuccess<ValidateInput> success = (StageSuccess<ValidateInput>)result;
        success.Value.Profile.Should().Be(profile);
        success.Value.Media.Should().Be(media);
    }

    // ------------------------------------------------------------------
    // Invalid profile (audio BitrateKbps = 0) → ProfileInvalid failure
    // ------------------------------------------------------------------

    [Fact]
    public async Task InvalidProfile_WithErrors_ReturnsProfileInvalidFailure()
    {
        EncodingProfile profile = BuildInvalidProfile();
        MediaInfo media = BuildMediaInfo();
        ValidateInput input = new(media, profile);

        StageResult result = await _stage.ExecuteAsync(input, _context, default);

        result.Should().BeOfType<StageFailure>();
        StageFailure failure = (StageFailure)result;
        failure.Error.Kind.Should().Be(EncodingErrorKind.ProfileInvalid);
        failure.Error.StageName.Should().Be("Validate");
        failure.Error.Message.Should().Contain("BitrateKbps");
    }

    // ------------------------------------------------------------------
    // Profile with no audio/video → still valid (nothing to validate)
    // ------------------------------------------------------------------

    [Fact]
    public async Task ProfileWithNoOutputs_ReturnsSuccess()
    {
        EncodingProfile profile = new(
            Id: Ulid.NewUlid(),
            Name: "Empty",
            Container: Container.HlsTs,
            Video: null,
            Audio: [],
            Subtitles: []
        );
        MediaInfo media = BuildMediaInfo();
        ValidateInput input = new(media, profile);

        StageResult result = await _stage.ExecuteAsync(input, _context, default);

        result.Should().BeOfType<StageSuccess<ValidateInput>>();
    }

    // ------------------------------------------------------------------
    // Profile with incompatible codec → ProfileInvalid failure
    // ------------------------------------------------------------------

    [Fact]
    public async Task ProfileWithIncompatibleCodec_ReturnsFailure()
    {
        EncodingProfile profile = new(
            Id: Ulid.NewUlid(),
            Name: "Bad Codec",
            Container: Container.HlsTs,
            Video: new(
                Policy: StreamPolicy.Transcode,
                Codec: VideoCodecType.H265,
                Width: 1920,
                Height: 1080,
                RateControl: RateControlMode.Crf,
                Crf: 23,
                BitrateKbps: 4000,
                MaxBitrateKbps: null,
                BufferSizeKbps: null,
                Preset: "medium",
                CodecProfile: CodecProfile.Main,
                Level: "4.1",
                Tune: null,
                BitDepth: 8,
                PixelFormat: null,
                KeyframeIntervalSeconds: 2,
                ConvertHdrToSdr: false,
                SegmentNameTemplate: ":type:_:framesize:_:colorrange:/:type:_:framesize:_:colorrange:",
                PlaylistNameTemplate: ":type:_:framesize:_:colorrange:/:type:_:framesize:_:colorrange:"
            ),
            Audio: [],
            Subtitles: []
        );
        MediaInfo media = BuildMediaInfo();
        ValidateInput input = new(media, profile);

        StageResult result = await _stage.ExecuteAsync(input, _context, default);

        result.Should().BeOfType<StageFailure>();
        StageFailure failure = (StageFailure)result;
        failure.Error.Kind.Should().Be(EncodingErrorKind.ProfileInvalid);
    }
}
