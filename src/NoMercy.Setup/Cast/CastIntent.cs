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

using Newtonsoft.Json;

namespace NoMercy.Setup.Cast;

/// <summary>
/// What the receiver should do on boot. Discriminated by <see cref="Type"/>.
/// Mirrors the TypeScript union in §8.2 of the cast-receiver-leanback spec.
/// </summary>
public class CastIntent
{
    [JsonProperty("type")]
    public string Type { get; set; } = "idle";

    /// <summary>Movie / tv — only set when <see cref="Type"/> = "play_video".</summary>
    [JsonProperty("media_type", NullValueHandling = NullValueHandling.Ignore)]
    public string? MediaType { get; set; }

    /// <summary>Movie / tv ID — only set when <see cref="Type"/> = "play_video".</summary>
    [JsonProperty("media_id", NullValueHandling = NullValueHandling.Ignore)]
    public string? MediaId { get; set; }

    /// <summary>Music list type — only set when <see cref="Type"/> = "play_music".</summary>
    [JsonProperty("list_type", NullValueHandling = NullValueHandling.Ignore)]
    public string? ListType { get; set; }

    /// <summary>Music list ID — only set when <see cref="Type"/> = "play_music".</summary>
    [JsonProperty("list_id", NullValueHandling = NullValueHandling.Ignore)]
    public string? ListId { get; set; }

    /// <summary>Optional starting track within a list — only set when <see cref="Type"/> = "play_music".</summary>
    [JsonProperty("track_id", NullValueHandling = NullValueHandling.Ignore)]
    public string? TrackId { get; set; }

    /// <summary>Resume position in seconds (video) or seconds (music). Optional.</summary>
    [JsonProperty("resume_at", NullValueHandling = NullValueHandling.Ignore)]
    public int? ResumeAt { get; set; }

    /// <summary>Vue Router target — only set when <see cref="Type"/> = "navigate".</summary>
    [JsonProperty("route", NullValueHandling = NullValueHandling.Ignore)]
    public string? Route { get; set; }

    public static CastIntent Idle() => new() { Type = "idle" };

    public static CastIntent PlayVideo(string mediaType, string mediaId, int? resumeAt = null) =>
        new()
        {
            Type = "play_video",
            MediaType = mediaType,
            MediaId = mediaId,
            ResumeAt = resumeAt,
        };

    public static CastIntent PlayMusic(
        string listType,
        string listId,
        string? trackId = null,
        int? resumeAt = null
    ) =>
        new()
        {
            Type = "play_music",
            ListType = listType,
            ListId = listId,
            TrackId = trackId,
            ResumeAt = resumeAt,
        };

    public static CastIntent Navigate(string route) => new() { Type = "navigate", Route = route };
}
