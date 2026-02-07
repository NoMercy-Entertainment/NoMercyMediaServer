## 6. Performance

> Items that cause slow responses, excessive resource use, or scalability issues.

### CRIT-02: Client-Side Filtering on Full Table Scans
- **File**: `src/NoMercy.Data/Repositories/MusicRepository.cs:435-480`
- **Problem**: Search methods load ALL entities into memory, then filter client-side
- **Impact**: O(n) search with full table materialization; catastrophic with large libraries
- **Example**:
  ```csharp
  return context.Artists
      .AsNoTracking()
      .Select(artist => new { artist.Id, artist.Name })
      .ToList()  // Loads ALL artists into memory
      .Where(artist => artist.Name.NormalizeSearch().Contains(normalizedQuery))  // Client-side
      .Select(artist => artist.Id)
      .ToList();
  ```
- **Fix**: Move filtering to database with `EF.Functions.Like()` or full-text search

  **CRITICAL WARNING**: The `NormalizeSearch()` method likely applies custom normalization (accent removal, case folding, special character handling) that SQLite's built-in `LIKE` or `COLLATE NOCASE` may not replicate exactly. Search results **MUST remain identical** after migration.

  **Migration approach**:
  1. **Before any code changes**, capture golden-master snapshots of search results for a comprehensive set of queries (including accented characters, mixed case, partial matches, empty strings, special characters)
  2. Investigate what `NormalizeSearch()` does — if it's just `ToLower().Trim()`, `EF.Functions.Like()` with `COLLATE NOCASE` may suffice
  3. If `NormalizeSearch()` does accent folding or Unicode normalization, consider:
     - SQLite custom collation via `CreateCollation()`
     - A pre-computed `SearchName` column with normalized values indexed for fast lookup
     - SQLite FTS5 for full-text search with custom tokenizer
  4. **After migration**, run the golden-master comparison to verify 100% result equivalence
  5. Only then remove the client-side path

  ```csharp
  // Option A: Simple case — if NormalizeSearch is just case-insensitive
  return context.Artists
      .AsNoTracking()
      .Where(artist => EF.Functions.Like(artist.Name, $"%{query}%"))
      .Select(artist => artist.Id)
      .ToListAsync();

  // Option B: Pre-computed column for complex normalization
  return context.Artists
      .AsNoTracking()
      .Where(artist => artist.SearchName.Contains(normalizedQuery))
      .Select(artist => artist.Id)
      .ToListAsync();
  ```

- **Tests Required**:
  - [ ] **Characterization test**: Capture search results for 50+ diverse queries BEFORE any changes
  - [ ] Unit test: Verify SQL query contains WHERE clause (inspect query log)
  - [ ] **Equivalence test**: Compare post-migration results against golden-master snapshots (must be 100% match)
  - [ ] Equivalence test: Verify accent/Unicode/special character queries return same results
  - [ ] Performance test: Search with 10,000+ artists stays under 100ms
  - [ ] API test: `/api/v1/search` returns correct results after migration

