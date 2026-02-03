namespace NoMercy.EncoderV2.Jobs.EncodingJobRules;

/// <summary>
/// Configures job execution for live transcoding scenarios.
/// 
/// Live Transcoding Requirements:
/// 1. Real-time encoding (must keep up with playback speed or faster)
/// 2. Adaptive quality based on network conditions
/// 3. Client-side pause/resume support
/// 4. Progress monitoring with frame-accurate feedback
/// 5. Thread-safe stream handling
/// 
/// Differences from VOD:
/// - VOD: Encode once, store, optimize for distribution
/// - Live: Encode on-demand, stream immediately, priority is latency
/// 
/// Strategy:
/// 1. Use hardware acceleration when available (real-time requirement)
/// 2. Lower quality presets (fast/medium vs slow/veryslow)
/// 3. Segment-based output with shorter segments (for quick adaptation)
/// 4. Monitor network metrics and adjust bitrate
/// 5. Maintain connection state across pause/resume cycles
/// </summary>
public class LiveTranscodingRule : IEncodingJobRule
{
    public string RuleName => "Live Transcoding";

    public bool AppliesToJob(EncodingJobPayload job)
    {
        // Check if job metadata indicates live streaming
        return job.Status.Metadata?.ContainsKey("is_live_streaming") == true &&
               (bool)job.Status.Metadata["is_live_streaming"];
    }

    public Task<List<PostProcessingAction>> GetPostProcessingActionsAsync(EncodingJobPayload job)
    {
        List<PostProcessingAction> actions = new();

        if (job.Profile.VideoProfile is null)
            return Task.FromResult(actions);

        VideoProfileConfig? videoProfile = job.Profile.VideoProfile;

        // Action 1: Real-time preset enforcement
        actions.Add(new()
        {
            ActionType = "live-transcoding-config",
            DisplayName = "Configuring for live streaming",
            Format = videoProfile.Codec,
            Metadata = new()
            {
                // Force hardware acceleration for live
                { "require_hardware_accel", true },
                
                // Real-time speed targets
                { "target_speed", "1.0+" }, // Must be >= 1.0x (faster is better for buffering)
                { "warn_speed_below", 0.8 }, // Alert if falling behind
                
                // Preset overrides for live
                { "forced_preset", "fast" }, // Override user preset with live-safe preset
                { "allowed_live_presets", new[] { "ultrafast", "fast" } },
                
                // CRF disabled for live (use bitrate-based quality)
                { "disable_crf_for_live", true },
                { "use_constrained_vbv", true },
                
                // Buffer constraints
                { "vbv_bufsize_factor", 1.0 }, // Conservative buffering
                { "maxrate_factor", 1.5 }      // Peak bitrate headroom
            }
        });

        // Action 2: Segment management for quick adaptation
        actions.Add(new()
        {
            ActionType = "hls-segment-config",
            DisplayName = "Configuring HLS segments for low latency",
            Format = "m3u8",
            Metadata = new()
            {
                // Low latency segment timing
                { "segment_duration", 2 },       // 2-second segments (faster than default 4s)
                { "segment_list_size", 3 },      // Keep only 3 segments in playlist (6s total buffer)
                { "playlist_type", "event" },    // Event = can add segments, not sliding window
                { "init_time", 2 },              // Init segment timing
                
                // Client behavior guidance
                { "targetduration", 2 },         // Hint to player
                { "playlist_autoload", true },   // Auto-refresh playlist
                
                // Network-aware streaming
                { "enable_bitrate_adaptation", true },
                { "adaptation_trigger", "network-change" },
                { "fallback_qualities", new[] { "1080p", "720p", "480p" } }
            }
        });

        // Action 3: Progress monitoring for UI feedback
        actions.Add(new()
        {
            ActionType = "progress-monitoring",
            DisplayName = "Setting up real-time progress monitoring",
            Metadata = new()
            {
                // Update frequency for dashboard
                { "progress_update_interval_ms", 500 }, // Update every 500ms
                { "frame_accuracy", true },             // Report by frame, not estimated
                
                // Metrics to track
                { "track_metrics", new[] { "fps", "speed", "bitrate", "buffer", "network_latency" } },
                
                // Pause/resume support
                { "support_pause", true },
                { "support_resume", true },
                { "maintain_sync_on_pause", true },
                { "buffer_during_pause", false } // Don't encode while paused
            }
        });

        // Action 4: Error recovery for network interruptions
        actions.Add(new()
        {
            ActionType = "error-recovery",
            DisplayName = "Configuring network error handling",
            Metadata = new()
            {
                { "retry_on_network_error", true },
                { "max_reconnection_attempts", 5 },
                { "reconnection_backoff_ms", 2000 },
                
                // Graceful degradation
                { "quality_reduction_on_lag", true },
                { "quality_reduction_steps", new[] { "1080p", "720p", "480p" } },
                
                // Client notifications
                { "notify_quality_change", true },
                { "notify_buffer_status", true }
            }
        });

        return Task.FromResult(actions);
    }
}
