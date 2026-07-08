// -----------------------------------------------------------------------------
//  Copyright (c) 2024-present NoMercy Entertainment. All rights reserved.
//
//  This file is part of NoMercy MediaServer, source-available software (NOT open
//  source). Personal use and contributions are welcome; distribution, resale,
//  relicensing, and commercial exploitation are prohibited without explicit
//  written consent. See LICENSE for full terms. Distributed WITHOUT ANY WARRANTY.
//
//  SPDX-License-Identifier: LicenseRef-NoMercy-Proprietary
// -----------------------------------------------------------------------------

using NoMercy.Encoder.Errors;
using NoMercy.Encoder.Hardware;
using NoMercy.Encoder.Pipeline;
using NoMercy.Encoder.Profiles;

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
                new(
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
            new(
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

        return new(handle, null);
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
            new(
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

        return new(handle, null);
    }

    private static HardwareResolutionResult ResolvePreferHardware(
        VideoCodecType codec,
        IReadOnlyList<string> availableEncoders,
        SpeedIndex speedIndex,
        IDecisionLogSink decisions
    )
    {
        string swHandle = CanonicalSoftwareHandle(codec);

        (string best, double bestFps) = BestHardwareEntry(codec, speedIndex, availableEncoders);

        if (best == string.Empty)
        {
            // SpeedIndex has no HW measurements for this codec — the benchmark
            // runs lazily, so the first encode after install hits this path.
            // Probe availableEncoders for a codec-matching hardware handle so
            // PreferHardware actually prefers hardware until the benchmark
            // populates the cache. Codec match goes by the encoder-name prefix
            // ("hevc_nvenc" matches H265, not H264).
            string? availableHw = availableEncoders.FirstOrDefault(e =>
                CodecRegistry.IsHardware(e) && MatchesCodec(e, codec)
            );
            if (availableHw is not null)
            {
                decisions.Add(
                    new(
                        "plan",
                        "plan.encoder_resolved",
                        $"PreferHardware → {availableHw} (no benchmark yet, picked from availableEncoders)",
                        new
                        {
                            handle = availableHw,
                            codec = codec.ToString(),
                            reason = "prefer_hardware_unmeasured",
                        }
                    )
                );

                return new(availableHw, null);
            }

            decisions.Add(
                new(
                    "plan",
                    "plan.encoder_resolved",
                    $"PreferHardware → {swHandle} (no HW encoder available)",
                    new
                    {
                        handle = swHandle,
                        codec = codec.ToString(),
                        reason = "prefer_hardware_sw_fallback",
                    }
                )
            );

            return new(swHandle, null);
        }

        // Compute speed ratio vs software baseline
        double swFps = BestFpsForHandle(codec, swHandle, speedIndex);
        string ratio = swFps > 0 ? $"{bestFps / swFps:F1}×" : "?×";

        decisions.Add(
            new(
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

        return new(best, null);
    }

    private static HardwareResolutionResult ResolveForceHardware(
        VideoCodecType codec,
        IReadOnlyList<string> availableEncoders,
        SpeedIndex speedIndex,
        IDecisionLogSink decisions
    )
    {
        (string best, double bestFps) = BestHardwareEntry(codec, speedIndex, availableEncoders);

        if (best == string.Empty)
        {
            // Mirror PreferHardware's fallback: an unmeasured HW encoder is still
            // a HW encoder. Only hard-fail when availableEncoders also lacks a
            // codec-matching HW handle.
            string? availableHw = availableEncoders.FirstOrDefault(e =>
                CodecRegistry.IsHardware(e) && MatchesCodec(e, codec)
            );
            if (availableHw is not null)
            {
                decisions.Add(
                    new(
                        "plan",
                        "plan.encoder_resolved",
                        $"ForceHardware → {availableHw} (no benchmark yet, picked from availableEncoders)",
                        new
                        {
                            handle = availableHw,
                            codec = codec.ToString(),
                            reason = "force_hardware_unmeasured",
                        }
                    )
                );

                return new(availableHw, null);
            }

            EncoderRuntimeException failure = RuntimeErrors.HardwareForcedButUnavailable(
                codec.ToString()
            );

            decisions.Add(
                new(
                    "plan",
                    "plan.encoder_resolved",
                    $"ForceHardware → FAILED (no HW encoder available for {codec})",
                    new { codec = codec.ToString(), reason = "force_hardware_failed" }
                )
            );

            return new(null, failure);
        }

        string swHandle = CanonicalSoftwareHandle(codec);
        double swFps = BestFpsForHandle(codec, swHandle, speedIndex);
        string ratio = swFps > 0 ? $"{bestFps / swFps:F1}×" : "?×";

        decisions.Add(
            new(
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

        return new(best, null);
    }

    // Returns the highest-fps hardware entry for the codec in the speed index.
    // Returns ("", 0) when no hardware entries exist.
    //
    // The speed index is a persisted cache: it can carry measurements from a
    // previous ffmpeg build that had encoders the current build lacks (e.g.
    // nvenc rows after swapping to an ffmpeg compiled without nvenc). Emitting
    // such a handle makes every encode die on "Unknown encoder", so entries
    // absent from availableEncoders are skipped.
    private static (string Handle, double Fps) BestHardwareEntry(
        VideoCodecType codec,
        SpeedIndex speedIndex,
        IReadOnlyList<string> availableEncoders
    )
    {
        HashSet<string> available = new(availableEncoders, StringComparer.OrdinalIgnoreCase);

        string bestHandle = string.Empty;
        double bestFps = 0;

        foreach (KeyValuePair<SpeedKey, SpeedMeasurement> kv in speedIndex.Measurements)
        {
            if (kv.Key.Codec != codec)
                continue;

            if (!CodecRegistry.IsHardware(kv.Key.Encoder))
                continue;

            if (!available.Contains(kv.Key.Encoder))
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

    // Encoder-name prefix per codec — used to match HW handles (e.g. hevc_nvenc)
    // back to the requested codec without keeping a static table per encoder.
    private static bool MatchesCodec(string ffmpegEncoderName, VideoCodecType codec)
    {
        string prefix = codec switch
        {
            VideoCodecType.H264 => "h264_",
            VideoCodecType.H265 => "hevc_",
            VideoCodecType.Av1 => "av1_",
            VideoCodecType.Vp9 => "vp9_",
            _ => string.Empty,
        };

        return prefix.Length > 0
            && ffmpegEncoderName.StartsWith(prefix, StringComparison.OrdinalIgnoreCase);
    }
}
