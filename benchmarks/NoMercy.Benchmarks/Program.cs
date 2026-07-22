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

using System.Collections;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using NoMercy.Data.Repositories;
using NoMercy.Database;
using NoMercy.Database.Models.Music;
using NoMercy.NmSystem.Information;

namespace NoMercy.Benchmarks;

// Creates a MediaContext bound to the real database. MediaContext.OnConfiguring
// self-configures from AppFiles.MediaDatabase, which resolves under the app path
// (NOMERCY_APP_PATH). Every query below is a read (AsNoTracking); nothing mutates.
file sealed class MediaContextFactory : IDbContextFactory<MediaContext>
{
    public MediaContext CreateDbContext() => new();
}

// Same real-database binding, but every executed SQL command is logged with its
// server-side elapsed time. Routing a repository through this factory (instead of
// the silent one) reveals which split-query leg dominates a slow call, without
// touching production code. Used by the --sql diagnostic only.
file sealed class LoggingMediaContextFactory : IDbContextFactory<MediaContext>
{
    public MediaContext CreateDbContext()
    {
        DbContextOptionsBuilder<MediaContext> builder = new();
        builder.UseSqlite(
            connectionString: $"Data Source={AppFiles.MediaDatabase}; Pooling=True; Foreign Keys=True; Default Timeout=30;",
            sqliteOptionsAction: o => o.UseQuerySplittingBehavior(querySplittingBehavior: QuerySplittingBehavior.SplitQuery)
        );
        builder.LogTo(
            action: Console.WriteLine,
            events: new[] { Microsoft.EntityFrameworkCore.Diagnostics.RelationalEventId.CommandExecuted }
        );
        return new(options: builder.Options);
    }

    public Task<MediaContext> CreateDbContextAsync(CancellationToken ct = default) =>
        Task.FromResult(result: CreateDbContext());
}

// A named query to time. Run returns the raw result so the harness can both count
// its rows and fingerprint its content (to prove an optimization loses no data).
internal sealed record BenchmarkCase(string Name, Func<Task<object?>> Run);

internal sealed record BenchmarkResult(
    string Name,
    double ColdMs,
    double WarmMedianMs,
    double WarmP95Ms,
    double WarmMinMs,
    double WarmMaxMs,
    int Rows,
    string Signature,
    string? Error
);

