namespace NoMercy.Tests.Encoder.V3.Pipeline.Stages;

using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using NoMercy.Encoder.V3.Analysis;
using NoMercy.Encoder.V3.Codecs;
using NoMercy.Encoder.V3.Errors;
using NoMercy.Encoder.V3.Pipeline;
using NoMercy.Encoder.V3.Pipeline.Stages;
using NoMercy.Encoder.V3.Profiles;

public class ValidateStageTests
{
    private readonly Mock<IProfileValidator> _validator = new();
    private readonly ValidateStage _stage;
    private readonly EncodingContext _context = EncodingContext.Create();

    public ValidateStageTests()
    {
        _stage = new ValidateStage(_validator.Object, NullLogger<ValidateStage>.Instance);
    }

    private static MediaInfo BuildMediaInfo() =>
        new(
            FilePath: "/movies/test.mkv",
            Format: "matroska",
            Duration: TimeSpan.FromHours(2),
            OverallBitRateKbps: 8000,
            FileSizeBytes: 7_200_000_000,
            VideoStreams:
            [
                new VideoStreamInfo(
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

    private static EncodingProfile BuildProfile() =>
        new(
            Id: "test-id",
            Name: "Test",
            Format: OutputFormat.Hls,
            VideoOutputs:
            [
                new VideoOutput(
                    Codec: VideoCodecType.H264,
                    Width: 1920,
                    Height: 1080,
                    BitrateKbps: 4000,
                    Crf: 23,
                    Preset: "medium",
                    Profile: "high",
                    Level: "4.1",
                    ConvertHdrToSdr: false,
                    KeyframeIntervalSeconds: 2,
                    TenBit: false
                ),
            ],
            AudioOutputs:
            [
                new AudioOutput(
                    Codec: AudioCodecType.Aac,
                    BitrateKbps: 192,
                    Channels: 2,
                    SampleRateHz: 48000,
                    AllowedLanguages: ["en"]
                ),
            ],
            SubtitleOutputs: []
        );

    // ------------------------------------------------------------------
    // Valid profile → success, passes input through
    // ------------------------------------------------------------------

    [Fact]
    public async Task ValidProfile_ReturnsSuccess_WithPassthrough()
    {
        EncodingProfile profile = BuildProfile();
        MediaInfo media = BuildMediaInfo();
        ValidateInput input = new(media, profile);

        _validator.Setup(v => v.Validate(profile)).Returns(ValidationResult.Success());

        StageResult result = await _stage.ExecuteAsync(input, _context, default);

        result.Should().BeOfType<StageSuccess<ValidateInput>>();
        StageSuccess<ValidateInput> success = (StageSuccess<ValidateInput>)result;
        success.Value.Profile.Should().Be(profile);
        success.Value.Media.Should().Be(media);
    }

    // ------------------------------------------------------------------
    // Invalid profile (has error) → ProfileInvalid failure
    // ------------------------------------------------------------------

    [Fact]
    public async Task InvalidProfile_WithErrors_ReturnsProfileInvalidFailure()
    {
        EncodingProfile profile = BuildProfile();
        MediaInfo media = BuildMediaInfo();
        ValidateInput input = new(media, profile);

        ValidationResult validationResult = new(
            false,
            [
                new ValidationError("Name", "Name is required", ValidationSeverity.Error),
                new ValidationError(
                    "VideoOutput[0].Width",
                    "Width must be positive",
                    ValidationSeverity.Error
                ),
            ]
        );

        _validator.Setup(v => v.Validate(profile)).Returns(validationResult);

        StageResult result = await _stage.ExecuteAsync(input, _context, default);

        result.Should().BeOfType<StageFailure>();
        StageFailure failure = (StageFailure)result;
        failure.Error.Kind.Should().Be(EncodingErrorKind.ProfileInvalid);
        failure.Error.StageName.Should().Be("Validate");
        failure.Error.Message.Should().Contain("Name is required");
    }

    // ------------------------------------------------------------------
    // Profile with only warnings → still success
    // ------------------------------------------------------------------

    [Fact]
    public async Task ProfileWithWarningsOnly_ReturnsSuccess()
    {
        EncodingProfile profile = BuildProfile();
        MediaInfo media = BuildMediaInfo();
        ValidateInput input = new(media, profile);

        ValidationResult validationResult = new(
            true,
            [
                new ValidationError(
                    "VideoOutput[0].Codec",
                    "VP9 in MP4 is non-standard",
                    ValidationSeverity.Warning
                ),
            ]
        );

        _validator.Setup(v => v.Validate(profile)).Returns(validationResult);

        StageResult result = await _stage.ExecuteAsync(input, _context, default);

        result.Should().BeOfType<StageSuccess<ValidateInput>>();
    }

    // ------------------------------------------------------------------
    // Mixed errors and warnings → fails on error
    // ------------------------------------------------------------------

    [Fact]
    public async Task ProfileWithErrorsAndWarnings_ReturnsFailure()
    {
        EncodingProfile profile = BuildProfile();
        MediaInfo media = BuildMediaInfo();
        ValidateInput input = new(media, profile);

        ValidationResult validationResult = new(
            false,
            [
                new ValidationError("Name", "Name is required", ValidationSeverity.Error),
                new ValidationError(
                    "VideoOutput[0].Codec",
                    "Non-standard combination",
                    ValidationSeverity.Warning
                ),
            ]
        );

        _validator.Setup(v => v.Validate(profile)).Returns(validationResult);

        StageResult result = await _stage.ExecuteAsync(input, _context, default);

        result.Should().BeOfType<StageFailure>();
        StageFailure failure = (StageFailure)result;
        failure.Error.Kind.Should().Be(EncodingErrorKind.ProfileInvalid);
        // Only the error message should appear, not the warning
        failure.Error.Message.Should().Contain("Name is required");
        failure.Error.Message.Should().NotContain("Non-standard");
    }
}
