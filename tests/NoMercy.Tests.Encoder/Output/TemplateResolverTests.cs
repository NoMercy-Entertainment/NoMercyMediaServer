namespace NoMercy.Tests.Encoder.Output;

using NoMercy.Encoder.Output;

/// <summary>
/// TemplateResolver drives the output file paths. Every variant / audio /
/// subtitle file gets a name derived from the profile's name template,
/// with :token: substitutions. If a token isn't replaced (typo, missing
/// value, case mismatch) the colon survives into the filesystem name —
/// broken on Windows, ugly on Linux. These tests pin the substitution
/// surface.
/// </summary>
public class TemplateResolverTests
{
    [Fact]
    public void Resolve_SingleToken_Replaced()
    {
        string result = TemplateResolver.Resolve(":type:_video.ts", new() { ["type"] = "video" });
        result.Should().Be("video_video.ts");
    }

    [Fact]
    public void Resolve_MultipleTokens_AllReplaced()
    {
        string result = TemplateResolver.Resolve(
            ":type:_:framesize:_:colorrange:/:type:_:framesize:_:colorrange:",
            new()
            {
                ["type"] = "video",
                ["framesize"] = "1920x1080",
                ["colorrange"] = "SDR",
            }
        );
        result.Should().Be("video_1920x1080_SDR/video_1920x1080_SDR");
    }

    [Fact]
    public void Resolve_UnknownToken_LeftIntact()
    {
        // Leaving unreplaced tokens in the output is the safest behavior:
        // the string is obviously broken on disk, which is a much louder
        // failure than silently dropping the token and producing two files
        // with the same name.
        string result = TemplateResolver.Resolve(":type:_:unknown:", new() { ["type"] = "video" });
        result.Should().Be("video_:unknown:");
    }

    [Fact]
    public void Resolve_EmptyTokens_ReturnsTemplate()
    {
        string result = TemplateResolver.Resolve(":type:_video", []);
        result.Should().Be(":type:_video");
    }

    [Fact]
    public void VideoTokens_IncludesTypeAndFrameSize()
    {
        Dictionary<string, string> tokens = TemplateResolver.VideoTokens(1920, 1080, tenBit: false);
        tokens["type"].Should().Be("video");
        tokens["framesize"].Should().Be("1920x1080");
        tokens["colorrange"].Should().Be("SDR");
    }

    [Fact]
    public void VideoTokens_TenBit_MarksAsHdr()
    {
        // 10-bit is treated as the "HDR track" marker in folder names even
        // though 10-bit ≠ HDR. The naming convention is about separating
        // the 10-bit ladder from the 8-bit one so players don't mix them.
        Dictionary<string, string> tokens = TemplateResolver.VideoTokens(3840, 2160, tenBit: true);
        tokens["colorrange"].Should().Be("HDR");
    }

    [Fact]
    public void AudioTokens_IncludesLanguageCodecChannels()
    {
        Dictionary<string, string> tokens = TemplateResolver.AudioTokens(
            language: "eng",
            codecName: "eac3",
            channels: 6
        );
        tokens["type"].Should().Be("audio");
        tokens["language"].Should().Be("eng");
        tokens["codec"].Should().Be("eac3");
        tokens["channels"].Should().Be("6");
    }

    [Fact]
    public void SubtitleTokens_IncludesVariantAndFilename()
    {
        Dictionary<string, string> tokens = TemplateResolver.SubtitleTokens(
            language: "eng",
            variant: "sign",
            filename: "movie"
        );
        tokens["language"].Should().Be("eng");
        tokens["variant"].Should().Be("sign");
        tokens["filename"].Should().Be("movie");
    }

    [Fact]
    public void Resolve_VideoTemplate_EndToEnd()
    {
        // The default video template from EncodingProfile:
        string template = ":type:_:framesize:_:colorrange:/:type:_:framesize:_:colorrange:";
        Dictionary<string, string> tokens = TemplateResolver.VideoTokens(1280, 720, tenBit: false);

        string resolved = TemplateResolver.Resolve(template, tokens);

        resolved.Should().Be("video_1280x720_SDR/video_1280x720_SDR");
    }

    [Fact]
    public void Resolve_AudioTemplate_EndToEnd()
    {
        string template = ":type:_:language:_:codec:/:type:_:language:_:codec:";
        Dictionary<string, string> tokens = TemplateResolver.AudioTokens("jpn", "opus", 2);

        string resolved = TemplateResolver.Resolve(template, tokens);

        resolved.Should().Be("audio_jpn_opus/audio_jpn_opus");
    }
}
