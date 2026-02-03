using NoMercy.Database;
using NoMercy.Database.Models;
using NoMercy.EncoderV2.Streams;
using NoMercy.EncoderV2.Tasks;

namespace NoMercy.EncoderV2.Core;

/// <summary>
/// Computes task weights for load balancing across distributed encoding nodes.
/// Weights are calculated based on resolution, duration, codec complexity, and task type.
/// Higher weights indicate more computationally expensive tasks.
/// </summary>
public static class TaskWeighting
{
    // Base weights for different task types
    private const double BaseVideoWeight = 1.0;
    private const double BaseAudioWeight = 0.1;
    private const double BaseSubtitleWeight = 0.05;
    private const double BaseMuxWeight = 0.02;

    // Resolution multipliers (relative to 1080p = 1.0)
    private static readonly Dictionary<string, double> ResolutionMultipliers = new()
    {
        { "4320p", 16.0 },  // 8K = 16x pixels of 1080p
        { "2160p", 4.0 },   // 4K = 4x pixels of 1080p
        { "1440p", 1.78 },  // 2.5K
        { "1080p", 1.0 },   // Reference
        { "720p", 0.44 },   // 720p
        { "576p", 0.28 },   // PAL SD
        { "480p", 0.22 },   // NTSC SD
        { "360p", 0.11 },   // Low quality
        { "240p", 0.05 }    // Very low quality
    };

    // Codec complexity multipliers (relative to h264 = 1.0)
    private static readonly Dictionary<string, double> CodecMultipliers = new()
    {
        // Video codecs
        { "av1", 3.0 },           // Most complex, best compression
        { "libaom-av1", 3.0 },
        { "libsvtav1", 2.0 },     // Faster AV1 encoder
        { "hevc", 1.5 },          // H.265
        { "libx265", 1.5 },
        { "h265", 1.5 },
        { "h264", 1.0 },          // Reference
        { "libx264", 1.0 },
        { "vp9", 1.8 },           // VP9
        { "libvpx-vp9", 1.8 },
        { "vp8", 0.9 },           // VP8
        { "libvpx", 0.9 },
        { "mpeg4", 0.6 },
        { "mpeg2video", 0.5 },
        { "mjpeg", 0.3 },

        // Hardware encoders (much faster, lower weight)
        { "h264_nvenc", 0.3 },
        { "hevc_nvenc", 0.4 },
        { "av1_nvenc", 0.5 },
        { "h264_qsv", 0.35 },
        { "hevc_qsv", 0.45 },
        { "av1_qsv", 0.55 },
        { "h264_amf", 0.35 },
        { "hevc_amf", 0.45 },
        { "h264_videotoolbox", 0.35 },
        { "hevc_videotoolbox", 0.45 },

        // Audio codecs
        { "aac", 0.1 },
        { "libfdk_aac", 0.1 },
        { "mp3", 0.08 },
        { "libmp3lame", 0.08 },
        { "opus", 0.12 },
        { "libopus", 0.12 },
        { "vorbis", 0.1 },
        { "flac", 0.05 },
        { "ac3", 0.08 },
        { "eac3", 0.09 },
        { "dts", 0.08 },
        { "pcm_s16le", 0.02 },
        { "copy", 0.01 }
    };

    // Preset multipliers for x264/x265 (relative to medium = 1.0)
    private static readonly Dictionary<string, double> PresetMultipliers = new()
    {
        { "ultrafast", 0.2 },
        { "superfast", 0.3 },
        { "veryfast", 0.4 },
        { "faster", 0.6 },
        { "fast", 0.8 },
        { "medium", 1.0 },
        { "slow", 1.5 },
        { "slower", 2.5 },
        { "veryslow", 4.0 },
        { "placebo", 8.0 }
    };

    /// <summary>
    /// Computes the weight of a task based on its characteristics.
    /// </summary>
    /// <param name="task">The task object (can be EncodingTask, EncodingTaskDefinition, or other)</param>
    /// <returns>A weight value where 1.0 is a reference 1080p h264 medium preset encode</returns>
    public static double ComputeWeight(object? task)
    {
        return task switch
        {
            EncodingTask encodingTask => ComputeEncodingTaskWeight(encodingTask),
            EncodingTaskDefinition taskDef => ComputeTaskDefinitionWeight(taskDef),
            StreamAnalysis analysis => ComputeStreamAnalysisWeight(analysis),
            _ => 1.0
        };
    }

    /// <summary>
    /// Computes weight for an EncodingTask from database
    /// </summary>
    public static double ComputeEncodingTaskWeight(EncodingTask task)
    {
        double baseWeight = task.TaskType?.ToLowerInvariant() switch
        {
            "video" => BaseVideoWeight,
            "audio" => BaseAudioWeight,
            "subtitle" => BaseSubtitleWeight,
            "mux" => BaseMuxWeight,
            _ => 1.0
        };

        // Use stored weight if available
        if (task.Weight > 0)
        {
            return task.Weight;
        }

        return baseWeight;
    }

    /// <summary>
    /// Computes weight for a task definition before database storage
    /// </summary>
    public static double ComputeTaskDefinitionWeight(EncodingTaskDefinition taskDef)
    {
        return taskDef.Weight > 0 ? taskDef.Weight : 1.0;
    }

