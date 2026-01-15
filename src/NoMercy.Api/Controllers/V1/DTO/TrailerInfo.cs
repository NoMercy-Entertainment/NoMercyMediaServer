using Mono.Nat;
using Newtonsoft.Json;
using NoMercy.Providers.Helpers;

namespace NoMercy.Api.Controllers.V1.DTO;

public class TrailerInfo
{
    [JsonProperty("id")] public string? Id { get; set; }
    [JsonProperty("title")] public string? Title { get; set; }
    [JsonProperty("formats")] public Format[] Formats { get; set; } = [];
    [JsonProperty("thumbnails")] public Thumbnail[] Thumbnails { get; set; } = [];
    [JsonProperty("thumbnail")] public Uri? Thumbnail { get; set; }
    [JsonProperty("description")] public string? Description { get; set; }
    [JsonProperty("channel_id")] public string? ChannelId { get; set; }
    [JsonProperty("channel_url")] public Uri? ChannelUrl { get; set; }
    [JsonProperty("duration")] public long Duration { get; set; }
    [JsonProperty("view_count")] public long ViewCount { get; set; }
    [JsonProperty("average_rating")] public object? AverageRating { get; set; }
    [JsonProperty("age_limit")] public long AgeLimit { get; set; }
    [JsonProperty("webpage_url")] public Uri? WebpageUrl { get; set; }
    [JsonProperty("categories")] public string[] Categories { get; set; } = [];
    [JsonProperty("tags")] public string[] Tags { get; set; } = [];
    [JsonProperty("playable_in_embed")] public bool PlayableInEmbed { get; set; }
    [JsonProperty("live_status")] public string? LiveStatus { get; set; }
    [JsonProperty("release_timestamp")] public object? ReleaseTimestamp { get; set; }
    [JsonProperty("_format_sort_fields")] public string[] FormatSortFields { get; set; } = [];
    [JsonProperty("automatic_captions")] public Dictionary<string, Caption> AutomaticCaptions { get; set; } = new();
    [JsonProperty("subtitles")] public Dictionary<string, Caption[]> Subtitles { get; set; } = new();
    [JsonProperty("comment_count")] public long CommentCount { get; set; }
    [JsonProperty("chapters")] public object? Chapters { get; set; }
    [JsonProperty("heatmap")] public Heatmap[] Heatmap { get; set; } = [];
    [JsonProperty("like_count")] public long LikeCount { get; set; }
    [JsonProperty("channel")] public string? Channel { get; set; }
    [JsonProperty("channel_follower_count")] public long ChannelFollowerCount { get; set; }
    [JsonProperty("channel_is_verified")] public bool ChannelIsVerified { get; set; }
    [JsonProperty("uploader")] public string? Uploader { get; set; }
    [JsonProperty("uploader_id")] public string? UploaderId { get; set; }
    [JsonProperty("uploader_url")] public Uri? UploaderUrl { get; set; }
    [JsonProperty("upload_date")]
    [JsonConverter(typeof(ParseStringConverter))] public long UploadDate { get; set; }
    [JsonProperty("timestamp")] public long Timestamp { get; set; }
    [JsonProperty("availability")] public string? Availability { get; set; }
    [JsonProperty("original_url")] public string? OriginalUrl { get; set; }
    [JsonProperty("webpage_url_basename")] public string? WebpageUrlBasename { get; set; }
    [JsonProperty("webpage_url_domain")] public string? WebpageUrlDomain { get; set; }
    [JsonProperty("extractor")] public string? Extractor { get; set; }
    [JsonProperty("extractor_key")] public string? ExtractorKey { get; set; }
    [JsonProperty("playlist")] public object? Playlist { get; set; }
    [JsonProperty("playlist_index")] public object? PlaylistIndex { get; set; }
    [JsonProperty("display_id")] public string? DisplayId { get; set; }
    [JsonProperty("fulltitle")] public string? Fulltitle { get; set; }
    [JsonProperty("duration_string")] public string? DurationString { get; set; }
    [JsonProperty("release_year")] public object? ReleaseYear { get; set; }
    [JsonProperty("is_live")] public bool IsLive { get; set; }
    [JsonProperty("was_live")] public bool WasLive { get; set; }
    [JsonProperty("requested_subtitles")] public object? RequestedSubtitles { get; set; }
    [JsonProperty("_has_drm")] public object? HasDrm { get; set; }
    [JsonProperty("epoch")] public long Epoch { get; set; }
    [JsonProperty("requested_formats")] public Format[] RequestedFormats { get; set; } = [];
    [JsonProperty("format")] public string? Format { get; set; }
    [JsonProperty("format_id")] public string? FormatId { get; set; }
    [JsonProperty("ext")] public string? Ext { get; set; }
    [JsonProperty("protocol")] public string? Protocol { get; set; }
    [JsonProperty("language")] public string? Language { get; set; }
    [JsonProperty("format_note")] public string? FormatNote { get; set; }
    [JsonProperty("filesize_approx")] public long FilesizeApprox { get; set; }
    [JsonProperty("tbr")] public double Tbr { get; set; }
    [JsonProperty("width")] public long Width { get; set; }
    [JsonProperty("height")] public long Height { get; set; }
    [JsonProperty("resolution")] public string? Resolution { get; set; }
    [JsonProperty("fps")] public long Fps { get; set; }
    [JsonProperty("dynamic_range")] public string? DynamicRange { get; set; }
    [JsonProperty("vcodec")] public string? Vcodec { get; set; }
    [JsonProperty("vbr")] public double Vbr { get; set; }
    [JsonProperty("stretched_ratio")] public object? StretchedRatio { get; set; }
    [JsonProperty("aspect_ratio")] public double AspectRatio { get; set; }
    [JsonProperty("acodec")] public string? Acodec { get; set; }
    [JsonProperty("abr")] public double Abr { get; set; }
    [JsonProperty("asr")] public long Asr { get; set; }
    [JsonProperty("audio_channels")] public long AudioChannels { get; set; }
    [JsonProperty("_filename")] public string? Filename { get; set; }
    [JsonProperty("filename")] public string? TrailerInfoFilename { get; set; }
    [JsonProperty("_type")] public string? Type { get; set; }
    [JsonProperty("_version")] public Version? Version { get; set; }    
}

