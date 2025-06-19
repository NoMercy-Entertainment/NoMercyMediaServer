// ReSharper disable All

using NoMercy.Providers.CoverArt.Models;
using NoMercy.Providers.MusicBrainz.Models;

namespace NoMercy.Providers.MusicBrainz.Client;

public class MusicBrainzReleaseClient : MusicBrainzBaseClient
{
    public MusicBrainzReleaseClient() : base()
    {
    }

    public MusicBrainzReleaseClient(Guid? id, string[]? appendices = null) : base((Guid)id!)
    {
    }

    public Task<MusicBrainzReleaseAppends?> WithAppends(Guid? id, string[] appendices, bool? priority = false)
    {
        Dictionary<string, string> queryParams = new()
        {
            ["inc"] = string.Join("+", appendices),
            ["fmt"] = "json"
        };

        return Get<MusicBrainzReleaseAppends>("release/" + id, queryParams, priority);
    }

    public Task<MusicBrainzReleaseAppends?> WithAppends(string[] appendices, bool? priority = false)
    {
        Dictionary<string, string> queryParams = new()
        {
            ["inc"] = string.Join("+", appendices),
            ["fmt"] = "json"
        };

        return Get<MusicBrainzReleaseAppends>("release/" + Id, queryParams, priority);
    }

    public Task<MusicBrainzReleaseAppends?> WithAllAppends(Guid? id, bool? priority = false)
    {
        return WithAppends((Guid)id!, new[]
        {
            "artists",
            "labels",
            "recordings",
            "release-groups",
            "media",
            "artist-credits",
            "discids",
            "puids",
            "isrcs",
            "artist-rels",
            "label-rels",
            "recording-rels",
            "release-rels",
            "release-group-rels",
            "url-rels",
            "work-rels",
            "recording-level-rels",
            "work-level-rels",
            "annotation",
            "aliases",
            "artist-credits",
            "collections",
            "genres",
            "tags"
        }, priority);
    }

    public Task<MusicBrainzReleaseAppends?> WithAllAppends(bool? priority = false)
    {
        return WithAppends([
            "artists",
            "labels",
            "recordings",
            "release-groups",
            "media",
            "artist-credits",
            "discids",
            "puids",
            "isrcs",
            "artist-rels",
            "label-rels",
            "recording-rels",
            "release-rels",
            "release-group-rels",
            "url-rels",
            "work-rels",
            "recording-level-rels",
            "work-level-rels",
            "annotation",
            "aliases",
            "artist-credits",
            "collections",
            "genres",
            "tags"
        ], priority);
    }

    public Task<MusicBrainzReleaseSearchResponse?> SearchReleases(string query, bool? priority = false)
    {
        Dictionary<string, string>? queryParams = new()
        {
            ["query"] = query,
            ["inc"] = "recordings",
            ["fmt"] = "json"
        };
        return Get<MusicBrainzReleaseSearchResponse>($"release", queryParams, priority);
    }
}