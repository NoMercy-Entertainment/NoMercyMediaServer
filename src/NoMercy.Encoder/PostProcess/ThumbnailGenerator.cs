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

using System.Text;
using NoMercy.Encoder.BuildingBlocks;
using NoMercy.Encoder.Commands;
using NoMercy.Encoder.Output;
using NoMercy.Storage;

namespace NoMercy.Encoder.PostProcess;

public class ThumbnailGenerator(IStorage storage) : IThumbnailGenerator
{
    public FfmpegCommand BuildCaptureCommand(
        string ffmpegPath,
        string inputPath,
        string outputDirectory,
        ThumbnailOutputPlan plan,
        TimeSpan duration
    )
    {
        string thumbDir = Path.Combine(outputDirectory, $"thumbs_{plan.Width}");

        FfmpegCommand cmd = new FfmpegCommandBuilder()
            .WithGlobalOptions(new(ProgressPipe: false, Overwrite: true))
            .AddInput(new(inputPath))
            .AddOutput(
                new(
                    Path.Combine(thumbDir, "thumb_%04d.jpg"),
                    MapStreams: ["0:v:0"],
                    ExtraFlags: new()
                    {
                        ["-vf"] = $"fps=1/{plan.IntervalSeconds},scale={plan.Width}:-2",
                        ["-q:v"] = "5",
                    }
                )
            )
            .Build(ffmpegPath);

        return cmd;
    }

    public FfmpegCommand BuildSpriteCommand(
        string ffmpegPath,
        string outputDirectory,
        ThumbnailOutputPlan plan,
        int imageCount
    )
    {
        string thumbDir = Path.Combine(outputDirectory, $"thumbs_{plan.Width}");
        (int gridWidth, int gridHeight) = ComputeGrid(imageCount);
        // Sprite filename must match what WriteVttCueFileAsync embeds in the
        // VTT cues (thumbs_WIDTHxHEIGHT.webp). Without the height suffix the
        // sprite lands on disk as `thumbs_320.webp` while the player follows
        // the VTT and asks for `thumbs_320x178.webp` — 404 every hover.
        string spriteFile = Path.Combine(
            outputDirectory,
            $"thumbs_{plan.Width}x{plan.Height}.webp"
        );

        FfmpegCommand cmd = new FfmpegCommandBuilder()
            .WithGlobalOptions(new(ProgressPipe: false, Overwrite: true))
            .AddInput(new(Path.Combine(thumbDir, "thumb_%04d.jpg")))
            .AddOutput(
                new(
                    spriteFile,
                    ExtraFlags: new() { ["-filter_complex"] = $"tile={gridWidth}x{gridHeight}" }
                )
            )
            .Build(ffmpegPath);

        return cmd;
    }

    public async Task WriteVttCueFileAsync(
        string outputDirectory,
        ThumbnailOutputPlan plan,
        int imageCount,
        TimeSpan duration,
        CancellationToken ct
    )
    {
        (int gridWidth, _) = ComputeGrid(imageCount);
        string spriteFilename = $"thumbs_{plan.Width}x{plan.Height}.webp";
        string vttFile = Path.Combine(outputDirectory, $"thumbs_{plan.Width}x{plan.Height}.vtt");

        int thumbHeight = plan.Height;

        StringBuilder sb = new();
        sb.AppendLine("WEBVTT");
        sb.AppendLine();

        for (int i = 0; i < imageCount; i++)
        {
            TimeSpan start = TimeSpan.FromSeconds(i * plan.IntervalSeconds);
            TimeSpan end = TimeSpan.FromSeconds(
                Math.Min((i + 1) * plan.IntervalSeconds, duration.TotalSeconds)
            );

            int col = i % gridWidth;
            int row = i / gridWidth;
            int x = col * plan.Width;
            int y = row * thumbHeight;

            sb.AppendLine($"{i + 1}");
            sb.AppendLine($"{FormatVttTime(start)} --> {FormatVttTime(end)}");
            sb.AppendLine($"{spriteFilename}#xywh={x},{y},{plan.Width},{thumbHeight}");
            sb.AppendLine();
        }

        await storage.WriteAsync(vttFile, Encoding.UTF8.GetBytes(sb.ToString()), ct);
    }

    public void CleanupIndividualThumbnails(string outputDirectory, ThumbnailOutputPlan plan)
    {
        string thumbDir = Path.Combine(outputDirectory, $"thumbs_{plan.Width}");

        if (storage.Exists(thumbDir))
            storage.DeleteDirectory(thumbDir, true);
    }

    public static (int Width, int Height) ComputeGrid(int imageCount)
    {
        int gridWidth = (int)Math.Ceiling(Math.Sqrt(imageCount));
        int gridHeight = (int)Math.Ceiling((double)imageCount / gridWidth);

        return (gridWidth, gridHeight);
    }

    private static string FormatVttTime(TimeSpan ts) =>
        $"{(int)ts.TotalHours:D2}:{ts.Minutes:D2}:{ts.Seconds:D2}.{ts.Milliseconds:D3}";
}