### CRIT-03: Cartesian Explosion from 55+ Include Chains
- **Files**: `src/NoMercy.Data/Repositories/TvShowRepository.cs:16-94`, `src/NoMercy.Data/Repositories/CollectionRepository.cs:132-209`
- **Problem**: Compiled queries with 55+ Include/ThenInclude chains with SplitQuery mode generating 55+ separate database queries per request
- **Impact**: TV show with 10 seasons x 10 episodes = 200+ rows materialized per query; extreme memory and query overhead

  **CRITICAL WARNING**: Split queries (`AsSplitQuery()`) is the **default** configuration for this project and lazy loading is **disabled**. This has important implications:

  - **Split queries are already in use**: Each Include chain generates a separate SQL query (not a cartesian join). The real cost is 55+ separate round-trips to SQLite, not row explosion. This changes the optimization strategy.
  - **Lazy loading is disabled**: You **cannot** remove Include chains and expect related data to load on access — it won't. Any Include removed means that related data simply won't be available, causing `null` navigation properties and broken responses.
  - **Do NOT blindly remove Includes**: Every Include chain must be verified as actually needed by the endpoint's response DTO before removal. If the front-end consumes the data, the Include must stay.

  **Safe optimization approach**:
  1. **Audit which Includes are actually consumed**: Trace each Include's navigation property through the DTO mapping to verify it appears in the API response. Only Includes that map to no output field can be removed.
  2. **Use projection (`.Select()`) instead of Include**: Project directly into DTOs, which allows EF Core to generate a single optimized query that only fetches needed columns — no Include chains needed at all.
  3. **Split list vs. detail endpoints**: List endpoints need far fewer fields than detail endpoints. Create focused queries for each.
  4. **Keep split query mode**: Since lazy loading is off, split queries avoid cartesian explosion while still loading all needed data. The optimization is reducing *which* data loads, not *how* it loads.

  ```csharp
  // WRONG: Removing Includes without projection (data will be null!)
  return context.TvShows.FirstAsync(s => s.Id == id);
  // s.Seasons, s.Episodes, s.Cast — all null!

  // RIGHT: Projection eliminates need for Includes entirely
  return context.TvShows
      .Where(s => s.Id == id)
      .Select(s => new TvShowDetailDto
      {
          Id = s.Id,
          Title = s.Title,
          Seasons = s.Seasons.Select(se => new SeasonDto { ... }).ToList(),
          // EF Core generates optimized SQL — no Include needed
      })
      .FirstAsync();
  ```

- **Fix**: Use projection DTOs to replace Include chains; split list vs detail queries
- **Tests Required**:
  - [ ] **Characterization test**: Capture full JSON response for every TV show/collection endpoint BEFORE changes
  - [ ] Integration test: Verify TV show detail endpoint returns all required data (compare against snapshot)
  - [ ] Integration test: Verify TV show list endpoint returns paginated data correctly
  - [ ] **Equivalence test**: Post-migration JSON response must match pre-migration snapshot field-by-field
  - [ ] Integration test: Verify no null navigation properties where data previously existed
  - [ ] Performance test: TV show detail query completes under 500ms
  - [ ] API test: Verify all fields present in response JSON

### CRIT-12: Synchronous Blocking in Async Playlist Generation
- **File**: `src/NoMercy.Encoder/Core/HLSPlaylistGenerator.cs:166,300`
- **Problem**: `Shell.ExecStdOutSync()` called inside LINQ `Select()` for every video file
- **Impact**: Blocks thread for 100+ seconds with large media libraries

  **IMPORTANT CONTEXT — One-Time Build, Not Hot Path**: This code runs **once per encoding completion** to build the HLS master playlist. It's not called during streaming or on every request. The sync calls probe each video variant's codec profile/level (needed for HLS CODECS attribute). Converting to async requires breaking the LINQ chain and adds complexity for minimal gain.

  **Pragmatic approach**:
  - If the number of video variants is small (typically 3-6 quality levels), the blocking is ~1-3 seconds total — acceptable for a one-time operation
  - If it grows to many variants, consider parallel probing with a semaphore

  ```csharp
  // Option A: Keep sync for small variant counts (practical)
  // Current code is fine for <= 10 variants

  // Option B: Parallel probing for large variant counts
  SemaphoreSlim semaphore = new(3);  // Max 3 concurrent ffprobe calls
  List<Task<ProbeResult>> probeTasks = videoFiles.Select(async file =>
  {
      await semaphore.WaitAsync();
      try { return await Shell.ExecStdOutAsync(AppFiles.FfProbePath, ...); }
      finally { semaphore.Release(); }
  }).ToList();
  ProbeResult[] results = await Task.WhenAll(probeTasks);
  ```