internal static class Program
{
    private static async Task<int> Main(string[] args)
    {
        Dictionary<string, string> opts = ParseArgs(args: args);

        int samples = int.TryParse(s: opts.GetValueOrDefault(key: "samples"), result: out int s) ? s : 3;
        Guid userId = Guid.Parse(
            input: opts.GetValueOrDefault(key: "user", defaultValue: "37d03e60-7b0a-4246-a85b-a5618966a383")
        );
        Ulid libraryId = Ulid.Parse(
            base32: opts.GetValueOrDefault(key: "library", defaultValue: "01HQ5W2HMZ5QKDSXTTN9EQRERH")
        );
        Guid artistId = Guid.Parse(
            input: opts.GetValueOrDefault(key: "artist", defaultValue: "27638856-79A1-4495-B7DA-1912899560C7")
        );
        int tvId = int.Parse(s: opts.GetValueOrDefault(key: "tv", defaultValue: "14610"));
        int movieId = int.Parse(s: opts.GetValueOrDefault(key: "movie", defaultValue: "13654"));
        string language = opts.GetValueOrDefault(key: "language", defaultValue: "en");
        string country = opts.GetValueOrDefault(key: "country", defaultValue: "NL");
        string searchTerm = opts.GetValueOrDefault(key: "query", defaultValue: "love");
        string? filter = opts.GetValueOrDefault(key: "filter");

        // Bind to the real database. Default to the dev app root; override with
        // --app-path or NOMERCY_APP_PATH to benchmark a production data directory.
        string appPath =
            opts.GetValueOrDefault(key: "app-path")
            ?? Environment.GetEnvironmentVariable(variable: "NOMERCY_APP_PATH")
            ?? Path.Combine(
                path1: Environment.GetFolderPath(folder: Environment.SpecialFolder.LocalApplicationData),
                path2: "NoMercy_dev"
            );
        Environment.SetEnvironmentVariable(variable: "NOMERCY_APP_PATH", value: appPath);

        string dbPath = AppFiles.MediaDatabase;
        if (!File.Exists(path: dbPath))
        {
            Console.Error.WriteLine(value: $"media.db not found at: {dbPath}");
            Console.Error.WriteLine(
                value: "Pass --app-path <NoMercy app data root> to point at the real DB."
            );
            return 1;
        }

        long dbSizeMb = new FileInfo(fileName: dbPath).Length / (1024 * 1024);
        Console.WriteLine(value: "NoMercy query benchmarks (read-only, real database)");
        Console.WriteLine(value: $"  db       : {dbPath} ({dbSizeMb} MB)");
        Console.WriteLine(value: $"  user     : {userId}");
        Console.WriteLine(value: $"  samples  : {samples} warm runs + 1 cold, fresh DbContext per run");
        Console.WriteLine(
            value: $"  built    : {(IsDebug() ? "DEBUG (run -c Release for reliable numbers)" : "Release")}"
        );
        Console.WriteLine();

        MediaContextFactory factory = new();

        // Load the mega-artist's per-track album color palettes once. The palette A/B
        // cases below iterate this in-memory list, so they time only the conversion
        // strategy — the old deserialize+reserialize round-trip vs the new ToRaw()
        // passthrough — and not the query that fetched the strings.
        List<string?> artistPalettes;
        await using (MediaContext paletteCtx = factory.CreateDbContext())
        {
            artistPalettes = await paletteCtx
                .AlbumTrack.AsNoTracking()
                .Where(predicate: albumTrack =>
                    albumTrack.Track.ArtistTrack.Any(artistTrack =>
                        artistTrack.ArtistId == artistId
                    )
                )
                .Select(selector: albumTrack => albumTrack.Album._colorPalette)
                .ToListAsync();
        }
        Console.WriteLine(
            value: $"  palettes : {artistPalettes.Count} track-album color palettes loaded for the A/B"
        );
        Console.WriteLine();

        // --sql: run GetArtistAsync through a logging context so every split-query leg
        // prints its own "Executed DbCommand (Xms)". Run it twice; the second pass is
        // warm and shows where the wall-clock actually goes. Then exit.
        if (opts.ContainsKey(key: "sql"))
        {
            LoggingMediaContextFactory loggingFactory = new();
            Console.WriteLine(value: "=== GetArtistAsync SQL (cold) ===");
            await new MusicRepository(contextFactory: loggingFactory).GetArtistAsync(userId: userId, id: artistId);
            Console.WriteLine();
            Console.WriteLine(value: "=== GetArtistAsync SQL (warm) ===");
            Stopwatch sqlSw = Stopwatch.StartNew();
            await new MusicRepository(contextFactory: loggingFactory).GetArtistAsync(userId: userId, id: artistId);
            sqlSw.Stop();
            Console.WriteLine();
            Console.WriteLine(value: $"=== warm total: {sqlSw.ElapsedMilliseconds}ms ===");
            return 0;
        }

        List<BenchmarkCase> cases =
        [
            new(
                Name: "genres        GetGenresWithCountsAsync",
                Run: async () =>
                {
                    await using MediaContext c = factory.CreateDbContext();
                    return await new GenreRepository(context: c).GetGenresWithCountsAsync(
                        userId: userId,
                        language: language,
                        take: 21,
                        page: 0
                    );
                }
            ),
            new(
                Name: "home          GetHome",
                Run: async () =>
                {
                    await using MediaContext c = factory.CreateDbContext();
                    return await new HomeRepository(context: c, contextFactory: factory).GetHome(userId: userId, language: language, take: 21, page: 0);
                }
            ),
            new(
                Name: "home          GetHomeParallelDataAsync",
                Run: async () =>
                {
                    await using MediaContext c = factory.CreateDbContext();
                    return await new HomeRepository(context: c, contextFactory: factory).GetHomeParallelDataAsync(
                        userId: userId,
                        language: language,
                        country: country
                    );
                }
            ),
            new(
                Name: "home          GetHomeGenresAsync",
                Run: async () =>
                {
                    await using MediaContext c = factory.CreateDbContext();
                    return await new HomeRepository(context: c, contextFactory: factory).GetHomeGenresAsync(
                        userId: userId,
                        language: language,
                        take: 21,
                        page: 0
                    );
                }
            ),
            new(
                Name: "screensaver   GetScreensaverImagesAsync",
                Run: async () =>
                {
                    await using MediaContext c = factory.CreateDbContext();
                    return await new HomeRepository(context: c, contextFactory: factory).GetScreensaverImagesAsync(userId: userId);
                }
            ),
            new(
                Name: "libraries     GetRandomTvCardAsync",
                Run: async () =>
                    await new LibraryRepository(contextFactory: factory).GetRandomTvCardAsync(
                        userId: userId,
                        language: language,
                        country: country
                    )
            ),
            new(
                Name: "libraries     GetRandomMovieCardAsync",
                Run: async () =>
                    await new LibraryRepository(contextFactory: factory).GetRandomMovieCardAsync(
                        userId: userId,
                        language: language,
                        country: country
                    )
            ),
            new(
                Name: "libraries     GetLibraryMovieCardsAsync",
                Run: async () =>
                    await new LibraryRepository(contextFactory: factory).GetLibraryMovieCardsAsync(
                        userId: userId,
                        libraryId: libraryId,
                        country: country,
                        take: 10,
                        skip: 0
                    )
            ),
            new(
                Name: "libraries/tv  GetLibraryTvCardsAsync",
                Run: async () =>
                    await new LibraryRepository(contextFactory: factory).GetLibraryTvCardsAsync(
                        userId: userId,
                        libraryId: libraryId,
                        country: country,
                        take: 50,
                        skip: 0
                    )
            ),
            new(
                Name: "video/tv      GetTvAsync",
                Run: async () =>
                    await new TvShowRepository(contextFactory: factory).GetTvAsync(userId: userId, id: tvId, language: language, country: country)
            ),
            new(
                Name: "video/movie   GetMovieAsync",
                Run: async () =>
                    await new MovieRepository(
                        contextFactory: factory,
                        logger: NullLogger<MovieRepository>.Instance
                    ).GetMovieAsync(userId: userId, id: movieId, language: language, country: country)
            ),
            new(
                Name: "music/artist  GetArtistAsync",
                Run: async () => await new MusicRepository(contextFactory: factory).GetArtistAsync(userId: userId, id: artistId)
            ),
            // Full server-side response cost for the artist endpoint: load + build the
            // exact ArtistResponseItemDto the controller returns + serialize it with the
            // API's Newtonsoft settings. This is everything except HTTP/auth/network, so
            // it is the honest "is the endpoint under the budget" number.
            new(
                Name: "music/artist  full response build+serialize",
                Run: async () =>
                {
                    Artist? artist = await new MusicRepository(contextFactory: factory).GetArtistAsync(
                        userId: userId,
                        id: artistId
                    );
                    if (artist is null)
                        return 0;
                    Api.DTOs.Music.ArtistResponseItemDto dto = new(artist: artist, userId: userId, country: country);
                    Newtonsoft.Json.JsonSerializerSettings settings = new()
                    {
                        ReferenceLoopHandling = Newtonsoft.Json.ReferenceLoopHandling.Ignore,
                        Converters = { new Newtonsoft.Json.Converters.StringEnumConverter() },
                    };
                    return Newtonsoft.Json.JsonConvert.SerializeObject(value: dto, settings: settings).Length;
                }
            ),
            // A/B for the color-palette response cost. Both serialize with Newtonsoft
            // (as the real response does); the only difference is how the stored JSON
            // string becomes a serializable value. Old: parse into a typed ColorPalette
            // then reflect it back to JSON. New: parse straight to a JToken and emit it
            // verbatim. Same output, one fewer reflection pass per palette.
            new(
                Name: "palette       roundtrip Deserialize+Serialize (old)",
                Run: () =>
                    Task.FromResult<object?>(
                        result: artistPalettes.Count(predicate: palette =>
                            Newtonsoft.Json.JsonConvert.SerializeObject(
                                value: ColorPalette.FromJsonOrNull(json: palette)
                            )
                                is not null
                        )
                    )
            ),
            new(
                Name: "palette       ToRaw passthrough (new)",
                Run: () =>
                    Task.FromResult<object?>(
                        result: artistPalettes.Count(predicate: palette =>
                            Newtonsoft.Json.JsonConvert.SerializeObject(value: palette.ToRaw()) is not null
                        )
                    )
            ),
            new(
                Name: "search        SearchTrackIdsAsync",
                Run: async () => await new MusicRepository(contextFactory: factory).SearchTrackIdsAsync(normalizedQuery: searchTerm)
            ),
            new(
                Name: "search        SearchArtistIdsAsync",
                Run: async () => await new MusicRepository(contextFactory: factory).SearchArtistIdsAsync(normalizedQuery: searchTerm)
            ),
            new(
                Name: "search        SearchAlbumIdsAsync",
                Run: async () => await new MusicRepository(contextFactory: factory).SearchAlbumIdsAsync(normalizedQuery: searchTerm)
            ),
            new(
                Name: "search        SearchPlaylistIdsAsync",
                Run: async () => await new MusicRepository(contextFactory: factory).SearchPlaylistIdsAsync(normalizedQuery: searchTerm)
            ),
            new(
                Name: "search        SearchTrackCards(uncapped)",
                Run: async () =>
                {
                    MusicRepository repo = new(contextFactory: factory);
                    List<Guid> t = await repo.SearchTrackIdsAsync(normalizedQuery: searchTerm);
                    return await repo.SearchTrackCardsAsync(trackIds: t, userId: userId, country: country);
                }
            ),
            new(
                Name: "search        SearchTrackCards(cap50)",
                Run: async () =>
                {
                    MusicRepository repo = new(contextFactory: factory);
                    List<Guid> t = (await repo.SearchTrackIdsAsync(normalizedQuery: searchTerm)).Take(count: 50).ToList();
                    return await repo.SearchTrackCardsAsync(trackIds: t, userId: userId, country: country);
                }
            ),
            new(
                Name: "search        SearchMusicFullDataAsync",
                Run: async () =>
                {
                    MusicRepository repo = new(contextFactory: factory);
                    List<Guid> a = await repo.SearchArtistIdsAsync(normalizedQuery: searchTerm);
                    List<Guid> al = await repo.SearchAlbumIdsAsync(normalizedQuery: searchTerm);
                    List<Guid> p = await repo.SearchPlaylistIdsAsync(normalizedQuery: searchTerm);
                    List<Guid> t = await repo.SearchTrackIdsAsync(normalizedQuery: searchTerm);
                    return await repo.SearchMusicFullDataAsync(artistIds: a, albumIds: al, playlistIds: p, trackIds: t);
                }
            ),
        ];

        if (filter is not null)
            cases = cases
                .Where(predicate: c => c.Name.Contains(value: filter, comparisonType: StringComparison.OrdinalIgnoreCase))
                .ToList();

        List<BenchmarkResult> results = [];
        Console.WriteLine(
            value: $"{"query", -42} {"cold", 8} {"p50", 8} {"p95", 8} {"rows", 7}  {"signature", 16}"
        );
        Console.WriteLine(value: new string(c: '-', count: 96));
        foreach (BenchmarkCase bench in cases)
        {
            BenchmarkResult res = await Measure(bench: bench, samples: samples);
            results.Add(item: res);
            if (res.Error is not null)
                Console.WriteLine(value: $"{res.Name, -42} ERROR: {res.Error}");
            else
                Console.WriteLine(
                    value: $"{res.Name, -42} {res.ColdMs, 6:F0}ms {res.WarmMedianMs, 6:F0}ms {res.WarmP95Ms, 6:F0}ms {res.Rows, 7}  {res.Signature, 16}"
                );
        }

        Console.WriteLine(value: new string(c: '-', count: 96));
        Console.WriteLine(value: "Ranked by warm p50 (slowest first):");
        foreach (
            BenchmarkResult r in results
                .Where(predicate: r => r.Error is null)
                .OrderByDescending(keySelector: r => r.WarmMedianMs)
        )
            Console.WriteLine(value: $"  {r.WarmMedianMs, 8:F0}ms  {r.Name.Trim()}");

        // Data-integrity guard: compare each query's content fingerprint against a
        // prior run. A changed signature means the result SET changed (rows missing,
        // added, or altered) — the exact failure mode an "optimization" must not cause.
        string? baseline = opts.GetValueOrDefault(key: "baseline");
        if (baseline is not null && File.Exists(path: baseline))
        {
            List<BenchmarkResult>? prev = JsonSerializer.Deserialize<List<BenchmarkResult>>(
                json: await File.ReadAllTextAsync(path: baseline)
            );
            bool anyChanged = false;
            Console.WriteLine(value: $"\nData-integrity check vs baseline ({baseline}):");
            foreach (BenchmarkResult r in results.Where(predicate: r => r.Error is null))
            {
                BenchmarkResult? b = prev?.FirstOrDefault(predicate: p => p.Name == r.Name);
                if (b is null)
                    Console.WriteLine(value: $"  NEW      {r.Name.Trim()} ({r.Rows} rows)");
                else if (b.Signature != r.Signature)
                {
                    anyChanged = true;
                    Console.WriteLine(
                        value: $"  CHANGED  {r.Name.Trim()}  rows {b.Rows} -> {r.Rows}  sig {b.Signature} -> {r.Signature}"
                    );
                }
                else
                    Console.WriteLine(value: $"  OK       {r.Name.Trim()} (unchanged, {r.Rows} rows)");
            }

            if (anyChanged)
                Console.WriteLine(
                    value: "\nWARNING: a query's result set changed vs baseline — verify no data went missing."
                );
        }

        string outPath = opts.GetValueOrDefault(key: "json", defaultValue: "benchmark-results.json");
        await File.WriteAllTextAsync(
            path: outPath,
            contents: JsonSerializer.Serialize(value: results, options: new JsonSerializerOptions { WriteIndented = true })
        );
        Console.WriteLine(value: $"\nWrote {outPath} (use it as --baseline on the next run)");
        return 0;
    }

