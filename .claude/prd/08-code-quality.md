## 8. Code Quality & Cleanup

> Code organization, naming, duplication, readability, and style issues.

### HIGH-02: Pagination Inside Include()
- **File**: `src/NoMercy.Data/Repositories/LibraryRepository.cs:71-86,88-105`
- **Problem**: `.Take(take)` applied inside `Include()` limits per-parent, not total results

  **IMPORTANT CONTEXT — Intentional Carousel Limits**: The `.Take(take)` inside `Include()` is by design. The home page shows carousels like "Latest Movies (36)" and "Latest TV Shows (36)". The `.Take(36)` limits how many items load **per carousel**, preventing the page from loading the entire 10,000+ item library into memory. This is a **correct memory optimization**.

  The original PRD problem description was inaccurate — the "per-parent" limiting is the desired behavior for carousel-style UIs.

  **What to actually fix**: Ensure the UI's expected carousel size matches the query's `.Take()` value. If the UI shows 36 cards max, the query should `.Take(36)`. Document the relationship.

- **Fix**: Document the intent; verify `.Take()` values match UI carousel sizes; no code change needed unless sizes mismatch
- **Severity**: Downgraded from High to **Low** — intentional behavior, just needs documentation
- **Tests Required**:
  - [ ] API test: Library endpoint returns exactly `take` items per carousel
  - [ ] API test: Carousel item count matches frontend expectation

### HIGH-11: Unbounded Cache Growth
- **File**: `src/NoMercy.Providers/Helpers/CacheController.cs:13`
- **Problem**: `ConcurrentDictionary<string, SemaphoreSlim>` grows without bound per URL

  **IMPORTANT CONTEXT — Dev-Only Cache**: The cache is **disabled in production** (`Config.IsDev == false` check). In development, it prevents burning external API quotas (TMDB, MusicBrainz) by caching responses to disk with a 1-day TTL. The unbounded growth is a dev convenience, not a production issue.

  **Still worth adding a size limit** to prevent filling disk during long dev sessions:
  ```csharp
  const long MaxCacheSizeBytes = 500_000_000;  // 500MB

  private static void PruneCache()
  {
      DirectoryInfo cacheDir = new(AppFiles.ApiCachePath);
      FileInfo[] files = cacheDir.GetFiles()
          .OrderBy(f => f.CreationTime)
          .ToArray();
      long totalSize = files.Sum(f => f.Length);

      foreach (FileInfo file in files)
      {
          if (totalSize <= MaxCacheSizeBytes) break;
          totalSize -= file.Length;
          file.Delete();
      }
  }
  ```

- **Fix**: Add 500MB size limit with LRU pruning; keep dev-only behavior
- **Severity**: Downgraded from High to **Low** — dev-only cache
- **Tests Required**:
  - [ ] Unit test: Cache prunes oldest entries when exceeding 500MB

### HIGH-13: Cron Jobs Double Registration
- **Files**: `src/NoMercy.Server/AppConfig/ServiceConfiguration.cs:63-89`, `ApplicationConfiguration.cs:30-49`
- **Problem**: Each cron job registered in both ServiceConfiguration AND ApplicationConfiguration
- **Fix**: Choose one registration site
- **Tests Required**:
  - [ ] Integration test: Each cron job runs exactly once per schedule

### HIGH-19: FFmpeg Process Termination Without Exception Handling
- **File**: `src/NoMercy.Encoder/Ffprobe.cs:174-186`
- **Problem**: `Kill(entireProcessTree: true)` can throw; process becomes zombie
- **Revalidation note**: Most `Kill()` calls across the encoder codebase already have exception handling. This specific instance in `Ffprobe.cs` is one of the few remaining unprotected calls. Downgraded to **Medium** — only the Ffprobe timeout path needs the try-catch wrapper.
- **Fix**: Wrap this specific Kill call in try-catch
- **Severity**: Downgraded to **Medium**
- **Tests Required**:
  - [ ] Unit test: Process cleanup succeeds even when Kill fails

### MED-08: Dual JSON Serializer Configuration
- **File**: `src/NoMercy.Server/AppConfig/ServiceConfiguration.cs:246-258`
- **Fix**: Choose one (System.Text.Json preferred), use consistently

### MED-19: Duplicate RequestLocalization Call
- **File**: `src/NoMercy.Server/AppConfig/ApplicationConfiguration.cs:62,74`
- **Fix**: Remove redundant call

