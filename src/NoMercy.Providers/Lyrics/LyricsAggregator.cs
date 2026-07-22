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

using System.Diagnostics;
using System.Globalization;
using NoMercy.Database.Models.Music;
using NoMercy.NmSystem.Extensions;
using NoMercy.NmSystem.SystemCalls;
using NoMercy.Providers.Lrclib.Client;
using NoMercy.Providers.Lrclib.Models;
using NoMercy.Providers.MusixMatch.Client;
using NoMercy.Providers.MusixMatch.Models;
using NoMercy.Providers.NoMercy.Client;
using NoMercy.Providers.NoMercy.Models;
using Serilog.Events;

namespace NoMercy.Providers.Lyrics;

public class LyricsAggregator : ILyricsAggregator
{
    // Bounds how long a single provider stage may keep the caller waiting.
    // Lrclib/Musixmatch normally answer in well under a second once their
    // rate-limited Queue lets the request through, so the default is generous.
    // A stage that blows the budget is NOT cancelled server-side -- its
    // HttpClient.Timeout / Queue retry chain keeps running in the background
    // so the provider's rate-limited slot is still released cleanly -- the
    // caller just stops waiting on it and the resolve treats it as an error.
    private static readonly TimeSpan DefaultProviderTimeout = TimeSpan.FromSeconds(seconds: 8);

    private readonly TimeSpan _providerTimeout;

    public LyricsAggregator()
        : this(providerTimeout: DefaultProviderTimeout) { }

    // Test-only seam (NoMercy.Tests.Providers has InternalsVisibleTo) so a
    // timeout test can shrink the budget instead of waiting out the real one.
    internal LyricsAggregator(TimeSpan providerTimeout)
    {
        _providerTimeout = providerTimeout;
    }

    /// <summary>
    /// Resolves lyrics for a track, preferring the free Lrclib service and
    /// falling back to Musixmatch. Every candidate is validated against the
    /// track's title, artist and (for synced lyrics) release length, so a
    /// mismatched song or a wrong-release timing is rejected instead of stored.
    /// </summary>
    public async Task<LyricsFetchResult> SearchLyrics(Track track)
    {
        // `using` so an exception mid-search doesn't leak the HttpClient wrappers.
        using MusixmatchClient musixmatchClient = new();
        using LrclibClient lrclibClient = new();

        string[] artists = track
            .ArtistTrack.Select(selector: artistTrack => artistTrack.Artist.Name)
            .ToArray();
        string? albumName = track.AlbumTrack.FirstOrDefault()?.Album.Name;
        int parsedDuration = track.Duration.ToSeconds();
        int? durationSeconds = parsedDuration > 0 ? parsedDuration : null;

        LyricQuery query = new(Title: track.Name, Artists: artists, Album: albumName, DurationSeconds: durationSeconds);
        string label = $"{track.Name} - {string.Join(separator: ", ", value: artists)}";
        Stopwatch total = Stopwatch.StartNew();

        // The /get endpoint is an exact artist+title+album+duration lookup.
        // When it yields a synced match that passes validation it's
        // authoritative, so skip the broader /search + Musixmatch calls
        // entirely: one provider round trip instead of up to four.
        ProviderAttempt exact = await RunProvider(
            provider: "Lrclib-get",
            fetch: () => FromLrclibGet(client: lrclibClient, query: query, artists: artists)
        );
        if (exact.Candidate is { HasSyncedLyrics: true })
            return Resolved(label: label, winner: exact, elapsedMs: total.ElapsedMilliseconds);

        // /get missed or only had plain lyrics: race the fuzzy Lrclib search
        // against Musixmatch instead of running every remaining call one after
        // another. Racing never lets a wrong match through -- both branches
        // still validate every candidate they see, and PickBest below scores
        // everything that came back (plus a lingering plain /get hit) and only
        // ever returns the best-scoring, still-valid candidate.
        Task<ProviderAttempt> searchTask = RunProvider(
            provider: "Lrclib-search",
            fetch: () => FromLrclibSearch(client: lrclibClient, query: query, artists: artists)
        );
        Task<ProviderAttempt> musixmatchTask = RunMusixmatch(client: musixmatchClient, query: query);
        ProviderAttempt[] raced = await Task.WhenAll(tasks: new[]{searchTask, musixmatchTask});

        ProviderAttempt[] attempts = [exact, raced[0], raced[1]];
        LyricCandidate? best = LyricMatcher.PickBest(
            query: query,
            candidates: attempts.Select(selector: attempt => attempt.Candidate).OfType<LyricCandidate>()
        );

        total.Stop();

        if (best is not null)
        {
            ProviderAttempt winner = attempts.First(predicate: attempt =>
                ReferenceEquals(objA: attempt.Candidate, objB: best)
            );
            return Resolved(label: label, winner: winner, elapsedMs: total.ElapsedMilliseconds);
        }

        if (attempts.Any(predicate: attempt => attempt.Errored))
        {
            Logger.Lyrics(
                message: $"lyrics NOT resolved for {label} after {total.ElapsedMilliseconds}ms: a provider call failed or timed out, treating as transient",
                level: LogEventLevel.Warning
            );
            return LyricsFetchResult.TransientFailure;
        }

        Logger.Lyrics(
            message: $"lyrics NOT found for {label} after {total.ElapsedMilliseconds}ms, providers=[Lrclib-get, Lrclib-search, Musixmatch]"
        );
        return LyricsFetchResult.NotFound;
    }

