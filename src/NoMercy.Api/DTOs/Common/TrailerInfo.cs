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

using Mono.Nat;
using Newtonsoft.Json;
using NoMercy.Providers.Helpers;

namespace NoMercy.Api.DTOs.Common;

public class TrailerInfo
{
    [JsonProperty(propertyName: "id")]
    public string? Id { get; set; }

    [JsonProperty(propertyName: "title")]
    public string? Title { get; set; }

    [JsonProperty(propertyName: "formats")]
    public Format[] Formats { get; set; } = [];

    [JsonProperty(propertyName: "thumbnails")]
    public Thumbnail[] Thumbnails { get; set; } = [];

    [JsonProperty(propertyName: "thumbnail")]
    public Uri? Thumbnail { get; set; }

    [JsonProperty(propertyName: "description")]
    public string? Description { get; set; }

    [JsonProperty(propertyName: "channel_id")]
    public string? ChannelId { get; set; }

    [JsonProperty(propertyName: "channel_url")]
    public Uri? ChannelUrl { get; set; }

    [JsonProperty(propertyName: "duration")]
    public long Duration { get; set; }

    [JsonProperty(propertyName: "view_count")]
    public long ViewCount { get; set; }

    [JsonProperty(propertyName: "average_rating")]
    public object? AverageRating { get; set; }

    [JsonProperty(propertyName: "age_limit")]
    public long AgeLimit { get; set; }

    [JsonProperty(propertyName: "webpage_url")]
    public Uri? WebpageUrl { get; set; }

    [JsonProperty(propertyName: "categories")]
    public string[] Categories { get; set; } = [];

    [JsonProperty(propertyName: "tags")]
    public string[] Tags { get; set; } = [];

    [JsonProperty(propertyName: "playable_in_embed")]
    public bool PlayableInEmbed { get; set; }

    [JsonProperty(propertyName: "live_status")]
    public string? LiveStatus { get; set; }

    [JsonProperty(propertyName: "release_timestamp")]
    public object? ReleaseTimestamp { get; set; }

    [JsonProperty(propertyName: "_format_sort_fields")]
    public string[] FormatSortFields { get; set; } = [];

    [JsonProperty(propertyName: "automatic_captions")]
    public Dictionary<string, Caption> AutomaticCaptions { get; set; } = new();

    [JsonProperty(propertyName: "subtitles")]
    public Dictionary<string, Caption[]> Subtitles { get; set; } = new();

    [JsonProperty(propertyName: "comment_count")]
    public long CommentCount { get; set; }

    [JsonProperty(propertyName: "chapters")]
    public object? Chapters { get; set; }

    [JsonProperty(propertyName: "heatmap")]
    public Heatmap[] Heatmap { get; set; } = [];

    [JsonProperty(propertyName: "like_count")]
    public long LikeCount { get; set; }

    [JsonProperty(propertyName: "channel")]
    public string? Channel { get; set; }

    [JsonProperty(propertyName: "channel_follower_count")]
    public long ChannelFollowerCount { get; set; }

    [JsonProperty(propertyName: "channel_is_verified")]
    public bool ChannelIsVerified { get; set; }

    [JsonProperty(propertyName: "uploader")]
    public string? Uploader { get; set; }

    [JsonProperty(propertyName: "uploader_id")]
    public string? UploaderId { get; set; }

    [JsonProperty(propertyName: "uploader_url")]
    public Uri? UploaderUrl { get; set; }

    [JsonProperty(propertyName: "upload_date")]
    [JsonConverter(converterType: typeof(ParseStringConverter))]
    public long UploadDate { get; set; }

    [JsonProperty(propertyName: "timestamp")]
    public long Timestamp { get; set; }

    [JsonProperty(propertyName: "availability")]
    public string? Availability { get; set; }

    [JsonProperty(propertyName: "original_url")]
    public string? OriginalUrl { get; set; }

    [JsonProperty(propertyName: "webpage_url_basename")]
    public string? WebpageUrlBasename { get; set; }

    [JsonProperty(propertyName: "webpage_url_domain")]
    public string? WebpageUrlDomain { get; set; }

    [JsonProperty(propertyName: "extractor")]
    public string? Extractor { get; set; }

    [JsonProperty(propertyName: "extractor_key")]
    public string? ExtractorKey { get; set; }

    [JsonProperty(propertyName: "playlist")]
    public object? Playlist { get; set; }

    [JsonProperty(propertyName: "playlist_index")]
    public object? PlaylistIndex { get; set; }

    [JsonProperty(propertyName: "display_id")]
    public string? DisplayId { get; set; }

    [JsonProperty(propertyName: "fulltitle")]
    public string? Fulltitle { get; set; }

    [JsonProperty(propertyName: "duration_string")]
    public string? DurationString { get; set; }

    [JsonProperty(propertyName: "release_year")]
    public object? ReleaseYear { get; set; }

