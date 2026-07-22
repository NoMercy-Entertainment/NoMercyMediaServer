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

using System.Globalization;
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
        [key: VideoCodecType.H264] = "libx264",
        [key: VideoCodecType.H265] = "libx265",
        [key: VideoCodecType.Av1] = "libsvtav1",
        [key: VideoCodecType.Vp9] = "libvpx-vp9",
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
                entry: new(
                    Stage: "encoder.select",
                    Key: "encoder.select.copy",
                    Message: "Stream copy — no encoder selection performed",
                    Data: new { codec = codec.ToString() }
                )
            );
            return new(EncoderHandle: "copy", Failure: null);
        }

        return preference switch
        {
            HardwarePreference.ForceSoftware => ResolveForceSoftware(
                codec: codec,
                availableEncoders: availableEncoders,
                decisions: decisions
            ),
            HardwarePreference.PreferQuality => ResolvePreferQuality(
                codec: codec,
                availableEncoders: availableEncoders,
                decisions: decisions
            ),
            HardwarePreference.PreferHardware => ResolvePreferHardware(
                codec: codec,
                availableEncoders: availableEncoders,
                speedIndex: speedIndex,
                decisions: decisions
            ),
            HardwarePreference.ForceHardware => ResolveForceHardware(
                codec: codec,
                availableEncoders: availableEncoders,
                speedIndex: speedIndex,
                decisions: decisions
            ),
            _ => ResolvePreferHardware(codec: codec, availableEncoders: availableEncoders, speedIndex: speedIndex, decisions: decisions),
        };
    }

    private static HardwareResolutionResult ResolveForceSoftware(
        VideoCodecType codec,
        IReadOnlyList<string> availableEncoders,
        IDecisionLogSink decisions
    )
    {
        string handle = CanonicalSoftwareHandle(codec: codec);

        decisions.Add(
            entry: new(
                Stage: "plan",
                Key: "plan.encoder_resolved",
                Message: $"ForceSoftware → {handle}",
                Data: new
                {
                    handle,
                    codec = codec.ToString(),
                    reason = "force_software",
                }
            )
        );

        return new(EncoderHandle: handle, Failure: null);
    }

    private static HardwareResolutionResult ResolvePreferQuality(
        VideoCodecType codec,
        IReadOnlyList<string> availableEncoders,
        IDecisionLogSink decisions
    )
    {
        string handle = CanonicalSoftwareHandle(codec: codec);
        bool hwAvailable = availableEncoders.Any(predicate: CodecRegistry.IsHardware);

        string message = hwAvailable
            ? $"PreferQuality → {handle} (HW available but quality preferred)"
            : $"PreferQuality → {handle}";

        decisions.Add(
            entry: new(
                Stage: "plan",
                Key: "plan.encoder_resolved",
                Message: message,
                Data: new
                {
                    handle,
                    codec = codec.ToString(),
                    reason = "prefer_quality",
                    hw_available = hwAvailable,
                }
            )
        );

        return new(EncoderHandle: handle, Failure: null);
    }

    private static HardwareResolutionResult ResolvePreferHardware(
        VideoCodecType codec,
        IReadOnlyList<string> availableEncoders,
        SpeedIndex speedIndex,
        IDecisionLogSink decisions
    )
    {
        string swHandle = CanonicalSoftwareHandle(codec: codec);

        (string best, double bestFps) = BestHardwareEntry(codec: codec, speedIndex: speedIndex, availableEncoders: availableEncoders);

        if (best == string.Empty)
        {
            // SpeedIndex has no HW measurements for this codec — the benchmark
            // runs lazily, so the first encode after install hits this path.
            // Probe availableEncoders for a codec-matching hardware handle so
            // PreferHardware actually prefers hardware until the benchmark
            // populates the cache. Codec match goes by the encoder-name prefix
            // ("hevc_nvenc" matches H265, not H264).
            string? availableHw = availableEncoders.FirstOrDefault(predicate: e =>
                CodecRegistry.IsHardware(ffmpegEncoderName: e) && MatchesCodec(ffmpegEncoderName: e, codec: codec)
            );
            if (availableHw is not null)
            {
                decisions.Add(
                    entry: new(
                        Stage: "plan",
                        Key: "plan.encoder_resolved",
                        Message: $"PreferHardware → {availableHw} (no benchmark yet, picked from availableEncoders)",
                        Data: new
                        {
                            handle = availableHw,
                            codec = codec.ToString(),
                            reason = "prefer_hardware_unmeasured",
                        }
                    )
                );

                return new(EncoderHandle: availableHw, Failure: null);
            }

            decisions.Add(
                entry: new(
                    Stage: "plan",
                    Key: "plan.encoder_resolved",
                    Message: $"PreferHardware → {swHandle} (no HW encoder available)",
                    Data: new
                    {
                        handle = swHandle,
                        codec = codec.ToString(),
                        reason = "prefer_hardware_sw_fallback",
                    }
                )
            );

            return new(EncoderHandle: swHandle, Failure: null);
        }

        // Compute speed ratio vs software baseline
        double swFps = BestFpsForHandle(codec: codec, handle: swHandle, speedIndex: speedIndex);
        // This ratio rides the DecisionLog Message/Data the dashboard reads over
        // the API — keep it period-decimal regardless of host locale.
        string ratio =
            swFps > 0 ? $"{(bestFps / swFps).ToString(format: "F1", provider: CultureInfo.InvariantCulture)}×" : "?×";

        decisions.Add(
            entry: new(
                Stage: "plan",
                Key: "plan.encoder_resolved",
                Message: $"PreferHardware → {best} ({ratio} over {swHandle})",
                Data: new
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

        return new(EncoderHandle: best, Failure: null);
    }

    private static HardwareResolutionResult ResolveForceHardware(
        VideoCodecType codec,
        IReadOnlyList<string> availableEncoders,
        SpeedIndex speedIndex,
        IDecisionLogSink decisions
    )
    {
        (string best, double bestFps) = BestHardwareEntry(codec: codec, speedIndex: speedIndex, availableEncoders: availableEncoders);

        if (best == string.Empty)
        {
            // Mirror PreferHardware's fallback: an unmeasured HW encoder is still
            // a HW encoder. Only hard-fail when availableEncoders also lacks a
            // codec-matching HW handle.
            string? availableHw = availableEncoders.FirstOrDefault(predicate: e =>
                CodecRegistry.IsHardware(ffmpegEncoderName: e) && MatchesCodec(ffmpegEncoderName: e, codec: codec)
            );
            if (availableHw is not null)
            {
                decisions.Add(
                    entry: new(
                        Stage: "plan",
                        Key: "plan.encoder_resolved",
                        Message: $"ForceHardware → {availableHw} (no benchmark yet, picked from availableEncoders)",
                        Data: new
                        {
                            handle = availableHw,
                            codec = codec.ToString(),
                            reason = "force_hardware_unmeasured",
                        }
                    )
                );

                return new(EncoderHandle: availableHw, Failure: null);
            }

            EncoderRuntimeException failure = RuntimeErrors.HardwareForcedButUnavailable(
                requested: codec.ToString()
            );

            decisions.Add(
                entry: new(
                    Stage: "plan",
                    Key: "plan.encoder_resolved",
                    Message: $"ForceHardware → FAILED (no HW encoder available for {codec})",
                    Data: new { codec = codec.ToString(), reason = "force_hardware_failed" }
                )
            );

            return new(EncoderHandle: null, Failure: failure);
        }

        string swHandle = CanonicalSoftwareHandle(codec: codec);
        double swFps = BestFpsForHandle(codec: codec, handle: swHandle, speedIndex: speedIndex);
        // This ratio rides the DecisionLog Message/Data the dashboard reads over
        // the API — keep it period-decimal regardless of host locale.
        string ratio =
            swFps > 0 ? $"{(bestFps / swFps).ToString(format: "F1", provider: CultureInfo.InvariantCulture)}×" : "?×";

        decisions.Add(
            entry: new(
                Stage: "plan",
                Key: "plan.encoder_resolved",
                Message: $"ForceHardware → {best} ({ratio} over {swHandle})",
                Data: new
                {
                    handle = best,
                    codec = codec.ToString(),
                    reason = "force_hardware",
                    fps = bestFps,
                    ratio,
                }
            )
        );

        return new(EncoderHandle: best, Failure: null);
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
        HashSet<string> available = new(collection: availableEncoders, comparer: StringComparer.OrdinalIgnoreCase);

        string bestHandle = string.Empty;
        double bestFps = 0;

        foreach (KeyValuePair<SpeedKey, SpeedMeasurement> kv in speedIndex.Measurements)
        {
            if (kv.Key.Codec != codec)
                continue;

            if (!CodecRegistry.IsHardware(ffmpegEncoderName: kv.Key.Encoder))
                continue;

            if (!available.Contains(item: kv.Key.Encoder))
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

            if (!string.Equals(a: kv.Key.Encoder, b: handle, comparisonType: StringComparison.OrdinalIgnoreCase))
                continue;

            if (kv.Value.Fps > best)
                best = kv.Value.Fps;
        }

        return best;
    }

    private static string CanonicalSoftwareHandle(VideoCodecType codec)
    {
        return SoftwareHandles.TryGetValue(key: codec, value: out string? handle)
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
            && ffmpegEncoderName.StartsWith(value: prefix, comparisonType: StringComparison.OrdinalIgnoreCase);
    }
}