    private static async Task<BenchmarkResult> Measure(BenchmarkCase bench, int samples)
    {
        try
        {
            object? last;
            Stopwatch sw = Stopwatch.StartNew();
            last = await bench.Run(); // cold: query-plan compile + cold page cache
            sw.Stop();
            double cold = sw.Elapsed.TotalMilliseconds;

            List<double> warm = new(capacity: samples);
            for (int i = 0; i < samples; i++)
            {
                sw.Restart();
                last = await bench.Run();
                sw.Stop();
                warm.Add(item: sw.Elapsed.TotalMilliseconds);
            }

            warm.Sort();
            (int rows, string signature) = Fingerprint(result: last); // untimed
            return new(
                Name: bench.Name.Trim(),
                ColdMs: cold,
                WarmMedianMs: Median(sorted: warm),
                WarmP95Ms: Percentile(sorted: warm, p: 95),
                WarmMinMs: warm[index: 0],
                WarmMaxMs: warm[^1],
                Rows: rows,
                Signature: signature,
                Error: null
            );
        }
        catch (Exception ex)
        {
            return new(
                Name: bench.Name.Trim(),
                ColdMs: 0,
                WarmMedianMs: 0,
                WarmP95Ms: 0,
                WarmMinMs: 0,
                WarmMaxMs: 0,
                Rows: 0,
                Signature: "-",
                Error: $"{ex.GetType().Name}: {ex.Message}"
            );
        }
    }

