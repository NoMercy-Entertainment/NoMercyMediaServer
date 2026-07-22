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

namespace NoMercy.Encoder.Hardware;

/// <summary>
/// FFmpeg calibration-argument assembly extracted from HardwareBenchmark:
/// builds the per-target encode command (hwaccel init, hw-upload filter,
/// device selector) and the stderr truncation helper.
/// </summary>
internal static class CalibrationArgumentBuilder
{
    internal static string[] BuildCalibrationArguments(
        CalibrationTarget target,
        int width,
        int height
    )
    {
        (double sourceSeconds, int _) = HardwareBenchmark.CalibrationProfile(encoder: target.Encoder);

        List<string> args = ["-hide_banner", "-nostats", "-loglevel", "error"];

        // Hardware device init — must come BEFORE the input flag so the
        // encoder's hw context is ready when frames arrive. Without this,
        // ffmpeg can fall back to CPU or fail the encoder entirely, making
        // our measurement meaningless.
        AddHwaccelInitArgs(args: args, target: target);

        args.Add(item: "-f");
        args.Add(item: "lavfi");
        args.Add(item: "-i");
        args.Add(
            item: $"testsrc=duration={sourceSeconds.ToString(provider: CultureInfo.InvariantCulture)}:size={width}x{height}:rate={HardwareBenchmark.SourceFrameRate.ToString(provider: CultureInfo.InvariantCulture)}"
        );

        // Upload lavfi frames to the GPU before the encoder consumes them.
        // Without the upload filter the encoder still works (modern ffmpeg
        // auto-uploads) but CPU→GPU transfer bleeds into the encode
        // measurement. Explicit hwupload keeps the fps number tied to GPU
        // encode throughput.
        AddHwUploadFilter(args: args, target: target);

        args.Add(item: "-c:v");
        args.Add(item: target.Encoder.FfmpegName);

        // Vendor-specific flags (e.g. -usage transcoding for AMF).
        foreach ((string flag, string value) in target.Encoder.VendorSpecificFlags)
        {
            args.Add(item: flag);
            args.Add(item: value);
        }

        // GPU device selector on the encoder itself. Matters when the host
        // has multiple GPUs of the same vendor — without this, ffmpeg picks
        // the first one and every "per-device" benchmark actually exercises
        // the same card.
        AddEncoderDeviceSelector(args: args, target: target);

        // Use a reasonable default preset if the encoder has one. Matches
        // what production encodes would typically do.
        if (target.Encoder.Presets.Length > 0)
        {
            string preset =
                target.Encoder.Presets.Contains(value: "medium") ? "medium"
                : target.Encoder.Presets.Contains(value: "p4") ? "p4"
                : target.Encoder.Presets[target.Encoder.Presets.Length / 2];
            args.Add(item: "-preset");
            args.Add(item: preset);
        }

        // Hard cap on encoded frames — ffmpeg stops as soon as the output
        // reaches this count, regardless of how much source remains. Keeps
        // slow encoders from holding the benchmark thread hostage. The cap
        // varies per encoder via CalibrationProfile so fast encoders get a
        // long enough probe to settle into steady state (300 frames) and
        // slow encoders bail early (60 frames).
        (_, int maxFrames) = HardwareBenchmark.CalibrationProfile(encoder: target.Encoder);
        args.Add(item: "-frames:v");
        args.Add(item: maxFrames.ToString(provider: CultureInfo.InvariantCulture));

        args.Add(item: "-f");
        args.Add(item: "null");
        args.Add(item: "-");
        args.Add(item: "-progress");
        args.Add(item: "pipe:1");

        return [.. args];
    }

    /// <summary>
    /// Appends vendor-specific <c>-init_hw_device</c> arguments so the hw
    /// encoder wires itself to the real GPU (and the right GPU on
    /// multi-card systems). No-op for software encoders and for encoder
    /// families where ffmpeg's auto-init already handles device selection.
    /// </summary>
    private static void AddHwaccelInitArgs(List<string> args, CalibrationTarget target)
    {
        if (target.Encoder.RequiredVendor is not GpuVendor vendor)
            return;

        string deviceArg = target.VendorIndex.ToString(provider: CultureInfo.InvariantCulture);

        switch (vendor)
        {
            case GpuVendor.Nvidia:
                args.Add(item: "-init_hw_device");
                args.Add(item: $"cuda=cu:{deviceArg}");
                args.Add(item: "-filter_hw_device");
                args.Add(item: "cu");
                break;
            case GpuVendor.Intel:
                if (target.Encoder.FfmpegName.Contains(value: "_qsv", comparisonType: StringComparison.OrdinalIgnoreCase))
                {
                    args.Add(item: "-init_hw_device");
                    args.Add(item: "qsv=hw");
                    args.Add(item: "-filter_hw_device");
                    args.Add(item: "hw");
                }
                break;
            case GpuVendor.Amd:
                // AMF on Windows runs on CPU-side frames natively; on Linux
                // it uses vaapi. We skip explicit init — the encoder
                // initializes on first use.
                break;
            case GpuVendor.Apple:
                // VideoToolbox auto-initializes on the default GPU.
                break;
        }
    }

    private static void AddHwUploadFilter(List<string> args, CalibrationTarget target)
    {
        if (target.Encoder.RequiredVendor is not GpuVendor vendor)
            return;

        string? filter = vendor switch
        {
            GpuVendor.Nvidia => "format=nv12,hwupload_cuda",
            GpuVendor.Intel
                when target.Encoder.FfmpegName.Contains(
                    value: "_qsv",
                    comparisonType: StringComparison.OrdinalIgnoreCase
                ) => "format=nv12,hwupload=extra_hw_frames=16",
            _ => null,
        };

        if (filter is null)
            return;

        args.Add(item: "-vf");
        args.Add(item: filter);
    }

    /// <summary>
    /// Encoder-level device selector flag for encoders that accept one.
    /// NVENC uses <c>-gpu N</c>; other encoder families derive the device
    /// from <c>-init_hw_device</c> or leave selection to driver defaults.
    /// </summary>
    private static void AddEncoderDeviceSelector(List<string> args, CalibrationTarget target)
    {
        if (target.Device is null)
            return;

        if (
            target.Encoder.RequiredVendor == GpuVendor.Nvidia
            && target.Encoder.FfmpegName.Contains(value: "_nvenc", comparisonType: StringComparison.OrdinalIgnoreCase)
        )
        {
            args.Add(item: "-gpu");
            args.Add(item: target.VendorIndex.ToString(provider: CultureInfo.InvariantCulture));
        }
    }

    internal static string TruncateStderr(string stderr)
    {
        if (string.IsNullOrEmpty(value: stderr))
            return "<empty>";
        const int maxLen = 500;
        return stderr.Length > maxLen ? stderr[^maxLen..] : stderr;
    }
}
