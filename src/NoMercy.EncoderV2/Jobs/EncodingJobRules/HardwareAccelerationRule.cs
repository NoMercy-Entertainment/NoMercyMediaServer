namespace NoMercy.EncoderV2.Jobs.EncodingJobRules;

/// <summary>
/// Configures hardware acceleration based on available GPUs and codec requirements.
/// 
/// Supported Accelerators:
/// - NVIDIA: NVENC (H.264/H.265), NVDEC (decode)
/// - AMD: VCE (H.264/H.265), AMF (advanced encoding)
/// - Intel: QSV (H.264/H.265/AV1)
/// - Apple: VideoToolbox (H.264/H.265)
/// 
/// Strategy:
/// 1. Detect available hardware at job startup
/// 2. Match codec to accelerator capability (e.g., H.265 NVENC for NVIDIA)
/// 3. Fall back to software encoding if hardware unavailable
/// 4. For live transcoding: Use fast presets + hardware to meet real-time requirements
/// 5. For archival: Prefer software quality over speed
/// </summary>
public class HardwareAccelerationRule : IEncodingJobRule
{
    public string RuleName => "Hardware Acceleration";

    public bool AppliesToJob(EncodingJobPayload job)
    {
        // Could apply to any encoding job if hardware is available
        // This rule determines whether to use it
        return job.Profile?.VideoProfile != null;
    }

    public Task<List<PostProcessingAction>> GetPostProcessingActionsAsync(EncodingJobPayload job)
    {
        List<PostProcessingAction> actions = new();

        if (job.Profile?.VideoProfile is null)
            return Task.FromResult(actions);

        VideoProfileConfig? videoProfile = job.Profile.VideoProfile;

        // Determine acceleration strategy based on job metadata
        actions.Add(new()
        {
            ActionType = "hardware-acceleration-config",
            DisplayName = "Configuring hardware acceleration",
            Format = videoProfile.Codec,
            Metadata = new()
            {
                // Hardware priority: try in order
                { "accelerator_priority", new[] { "nvidia", "intel", "amd", "apple", "software" } },
                
                // Codec-to-accelerator mapping
                { "codec_accelerators", new Dictionary<string, string[]>
                {
                    { "h264", new[] { "h264_nvenc", "h264_qsv", "h264_amf", "h264_videotoolbox" } },
                    { "h265", new[] { "hevc_nvenc", "hevc_qsv", "hevc_amf", "hevc_videotoolbox" } },
                    { "av1", new[] { "av1_nvenc", "av1_qsv" } },  // Limited AV1 hardware support
                    { "vp9", new[] { "vp9_qsv" } }
                } },
                
                // Preset mapping: software preset → hardware equivalent
                { "preset_mapping", new Dictionary<string, string>
                {
                    // NVIDIA NVENC (0-fast, 1-med, 2-slow)
                    { "nvidia-ultrafast", "0" },
                    { "nvidia-fast", "0" },
                    { "nvidia-medium", "1" },
                    { "nvidia-slow", "2" },
                    
                    // Intel QSV (1-veryfast, 2-fast, 3-medium, 4-slow, 5-veryslow)
                    { "intel-ultrafast", "1" },
                    { "intel-fast", "2" },
                    { "intel-medium", "3" },
                    { "intel-slow", "4" },
                    
                    // AMD AMF (0-preset, 1-balanced, 2-quality)
                    { "amd-fast", "0" },
                    { "amd-medium", "1" },
                    { "amd-slow", "2" }
                } },
                
                // Live transcoding: require real-time capable acceleration
                { "live_transcoding_capable", new[] { "h264_nvenc", "h264_qsv", "h265_nvenc", "hevc_qsv" } },
                
                // Fallback behavior
                { "fallback_to_software", true },
                { "log_acceleration_used", true }
            }
        });

        return Task.FromResult(actions);
    }
}
