using NoMercy.Encoder.Errors;
using NoMercy.Encoder.Hardware;
using NoMercy.Encoder.Pipeline;
using NoMercy.Encoder.Profiles.V2;

namespace NoMercy.Encoder.Codecs;

public sealed record HardwareResolutionResult(
    string? EncoderHandle,
    EncoderRuntimeException? Failure
);

public interface IHardwarePreferenceResolver
{
    HardwareResolutionResult Resolve(
        VideoCodecType codec,
        HardwarePreference preference,
        IReadOnlyList<string> availableEncoders,
        SpeedIndex speedIndex,
        IDecisionLogSink decisions
    );
}

public sealed class HardwarePreferenceResolver : IHardwarePreferenceResolver
{
    // Canonical software handles per codec — the definitive list of SW encoders
    // that deliver the best quality at the same bitrate. Codecs not in the table
    // fall back to a lib<lowerCodecName> heuristic.
    private static readonly Dictionary<VideoCodecType, string> SoftwareHandles = new()
    {
        [VideoCodecType.H264] = "libx264",
        [VideoCodecType.H265] = "libx265",
        [VideoCodecType.Av1] = "libsvtav1",
        [VideoCodecType.Vp9] = "libvpx-vp9",
    };

    public HardwareResolutionResult Resolve(
        VideoCodecType codec,
        HardwarePreference preference,
        IReadOnlyList<string> availableEncoders,
        SpeedIndex speedIndex,
        IDecisionLogSink decisions
    )
    {
        // Stream copy short-circuit. There is no encoder selection for "copy"
        // — ffmpeg's pseudo-codec is always available, never a hardware path,
        // never gated by the speed index. Skipping the preference dispatch
        // also keeps Copy out of the SoftwareHandles table (it doesn't need
        // a "canonical" entry — its handle is just "copy").
        if (codec == VideoCodecType.Copy)
        {
            decisions.Add(
                new DecisionLog(
                    "encoder.select",
                    "encoder.select.copy",
                    "Stream copy — no encoder selection performed",
                    new { codec = codec.ToString() }
                )
            );
            return new(EncoderHandle: "copy", Failure: null);
        }

        return preference switch
        {
            HardwarePreference.ForceSoftware => ResolveForceSoftware(
                codec,
                availableEncoders,
                decisions
            ),
            HardwarePreference.PreferQuality => ResolvePreferQuality(
                codec,
                availableEncoders,
                decisions
            ),
            HardwarePreference.PreferHardware => ResolvePreferHardware(
                codec,
                availableEncoders,
                speedIndex,
                decisions
            ),
            HardwarePreference.ForceHardware => ResolveForceHardware(
                codec,
                availableEncoders,
                speedIndex,
                decisions
            ),
            _ => ResolvePreferHardware(codec, availableEncoders, speedIndex, decisions),
        };
    }

    private static HardwareResolutionResult ResolveForceSoftware(
        VideoCodecType codec,
        IReadOnlyList<string> availableEncoders,
        IDecisionLogSink decisions
    )
    {
        string handle = CanonicalSoftwareHandle(codec);

        decisions.Add(
            new DecisionLog(
                "plan",
                "plan.encoder_resolved",
                $"ForceSoftware → {handle}",
                new
                {
                    handle,
                    codec = codec.ToString(),
                    reason = "force_software",
                }
            )
        );

        return new HardwareResolutionResult(handle, null);
    }

    private static HardwareResolutionResult ResolvePreferQuality(
        VideoCodecType codec,
        IReadOnlyList<string> availableEncoders,
        IDecisionLogSink decisions
    )
    {
        string handle = CanonicalSoftwareHandle(codec);
        bool hwAvailable = availableEncoders.Any(CodecRegistry.IsHardware);

        string message = hwAvailable
            ? $"PreferQuality → {handle} (HW available but quality preferred)"
            : $"PreferQuality → {handle}";

        decisions.Add(
            new DecisionLog(
                "plan",
                "plan.encoder_resolved",
                message,
                new
                {
                    handle,
                    codec = codec.ToString(),
                    reason = "prefer_quality",
                    hw_available = hwAvailable,
                }
            )
        );

        return new HardwareResolutionResult(handle, null);
    }

