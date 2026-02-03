namespace NoMercy.EncoderV2.Jobs.EncodingJobRules;

/// <summary>
/// Post-job validation to ensure encoding completed successfully and output is usable.
/// 
/// Validation Points:
/// 1. File size check: Output file exists and has reasonable size
/// 2. Codec verification: Output stream contains expected video/audio/subtitles
/// 3. Playability test: Quick ffprobe to verify stream integrity
/// 4. Master playlist generation: Verify HLS manifest is valid
/// 5. Sprite generation: Verify thumbnail sprite created
/// 6. Error logging: Capture any FFmpeg warnings/errors for debugging
/// 
/// This ensures that broken/incomplete encoding jobs don't proceed to multi-quality assembly.
/// Critical for master playlist reliability (can't create master from bad segments).
/// </summary>
public class JobValidationRule : IEncodingJobRule
{
    public string RuleName => "Job Validation";

    public bool AppliesToJob(EncodingJobPayload job)
    {
        // Always validate jobs (especially before next quality in multi-quality chain)
        return true;
    }

    public Task<List<PostProcessingAction>> GetPostProcessingActionsAsync(EncodingJobPayload job)
    {
        List<PostProcessingAction> actions = new();

        // Action 1: Output file validation
        actions.Add(new()
        {
            ActionType = "file-validation",
            DisplayName = "Validating output file",
            OutputPath = job.Output.DestinationFolder,
            Metadata = new()
            {
                { "check_file_exists", true },
                { "min_file_size_bytes", 10_000 },  // At least 10KB
                { "max_duration_variance_percent", 5 }, // Allow 5% duration difference from estimate
                
                // Fail conditions
                { "fail_on_missing_file", true },
                { "fail_on_size_zero", true },
                { "fail_on_incomplete_segment", true }
            }
        });

        // Action 2: Stream codec verification
        actions.Add(new()
        {
            ActionType = "codec-verification",
            DisplayName = "Verifying encoded streams",
            Format = job.Profile.Container,
            Metadata = new()
            {
                { "verify_video_codec", job.Profile.VideoProfile != null },
                { "expected_video_codec", job.Profile.VideoProfile?.Codec ?? "none" },
                
                { "verify_audio_codec", job.Profile.AudioProfile != null },
                { "expected_audio_codec", job.Profile.AudioProfile?.Codec ?? "none" },
                
                { "verify_subtitle_codec", job.Profile.SubtitleProfile != null },
                { "expected_subtitle_codec", job.Profile.SubtitleProfile?.Codec ?? "none" },
                
                // Failure behavior
                { "fail_on_missing_stream", true },
                { "fail_on_codec_mismatch", true },
                { "warn_on_bitrate_variance", true }
            }
        });

        // Action 3: Playability testing (quick sanity check)
        actions.Add(new()
        {
            ActionType = "playability-test",
            DisplayName = "Testing stream playability",
            OutputPath = job.Output.DestinationFolder,
            Metadata = new()
            {
                // Quick probe (don't decode everything)
                { "use_ffprobe_only", true },
                { "timeout_seconds", 30 },
                
                // Decode frame count check
                { "verify_stream_decodable", true },
                { "decode_first_n_frames", 10 }, // Verify first 10 frames decode
                
                // Hardware compatibility check
                { "check_device_compatibility", job.Status.Metadata?.ContainsKey("target_devices") == true },
                
                // WebVTT validation if HLS
                { "validate_hls_manifest", job.Profile.Container == "m3u8" },
                { "validate_segment_timing", job.Profile.Container == "m3u8" }
            }
        });

        // Action 4: HLS master playlist readiness
        if (job.Profile.Container == "m3u8")
        {
            actions.Add(new()
            {
                ActionType = "master-playlist-check",
                DisplayName = "Checking prerequisites for master playlist",
                OutputPath = job.Output.DestinationFolder,
                Metadata = new()
                {
                    { "require_all_qualities_ready", false }, // Set true only for final multi-quality job
                    { "missing_quality_handling", "defer-master-generation" },
                    
                    // Segment completeness
                    { "verify_all_segments_present", true },
                    { "verify_playlist_manifest", true },
                    
                    // Bandwidth metadata
                    { "include_bandwidth_metrics", true },
                    { "resolution_string", $"{job.Profile.VideoProfile?.Width}x{job.Profile.VideoProfile?.Height}" },
                    { "codec_string", job.Profile.VideoProfile?.Codec ?? "unknown" }
                }
            });
        }

        // Action 5: Sprite/thumbnail generation validation
        actions.Add(new()
        {
            ActionType = "sprite-validation",
            DisplayName = "Validating thumbnail sprite",
            OutputPath = Path.Combine(job.Output.DestinationFolder, "sprites"),
            Metadata = new()
            {
                { "require_sprite", true },
                { "min_sprite_size_kb", 100 },
                { "verify_vtt_manifest", true },
                
                { "fail_on_missing_sprite", false }, // Non-critical, warn only
                { "fallback_on_sprite_failure", true }
            }
        });

        // Action 6: Error collection from FFmpeg output
        actions.Add(new()
        {
            ActionType = "error-logging",
            DisplayName = "Analyzing encoding errors and warnings",
            Metadata = new()
            {
                { "log_ffmpeg_warnings", true },
                { "fail_on_critical_error", true },
                { "warn_on_quality_issues", true },
                
                // Error patterns to watch for
                { "error_patterns", new[] 
                {
                    "frame=.*bitrate=.*",        // Normal progress indicators
                    "Bitrate.*way too low",       // Quality warning
                    "Too many packets",           // Buffer warning
                    "Incomplete frame",           // Corruption warning
                    "Frame.*corrupted",           // Serious issue
                    "Segmentation fault",         // Process crash
                    "Out of memory"               // Resource exhaustion
                } },
                
                // Severity mapping
                { "error_severity_levels", new Dictionary<string, string>
                {
                    { "bitrate.*low", "warn" },
                    { "frame.*drop", "warn" },
                    { "corrupted", "error" },
                    { "fault|crash", "critical" }
                } }
            }
        });

        return Task.FromResult(actions);
    }
}