public class Caption
{
    [JsonProperty("ext")] public string? Ext { get; set; }
    [JsonProperty("url")] public Uri? Url { get; set; }
    [JsonProperty("name")] public string? Name { get; set; }
    [JsonProperty("__yt_dlp_client")] public string? YtDlpClient { get; set; }
}

public class Format
{
    [JsonProperty("format_id")] public string? FormatId { get; set; }
    [JsonProperty("format_note", NullValueHandling = NullValueHandling.Ignore)] public string? FormatNote { get; set; }
    [JsonProperty("ext")] public string? Ext { get; set; }
    [JsonProperty("protocol")] public Protocol Protocol { get; set; }
    [JsonProperty("acodec", NullValueHandling = NullValueHandling.Ignore)] public string? Acodec { get; set; }
    [JsonProperty("vcodec")] public string? Vcodec { get; set; }
    [JsonProperty("url")] public Uri? Url { get; set; }
    [JsonProperty("width")] public long? Width { get; set; }
    [JsonProperty("height")] public long? Height { get; set; }
    [JsonProperty("fps")] public double? Fps { get; set; }
    [JsonProperty("rows", NullValueHandling = NullValueHandling.Ignore)] public long? Rows { get; set; }
    [JsonProperty("columns", NullValueHandling = NullValueHandling.Ignore)] public long? Columns { get; set; }
    [JsonProperty("fragments", NullValueHandling = NullValueHandling.Ignore)] public Fragment[] Fragments { get; set; } = [];
    [JsonProperty("resolution")] public string? Resolution { get; set; }
    [JsonProperty("aspect_ratio")] public double? AspectRatio { get; set; }
    [JsonProperty("filesize_approx")] public long? FilesizeApprox { get; set; }
    [JsonProperty("http_headers")] public HttpHeaders? HttpHeaders { get; set; }
    [JsonProperty("audio_ext")] public string? AudioExt { get; set; }
    [JsonProperty("video_ext")] public string? VideoExt { get; set; }
    [JsonProperty("vbr")] public double Vbr { get; set; }
    [JsonProperty("abr")] public double? Abr { get; set; }
    [JsonProperty("tbr")] public double? Tbr { get; set; }
    [JsonProperty("format")] public string? FormatFormat { get; set; }
    [JsonProperty("format_index")] public object? FormatIndex { get; set; }
    [JsonProperty("manifest_url", NullValueHandling = NullValueHandling.Ignore)] public Uri? ManifestUrl { get; set; }
    [JsonProperty("language")] public object? Language { get; set; }
    [JsonProperty("preference")] public object? Preference { get; set; }
    [JsonProperty("quality", NullValueHandling = NullValueHandling.Ignore)] public long? Quality { get; set; }
    [JsonProperty("has_drm", NullValueHandling = NullValueHandling.Ignore)] public bool? HasDrm { get; set; }
    [JsonProperty("source_preference", NullValueHandling = NullValueHandling.Ignore)] public long? SourcePreference { get; set; }
    [JsonProperty("asr")] public long? Asr { get; set; }
    [JsonProperty("filesize", NullValueHandling = NullValueHandling.Ignore)] public long? Filesize { get; set; }
    [JsonProperty("audio_channels")] public long? AudioChannels { get; set; }
    [JsonProperty("language_preference", NullValueHandling = NullValueHandling.Ignore)] public long? LanguagePreference { get; set; }
    [JsonProperty("dynamic_range")] public string? DynamicRange { get; set; }
    [JsonProperty("container", NullValueHandling = NullValueHandling.Ignore)] public string? Container { get; set; }
    [JsonProperty("downloader_options", NullValueHandling = NullValueHandling.Ignore)] public DownloaderOptions? DownloaderOptions { get; set; }    
}