### LOW-01: Method naming inconsistency (async suffix on non-async methods)
### LOW-02: Unused compiled queries in LibraryRepository
### LOW-03: String allocation in hot loops (`String.Replace()`)
### LOW-04: Bare catch blocks in HLSPlaylistGenerator swallowing all exceptions
### LOW-05: Console.WriteLine instead of structured logging
### LOW-06: Image dimension extraction loading full image (use `Image.Identify()`)
### LOW-07: `.Count()` LINQ method on already-materialized List (use `.Count` property)
### LOW-08: Unused route parameters in controller methods
### LOW-09: DnsClient created per connection (should cache)
### LOW-10: Levenshtein distance allocation per call (should cache)

### 6.4 Code Style & Readability Guidelines

> The primary developer has dyslexia. Code style choices prioritize **visual clarity and scanability** over density. All contributors must follow these guidelines.

### Spacing & Line Length
- **Maximum line length: 120 characters** — long lines are hard to track visually
- **One operation per line** — avoid chaining multiple operations on a single line
- **Blank lines between logical blocks** — group related statements, separate unrelated ones
- **Generous vertical spacing** — don't cram code; whitespace aids comprehension

```csharp
// GOOD: Clear spacing, one thing per line
List<Movie> movies = await _mediaContext.Movies
    .Where(m => m.LibraryId == libraryId)
    .AsNoTracking()
    .OrderByDescending(m => m.CreatedAt)
    .Take(36)
    .ToListAsync();

List<MovieDto> result = movies
    .Select(m => new MovieDto
    {
        Id = m.Id,
        Title = m.Title,
        Poster = m.PosterPath,
    })
    .ToList();

return Ok(result);

// BAD: Dense, hard to scan
var result = (await _mediaContext.Movies.Where(m => m.LibraryId == libraryId).AsNoTracking().OrderByDescending(m => m.CreatedAt).Take(36).ToListAsync()).Select(m => new MovieDto { Id = m.Id, Title = m.Title, Poster = m.PosterPath }).ToList();
```

### LINQ Chain Formatting
- Each `.Method()` call on its own line, indented
- Comments on complex `.Where()` or `.Select()` clauses

```csharp
// GOOD: Each clause on its own line
List<Genre> genres = await _mediaContext.Genres
    .Where(g => g.LibraryId == libraryId)          // Filter by library
    .Include(g => g.Translations)
    .OrderBy(g => g.Name)
    .AsNoTracking()
    .ToListAsync();

// BAD: One long chain
var genres = await _mediaContext.Genres.Where(g => g.LibraryId == libraryId).Include(g => g.Translations).OrderBy(g => g.Name).AsNoTracking().ToListAsync();
```

### Method Length
- Prefer methods under **30 lines** — easier to understand at a glance
- Extract complex logic into well-named helper methods
- If a method has more than 3 levels of nesting, refactor

### Naming
- Descriptive names over short abbreviations: `libraryMovies` not `lm`
- Boolean variables start with `is`, `has`, `should`, `can`
- Method names describe what they do: `GetLatestMoviesForUser` not `GetMovies`

### Consistent Formatting Rules (enforce via `.editorconfig`)
```ini
# Add to .editorconfig
max_line_length = 120
indent_style = space
indent_size = 4
insert_final_newline = true
trim_trailing_whitespace = true

# C# specific
csharp_new_line_before_open_brace = all
csharp_new_line_before_else = true
csharp_new_line_before_catch = true
csharp_new_line_before_finally = true
csharp_indent_case_contents = true
```

### Implementation Tasks

| Task ID | Description | Effort |
|---------|-------------|--------|
| STYLE-01 | Create/update `.editorconfig` with readability rules | Small |
| STYLE-02 | Run `dotnet format` across entire solution | Small |
| STYLE-03 | Review and break long lines (>120 chars) in modified files | Ongoing |

### 21. Boolean Parameter Cleanup

> Boolean arguments are unreadable at the call site. `DoThing(true, false, true)` tells the reader nothing. Prefer enums, named arguments, or separate methods.

### 21.1 Findings

