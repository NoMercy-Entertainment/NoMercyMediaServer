namespace NoMercy.EncoderV2.Jobs.EncodingJobRules;

/// <summary>
/// Handles audio stream selection based on language preferences and defaults.
/// 
/// Strategy:
/// 1. Select streams matching AllowedLanguages from profile
/// 2. Apply language fallback chain: user preferred → eng → default → first available
/// 3. Handle multi-track scenarios (multiple languages, commentary, descriptive audio)
/// 4. Respect audio codec compatibility with chosen container
/// 5. Maintain relative positioning (keep original order for master playlist)
/// </summary>
public class AudioStreamSelectionRule : IEncodingJobRule
{
    public string RuleName => "Audio Stream Selection";

    public bool AppliesToJob(EncodingJobPayload job)
    {
        return job.Profile.AudioProfile != null;
    }

    public Task<List<PostProcessingAction>> GetPostProcessingActionsAsync(EncodingJobPayload job)
    {
        List<PostProcessingAction> actions = new();

        if (job.Profile.AudioProfile is null)
            return Task.FromResult(actions);

        AudioProfileConfig? audioProfile = job.Profile.AudioProfile;

        // Action 1: Stream selection and mapping
        actions.Add(new()
        {
            ActionType = "audio-stream-selection",
            DisplayName = "Selecting audio streams by language",
            Format = audioProfile.Codec,
            Metadata = new()
            {
                { "allowed_languages", audioProfile.AllowedLanguages.Count == 0
                    ? "all"
                    : string.Join(",", audioProfile.AllowedLanguages) },
                { "language_fallback_chain", new[] { "user-preferred", "eng", "en", "default", "first-available" } },
                { "keep_order", true },
                { "bitrate", audioProfile.Bitrate },
                { "channels", audioProfile.Channels },
                { "sample_rate", audioProfile.SampleRate }
            }
        });

        // Action 2: Quality-specific settings for different codec scenarios
        actions.Add(new()
        {
            ActionType = "audio-codec-optimization",
            DisplayName = "Optimizing audio codec settings",
            Format = audioProfile.Codec,
            Metadata = new()
            {
                // Codec-specific quality settings
                { "aac_quality", 4 },              // FFmpeg default
                { "opus_bitrate_control", "cbr" }, // Constant bitrate for streaming
                { "eac3_room_type", "large" },    // Surround optimization
                { "flac_compression", 8 },         // Maximum lossless compression
                // Streaming-specific settings
                { "hls_segment_duration", 4 },     // Segment alignment
                { "force_mono_compatibility", false }
            }
        });

        return Task.FromResult(actions);
    }
}
