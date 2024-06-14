﻿// ReSharper disable All

using NoMercy.Providers.MusicBrainz.Models;

namespace NoMercy.Providers.MusicBrainz.Client;

public class MusicBrainzArtistClient : MusicBrainzBaseClient
{
    public MusicBrainzArtistClient(Guid? id, string[]? appendices = null) : base((Guid)id!)
    {
    }

    public Task<MusicBrainzArtistAppends?> WithAppends(string[] appendices, bool? priority = false)
    {
        Dictionary<string, string?> queryParams = new()
        {
            ["inc"] = string.Join("+", appendices),
            ["fmt"] = "json"
        };

        return Get<MusicBrainzArtistAppends>("artist/" + Id, queryParams, priority);
    }

    public Task<MusicBrainzArtistAppends?> WithAllAppends(bool? priority = false)
    {
        return WithAppends([
            "genres",
            "recordings",
            "releases",
            "release-groups",
            "works"
        ], priority);
    }
}