| ID | Severity | Description | File |
|----|----------|-------------|------|
| BOOL-01 | **HIGH** | `User` constructor has **5 boolean parameters** in a row — impossible to read at call site | `Database/Models/User.cs:50` |
| BOOL-02 | **HIGH** | `priority` parameter passed as bare `true`/`false` across 7+ call sites — unclear intent | `MediaProcessing/Artists/ArtistManager.cs:210`, `FileRepository.cs:597`, `AudioImportJob.cs:202` + 4 more |
| BOOL-03 | **MEDIUM** | `watch` parameter on `CardData` constructors — ambiguous: "include watch link" or "is being watched"? | `Api/.../CardData.cs:36`, `HomeService.cs:312` |
| BOOL-04 | **LOW** | `CacheController.Read<T>(url, out value, bool xml)` — reads fine at default, unclear when `true` | `Providers/Helpers/CacheController.cs:33` |

### 21.2 Fixes

#### BOOL-01: User Constructor → Object Initializer

The constructor with 5 bools is never needed — EF Core doesn't use it, and the codebase already uses object initializers for `User` (see `Register.cs:82-94`). Delete the constructor.

```csharp
// BEFORE — what does this mean?
new User(id, email, true, true, name, true, true, true, false)

// AFTER — already the pattern used in Register.cs
new User
{
    Id = id,
    Email = email,
    Name = name,
    Owner = true,
    Allowed = true,
    Manage = true,
    AudioTranscoding = true,
    VideoTranscoding = true,
    NoTranscoding = false
}
```

#### BOOL-02: Priority → Enum

```csharp
// BEFORE — what does true mean?
FanArtImageManager.Add(artistCredit.Id, true);
CoverArtImageManagerManager.GetCoverUrl(bestResult.Id, true);

// AFTER
FanArtImageManager.Add(artistCredit.Id, DownloadPriority.High);
CoverArtImageManagerManager.GetCoverUrl(bestResult.Id, DownloadPriority.High);

public enum DownloadPriority { Normal, High }
```

#### BOOL-03: Watch → Enum or Separate Method

```csharp
// BEFORE — false what?
new CardData(movie, country, false)

// Option A: Enum
new CardData(movie, country, CardLinkMode.Detail)
new CardData(movie, country, CardLinkMode.Watch)

// Option B: Separate factory methods
CardData.ForDetail(movie, country)
CardData.ForWatch(movie, country)
```

#### BOOL-04: CacheController xml → Enum

```csharp
// BEFORE
CacheController.Read(url, out result, true)

// AFTER
CacheController.Read(url, out result, ContentFormat.Xml)

public enum ContentFormat { Json, Xml }
```

### 21.3 Code Style Rule

Add to project conventions: **No bare boolean arguments at call sites.** Acceptable patterns:

| Pattern | Acceptable? | Example |
|---------|:-----------:|---------|
| Single bool that reads naturally | Yes | `SetVisible(true)`, `Dispose(true)` |
| Named argument | Yes | `Fetch(url, priority: true)` |
| Enum instead of bool | **Preferred** | `Fetch(url, DownloadPriority.High)` |
| Multiple bools | **Never** | `DoThing(true, false, true)` |

### 21.4 Implementation Tasks

| ID | Task | Effort |
|----|------|--------|
| BOOL-IMPL-01 | Delete `User` constructor with 5 bools (object initializer already used everywhere) | Trivial |
| BOOL-IMPL-02 | Create `DownloadPriority` enum, update all `priority` parameter call sites | Small |
| BOOL-IMPL-03 | Create `CardLinkMode` enum or factory methods for `CardData` | Small |
| BOOL-IMPL-04 | Create `ContentFormat` enum for `CacheController.Read` | Trivial |

### 15.2 Architectural Violations

#### PMOD-H01: Database Model Reference in Provider
- **File**: `src/NoMercy.Providers/TMDB/Models/People/TmdbPerson.cs:20`
- **Problem**: `[JsonProperty("external_ids")] public Database.Models.TmdbPersonExternalIds? ExternalIds` — Provider model directly references a Database entity, coupling the provider layer to the database layer.
- **Fix**: Create a `TmdbExternalIds` model in the Providers project.

#### PMOD-H02: EF Core Attributes in Provider Model
- **File**: `src/NoMercy.Providers/MusixMatch/Models/MusixMatchSubtitle.cs:13-26`
- **Problem**: Uses `[Column("SubtitleBody")]`, `[NotMapped]`, and `[System.Text.Json.Serialization.JsonIgnore]` — EF Core annotations and System.Text.Json in a Providers model. Suggests the model is reused as both DTO and entity.
- **Fix**: Separate provider DTO from database entity.

