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

using System.Diagnostics;
using Newtonsoft.Json.Linq;

namespace NoMercy.Tests.Encoder.Fidelity;

/// <summary>
/// A thin, structured ffprobe wrapper for the <see cref="EncodeFidelityOracle"/>.
/// Runs a single ffprobe invocation that returns streams + format + chapters as
/// JSON, plus a second pass that pulls the FIRST frame's side-data list (where
/// HDR mastering-display / content-light-level / Dolby-Vision RPU records live —
/// those are per-frame SEI, not stream-level fields).
///
/// The parsed shape is deliberately dynamic (<see cref="JObject"/>): ffprobe's
/// field set varies by codec/container and the oracle only ever reads named
/// fields, so a rigid POCO would fight the tool for no gain.
/// </summary>
public sealed class ProbedMedia
{
    public required string Path { get; init; }
    public required IReadOnlyList<JObject> Streams { get; init; }
    public required JObject Format { get; init; }
    public required IReadOnlyList<JObject> Chapters { get; init; }

    /// <summary>First-frame side-data blocks (mastering display, CLL, DOVI, display matrix).</summary>
    public required IReadOnlyList<JObject> FirstFrameSideData { get; init; }

    public IEnumerable<JObject> StreamsOfType(string codecType) =>
        Streams.Where(predicate: s => (string?)s[propertyName: "codec_type"] == codecType);

    public IEnumerable<JObject> VideoStreams => StreamsOfType(codecType: "video");
    public IEnumerable<JObject> AudioStreams => StreamsOfType(codecType: "audio");
    public IEnumerable<JObject> SubtitleStreams => StreamsOfType(codecType: "subtitle");

    /// <summary>The primary (first) video stream, or null for audio-only media.</summary>
    public JObject? PrimaryVideo => VideoStreams.FirstOrDefault();

    public bool HasSideData(string sideDataType) =>
        FirstFrameSideData.Any(predicate: sd =>
            string.Equals(
                a: (string?)sd[propertyName: "side_data_type"],
                b: sideDataType,
                comparisonType: StringComparison.OrdinalIgnoreCase
            )
        );

    public JObject? SideData(string sideDataType) =>
        FirstFrameSideData.FirstOrDefault(predicate: sd =>
            string.Equals(
                a: (string?)sd[propertyName: "side_data_type"],
                b: sideDataType,
                comparisonType: StringComparison.OrdinalIgnoreCase
            )
        );
}

public static class FfprobeRunner
{
    /// <summary>
    /// Probe a media file (or an HLS variant playlist) into a
    /// <see cref="ProbedMedia"/>. Throws on a non-zero ffprobe exit so a broken
    /// probe never masquerades as "no defects".
    /// </summary>
    public static ProbedMedia Probe(string ffprobePath, string filePath)
    {
        JObject root = RunJson(
            ffprobePath: ffprobePath,
            args: ["-show_streams", "-show_format", "-show_chapters", "-i", filePath]
        );

        // First-frame side data: select the primary video stream, read one frame.
        // spans HLS m3u8 inputs fine (ffprobe follows the playlist).
        List<JObject> sideData = [];
        try
        {
            JObject frames = RunJson(
                ffprobePath: ffprobePath,
                args:
                [
                    "-select_streams",
                    "v:0",
                    "-read_intervals",
                    "%+#1",
                    "-show_frames",
                    "-show_entries",
                    "frame=side_data_list:frame_side_data=side_data_type,red_x,red_y,green_x,green_y,blue_x,blue_y,white_point_x,white_point_y,min_luminance,max_luminance,max_content,max_average,rotation,dv_profile,rpu_present_flag,dv_bl_signal_compatibility_id",
                    "-i",
                    filePath,
                ]
            );
            JArray? frameArr = frames[propertyName: "frames"] as JArray;
            JObject? firstFrame = frameArr?.OfType<JObject>().FirstOrDefault();
            if (firstFrame?[propertyName: "side_data_list"] is JArray sd)
                sideData = sd.OfType<JObject>().ToList();
        }
        catch
        {
            // Audio-only / no video: no side data. Not a probe failure.
        }

        return new ProbedMedia
        {
            Path = filePath,
            Streams = (root[propertyName: "streams"] as JArray)?.OfType<JObject>().ToList() ?? [],
            Format = root[propertyName: "format"] as JObject ?? new JObject(),
            Chapters = (root[propertyName: "chapters"] as JArray)?.OfType<JObject>().ToList() ?? [],
            FirstFrameSideData = sideData,
        };
    }

    private static JObject RunJson(string ffprobePath, IEnumerable<string> args)
    {
        ProcessStartInfo psi = new()
        {
            FileName = ffprobePath,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        psi.ArgumentList.Add(item: "-hide_banner");
        psi.ArgumentList.Add(item: "-loglevel");
        psi.ArgumentList.Add(item: "error");
        psi.ArgumentList.Add(item: "-print_format");
        psi.ArgumentList.Add(item: "json");
        foreach (string arg in args)
            psi.ArgumentList.Add(item: arg);

        using Process process =
            Process.Start(startInfo: psi) ?? throw new InvalidOperationException(message: "ffprobe failed to start");
        Task<string> stdoutTask = process.StandardOutput.ReadToEndAsync();
        Task<string> stderrTask = process.StandardError.ReadToEndAsync();
        process.WaitForExit(milliseconds: 60_000);
        string stdout = stdoutTask.GetAwaiter().GetResult();
        string stderr = stderrTask.GetAwaiter().GetResult();

        if (process.ExitCode != 0)
            throw new InvalidOperationException(
                message: $"ffprobe exited {process.ExitCode} for {string.Join(separator: ' ', values: args)}: {stderr}"
            );

        return JObject.Parse(json: string.IsNullOrWhiteSpace(value: stdout) ? "{}" : stdout);
    }
}
