using NoMercy.EncoderV2.Jobs;

namespace NoMercy.EncoderV2.Profiles;

/// <summary>
/// Default production-ready encoding profiles
/// These are built-in profiles that can be used for common streaming scenarios
/// </summary>
public static class ProductionProfiles
{
    /// <summary>
    /// Get all available production profiles
    /// </summary>
    public static List<EncodingProfile> GetAllProfiles()
    {
        return new()
        {
            // 1080p High Quality Profile
            new EncodingProfile
            {
                ProfileId = "playback-1080p-high",
                Name = "1080p High Quality",
                Container = "mp4",
                Purpose = "playback",
                CreatedAt = DateTime.UtcNow,
                VideoProfile = new VideoProfileConfig
                {
                    Codec = "h264",
                    Width = 1920,
                    Height = 1080,
                    Bitrate = 5000,
                    Framerate = 30,
                    Crf = 23,
                    Preset = "medium",
                    Profile = "high",
                    PixelFormat = "yuv420p"
                },
                AudioProfile = new AudioProfileConfig
                {
                    Codec = "aac",
                    Bitrate = 192,
                    Channels = 2,
                    SampleRate = 48000
                }
            },
            // 720p Medium Quality Profile
            new EncodingProfile
            {
                ProfileId = "playback-720p-medium",
                Name = "720p Medium Quality",
                Container = "mp4",
                Purpose = "playback",
                CreatedAt = DateTime.UtcNow,
                VideoProfile = new VideoProfileConfig
                {
                    Codec = "h264",
                    Width = 1280,
                    Height = 720,
                    Bitrate = 2500,
                    Framerate = 30,
                    Crf = 23,
                    Preset = "medium",
                    Profile = "high",
                    PixelFormat = "yuv420p"
                },
                AudioProfile = new AudioProfileConfig
                {
                    Codec = "aac",
                    Bitrate = 128,
                    Channels = 2,
                    SampleRate = 48000
                }
            },
            // 480p Low Bandwidth Profile
            new EncodingProfile
            {
                ProfileId = "playback-480p-low",
                Name = "480p Low Bandwidth",
                Container = "mp4",
                Purpose = "playback",
                CreatedAt = DateTime.UtcNow,
                VideoProfile = new VideoProfileConfig
                {
                    Codec = "h264",
                    Width = 854,
                    Height = 480,
                    Bitrate = 1000,
                    Framerate = 30,
                    Crf = 23,
                    Preset = "fast",
                    Profile = "baseline",
                    PixelFormat = "yuv420p"
                },
                AudioProfile = new AudioProfileConfig
                {
                    Codec = "aac",
                    Bitrate = 96,
                    Channels = 2,
                    SampleRate = 44100
                }
            }
        };
    }

    /// <summary>
    /// Get default playback profile for standard quality
    /// </summary>
    public static EncodingProfile GetDefaultPlaybackProfile()
    {
        return GetAllProfiles().First(p => p.ProfileId == "playback-1080p-high");
    }

    /// <summary>
    /// Get profile by ID
    /// </summary>
    public static EncodingProfile? GetProfile(string profileId)
    {
        return GetAllProfiles().FirstOrDefault(p => p.ProfileId == profileId);
    }
}