#### PMOD-H03: `dynamic Id` in MovieOrEpisode
- **File**: `src/NoMercy.Providers/TMDB/Models/Shared/MovieOrEpisode.cs:7`
- **Problem**: `public dynamic Id { get; set; } = string.Empty;` — Only `dynamic` usage in all 365 models. Defeats compile-time type checking.
- **Fix**: Use concrete type (`int` for TMDB IDs).

### 15.3 Duplicate Model Types

| Duplicate Group | Files | Fix |
|----------------|-------|-----|
| 3 ArtistCredit types | `MusicBrainzArtistCredit.cs`, `RecordingArtistCredit.cs`, `ReleaseArtistCredit.cs` | Unify with `ArtistCredit<TArtist>` |
| RecordingRelation / WorkRelation | `RecordingRelation.cs`, `MusicBrainzWorkRelation.cs` | Extract `MusicBrainzRelationBase` |
| RelationRecording / Recording | `RelationRecording.cs`, `MusicBrainzRecording.cs` | Use one type |
| 4 "movie or show" types | `TmdbKnownFor.cs`, `TmdbResult.cs`, `TmdbShowOrMovie.cs`, `TmdbPersonCredit.cs` | All inherit `TmdbBase` |
| TmdbCollectionPart | `TmdbCollectionPart.cs` | Extend `TmdbBase` instead of redeclaring |
| FanArtAlbum / FanArtArtist | `FanArtAlbum.cs`, `FanArtArtists.cs` | Unify shape |
| TvdbCharacterTagOption / TvdbTagOption | `TvdbCharacterResponse.cs`, `TvdbShared.cs` | Merge |

### 15.4 Property Shadowing with `new`

11 occurrences of `new` keyword hiding base class properties in TMDB translation models. Every specialized translation type shadows the `Data` property:

| Class | Hides |
|-------|-------|
| `TmdbMovieTranslation.Data` | `TmdbSharedTranslation.Data` |
| `TmdbTvTranslation.Data` | `TmdbSharedTranslation.Data` |
| `EpisodeTranslation.Data` | `TmdbSharedTranslation.Data` |
| `TmdbSeasonTranslation.Data` | `TmdbSharedTranslation.Data` |
| `TmdbCollectionsTranslation.Data` | `TmdbSharedTranslation.Data` |

**Fix**: Use generics: `TmdbTranslation<TData>` and `TmdbTranslations<TTranslation>`

### 15.5 Type Inconsistencies

#### Float vs Double (TMDB)
- `Popularity` is `float` in Cast/Crew/GuestStar, `double` in TmdbBase/TmdbPerson/TmdbResult
- `VoteAverage` is `float` in TmdbImage, `double` in TmdbBase, `int` in TmdbLogo
- **Fix**: Standardize to `double` (TMDB returns decimals)

#### `object` Types That Should Be Typed (10+ occurrences)
| File | Property | Should Be |
|------|----------|-----------|
| `TmdbCollectionPart.cs:8` | `BackdropPath` as `object?` | `string?` |
| `TmdbDetails.cs:11` | `PosterPath` as `object?` | `string?` |
| `TmdbSeasonExternalIds.cs:9` | `FreebaseId` as `object?` | `string?` |
| `MusicBrainzArea.cs:9` | `Type` as `object` | `string?` |
| `MusicBrainzWork.cs:7` | `Attributes` as `object[]` | Typed array |
| `MusicBrainzArtistDetails.cs:8` | `ArtistAppendsEndArea` as `object?` | `MusicBrainzArea?` |
| `MusixMatchLyrics.cs:30-31` | `WriterList`, `PublisherList` as `object[]` | Typed arrays |
| `MusixMatchMusixMatchTrack.cs:25` | `TrackNameTranslationList` as `object[]` | Typed array |

#### Mixed List/Array
- TMDB `TmdbPaginatedResponse<T>` uses `List<T>`; MusicBrainz `MusicBrainzPaginatedResponse<T>` uses `T[]`
- TVDB mixes: `TvdbCharacterData.Aliases` uses `List<TvdbAlias>`, `TvdbCompany.Aliases` uses `TvdbAlias[]`
- **Fix**: Standardize to one collection type per provider (arrays preferred for DTOs)

### 15.6 Naming & Code Quality Issues