    private static LyricsFetchResult Resolved(string label, ProviderAttempt winner, long elapsedMs)
    {
        LyricCandidate candidate = winner.Candidate!;
        Logger.Lyrics(
            message: $"lyrics resolved for {label} via {winner.Provider} in {elapsedMs}ms (synced={candidate.HasSyncedLyrics})"
        );
        return LyricsFetchResult.Found(lines: candidate.Lines, winner: winner.Provider);
    }

    /// <summary>
    /// Runs one provider stage with a timing/outcome log line and a hard wall
    /// clock budget. A timeout or any other exception is reported as
    /// <see cref="ProviderAttempt.Errored"/> rather than propagated, so one bad
    /// provider stage never takes the whole resolve down with it.
    /// </summary>
    private async Task<ProviderAttempt> RunProvider(
        string provider,
        Func<Task<LyricCandidate?>> fetch
    )
    {
        Stopwatch stopwatch = Stopwatch.StartNew();
        try
        {
            LyricCandidate? candidate = await fetch().WaitAsync(timeout: _providerTimeout);
            stopwatch.Stop();
            string outcome =
                candidate is null ? "miss"
                : candidate.HasSyncedLyrics ? "hit-synced"
                : "hit-plain";
            Logger.Lyrics(message: $"{provider}: {outcome} in {stopwatch.ElapsedMilliseconds}ms");
            return new(Provider: provider, Candidate: candidate, Errored: false);
        }
        catch (TimeoutException)
        {
            stopwatch.Stop();
            Logger.Lyrics(
                message: $"{provider}: timed out after {stopwatch.ElapsedMilliseconds}ms (call keeps running in the background to release its rate-limit slot)",
                level: LogEventLevel.Warning
            );
            return new(Provider: provider, Candidate: null, Errored: true);
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            Logger.Lyrics(
                message: $"{provider}: error after {stopwatch.ElapsedMilliseconds}ms ({ex.GetType().Name}: {ex.Message})",
                level: LogEventLevel.Warning
            );
            return new(Provider: provider, Candidate: null, Errored: true);
        }
    }

    private async Task<ProviderAttempt> RunMusixmatch(MusixmatchClient client, LyricQuery query)
    {
        // Tighten first (album + duration), then relax. A clean miss on the
        // tight query still tries the relaxed one; an error/timeout does not,
        // since a struggling provider is unlikely to answer the second call
        // any faster and it only extends the wait.
        ProviderAttempt tight = await RunProvider(
            provider: "Musixmatch-tight",
            fetch: () => MusixmatchTight(client: client, query: query)
        );
        if (tight.Candidate is not null || tight.Errored)
            return tight;

        return await RunProvider(provider: "Musixmatch-relaxed", fetch: () => MusixmatchRelaxed(client: client, query: query));
    }

    private static async Task<LyricCandidate?> FromLrclibGet(
        LrclibClient client,
        LyricQuery query,
        string[] artists
    )
    {
        LrclibSongResult? exact = await client.Get(
            artists: artists,
            trackName: query.Title,
            albumName: query.Album,
            duration: query.DurationSeconds
        );
        if (exact is null || LrclibClient.ToCandidate(result: exact) is not { } candidate)
            return null;

        return LyricMatcher.PickBest(query: query, candidates: [candidate]);
    }

    private static async Task<LyricCandidate?> FromLrclibSearch(
        LrclibClient client,
        LyricQuery query,
        string[] artists
    )
    {
        LrclibSongResult[]? results = await client.Search(artists: artists, trackName: query.Title);
        if (results is null)
            return null;

        List<LyricCandidate> candidates = [];
        foreach (LrclibSongResult result in results)
            if (LrclibClient.ToCandidate(result: result) is { } candidate)
                candidates.Add(item: candidate);

        return LyricMatcher.PickBest(query: query, candidates: candidates);
    }

    private static async Task<LyricCandidate?> MusixmatchTight(
        MusixmatchClient client,
        LyricQuery query
    )
    {
        string artistNames = string.Join(separator: ",", values: query.Artists);
        string duration =
            query.DurationSeconds?.ToString(provider: CultureInfo.InvariantCulture) ?? string.Empty;

        return Validate(
            query: query,
            response: await client.SongSearch(
                musixMatchTrackParameters: new()
                {
                    Album = query.Album,
                    Artist = artistNames,
                    Title = query.Title,
                    Duration = duration,
                    Sort = MusixMatchTrackSearchParameters.MusixMatchSortStrategy.TrackRatingDesc,
                }
            )
        );
    }

    private static async Task<LyricCandidate?> MusixmatchRelaxed(
        MusixmatchClient client,
        LyricQuery query
    )
    {
        string artistNames = string.Join(separator: ",", values: query.Artists);

        return Validate(
            query: query,
            response: await client.SongSearch(
                musixMatchTrackParameters: new()
                {
                    Artist = artistNames,
                    Title = query.Title,
                    Sort = MusixMatchTrackSearchParameters.MusixMatchSortStrategy.TrackRatingDesc,
                }
            )
        );
    }

    private static LyricCandidate? Validate(LyricQuery query, MusixMatchSubtitleGet? response)
    {
        LyricCandidate? candidate = MusixMatchLyricMapper.ToCandidate(response: response);
        if (candidate is null)
            return null;
        return LyricMatcher.Score(query: query, candidate: candidate) >= 0 ? candidate : null;
    }

    private sealed record ProviderAttempt(string Provider, LyricCandidate? Candidate, bool Errored);
}