- **Fix**: Keep sync for small variant counts; add async path with semaphore for large libraries
- **Severity**: Downgraded from Critical to **Medium** — one-time build operation
- **Tests Required**:
  - [ ] Unit test: HLS playlist contains correct CODECS attributes after generation
  - [ ] Performance test: Playlist generation for 10+ variants stays reasonable

### HIGH-01: IQueryable Returned Without Async Execution
- **File**: `src/NoMercy.Data/Repositories/MusicRepository.cs` (13 methods)
- **Problem**: Methods named `*Async` return `IQueryable<T>` without executing; DbContext disposes before iteration

  **IMPORTANT CONTEXT — Deferred Execution is Intentional**: Some of these methods return `IQueryable<T>` so that **callers can apply pagination** (`.Take(36)` for carousels, `.OrderBy()` for sorting) before the query hits the database. If we eagerly materialize with `.ToListAsync()`, we'd load thousands of records when only 36 are needed.

  **The real fix is to separate two patterns**:
  1. **Browsable queries** (caller adds pagination): Keep `IQueryable<T>`, but rename to drop `Async` suffix since they're not async yet
  2. **Terminal queries** (method returns final data): Convert to `Task<List<T>>` with `.ToListAsync()`

  ```csharp
  // Pattern 1: Browsable (caller paginates) — rename to drop Async
  public IQueryable<Album> GetLatestAlbums(Guid userId)
  {
      return _mediaContext.Albums
          .Where(a => a.LibraryUsers.Any(lu => lu.UserId == userId))
          .OrderByDescending(a => a.CreatedAt);
      // Caller adds: .Take(36).ToListAsync()
  }

  // Pattern 2: Terminal (returns final data) — keep Async, materialize
  public Task<List<Artist>> GetArtistsByIdsAsync(Guid userId, List<Guid> ids)
  {
      return _mediaContext.Artists
          .Where(a => ids.Contains(a.Id))
          .AsNoTracking()
          .ToListAsync();
  }
  ```

- **Fix**: Audit each method — keep `IQueryable` for browsable queries (rename to drop `Async`); materialize terminal queries
- **Tests Required**:
  - [ ] Unit test: Browsable query methods return valid IQueryable that can be paginated
  - [ ] Unit test: Terminal query methods return materialized data
  - [ ] Integration test: No "disposed context" exceptions under load

### HIGH-04: Loop-Based Database Saves
- **Files**: `src/NoMercy.Data/Logic/FileLogic.cs:88`, `src/NoMercy.Data/Jobs/StorageJob.cs:76-103`
- **Problem**: Individual `SaveChanges()` per item instead of batch

  **IMPORTANT CONTEXT — Intentional Error Isolation & Progress Tracking**: Per-item saves serve two purposes:
  1. **Error isolation**: If one file in a 1000-file TV season fails to save, the other 999 are already committed. A batch save would lose all progress on any single failure.
  2. **Crash recovery**: If the server crashes mid-scan, partially processed files are already in the database. Only unprocessed files need re-scanning on restart.
  3. **Progress visibility**: The UI can show "Processed 150/1000 files" because each save is committed immediately.

  **Hybrid approach** (preserves intent, improves throughput):
  ```csharp
  // Batch in chunks of 50-100, not all-or-nothing
  List<VideoFile> batch = new();
  foreach (MediaFile item in items)
  {
      batch.Add(PrepareVideoFile(item));
      if (batch.Count >= 50)
      {
          await _mediaContext.VideoFiles.UpsertRange(batch).RunAsync();
          batch.Clear();
          // Progress: can update UI here
      }
  }
  if (batch.Count > 0)
      await _mediaContext.VideoFiles.UpsertRange(batch).RunAsync();
  ```

- **Fix**: Use chunked batch saves (50-100 items) for a balance of throughput and crash resilience
- **Severity**: Downgraded from High to **Medium** — intentional trade-off, hybrid approach is optional
- **Tests Required**:
  - [ ] Integration test: Chunked batch save processes 100 items correctly
  - [ ] Integration test: Partial failure only loses current chunk, not all progress
  - [ ] Integration test: Progress reporting works with chunked saves