    [JsonProperty(propertyName: "is_live")]
    public bool IsLive { get; set; }

    [JsonProperty(propertyName: "was_live")]
    public bool WasLive { get; set; }

    [JsonProperty(propertyName: "requested_subtitles")]
    public object? RequestedSubtitles { get; set; }

    [JsonProperty(propertyName: "_has_drm")]
    public object? HasDrm { get; set; }

    [JsonProperty(propertyName: "epoch")]
    public long Epoch { get; set; }

    [JsonProperty(propertyName: "requested_formats")]
    public Format[] RequestedFormats { get; set; } = [];

    [JsonProperty(propertyName: "format")]
    public string? Format { get; set; }

    [JsonProperty(propertyName: "format_id")]
    public string? FormatId { get; set; }

    [JsonProperty(propertyName: "ext")]
    public string? Ext { get; set; }

    [JsonProperty(propertyName: "protocol")]
    public string? Protocol { get; set; }

    [JsonProperty(propertyName: "language")]
    public string? Language { get; set; }

    [JsonProperty(propertyName: "format_note")]
    public string? FormatNote { get; set; }

    [JsonProperty(propertyName: "filesize_approx")]
    public long FilesizeApprox { get; set; }

    [JsonProperty(propertyName: "tbr")]
    public double Tbr { get; set; }

    [JsonProperty(propertyName: "width")]
    public long Width { get; set; }

    [JsonProperty(propertyName: "height")]
    public long Height { get; set; }

    [JsonProperty(propertyName: "resolution")]
    public string? Resolution { get; set; }

    [JsonProperty(propertyName: "fps")]
    public long Fps { get; set; }

    [JsonProperty(propertyName: "dynamic_range")]
    public string? DynamicRange { get; set; }

    [JsonProperty(propertyName: "vcodec")]
    public string? Vcodec { get; set; }

    [JsonProperty(propertyName: "vbr")]
    public double Vbr { get; set; }

    [JsonProperty(propertyName: "stretched_ratio")]
    public object? StretchedRatio { get; set; }

    [JsonProperty(propertyName: "aspect_ratio")]
    public double AspectRatio { get; set; }

    [JsonProperty(propertyName: "acodec")]
    public string? Acodec { get; set; }

    [JsonProperty(propertyName: "abr")]
    public double Abr { get; set; }

    [JsonProperty(propertyName: "asr")]
    public long Asr { get; set; }

    [JsonProperty(propertyName: "audio_channels")]
    public long AudioChannels { get; set; }

    [JsonProperty(propertyName: "_filename")]
    public string? Filename { get; set; }

    [JsonProperty(propertyName: "filename")]
    public string? TrailerInfoFilename { get; set; }

    [JsonProperty(propertyName: "_type")]
    public string? Type { get; set; }

    [JsonProperty(propertyName: "_version")]
    public Version? Version { get; set; }
}

public class Caption
{
    [JsonProperty(propertyName: "ext")]
    public string? Ext { get; set; }

    [JsonProperty(propertyName: "url")]
    public Uri? Url { get; set; }

    [JsonProperty(propertyName: "name")]
    public string? Name { get; set; }

    [JsonProperty(propertyName: "__yt_dlp_client")]
    public string? YtDlpClient { get; set; }
}

public class Format
{
    [JsonProperty(propertyName: "format_id")]
    public string? FormatId { get; set; }

    [JsonProperty(propertyName: "format_note", NullValueHandling = NullValueHandling.Ignore)]
    public string? FormatNote { get; set; }

    [JsonProperty(propertyName: "ext")]
    public string? Ext { get; set; }

    [JsonProperty(propertyName: "protocol")]
    public Protocol Protocol { get; set; }

    [JsonProperty(propertyName: "acodec", NullValueHandling = NullValueHandling.Ignore)]
    public string? Acodec { get; set; }

    [JsonProperty(propertyName: "vcodec")]
    public string? Vcodec { get; set; }

    [JsonProperty(propertyName: "url")]
    public Uri? Url { get; set; }

    [JsonProperty(propertyName: "width")]
    public long? Width { get; set; }

    [JsonProperty(propertyName: "height")]
    public long? Height { get; set; }

    [JsonProperty(propertyName: "fps")]
    public double? Fps { get; set; }

    [JsonProperty(propertyName: "rows", NullValueHandling = NullValueHandling.Ignore)]
    public long? Rows { get; set; }

    [JsonProperty(propertyName: "columns", NullValueHandling = NullValueHandling.Ignore)]
    public long? Columns { get; set; }

    [JsonProperty(propertyName: "fragments", NullValueHandling = NullValueHandling.Ignore)]
    public Fragment[] Fragments { get; set; } = [];

    [JsonProperty(propertyName: "resolution")]
    public string? Resolution { get; set; }

