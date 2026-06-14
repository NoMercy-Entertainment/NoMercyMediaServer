using System.Collections.Concurrent;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json;
using NoMercy.Data.Repositories;
using NoMercy.Database.Models.Music;
using NoMercy.Providers.MusixMatch.Models;
using NoMercy.Providers.NoMercy.Client;

namespace NoMercy.Api.Services.Music;

/// <summary>
/// Coalesces concurrent lyric fetches per track. When several devices request
/// the same track's lyrics while none are cached, the provider fetch runs once
/// and every caller awaits that single result. Without this each device hits the
/// rate-limited Lrclib/Musixmatch queues independently, stacking the delay.
/// </summary>
public class LyricsResolver(IServiceScopeFactory scopeFactory)
{
    private readonly ConcurrentDictionary<Guid, Lazy<Task<Lyric[]?>>> _inFlight = new();

    /// <summary>
    /// Returns the track's lyrics, fetching + persisting them once even when
    /// called concurrently for the same track. Returns null when no lyrics
    /// could be resolved.
    /// </summary>
    public Task<Lyric[]?> ResolveAsync(Guid trackId)
    {
        // Lazy guarantees the factory runs once even under GetOrAdd contention.
        return _inFlight.GetOrAdd(trackId, _ => new(() => FetchAndPersistAsync(trackId))).Value;
    }

    private async Task<Lyric[]?> FetchAndPersistAsync(Guid trackId)
    {
        try
        {
            // Own scope: the fetch outlives any single request, so a caller
            // disconnecting mid-flight can't dispose the context the shared
            // task depends on.
            await using AsyncServiceScope scope = scopeFactory.CreateAsyncScope();
            IMusicRepository repository =
                scope.ServiceProvider.GetRequiredService<IMusicRepository>();

            Track? track = await repository.GetTrackWithIncludesAsync(trackId);
            if (track is null)
                return null;

            // A racing request may have persisted lyrics between the caller's
            // cache check and us acquiring the in-flight slot.
            if (track.Lyrics is not null)
                return track.Lyrics;

            MusixMatchFormattedLyric[]? lyrics = await NoMercyLyricsClient.SearchLyrics(track);
            if (lyrics is null)
                return null;

            return await repository.UpdateTrackLyricsAsync(
                track,
                JsonConvert.SerializeObject(lyrics)
            );
        }
        finally
        {
            _inFlight.TryRemove(trackId, out _);
        }
    }
}
