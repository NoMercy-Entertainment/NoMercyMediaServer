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
/// Builds FFmpeg -metadata / -metadata:s:* / -disposition:* / -attach args
/// from DB metadata rows. Pure function — no DI dependencies.
/// </summary>
public interface IMetadataInjector
{
    IReadOnlyList<string> BuildArgs(MetadataInjectionContext ctx);
}

// ---------------------------------------------------------------------------
// Context + supporting records
// ---------------------------------------------------------------------------

/// <summary>
/// All information needed to emit metadata args for one encode command.
/// </summary>
public record MetadataInjectionContext(
    MediaItemRef Media,
    IReadOnlyList<TrackMetadata> Tracks,
    IReadOnlyList<string> AttachmentPaths
);

/// <summary>Per-stream metadata supplied from the DB row.</summary>
public record TrackMetadata(
    int OutputIndex,
    /// <summary>"video" | "audio" | "subtitle"</summary>
    string Kind,
    string? Language,
    string? Title,
    bool IsDefault,
    bool IsForced
);

// ---------------------------------------------------------------------------
// MediaItemRef hierarchy — base lives in Naming; episode variant adds episode
// fields so the injector never has to reach into a DB context itself.
// ---------------------------------------------------------------------------

/// <summary>Movie-specific media reference with optional description.</summary>
public record MovieMediaRef(
    MediaType Type,
    long Id,
    string Title,
    int? Year,
    string? Description = null
) : MediaItemRef(Type, Id, Title, Year);

/// <summary>Episode-specific media reference carrying show/season/episode fields.</summary>
public record EpisodeMediaRef(
    MediaType Type,
    long Id,
    string Title,
    int? Year,
    string ShowTitle,
    int SeasonNumber,
    int EpisodeNumber,
    // The show's own TMDB id (Tv.Id), distinct from Id above (the episode's
    // TMDB id). Feeds BlueprintIdentity.Show.TmdbId. Defaults to 0 so
    // existing named-argument callers keep compiling.
    long ShowTmdbId = 0,
    string? Description = null
) : MediaItemRef(Type, Id, Title, Year);