| Issue | Files | Fix |
|-------|-------|-----|
| Stuttering prefixes | `TmdbTmdbNetworkDetails.cs`, `TmdbTmdbAggregatedCast.cs`, `MusixMatchMusixMatchTrack.cs` | Remove double prefix |
| Auto-generated name | `PurpleArtist.cs` | Rename to `MusicBrainzArtistSummary` |
| Typo "Messaged" | `TrackLyricsGetMessagedBody.cs` | Rename to `...MessageBody` |
| Missing "Tmdb" prefix | `EpisodeTranslation.cs`, `EpisodeTranslationData.cs` | Add `Tmdb` prefix |
| `MusicBrainzWorkRelation : MusicBrainzLifeSpan` | `MusicBrainzWorkRelation.cs` | Composition, not inheritance |
| Date parsing duplicated 6x | MusicBrainz + MusixMatch models | Extract `JsonConverter` |
| `TmdbCertification` 392 lines | `TmdbCertification.cs` — 27 identical while loops | Use `Dictionary<string, T[]>` |
| 130+ lines commented-out code | `SubtitleSearchResponse.cs:59-237` | Delete |
| MusixMatch: public fields | `MusixMatchCounters.cs`, `MusixMatchRichSync.cs` (35 fields) | Convert to auto-properties |
| Non-nullable `Uri = null!` | `MusixMatchMusixMatchTrack.cs:48-61` | Change to `Uri?` |

### 16.4 Primary Key Strategy Chaos

4 different PK types with no consistent pattern:

| Key Type | Models |
|----------|--------|
| `int` (TMDB ID, no auto-increment) | Movie, Tv, Season, Episode, Person, Genre, Keyword, Network |
| `int` (auto-increment) | Cast, Crew, GuestStar, Role, Job, Translation, Image |
| `Guid` | User, Album, Artist, Track, ReleaseGroup, MusicGenre, Playlist |
| `Ulid` | Library, Folder, VideoFile, Metadata, Device, EncoderProfile, Special, Notification |

Additional issues:
- `FailedJob.Id` is `long`, `QueueJob.Id` is `int` — inconsistent in same DB
- `Country.cs` has both `[PrimaryKey(nameof(Id))]` and `[Key]` on `Iso31661` — conflicting key declarations
- `EncoderProfile.cs` has `[PrimaryKey(nameof(Id))]` on class + `[Key]` on `Name` — conflicting
- `Library.cs:8` has `[Index(nameof(Id), IsUnique = true)]` — redundant with PK

### 16.5 Index Issues

#### Excessive Indexes
- **Image table**: 34 index annotations — extraordinarily high for SQLite, increases write time
- **Translation table**: 20 index annotations

#### Redundant Indexes (composite PK tables)
- `CollectionMovie`, `CompanyMovie`, `CompanyTv`, `NetworkTv` — all have unique index on their composite PK columns, which is redundant with the PK itself

#### Missing Indexes on Foreign Keys
| Column | Table | Impact |
|--------|-------|--------|
| `Metadata.AudioTrackId` | Metadata | Joins to Track scan |
| `Playlist.UserId` | Playlist | User playlist queries scan |
| `ActivityLog.UserId` | ActivityLog | User activity queries scan |
| `ActivityLog.DeviceId` | ActivityLog | Device queries scan |
| `Collection.LibraryId` | Collection | Library queries scan |

### 16.6 Naming Convention Violations

#### Classes Named with I-Prefix (NOT Interfaces)
| File | Class |
|------|-------|
| `Metadata.cs:186` | `class IPreview` |
| `Metadata.cs:192` | `class IPreviewFile` |
| `Metadata.cs:196` | `class IHash` |
| `Metadata.cs:203` | `class IFont` |
| `Metadata.cs:207` | `class IFontsFile` |
| `Metadata.cs:211` | `class IChapter` |
| `Metadata.cs:219` | `class IChapterFile` |

#### Underscore-Prefixed Public Properties
Backing fields for JSON-serialized columns are `public` instead of `private`:
- `ColorPalettes.cs:14` — `public string _colorPalette`
- `Tracks.cs:14` — `public string _tracks`
- `MetadataTrack.cs` — `public string? _video`, `_audio`, `_subtitle`
- `Person.cs:55` — `public string? _externalIds`
- `EncoderProfile.cs` — `public string _videoProfiles`, `_audioProfiles`, `_subtitleProfiles`

