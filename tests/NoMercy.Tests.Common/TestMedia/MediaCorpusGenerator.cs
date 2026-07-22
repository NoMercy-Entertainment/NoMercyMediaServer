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

namespace NoMercy.Tests.Common.TestMedia;

/// <summary>
/// Materializes <see cref="MediaCorpus"/> entries into tiny real media files
/// with ffmpeg. Each clip is a 1-second lavfi source encoded into the entry's
/// container + codecs, so the files are real (probe-able, encode-able) but
/// trivially small and contain no copyrighted content. Shared by the
/// filename-parser tests (which only need the names on disk) and the encoder
/// tests (which feed the files through the real pipeline).
/// </summary>
public static class MediaCorpusGenerator
{
    /// <summary>
    /// Generates every corpus entry under <paramref name="rootDir"/> using the
    /// ffmpeg at <paramref name="ffmpegPath"/>. Existing files are left as-is so
    /// repeated calls are cheap. Returns the absolute paths created.
    /// </summary>
    public static IReadOnlyList<string> GenerateAll(string rootDir, string ffmpegPath)
    {
        List<string> created = [];
        foreach (MediaCorpusEntry entry in MediaCorpus.Entries)
            created.Add(Generate(entry, rootDir, ffmpegPath));
        return created;
    }

    public static string Generate(MediaCorpusEntry entry, string rootDir, string ffmpegPath)
    {
        string outPath = Path.Combine(
            rootDir,
            entry.RelativePath.Replace('/', Path.DirectorySeparatorChar)
        );
        Directory.CreateDirectory(Path.GetDirectoryName(outPath)!);
        if (File.Exists(outPath))
            return outPath;

        List<string> args = BuildFfmpegArgs(entry, outPath);

        ProcessStartInfo psi = new()
        {
            FileName = ffmpegPath,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        foreach (string arg in args)
            psi.ArgumentList.Add(arg);

        using Process process = Process.Start(psi)!;
        string stderr = process.StandardError.ReadToEnd();
        process.StandardOutput.ReadToEnd();
        process.WaitForExit();
        if (process.ExitCode != 0)
            throw new InvalidOperationException(
                $"ffmpeg failed generating '{entry.RelativePath}': {stderr}"
            );

        return outPath;
    }

    private static List<string> BuildFfmpegArgs(MediaCorpusEntry entry, string outPath)
    {
        List<string> args = ["-y"];

        // Video source — short, deterministic. HDR entries get PQ/BT.2020 tags
        // so the analyzer classifies them as HDR.
        args.AddRange([
            "-f",
            "lavfi",
            "-i",
            $"testsrc2=size={entry.Resolution}:rate=24:duration=1",
        ]);

        // Audio source(s) — one or two tracks for dual-audio releases.
        int audioTracks = entry.DualAudio ? 2 : 1;
        for (int i = 0; i < audioTracks; i++)
            args.AddRange(["-f", "lavfi", "-i", $"sine=frequency={220 + (i * 220)}:duration=1"]);

        if (entry.Hdr)
            args.AddRange([
                "-vf",
                "format=yuv420p10le,setparams=color_primaries=bt2020:color_trc=smpte2084:colorspace=bt2020nc",
            ]);

        // Video codec.
        args.AddRange(
            entry.VideoCodec switch
            {
                MediaVideoCodec.H264 =>
                [
                    "-c:v",
                    "libx264",
                    "-preset",
                    "ultrafast",
                    "-pix_fmt",
                    entry.Hdr ? "yuv420p10le" : "yuv420p",
                ],
                MediaVideoCodec.H265 => ["-c:v", "libx265", "-preset", "ultrafast"],
                MediaVideoCodec.Av1 => ["-c:v", "libaom-av1", "-cpu-used", "8", "-b:v", "200k"],
                _ => throw new ArgumentOutOfRangeException(nameof(entry)),
            }
        );
        if (entry is { Hdr: true, VideoCodec: MediaVideoCodec.H265 })
            args.AddRange([
                "-x265-params",
                "hdr10=1:colorprim=bt2020:transfer=smpte2084:colormatrix=bt2020nc",
                "-color_primaries",
                "bt2020",
                "-color_trc",
                "smpte2084",
                "-colorspace",
                "bt2020nc",
            ]);

        // Audio codec.
        args.AddRange(
            entry.AudioCodec switch
            {
                MediaAudioCodec.Aac => ["-c:a", "aac", "-b:a", "64k"],
                MediaAudioCodec.Ac3 => ["-c:a", "ac3", "-b:a", "128k"],
                MediaAudioCodec.Flac => ["-c:a", "flac"],
                _ => throw new ArgumentOutOfRangeException(nameof(entry)),
            }
        );

        // Map every input so both audio tracks land in the output.
        args.AddRange(["-map", "0:v:0"]);
        for (int i = 0; i < audioTracks; i++)
            args.AddRange(["-map", $"{i + 1}:a:0"]);

        // Container is implied by the extension; ffmpeg picks the muxer.
        args.Add(outPath);
        return args;
    }
}