    // Order-independent content fingerprint of a query result. For a collection,
    // each item is serialized and hashed, the hashes are sorted, then combined — so
    // the signature is invariant to row order but sensitive to any missing, added,
    // or changed row. Cycles in EF entities are dropped rather than throwing.
    private static (int Count, string Signature) Fingerprint(object? result)
    {
        if (result is null)
            return (0, "-");

        JsonSerializerOptions opts = new()
        {
            ReferenceHandler = ReferenceHandler.IgnoreCycles,
            MaxDepth = 64,
        };

        if (result is IEnumerable enumerable and not string)
        {
            List<string> hashes = [];
            foreach (object? item in enumerable)
                hashes.Add(item: SafeHash(item: item, opts: opts));
            hashes.Sort(comparer: StringComparer.Ordinal);
            return (hashes.Count, Sha(value: string.Join(separator: '\n', values: hashes))[..16]);
        }

        return (1, SafeHash(item: result, opts: opts)[..16]);
    }

    private static string SafeHash(object? item, JsonSerializerOptions opts)
    {
        try
        {
            return Sha(value: JsonSerializer.Serialize(value: item, options: opts));
        }
        catch
        {
            return Sha(value: item?.ToString() ?? "null");
        }
    }

    private static string Sha(string value) =>
        Convert.ToHexStringLower(inArray: SHA256.HashData(source: Encoding.UTF8.GetBytes(s: value)));