### HIGH-05: No Response Caching
- **File**: `src/NoMercy.Server/AppConfig/ServiceConfiguration.cs:267` (commented out)
- **Problem**: All API responses hit database every time; no ETags, no Cache-Control

  **IMPORTANT CONTEXT — Intentionally Disabled for Real-Time Features**: Response caching was commented out because the app has real-time features (SignalR hubs for video/music playback, live dashboard updates). Global caching would cause:
  - Stale "Continue Watching" data (user watches episode → still shows old progress)
  - Cached search results from old database state
  - Users potentially seeing other users' personalized data (if not keyed by user)

  **The fix is per-endpoint caching, not global**:
  ```csharp
  // DON'T: Global caching (breaks real-time features)
  // services.AddResponseCaching();

  // DO: Per-endpoint caching for static-ish data
  [ResponseCache(Duration = 300, VaryByQueryKeys = new[] { "userId" })]
  public async Task<IActionResult> GetGenres() { ... }

  [ResponseCache(Duration = 3600)]
  public async Task<IActionResult> GetServerInfo() { ... }

  // NEVER cache: Continue Watching, Now Playing, Search, User-specific data
  [ResponseCache(NoStore = true)]
  public async Task<IActionResult> GetContinueWatching() { ... }
  ```

- **Fix**: Add per-endpoint `[ResponseCache]` attributes to static endpoints; leave real-time endpoints uncached
- **Tests Required**:
  - [ ] API test: Cacheable endpoints (genres, server info) return Cache-Control headers
  - [ ] API test: Real-time endpoints (continue watching, now playing) have `no-store`
  - [ ] API test: Cached data is correctly keyed by userId

### HIGH-08: Unbounded Progress Update Rate (SignalR Flooding)
- **File**: `src/NoMercy.Encoder/FfMpeg.cs:275-278`
- **Problem**: Sends progress to all clients at FFmpeg output rate (~100/sec); calls `GetThumbnail()` (disk I/O) on every update

  **IMPORTANT CONTEXT — Live Thumbnail Preview is Intentional**: `GetThumbnail()` scans the output folder for the most recently written thumbnail file, providing a **live preview of the current encoding frame** in the dashboard. The intent is correct — users want to see what's being encoded in real-time.

  **The optimization is sampling, not removal**:
  ```csharp
  private DateTime _lastProgressUpdate = DateTime.MinValue;
  private string? _lastThumbnail;

  // In progress handler:
  if ((DateTime.UtcNow - _lastProgressUpdate).TotalMilliseconds < 500)
      return;  // Skip — 2 updates/sec max

  _lastProgressUpdate = DateTime.UtcNow;
  string thumbnail = GetThumbnail(meta);  // Only called 2x/sec now
  ```

  The UI won't perceive any difference at 2 updates/sec vs 100/sec, but SignalR bandwidth and disk I/O drop 50x.