    private static HardwareResolutionResult ResolvePreferHardware(
        VideoCodecType codec,
        IReadOnlyList<string> availableEncoders,
        SpeedIndex speedIndex,
        IDecisionLogSink decisions
    )
    {
        string swHandle = CanonicalSoftwareHandle(codec);

        (string best, double bestFps) = BestHardwareEntry(codec, speedIndex);

        if (best == string.Empty)
        {
            // No hardware entries in the speed index — fall back to software
            decisions.Add(
                new DecisionLog(
                    "plan",
                    "plan.encoder_resolved",
                    $"PreferHardware → {swHandle} (no HW benchmark entries)",
                    new
                    {
                        handle = swHandle,
                        codec = codec.ToString(),
                        reason = "prefer_hardware_sw_fallback",
                    }
                )
            );

            return new HardwareResolutionResult(swHandle, null);
        }

        // Compute speed ratio vs software baseline
        double swFps = BestFpsForHandle(codec, swHandle, speedIndex);
        string ratio = swFps > 0 ? $"{bestFps / swFps:F1}×" : "?×";

        decisions.Add(
            new DecisionLog(
                "plan",
                "plan.encoder_resolved",
                $"PreferHardware → {best} ({ratio} over {swHandle})",
                new
                {
                    handle = best,
                    codec = codec.ToString(),
                    reason = "prefer_hardware",
                    fps = bestFps,
                    sw_fps = swFps,
                    ratio,
                }
            )
        );

        return new HardwareResolutionResult(best, null);
    }

    private static HardwareResolutionResult ResolveForceHardware(
        VideoCodecType codec,
        IReadOnlyList<string> availableEncoders,
        SpeedIndex speedIndex,
        IDecisionLogSink decisions
    )
    {
        (string best, double bestFps) = BestHardwareEntry(codec, speedIndex);

        if (best == string.Empty)
        {
            // No hardware encoder in the speed index — hard fail
            EncoderRuntimeException failure = RuntimeErrors.HardwareForcedButUnavailable(
                codec.ToString()
            );

            decisions.Add(
                new DecisionLog(
                    "plan",
                    "plan.encoder_resolved",
                    $"ForceHardware → FAILED (no HW benchmark entries for {codec})",
                    new { codec = codec.ToString(), reason = "force_hardware_failed" }
                )
            );

            return new HardwareResolutionResult(null, failure);
        }

        string swHandle = CanonicalSoftwareHandle(codec);
        double swFps = BestFpsForHandle(codec, swHandle, speedIndex);
        string ratio = swFps > 0 ? $"{bestFps / swFps:F1}×" : "?×";

        decisions.Add(
            new DecisionLog(
                "plan",
                "plan.encoder_resolved",
                $"ForceHardware → {best} ({ratio} over {swHandle})",
                new
                {
                    handle = best,
                    codec = codec.ToString(),
                    reason = "force_hardware",
                    fps = bestFps,
                    ratio,
                }
            )
        );

        return new HardwareResolutionResult(best, null);
    }

    // Returns the highest-fps hardware entry for the codec in the speed index.
    // Returns ("", 0) when no hardware entries exist.
    private static (string Handle, double Fps) BestHardwareEntry(
        VideoCodecType codec,
        SpeedIndex speedIndex
    )
    {
        string bestHandle = string.Empty;
        double bestFps = 0;

        foreach (KeyValuePair<SpeedKey, SpeedMeasurement> kv in speedIndex.Measurements)
        {
            if (kv.Key.Codec != codec)
                continue;

            if (!CodecRegistry.IsHardware(kv.Key.Encoder))
                continue;

            if (kv.Value.Fps > bestFps)
            {
                bestFps = kv.Value.Fps;
                bestHandle = kv.Key.Encoder;
            }
        }

        return (bestHandle, bestFps);
    }

    // Returns the best (highest) fps recorded for a specific encoder handle.
    private static double BestFpsForHandle(
        VideoCodecType codec,
        string handle,
        SpeedIndex speedIndex
    )
    {
        double best = 0;

        foreach (KeyValuePair<SpeedKey, SpeedMeasurement> kv in speedIndex.Measurements)
        {
            if (kv.Key.Codec != codec)
                continue;

            if (!string.Equals(kv.Key.Encoder, handle, StringComparison.OrdinalIgnoreCase))
                continue;

            if (kv.Value.Fps > best)
                best = kv.Value.Fps;
        }

        return best;
    }

    private static string CanonicalSoftwareHandle(VideoCodecType codec)
    {
        return SoftwareHandles.TryGetValue(codec, out string? handle)
            ? handle
            : $"lib{codec.ToString().ToLowerInvariant()}";
    }
}
