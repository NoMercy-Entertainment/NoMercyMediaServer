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

using Microsoft.Extensions.Options;
using NoMercy.Encoder.Analysis;
using NoMercy.Encoder.BuildingBlocks;
using NoMercy.Encoder.Commands;
using NoMercy.Encoder.Composition;
using NoMercy.Encoder.Execution;
using NoMercy.Storage;

namespace NoMercy.Encoder.PostProcess;

/// <inheritdoc />
public class SpriteSheetRefresher(
    IMediaAnalyzer analyzer,
    IFfmpegExecutor executor,
    IOptions<EncoderOptions> options
) : ISpriteSheetRefresher
{
    public async Task<string?> RefreshAsync(
        IStorage storage,
        string mediaFolder,
        int tileWidth,
        int intervalSeconds,
        CancellationToken ct = default
    )
    {
        string? source = FindPlayableSource(storage, mediaFolder);
        if (source is null)
            return null;

        MediaInfo media = await analyzer.AnalyzeAsync(storage.GetFullPath(source), ct);
        if (media.VideoStreams.Count == 0)
            return null;

        int tileHeight = TileHeightFor(tileWidth, media.VideoStreams[0]);
        string sheetName = $"thumbs_{tileWidth}x{tileHeight}.webp";
        string vttName = $"thumbs_{tileWidth}x{tileHeight}.vtt";

        // The encoded rendition is already SDR, so no tone-map chain: this is the
        // one sprite path that can say that for certain, which is why it reads
        // the output instead of hunting down a source it may no longer have.
        FfmpegCommand command = new FfmpegCommandBuilder()
            .WithGlobalOptions(new(ProgressPipe: false, Overwrite: true))
            .AddInput(new(storage.GetFullPath(source)))
            .AddOutput(
                new(
                    FilePath: sheetName,
                    MapStreams: ["0:v:0"],
                    ExtraFlags: new()
                    {
                        ["-vf"] = ThumbnailFilterResolver.Resolve(
                            intervalSeconds,
                            tileWidth,
                            sourceIsHdr: false,
                            tonemapChain: null
                        ),
                        ["-f"] = "spritevtt",
                        ["-vtt_filename"] = vttName,
                    }
                )
            )
            .Build(options.Value.FfmpegPath, storage.GetFullPath(mediaFolder));

        ExecutionResult result = await executor.ExecuteAsync(command, media.Duration, ct: ct);
        if (!result.Success)
            return null;

        RemoveSupersededSheets(storage, mediaFolder, keep: sheetName);
        return sheetName;
    }

    /// <summary>
    /// Height that preserves the encoded picture's own shape, rounded to an even
    /// number because the encoders downstream of this reject odd dimensions.
    /// Mirrors <c>ThumbnailPlanBuilder</c> so a refreshed sheet is dimensioned
    /// exactly as a freshly encoded one would be.
    /// </summary>
    internal static int TileHeightFor(int tileWidth, VideoStreamInfo video) =>
        (int)(2 * Math.Round((double)tileWidth * video.Height / video.Width / 2));

    /// <summary>
    /// A rendition playlist to sample frames from. The master playlist sits at
    /// the folder root and names the renditions, but ffmpeg reading the master
    /// picks a variant by its own rules; addressing a rendition directly keeps
    /// the frames tied to a known picture size, which is what the tile height is
    /// derived from. Highest rendition wins — the sheet is downscaled from it
    /// either way, and scaling down from the best copy is the sharper result.
    /// </summary>
    private static string? FindPlayableSource(IStorage storage, string mediaFolder)
    {
        List<string> renditions = [];

        foreach (StorageEntry entry in storage.List(mediaFolder, null, recursive: false))
        {
            if (!entry.IsDirectory)
                continue;

            string name = storage.GetName(entry.Path);
            if (!name.StartsWith("video_", StringComparison.OrdinalIgnoreCase))
                continue;

            foreach (StorageEntry inner in storage.List(entry.Path, null, recursive: false))
            {
                if (
                    !inner.IsDirectory
                    && storage
                        .GetName(inner.Path)
                        .EndsWith(".m3u8", StringComparison.OrdinalIgnoreCase)
                )
                    renditions.Add(
                        storage.CombinePath(mediaFolder, $"{name}/{storage.GetName(inner.Path)}")
                    );
            }
        }

        return renditions
            .OrderByDescending(path => path, StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault();
    }

    /// <summary>
    /// Drops every sheet and cue file except the one just written. A stale pair
    /// left beside the new one is not harmless: the scan registers whatever
    /// sprite it finds, so the old sheet can win and the upgrade goes unnoticed.
    /// </summary>
    private static void RemoveSupersededSheets(IStorage storage, string mediaFolder, string keep)
    {
        string keepStem = Path.GetFileNameWithoutExtension(keep);

        foreach (StorageEntry entry in storage.List(mediaFolder, null, recursive: false))
        {
            if (entry.IsDirectory)
                continue;

            string name = storage.GetName(entry.Path);
            string stem = Path.GetFileNameWithoutExtension(name);

            if (
                stem.Equals(keepStem, StringComparison.OrdinalIgnoreCase)
                || SpriteSheet.ReadTileWidth($"{stem}.webp") is null
            )
                continue;

            storage.Delete(entry.Path);
        }
    }
}
