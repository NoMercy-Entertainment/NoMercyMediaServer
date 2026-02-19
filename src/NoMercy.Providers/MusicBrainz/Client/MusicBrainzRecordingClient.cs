// ReSharper disable All

using NoMercy.Providers.MusicBrainz.Models;

namespace NoMercy.Providers.MusicBrainz.Client;

public class MusicBrainzRecordingClient : MusicBrainzBaseClient
{
    public MusicBrainzRecordingClient(Guid? id, string[]? appendices = null) : base((Guid)id!)
    {
    }

    public MusicBrainzRecordingClient() : base()
    {
    }

    public Task<MusicBrainzRecordingAppends?> WithAppends(Guid? id, string[] appendices, bool? priority = false)
    {
        Dictionary<string, string> queryParams = new()
        {
            ["inc"] = string.Join("+", appendices),
            ["fmt"] = "json"
        };

        return Get<MusicBrainzRecordingAppends>("recording/" + id, queryParams, priority);
    }

    public Task<MusicBrainzRecordingAppends?> WithAppends(string[] appendices, bool? priority = false)
    {
        Dictionary<string, string>? queryParams = new()
        {
            ["inc"] = string.Join("+", appendices),
            ["fmt"] = "json"
        };

        return Get<MusicBrainzRecordingAppends>("recording/" + Id, queryParams, priority);
    }

    public Task<MusicBrainzRecordingAppends?> WithAllAppends(Guid? id, bool? priority = false)
    {
        return WithAppends((Guid)id!, [
            "artist-credits",
            "artists",
            "releases",
            "tags",
            "genres"
        ], priority);
    }

    public Task<MusicBrainzRecordingAppends?> WithAllAppends(bool? priority = false)
    {
        return WithAppends([
            "artist-credits",
            "artists",
            "releases",
            "tags",
            "genres"
        ], priority);
    }

    public Task<MusicBrainzRecordingAppends?> SearchRecordings(string query, bool? priority = false)
    {
        Dictionary<string, string>? queryParams = new()
        {
            ["query"] = query,
            ["inc"] = "releases",
            ["fmt"] = "json"
        };
        return Get<MusicBrainzRecordingAppends>($"recording", queryParams, priority);
    }

    public Task<MusicBrainzSearchResponse?> SearchRecordingsDynamic(string query, bool? priority = false)
    {
        Dictionary<string, string>? queryParams = new()
        {
            ["query"] = query,
            ["inc"] = "releases",
            ["fmt"] = "json"
        };
        return Get<MusicBrainzSearchResponse>($"recording", queryParams, priority);
    }
}