public class DownloaderOptions
{
    [JsonProperty("http_chunk_size")] public long HttpChunkSize { get; set; }    
}

public class Fragment
{
    [JsonProperty("url")] public Uri? Url { get; set; }
    [JsonProperty("duration")] public double Duration { get; set; }    
}

public class HttpHeaders
{
    [JsonProperty("User-Agent")] public string? UserAgent { get; set; }
    [JsonProperty("Accept")] public string? Accept { get; set; }
    [JsonProperty("Accept-Language")] public string? AcceptLanguage { get; set; }
    [JsonProperty("Sec-Fetch-Mode")] public string? SecFetchMode { get; set; }    
}

public class Heatmap
{
    [JsonProperty("start_time")] public double StartTime { get; set; }
    [JsonProperty("end_time")] public double EndTime { get; set; }
    [JsonProperty("value")] public double Value { get; set; }    
}

public class Thumbnail
{
    [JsonProperty("url")] public Uri? Url { get; set; }
    [JsonProperty("preference")] public long Preference { get; set; }
    [JsonProperty("id")]
    [JsonConverter(typeof(ParseStringConverter))] public long Id { get; set; }
    [JsonProperty("height", NullValueHandling = NullValueHandling.Ignore)] public long? Height { get; set; }
    [JsonProperty("width", NullValueHandling = NullValueHandling.Ignore)] public long? Width { get; set; }
    [JsonProperty("resolution", NullValueHandling = NullValueHandling.Ignore)] public string? Resolution { get; set; }    
}

public class Version
{
    [JsonProperty("version")] public string? VersionVersion { get; set; }
    [JsonProperty("current_git_head")] public object? CurrentGitHead { get; set; }
    [JsonProperty("release_git_head")] public string? ReleaseGitHead { get; set; }
    [JsonProperty("repository")] public string? Repository { get; set; }    
}