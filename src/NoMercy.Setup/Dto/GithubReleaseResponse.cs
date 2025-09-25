using Newtonsoft.Json;

namespace NoMercy.Setup.Dto;

public class GithubReleaseResponse
{
    [JsonProperty("url")] public Uri Url { get; set; } = null!;
    [JsonProperty("assets_url")] public Uri AssetsUrl { get; set; } = null!;
    [JsonProperty("upload_url")] public string UploadUrl { get; set; } = string.Empty;
    [JsonProperty("html_url")] public Uri HtmlUrl { get; set; } = null!;
    [JsonProperty("id")] public long Id { get; set; }
    [JsonProperty("author")] public Author Author { get; set; } = new();
    [JsonProperty("node_id")] public string NodeId { get; set; } = string.Empty;
    [JsonProperty("tag_name")] public string TagName { get; set; } = string.Empty;
    [JsonProperty("target_commitish")] public string TargetCommitish { get; set; } = string.Empty;
    [JsonProperty("name")] public string Name { get; set; } = string.Empty;
    [JsonProperty("draft")] public bool Draft { get; set; }
    [JsonProperty("immutable")] public bool Immutable { get; set; }
    [JsonProperty("prerelease")] public bool Prerelease { get; set; }
    [JsonProperty("created_at")] public DateTimeOffset CreatedAt { get; set; }
    [JsonProperty("updated_at")] public DateTimeOffset UpdatedAt { get; set; }
    [JsonProperty("published_at")] public DateTimeOffset PublishedAt { get; set; }
    [JsonProperty("assets")] public Asset[] Assets { get; set; } = [];
    [JsonProperty("tarball_url")] public Uri TarballUrl { get; set; } = null!;
    [JsonProperty("zipball_url")] public Uri ZipballUrl { get; set; } = null!;
    [JsonProperty("body")] public string Body { get; set; } = string.Empty;
}

public class Asset
{
    [JsonProperty("url")] public Uri Url { get; set; } = null!;
    [JsonProperty("id")] public long Id { get; set; }
    [JsonProperty("node_id")] public string NodeId { get; set; } = string.Empty;
    [JsonProperty("name")] public string Name { get; set; } = string.Empty;
    [JsonProperty("label")] public string Label { get; set; } = string.Empty;
    [JsonProperty("uploader")] public Author Uploader { get; set; } = new();
    [JsonProperty("content_type")] public string ContentType { get; set; } = string.Empty;
    [JsonProperty("state")] public string State { get; set; } = string.Empty;
    [JsonProperty("size")] public long Size { get; set; }
    [JsonProperty("digest")] public string Digest { get; set; } = string.Empty;
    [JsonProperty("download_count")] public long DownloadCount { get; set; }
    [JsonProperty("created_at")] public DateTimeOffset CreatedAt { get; set; }
    [JsonProperty("updated_at")] public DateTimeOffset UpdatedAt { get; set; }
    [JsonProperty("browser_download_url")] public Uri BrowserDownloadUrl { get; set; } = null!;
}

public class Author
{
    [JsonProperty("login")] public string Login { get; set; } = string.Empty;
    [JsonProperty("id")] public long Id { get; set; }
    [JsonProperty("node_id")] public string NodeId { get; set; } = string.Empty;
    [JsonProperty("avatar_url")] public Uri AvatarUrl { get; set; } = null!;
    [JsonProperty("gravatar_id")] public string GravatarId { get; set; } = string.Empty;
    [JsonProperty("url")] public Uri Url { get; set; } = null!;
    [JsonProperty("html_url")] public Uri HtmlUrl { get; set; } = null!;
    [JsonProperty("followers_url")] public Uri FollowersUrl { get; set; } = null!;
    [JsonProperty("following_url")] public string FollowingUrl { get; set; } = string.Empty;
    [JsonProperty("gists_url")] public string GistsUrl { get; set; } = string.Empty;
    [JsonProperty("starred_url")] public string StarredUrl { get; set; } = string.Empty;
    [JsonProperty("subscriptions_url")] public Uri SubscriptionsUrl { get; set; } = null!;
    [JsonProperty("organizations_url")] public Uri OrganizationsUrl { get; set; } = null!;
    [JsonProperty("repos_url")] public Uri ReposUrl { get; set; } = null!;
    [JsonProperty("events_url")] public string EventsUrl { get; set; } = string.Empty;
    [JsonProperty("received_events_url")] public Uri ReceivedEventsUrl { get; set; } = null!;
    [JsonProperty("type")] public string Type { get; set; } = string.Empty;
    [JsonProperty("user_view_type")] public string UserViewType { get; set; } = string.Empty;
    [JsonProperty("site_admin")] public bool SiteAdmin { get; set; }
}