    [JsonProperty(propertyName: "aspect_ratio")]
    public double? AspectRatio { get; set; }

    [JsonProperty(propertyName: "filesize_approx")]
    public long? FilesizeApprox { get; set; }

    [JsonProperty(propertyName: "http_headers")]
    public HttpHeaders? HttpHeaders { get; set; }

    [JsonProperty(propertyName: "audio_ext")]
    public string? AudioExt { get; set; }

    [JsonProperty(propertyName: "video_ext")]
    public string? VideoExt { get; set; }

    [JsonProperty(propertyName: "vbr")]
    public double Vbr { get; set; }

    [JsonProperty(propertyName: "abr")]
    public double? Abr { get; set; }

    [JsonProperty(propertyName: "tbr")]
    public double? Tbr { get; set; }

    [JsonProperty(propertyName: "format")]
    public string? FormatFormat { get; set; }

    [JsonProperty(propertyName: "format_index")]
    public object? FormatIndex { get; set; }

    [JsonProperty(propertyName: "manifest_url", NullValueHandling = NullValueHandling.Ignore)]
    public Uri? ManifestUrl { get; set; }

    [JsonProperty(propertyName: "language")]
    public object? Language { get; set; }

    [JsonProperty(propertyName: "preference")]
    public object? Preference { get; set; }

    [JsonProperty(propertyName: "quality", NullValueHandling = NullValueHandling.Ignore)]
    public long? Quality { get; set; }

    [JsonProperty(propertyName: "has_drm", NullValueHandling = NullValueHandling.Ignore)]
    public bool? HasDrm { get; set; }

    [JsonProperty(propertyName: "source_preference", NullValueHandling = NullValueHandling.Ignore)]
    public long? SourcePreference { get; set; }

    [JsonProperty(propertyName: "asr")]
    public long? Asr { get; set; }

    [JsonProperty(propertyName: "filesize", NullValueHandling = NullValueHandling.Ignore)]
    public long? Filesize { get; set; }

    [JsonProperty(propertyName: "audio_channels")]
    public long? AudioChannels { get; set; }

    [JsonProperty(propertyName: "language_preference", NullValueHandling = NullValueHandling.Ignore)]
    public long? LanguagePreference { get; set; }

    [JsonProperty(propertyName: "dynamic_range")]
    public string? DynamicRange { get; set; }

    [JsonProperty(propertyName: "container", NullValueHandling = NullValueHandling.Ignore)]
    public string? Container { get; set; }

    [JsonProperty(propertyName: "downloader_options", NullValueHandling = NullValueHandling.Ignore)]
    public DownloaderOptions? DownloaderOptions { get; set; }
}

public class DownloaderOptions
{
    [JsonProperty(propertyName: "http_chunk_size")]
    public long HttpChunkSize { get; set; }
}

public class Fragment
{
    [JsonProperty(propertyName: "url")]
    public Uri? Url { get; set; }

    [JsonProperty(propertyName: "duration")]
    public double Duration { get; set; }
}

public class HttpHeaders
{
    [JsonProperty(propertyName: "User-Agent")]
    public string? UserAgent { get; set; }

    [JsonProperty(propertyName: "Accept")]
    public string? Accept { get; set; }

    [JsonProperty(propertyName: "Accept-Language")]
    public string? AcceptLanguage { get; set; }

    [JsonProperty(propertyName: "Sec-Fetch-Mode")]
    public string? SecFetchMode { get; set; }
}

public class Heatmap
{
    [JsonProperty(propertyName: "start_time")]
    public double StartTime { get; set; }

    [JsonProperty(propertyName: "end_time")]
    public double EndTime { get; set; }

    [JsonProperty(propertyName: "value")]
    public double Value { get; set; }
}

public class Thumbnail
{
    [JsonProperty(propertyName: "url")]
    public Uri? Url { get; set; }

    [JsonProperty(propertyName: "preference")]
    public long Preference { get; set; }

    [JsonProperty(propertyName: "id")]
    [JsonConverter(converterType: typeof(ParseStringConverter))]
    public long Id { get; set; }

    [JsonProperty(propertyName: "height", NullValueHandling = NullValueHandling.Ignore)]
    public long? Height { get; set; }

    [JsonProperty(propertyName: "width", NullValueHandling = NullValueHandling.Ignore)]
    public long? Width { get; set; }

    [JsonProperty(propertyName: "resolution", NullValueHandling = NullValueHandling.Ignore)]
    public string? Resolution { get; set; }
}

public class Version
{
    [JsonProperty(propertyName: "version")]
    public string? VersionVersion { get; set; }

    [JsonProperty(propertyName: "current_git_head")]
    public object? CurrentGitHead { get; set; }

    [JsonProperty(propertyName: "release_git_head")]
    public string? ReleaseGitHead { get; set; }

    [JsonProperty(propertyName: "repository")]
    public string? Repository { get; set; }
}
