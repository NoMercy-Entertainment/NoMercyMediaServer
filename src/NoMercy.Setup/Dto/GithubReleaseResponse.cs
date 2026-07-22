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

namespace NoMercy.Setup.Dto;

public class GithubReleaseResponse
{
    [JsonProperty(propertyName: "url")]
    public Uri Url { get; set; } = null!;

    [JsonProperty(propertyName: "assets_url")]
    public Uri AssetsUrl { get; set; } = null!;

    [JsonProperty(propertyName: "upload_url")]
    public string UploadUrl { get; set; } = string.Empty;

    [JsonProperty(propertyName: "html_url")]
    public Uri HtmlUrl { get; set; } = null!;

    [JsonProperty(propertyName: "id")]
    public long Id { get; set; }

    [JsonProperty(propertyName: "author")]
    public Author Author { get; set; } = new();

    [JsonProperty(propertyName: "node_id")]
    public string NodeId { get; set; } = string.Empty;

    [JsonProperty(propertyName: "tag_name")]
    public string TagName { get; set; } = string.Empty;

    [JsonProperty(propertyName: "target_commitish")]
    public string TargetCommitish { get; set; } = string.Empty;

    [JsonProperty(propertyName: "name")]
    public string Name { get; set; } = string.Empty;

    [JsonProperty(propertyName: "draft")]
    public bool Draft { get; set; }

    [JsonProperty(propertyName: "immutable")]
    public bool Immutable { get; set; }

    [JsonProperty(propertyName: "prerelease")]
    public bool Prerelease { get; set; }

    [JsonProperty(propertyName: "created_at")]
    public DateTimeOffset CreatedAt { get; set; }

    [JsonProperty(propertyName: "updated_at")]
    public DateTimeOffset UpdatedAt { get; set; }

    [JsonProperty(propertyName: "published_at")]
    public DateTimeOffset PublishedAt { get; set; }

    [JsonProperty(propertyName: "assets")]
    public Asset[] Assets { get; set; } = [];

    [JsonProperty(propertyName: "tarball_url")]
    public Uri TarballUrl { get; set; } = null!;

    [JsonProperty(propertyName: "zipball_url")]
    public Uri ZipballUrl { get; set; } = null!;

    [JsonProperty(propertyName: "body")]
    public string Body { get; set; } = string.Empty;
}

public class Asset
{
    [JsonProperty(propertyName: "url")]
    public Uri Url { get; set; } = null!;

    [JsonProperty(propertyName: "id")]
    public long Id { get; set; }

    [JsonProperty(propertyName: "node_id")]
    public string NodeId { get; set; } = string.Empty;

    [JsonProperty(propertyName: "name")]
    public string Name { get; set; } = string.Empty;

    [JsonProperty(propertyName: "label")]
    public string Label { get; set; } = string.Empty;

    [JsonProperty(propertyName: "uploader")]
    public Author Uploader { get; set; } = new();

    [JsonProperty(propertyName: "content_type")]
    public string ContentType { get; set; } = string.Empty;

    [JsonProperty(propertyName: "state")]
    public string State { get; set; } = string.Empty;

    [JsonProperty(propertyName: "size")]
    public long Size { get; set; }

    [JsonProperty(propertyName: "digest")]
    public string Digest { get; set; } = string.Empty;

    [JsonProperty(propertyName: "download_count")]
    public long DownloadCount { get; set; }

    [JsonProperty(propertyName: "created_at")]
    public DateTimeOffset CreatedAt { get; set; }

    [JsonProperty(propertyName: "updated_at")]
    public DateTimeOffset UpdatedAt { get; set; }

    [JsonProperty(propertyName: "browser_download_url")]
    public Uri BrowserDownloadUrl { get; set; } = null!;
}

public class Author
{
    [JsonProperty(propertyName: "login")]
    public string Login { get; set; } = string.Empty;

    [JsonProperty(propertyName: "id")]
    public long Id { get; set; }

    [JsonProperty(propertyName: "node_id")]
    public string NodeId { get; set; } = string.Empty;

    [JsonProperty(propertyName: "avatar_url")]
    public Uri AvatarUrl { get; set; } = null!;

    [JsonProperty(propertyName: "gravatar_id")]
    public string GravatarId { get; set; } = string.Empty;

    [JsonProperty(propertyName: "url")]
    public Uri Url { get; set; } = null!;

    [JsonProperty(propertyName: "html_url")]
    public Uri HtmlUrl { get; set; } = null!;

    [JsonProperty(propertyName: "followers_url")]
    public Uri FollowersUrl { get; set; } = null!;

    [JsonProperty(propertyName: "following_url")]
    public string FollowingUrl { get; set; } = string.Empty;

    [JsonProperty(propertyName: "gists_url")]
    public string GistsUrl { get; set; } = string.Empty;

    [JsonProperty(propertyName: "starred_url")]
    public string StarredUrl { get; set; } = string.Empty;

    [JsonProperty(propertyName: "subscriptions_url")]
    public Uri SubscriptionsUrl { get; set; } = null!;

    [JsonProperty(propertyName: "organizations_url")]
    public Uri OrganizationsUrl { get; set; } = null!;

    [JsonProperty(propertyName: "repos_url")]
    public Uri ReposUrl { get; set; } = null!;

    [JsonProperty(propertyName: "events_url")]
    public string EventsUrl { get; set; } = string.Empty;

    [JsonProperty(propertyName: "received_events_url")]
    public Uri ReceivedEventsUrl { get; set; } = null!;

    [JsonProperty(propertyName: "type")]
    public string Type { get; set; } = string.Empty;

    [JsonProperty(propertyName: "user_view_type")]
    public string UserViewType { get; set; } = string.Empty;

    [JsonProperty(propertyName: "site_admin")]
    public bool SiteAdmin { get; set; }
}