    /// <summary>
    /// Computes weight based on stream analysis (before task creation)
    /// </summary>
    public static double ComputeStreamAnalysisWeight(StreamAnalysis analysis)
    {
        double weight = 1.0;

        // Resolution factor (from primary video stream)
        if (analysis.PrimaryVideoStream != null)
        {
            string resolution = GetResolutionKey(analysis.PrimaryVideoStream.Width, analysis.PrimaryVideoStream.Height);
            weight *= GetResolutionMultiplier(resolution);

            // High framerate adds complexity
            double framerate = analysis.PrimaryVideoStream.FrameRate;
            if (framerate > 30)
            {
                weight *= framerate / 30.0;
            }
        }

        // Duration factor (normalize to 1 hour = 1.0)
        if (analysis.Duration.TotalMinutes > 0)
        {
            weight *= analysis.Duration.TotalMinutes / 60.0;
        }

        // HDR adds complexity for tone mapping
        if (analysis.IsHDR)
        {
            weight *= 1.3;
        }

        return weight;
    }

    /// <summary>
    /// Computes weight for a video encoding profile
    /// </summary>
    public static double ComputeVideoProfileWeight(IVideoProfile profile, StreamAnalysis? analysis = null)
    {
        double weight = BaseVideoWeight;

        // Resolution
        string resolution = GetResolutionKey(profile.Width, profile.Height);
        weight *= GetResolutionMultiplier(resolution);

        // Codec complexity
        weight *= GetCodecMultiplier(profile.Codec);

        // Preset complexity
        if (!string.IsNullOrEmpty(profile.Preset))
        {
            weight *= GetPresetMultiplier(profile.Preset);
        }

        // HDR to SDR conversion adds complexity
        if (profile.ConvertHdrToSdr && analysis?.IsHDR == true)
        {
            weight *= 1.5;
        }

        // Duration factor
        if (analysis?.Duration.TotalMinutes > 0)
        {
            weight *= analysis.Duration.TotalMinutes / 60.0;
        }

        return weight;
    }

    /// <summary>
    /// Computes weight for an audio encoding profile
    /// </summary>
    public static double ComputeAudioProfileWeight(IAudioProfile profile, TimeSpan duration)
    {
        double weight = BaseAudioWeight;

        // Codec complexity
        weight *= GetCodecMultiplier(profile.Codec);

        // Channel count
        weight *= profile.Channels / 2.0; // Stereo = 1.0

        // Duration factor
        if (duration.TotalMinutes > 0)
        {
            weight *= duration.TotalMinutes / 60.0;
        }

        return weight;
    }

    /// <summary>
    /// Computes total weight for a complete encoding job
    /// </summary>
    public static double ComputeJobWeight(
        StreamAnalysis analysis,
        EncoderProfile profile)
    {
        double totalWeight = 0;

        // Video profiles
        if (profile.VideoProfiles != null)
        {
            foreach (IVideoProfile videoProfile in profile.VideoProfiles)
            {
                totalWeight += ComputeVideoProfileWeight(videoProfile, analysis);
            }
        }

        // Audio profiles
        if (profile.AudioProfiles != null)
        {
            foreach (IAudioProfile audioProfile in profile.AudioProfiles)
            {
                totalWeight += ComputeAudioProfileWeight(audioProfile, analysis.Duration);
            }
        }

        // Subtitle profiles (minimal weight)
        if (profile.SubtitleProfiles != null)
        {
            totalWeight += profile.SubtitleProfiles.Length * BaseSubtitleWeight;
        }

        return totalWeight;
    }

    /// <summary>
    /// Estimates encoding time based on weight and hardware capabilities
    /// </summary>
    /// <param name="weight">The computed task weight</param>
    /// <param name="hardwareSpeedFactor">Hardware speed factor (1.0 = baseline CPU, 3.0 = fast GPU)</param>
    /// <returns>Estimated encoding time</returns>
    public static TimeSpan EstimateEncodingTime(double weight, double hardwareSpeedFactor = 1.0)
    {
        // Baseline: weight 1.0 = 60 minutes on reference hardware
        double baseMinutes = 60.0;
        double estimatedMinutes = (weight * baseMinutes) / hardwareSpeedFactor;
        return TimeSpan.FromMinutes(estimatedMinutes);
    }

    // Helper methods

    private static string GetResolutionKey(int width, int height)
    {
        int maxDim = Math.Max(width, height);

        return maxDim switch
        {
            >= 7680 => "4320p",
            >= 3840 => "2160p",
            >= 2560 => "1440p",
            >= 1920 => "1080p",
            >= 1280 => "720p",
            >= 720 => "576p",
            >= 640 => "480p",
            >= 480 => "360p",
            _ => "240p"
        };
    }

    private static double GetResolutionMultiplier(string resolution)
    {
        return ResolutionMultipliers.TryGetValue(resolution, out double multiplier)
            ? multiplier
            : 1.0;
    }

    private static double GetCodecMultiplier(string codec)
    {
        string normalizedCodec = codec.ToLowerInvariant().Trim();
        return CodecMultipliers.TryGetValue(normalizedCodec, out double multiplier)
            ? multiplier
            : 1.0;
    }

    private static double GetPresetMultiplier(string preset)
    {
        string normalizedPreset = preset.ToLowerInvariant().Trim();
        return PresetMultipliers.TryGetValue(normalizedPreset, out double multiplier)
            ? multiplier
            : 1.0;
    }
}