- **Fix**: Sample progress updates to 2/sec; keep thumbnail lookup (it's the feature)
- **Tests Required**:
  - [ ] Unit test: Progress updates throttled to max 2 per second
  - [ ] Unit test: Thumbnail still reflects most recent frame
  - [ ] Integration test: Dashboard shows smooth progress bar with throttled updates

### MED-01: Client-Side Deduplication
- **File**: `src/NoMercy.Data/Repositories/HomeRepository.cs:114-116`

  **IMPORTANT CONTEXT — Complex Multi-Path Deduplication**: A user can have progress on the same movie through different paths (standalone movie, part of collection, part of special). Each path creates a separate `UserData` row. The `DistinctBy` on `{ MovieId, CollectionId, TvId, SpecialId }` handles this correctly. A simple SQL `GROUP BY` would miss the multi-path deduplication logic.

  **Optimization**: Instead of loading all user data then deduplicating, project to minimal keys first:
  ```csharp
  // Load only the keys, deduplicate, then hydrate full data for unique keys
  List<Guid> uniqueMovieIds = await mediaContext.UserData
      .Where(ud => ud.UserId == userId && ud.MovieId != null)
      .Select(ud => ud.MovieId!.Value)
      .Distinct()
      .ToListAsync();
  // Then load full movie data for uniqueMovieIds only
  ```

- **Fix**: Project to minimal keys before deduplication to reduce memory; keep client-side logic for correctness

### MED-02: Client-Side Count Operations
- **Files**: `src/NoMercy.Data/Repositories/LibraryRepository.cs:248,288`, `CollectionRepository.cs:87`, `GenreRepository.cs:100-107`
- **Fix**: Use database `COUNT()` aggregation

### MED-03: Repeated Library Access Control Predicate
- **Files**: All repositories (~50+ occurrences)
- **Fix**: Create `ForUser()` IQueryable extension method

### MED-11: Localizer Created Per Request
- **File**: `src/NoMercy.Api/Middleware/LocalizationMiddleware.cs:31-34`
- **Fix**: Singleton localizer; load XML once

### MED-12: Regex Created in Loop
- **File**: `src/NoMercy.Encoder/FfMpeg.cs:157`
- **Fix**: Use `[GeneratedRegex]` source generator

### MED-16: Sequential Startup Tasks
- **File**: `src/NoMercy.Setup/Start.cs:30-88`
- **Fix**: Parallelize independent startup tasks with `Task.WhenAll()`

### 14.4 Rate Limiting Assessment

| Provider | Concurrent | Interval (ms) | API Limit | Assessment |
|----------|-----------|---------------|-----------|------------|
| TMDB | 50 | 1000 | ~40/sec | Slightly over |
| TVDB | 50 | 1000 | Unknown | Aggressive |
| MusicBrainz | 40 | 1000 | **1/sec** | **Far too aggressive** |
| AcoustId | 3 | 1000 | 3/sec | Appropriate |
| OpenSubtitles | 1 | 1000 | ~1/sec | Appropriate |
| FanArt | 3 | 1000 | Unknown | Reasonable |
| CoverArt | 3 | 1000 | 1/sec | Slightly over |
| Lrclib | 1 | 1000 | Unknown | Conservative |
| MusixMatch | 2 | 1000 | Unknown | Reasonable |
| Tadb | 2 | 1000 | Unknown | Reasonable |

**MusicBrainz Concurrent=40 will definitely trigger rate limiting.** MusicBrainz enforces 1 request/second for unauthenticated clients and the `"anonymous"` user agent violates their API guidelines.

#### SYS-H04: MediaScan.cs int overflow for >2GB files
- **File**: `src/NoMercy.NmSystem/MediaScan.cs:325`
- **Problem**: `(int)new FileInfo(file).Length` — int overflow for >2GB files (common for media)
- **Fix**: Use `long` for file size

#### SYS-H06: Download.cs entire file loaded into memory
- **File**: `src/NoMercy.NmSystem/Download.cs:25`
- **Problem**: Entire file loaded into memory via `ReadAsByteArrayAsync()` — FFmpeg/Whisper models can be 1GB+
- **Fix**: Stream to disk with `CopyToAsync`

#### SYS-M11: Storage.cs GetDirectorySize slow for large libraries
- **File**: `src/NoMercy.NmSystem/Storage.cs:200-207`
- **Problem**: `GetDirectorySize` enumerates all files recursively — slow for large libraries
- **Fix**: Cache directory sizes with periodic refresh

#### SYS-M16: Binaries.cs Sequential downloads could be Task.WhenAll
- **File**: `src/NoMercy.Setup/Binaries.cs:39-46`
- **Problem**: Sequential downloads could be `Task.WhenAll`
- **Fix**: Parallelize independent binary downloads

---