### 16.7 Relationship Issues

| Issue | File | Impact |
|-------|------|--------|
| `Movie` has `ICollection<Season>` | `Movie.cs:60` | Movies don't have seasons — likely orphaned navigation |
| `Image.CastCreditId` uses string FK to `Cast.CreditId` | `Image.cs:53-57` | EF Core convention expects `CastId` (int) — needs explicit config |
| `Role.CreditId` → `Cast` uses string FK, but Cast PK is int | `Role.cs` | Circular reference, needs explicit fluent config |
| `AlternativeTitle` has nullable FKs but non-nullable navs | `AlternativeTitle.cs:19-23` | `Movie { get; set; } = null!` misleads — will be null at runtime |
| Collection initialization inconsistency | 3 patterns: `= []`, `= new HashSet<T>()`, `= new List<T>()` | Functional but inconsistent |

### 16.8 Other Issues

| Severity | Issue | Details |
|----------|-------|---------|
| Medium | `[Timestamp]` on `DateTime` | `Timestamps.cs:13,21` — EF Core concurrency token, but SQLite has no `rowversion`. Does nothing. |
| Medium | Test dependencies in production | `NoMercy.Database.csproj` includes Moq, xunit, Castle.Core |
| Medium | 5 stub entities with only Id | Notification, MediaAttachment, MediaStream, Message, RunningTask |
| Medium | `ColorPalettes` base class — zero inheritors | Dead code, superseded by `ColorPaletteTimeStamps` |
| Medium | Mixed serializer usage | Most use Newtonsoft, but `Person.cs:40`, `Track.cs:32` use `System.Text.Json.Serialization.JsonIgnore` |
| Low | Empty `Extensions.cs` | Only method is commented out |

### 17.3 Medium Severity Issues (Code Quality)

| ID | File | Line | Issue |
|----|------|------|-------|
| SYS-M03 | `Wip.cs` | 23, 67 | Hardcoded Windows paths in production code |
| SYS-M04 | `MetaData.cs` | 10 | `dynamic? Data` property — no type safety |
| SYS-M05 | `Globals.cs` | — | Single mutable static `AccessToken` property, no thread-safety |
| SYS-M06 | `Shell.cs` | 117 | ExecCommand hardcodes `/bin/bash` — fails on Windows |
| SYS-M07 | `Config.cs` | 20-62 | Extensive mutable static state (15+ fields) without synchronization |
| SYS-M08 | `Cpu.cs` | 23, 71 | `ManagementObjectSearcher` not disposed (leaks COM resources) |
| SYS-M09 | `Gpu.cs` | 73 | `ManagementObjectSearcher` not disposed |
| SYS-M10 | `Storage.cs` | 25, 163 | `ManagementObjectSearcher` not disposed |
| SYS-M12 | `UpdateChecker.cs` | 23-33 | Always returns `false` but periodic timer runs every 6 hours |
| SYS-M13 | `GuidKeyDictionaryConverter.cs` | 41-43 | Invalid GUIDs silently mapped to `Guid.Empty` |
| SYS-M14 | `Auth.cs` | 267-270 | `FileStream` not using `using` (leak on exception) |
| SYS-M15 | `Auth.cs` | 35-38 | Token file read and parsed 4 separate times in succession |

### 17.4 Low Severity Issues

| ID | File | Issue |
|----|------|-------|
| SYS-L01 | `DriveMonitor.cs:37` | CancellationTokenSource named `CancellationToken` (confusing) |
| SYS-L02 | `Wip.cs:61` | Unused `MemoryStream` allocation |
| SYS-L03 | `FfProbeData.cs:149-172` | Duplicate types at bottom of file |
| SYS-L04 | `MediaScan.cs:31` | Global FFMpeg config in constructor (re-set on every instance) |
| SYS-L05 | `Download.cs:51` | `await Task.Delay(0)` to suppress async warning |
| SYS-L06 | `Hardware.cs` | Empty class (dead code) |
| SYS-L07 | `Helper.cs` | Duplicate of `Shell.ExecCommand` |
| SYS-L08 | `UserSettings.cs:64-68` | Two identical switch branches (both assign same value) |
| SYS-L09 | `Mutators.cs:16` | `new Random()` per call — use `Random.Shared` |
| SYS-L10 | `Screen.cs` | Hardcoded 1666 return for non-Windows |

---

