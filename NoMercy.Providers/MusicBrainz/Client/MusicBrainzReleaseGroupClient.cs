﻿// ReSharper disable All

using NoMercy.Providers.CoverArt.Models;
using NoMercy.Providers.MusicBrainz.Models;

namespace NoMercy.Providers.MusicBrainz.Client;

public class MusicBrainzReleaseGroupClient : MusicBrainzBaseClient
{
    public MusicBrainzReleaseGroupClient(Guid? id, string[]? appendices = null) : base((Guid)id!)
    {
    }

    public Task<MusicBrainzReleaseAppends?> WithAppends(string[] appendices, bool? priority = false)
    {
        Dictionary<string, string>? queryParams = new()
        {
            ["inc"] = string.Join("+", appendices),
            ["fmt"] = "json"
        };

        return Get<MusicBrainzReleaseAppends>("release-group/" + Id, queryParams, priority);
    }

    public Task<MusicBrainzReleaseAppends?> WithAllAppends(bool? priority = false)
    {
        return WithAppends([
            "artists",
            "releases"
        ], priority);
    }
}