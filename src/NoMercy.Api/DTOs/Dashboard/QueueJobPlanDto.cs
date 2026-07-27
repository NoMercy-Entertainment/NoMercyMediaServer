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

namespace NoMercy.Api.DTOs.Dashboard;

/// <summary>
/// What a queued encode is going to do, read from the preset it will run with.
///
/// <para>A waiting card has no progress to show and said only that it was
/// queued, which is the one thing the operator already knew. This is the rest
/// of the answer: the renditions, tracks and subtitles the job will produce.</para>
///
/// <para>Every field is a codec name, a number or a language code. No sentence
/// is composed here, because a sentence built on the server cannot be
/// translated on the client.</para>
/// </summary>
public class QueueJobPlanDto
{
    /// <summary><c>Container</c> by name: HlsFmp4, Mkv, Mp4, …</summary>
    [JsonProperty("container")]
    public string Container { get; set; } = string.Empty;

    [JsonProperty("video")]
    public PlannedVideoDto[] Video { get; set; } = [];

    /// <summary>
    /// Whether <see cref="Video"/> is exactly what gets produced
    /// (<c>fixed</c>), or a ceiling the source can fall short of
    /// (<c>capped</c> — an auto ladder that never upscales drops every rung
    /// above the source's own height).
    /// </summary>
    [JsonProperty("video_mode")]
    public string VideoMode { get; set; } = FixedVideo;

    [JsonProperty("audio")]
    public PlannedAudioDto[] Audio { get; set; } = [];

    [JsonProperty("subtitles")]
    public PlannedSubtitleDto[] Subtitles { get; set; } = [];

    /// <summary><see cref="Video"/> is the exact rendition list.</summary>
    public const string FixedVideo = "fixed";

    /// <summary><see cref="Video"/> is a ceiling; the source decides how much of it is used.</summary>
    public const string CappedVideo = "capped";
}
