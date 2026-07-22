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

using NoMercy.Encoder.Naming;

namespace NoMercy.Encoder.Metadata;

/// <summary>
/// Builds FFmpeg -metadata / per-stream -metadata:s:* / -disposition:* /
/// -attach arguments from DB metadata. Each flag is emitted as two separate
/// list elements (flag, value) so the process invoker handles quoting.
/// </summary>
public class MetadataInjector : IMetadataInjector
{
    public IReadOnlyList<string> BuildArgs(MetadataInjectionContext ctx)
    {
        List<string> args = [];

        EmitGlobalMetadata(args: args, media: ctx.Media);
        EmitTrackMetadata(args: args, tracks: ctx.Tracks);
        EmitAttachments(args: args, attachmentPaths: ctx.AttachmentPaths);

        return args;
    }

    // -----------------------------------------------------------------------
    // Global (container-level) metadata
    // -----------------------------------------------------------------------

    private static void EmitGlobalMetadata(List<string> args, MediaItemRef media)
    {
        AddMeta(args: args, key: "title", value: media.Title);

        if (media.Year.HasValue)
            AddMeta(args: args, key: "year", value: media.Year.Value.ToString());

        if (media is MovieMediaRef { Description: not null } movie)
            AddMeta(args: args, key: "description", value: movie.Description);

        if (media is EpisodeMediaRef episode)
        {
            AddMeta(args: args, key: "show", value: episode.ShowTitle);
            AddMeta(args: args, key: "season_number", value: episode.SeasonNumber.ToString());
            AddMeta(args: args, key: "episode_id", value: episode.EpisodeNumber.ToString());

            if (episode.Description is not null)
                AddMeta(args: args, key: "description", value: episode.Description);
        }
    }

    // -----------------------------------------------------------------------
    // Per-stream metadata and disposition
    // -----------------------------------------------------------------------

    private static void EmitTrackMetadata(List<string> args, IReadOnlyList<TrackMetadata> tracks)
    {
        foreach (TrackMetadata track in tracks)
        {
            string spec = StreamSpec(kind: track.Kind, index: track.OutputIndex);

            if (track.Language is not null)
                AddStreamMeta(args: args, streamSpec: spec, key: "language", value: track.Language);

            if (track.Title is not null)
                AddStreamMeta(args: args, streamSpec: spec, key: "title", value: track.Title);

            EmitDisposition(args: args, track: track);
        }
    }

    private static void EmitDisposition(List<string> args, TrackMetadata track)
    {
        if (track is { IsDefault: false, IsForced: false })
            return;

        string dispSpec = DispositionSpec(kind: track.Kind, index: track.OutputIndex);
        string value = (track.IsDefault, track.IsForced) switch
        {
            (true, true) => "default+forced",
            (true, false) => "default",
            (false, true) => "forced",
            _ => string.Empty,
        };

        args.Add(item: dispSpec);
        args.Add(item: value);
    }

    // -----------------------------------------------------------------------
    // Attachment (cover art etc.)
    // -----------------------------------------------------------------------

    private static void EmitAttachments(List<string> args, IReadOnlyList<string> attachmentPaths)
    {
        // FFmpeg attachment streams are numbered from 0. Each -attach adds one
        // stream; -metadata:s:t:N tags that stream. We track a local counter
        // so the :N index stays correct regardless of how many video/audio
        // streams precede the attachments.
        int attachIndex = 0;

        foreach (string path in attachmentPaths)
        {
            args.Add(item: "-attach");
            args.Add(item: path);

            string tagSpec = $"-metadata:s:t:{attachIndex}";
            string mime = MimeTypeForExtension(extension: Path.GetExtension(path: path));
            args.Add(item: tagSpec);
            args.Add(item: $"mimetype={mime}");

            string filename = Path.GetFileName(path: path);
            args.Add(item: tagSpec);
            args.Add(item: $"filename={filename}");

            attachIndex++;
        }
    }

    // -----------------------------------------------------------------------
    // Helpers
    // -----------------------------------------------------------------------

    private static void AddMeta(List<string> args, string key, string value)
    {
        args.Add(item: "-metadata");
        args.Add(item: $"{key}={value}");
    }

    private static void AddStreamMeta(
        List<string> args,
        string streamSpec,
        string key,
        string value
    )
    {
        args.Add(item: $"-metadata:{streamSpec}");
        args.Add(item: $"{key}={value}");
    }

    /// <summary>Returns the FFmpeg stream specifier segment for -metadata:s:* and -disposition:*.</summary>
    private static string StreamSpec(string kind, int index) =>
        kind switch
        {
            "video" => $"s:v:{index}",
            "audio" => $"s:a:{index}",
            "subtitle" => $"s:s:{index}",
            _ => $"s:a:{index}",
        };

    /// <summary>Returns the FFmpeg -disposition flag key for the given stream kind and index.</summary>
    private static string DispositionSpec(string kind, int index) =>
        kind switch
        {
            "video" => $"-disposition:v:{index}",
            "audio" => $"-disposition:a:{index}",
            "subtitle" => $"-disposition:s:{index}",
            _ => $"-disposition:a:{index}",
        };

    private static string MimeTypeForExtension(string extension) =>
        extension.ToLowerInvariant() switch
        {
            ".jpg" or ".jpeg" => "image/jpeg",
            ".png" => "image/png",
            ".webp" => "image/webp",
            _ => "application/octet-stream",
        };
}
