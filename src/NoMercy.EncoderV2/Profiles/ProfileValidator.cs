using NoMercy.Database;
using NoMercy.Database.Models;

namespace NoMercy.EncoderV2.Profiles;

/// <summary>
/// Profile validation result
/// </summary>
public class ProfileValidationResult
{
    public bool IsValid => Errors.Count == 0;
    public List<string> Errors { get; set; } = [];
    public List<string> Warnings { get; set; } = [];
}

/// <summary>
/// Interface for profile validation
/// </summary>
public interface IProfileValidator
{
    Task<ProfileValidationResult> ValidateAsync(EncoderProfile profile);
}

/// <summary>
/// Validates encoding profiles for correctness and compatibility
/// Implements rules from the final plan specification
/// </summary>
public class ProfileValidator : IProfileValidator
{
    private static readonly HashSet<string> ValidVideoCodecs =
    [
        "libx264", "h264", "h264_nvenc", "h264_qsv", "h264_amf", "h264_videotoolbox",
        "libx265", "hevc", "hevc_nvenc", "hevc_qsv", "hevc_amf", "hevc_videotoolbox",
        "libvpx-vp9", "vp9", "vp9_nvenc",
        "libaom-av1", "av1", "av1_nvenc"
    ];

    private static readonly HashSet<string> ValidAudioCodecs =
    [
        "aac", "libfdk_aac",
        "mp3", "libmp3lame",
        "opus", "libopus",
        "flac",
        "ac3", "eac3",
        "truehd", "dts", "dca"
    ];

    private static readonly HashSet<string> ValidSubtitleCodecs =
    [
        "ass", "ssa", "webvtt", "srt", "subrip", "mov_text"
    ];

    private static readonly HashSet<string> ValidContainers =
    [
        "hls", "m3u8", "mp4", "mkv", "webm", "flac", "mp3"
    ];

    public Task<ProfileValidationResult> ValidateAsync(EncoderProfile profile)
    {
        ProfileValidationResult result = new();

        ValidateBasicProperties(profile, result);
        ValidateVideoProfiles(profile, result);
        ValidateAudioProfiles(profile, result);
        ValidateSubtitleProfiles(profile, result);
        ValidateContainer(profile, result);

        return Task.FromResult(result);
    }

    private void ValidateBasicProperties(EncoderProfile profile, ProfileValidationResult result)
    {
        if (string.IsNullOrWhiteSpace(profile.Name))
        {
            result.Errors.Add("Profile name is required");
        }

        if (profile.Name.Length > 100)
        {
            result.Errors.Add("Profile name must be 100 characters or less");
        }
    }

    private void ValidateVideoProfiles(EncoderProfile profile, ProfileValidationResult result)
    {
        if (profile.VideoProfiles == null || profile.VideoProfiles.Length == 0)
        {
            result.Warnings.Add("No video profiles defined");
            return;
        }

        foreach (IVideoProfile video in profile.VideoProfiles)
        {
            if (!ValidVideoCodecs.Contains(video.Codec))
            {
                result.Errors.Add($"Invalid video codec: {video.Codec}");
            }

            if (video.Bitrate < 0)
            {
                result.Errors.Add("Video bitrate cannot be negative");
            }

            if (video.Width <= 0 || video.Height <= 0)
            {
                result.Errors.Add($"Invalid video dimensions: {video.Width}x{video.Height}");
            }

            if (video.Width > 7680 || video.Height > 4320)
            {
                result.Warnings.Add($"Very large resolution: {video.Width}x{video.Height} (8K+)");
            }

            if (video.Crf < 0 || video.Crf > 51)
            {
                result.Errors.Add($"CRF must be between 0 and 51, got {video.Crf}");
            }

            if (video.Framerate < 0 || video.Framerate > 120)
            {
                result.Errors.Add($"Invalid framerate: {video.Framerate}");
            }
        }
    }

    private void ValidateAudioProfiles(EncoderProfile profile, ProfileValidationResult result)
    {
        if (profile.AudioProfiles == null || profile.AudioProfiles.Length == 0)
        {
            result.Warnings.Add("No audio profiles defined");
            return;
        }

        foreach (IAudioProfile audio in profile.AudioProfiles)
        {
            if (!ValidAudioCodecs.Contains(audio.Codec))
            {
                result.Errors.Add($"Invalid audio codec: {audio.Codec}");
            }

            if (audio.Channels < 1 || audio.Channels > 8)
            {
                result.Errors.Add($"Invalid audio channels: {audio.Channels}. Must be between 1 and 8");
            }

            if (audio.SampleRate != 0 &&
                audio.SampleRate != 44100 &&
                audio.SampleRate != 48000 &&
                audio.SampleRate != 96000)
            {
                result.Warnings.Add($"Unusual sample rate: {audio.SampleRate}. Common values: 44100, 48000, 96000");
            }
        }
    }

    private void ValidateSubtitleProfiles(EncoderProfile profile, ProfileValidationResult result)
    {
        if (profile.SubtitleProfiles == null || profile.SubtitleProfiles.Length == 0)
        {
            result.Warnings.Add("No subtitle profiles defined");
            return;
        }

        foreach (ISubtitleProfile subtitle in profile.SubtitleProfiles)
        {
            if (!ValidSubtitleCodecs.Contains(subtitle.Codec.ToLower()))
            {
                result.Errors.Add($"Invalid subtitle codec: {subtitle.Codec}");
            }

            if (subtitle.Codec.ToLower() == "ass" || subtitle.Codec.ToLower() == "ssa")
            {
                result.Warnings.Add("ASS/SSA subtitles: Ensure fonts are extracted and preserved");
            }
        }
    }

    private void ValidateContainer(EncoderProfile profile, ProfileValidationResult result)
    {
        if (string.IsNullOrWhiteSpace(profile.Container))
        {
            result.Errors.Add("Container format is required");
            return;
        }

        string container = profile.Container.ToLower();
        if (!ValidContainers.Contains(container))
        {
            result.Errors.Add($"Invalid container: {profile.Container}");
        }

        if ((container == "hls" || container == "m3u8") &&
            (profile.VideoProfiles == null || profile.VideoProfiles.Length == 0))
        {
            result.Errors.Add("HLS container requires at least one video profile");
        }
    }
}