    private static double Median(List<double> sorted)
    {
        int n = sorted.Count;
        if (n == 0)
            return 0;
        return n % 2 == 1 ? sorted[index: n / 2] : (sorted[index: n / 2 - 1] + sorted[index: n / 2]) / 2.0;
    }

    private static double Percentile(List<double> sorted, int p)
    {
        if (sorted.Count == 0)
            return 0;
        int rank = (int)Math.Ceiling(a: p / 100.0 * sorted.Count) - 1;
        return sorted[index: Math.Clamp(value: rank, min: 0, max: sorted.Count - 1)];
    }

    private static Dictionary<string, string> ParseArgs(string[] args)
    {
        Dictionary<string, string> opts = new(comparer: StringComparer.OrdinalIgnoreCase);
        for (int i = 0; i < args.Length; i++)
        {
            string a = args[i];
            if (!a.StartsWith(value: "--", comparisonType: StringComparison.Ordinal))
                continue;
            string key = a[2..];
            int eq = key.IndexOf(value: '=');
            if (eq >= 0)
            {
                opts[key: key[..eq]] = key[(eq + 1)..];
            }
            else if (i + 1 < args.Length && !args[i + 1].StartsWith(value: "--", comparisonType: StringComparison.Ordinal))
            {
                opts[key: key] = args[++i];
            }
            else
            {
                opts[key: key] = "true";
            }
        }

        return opts;
    }

    private static bool IsDebug()
    {
#if DEBUG
        return true;
#else
        return false;
#endif
    }
}
