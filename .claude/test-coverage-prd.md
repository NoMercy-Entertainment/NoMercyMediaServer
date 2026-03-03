# Test Coverage Gap Analysis — NoMercy MediaServer
*Generated: 2026-02-24*

## Executive Summary

The codebase has 13 test projects with ~176 test files providing coverage across most modules, but several high-risk areas are entirely or substantially untested. The most critical gaps are concentrated in string/filename parsing utilities, the recommendation engine, complex EF Core repository projections, and networking/security infrastructure.

**Estimated missing coverage: ~530–600 new tests to reach comprehensive coverage.**

---

## Current Test Inventory

| Test Project | Files | Coverage State |
|---|---|---|
| NoMercy.Tests.Api | 22 | Snapshot tests only; business logic gaps |
| NoMercy.Tests.Repositories | 18 | 6 of 22 repositories tested |
| NoMercy.Tests.Providers | 38 | Good breadth; edge cases missing |
| NoMercy.Tests.Queue | 26 | Strong; minor cron edge cases missing |
| NoMercy.Tests.Database | 9 | Basic model/constraint tests only |
| NoMercy.Tests.Encoder | 6 | Core pipeline tested; codec selection missing |
| NoMercy.Tests.MediaProcessing | 8 | Very thin; most managers untested |
| NoMercy.Tests.Networking | 3 | Only config/IP tests; cert/strategy untested |
| NoMercy.Tests.Setup | 13 | Good initialization coverage |
| NoMercy.Tests.Events | 10 | Good integration event coverage |
| NoMercy.Tests.Plugins | 8 | Good plugin loading coverage |
| NoMercy.Tests.Cli | 2 | Minimal command structure only |
| NoMercy.Tests.Service | 3 | Minimal initialization only |
| **NmSystem** | 0 | **No test project exists** |
| **NoMercy.Helpers** | ~4 | Platform wallpaper only; credentials untested |

---

## Module-by-Module Gap Analysis

---

### 1. NoMercy.NmSystem — CRITICAL: No Test Project Exists

The `NmSystem` module has no dedicated test project at all. It contains heavily used utilities called throughout every other module.

**src/NoMercy.NmSystem/Extensions/Str.cs** (~300+ lines, 50+ methods)
- `MatchPercentage()` — Levenshtein distance algorithm used for metadata matching
- `RemoveAccents()` / `RemoveDiacritics()` — character normalization for filenames
- `TryGetYear()` — regex year extraction (edge cases: 4-digit years, false positives)
- `ToInt()`, `ToDouble()`, `ToLong()`, `ToBoolean()` — type conversion with null/fallback
- `Spacer()` — string padding logic
- Priority: **TIER 1 CRITICAL**

**src/NoMercy.NmSystem/AnimeParser.cs** (47 lines)
- `ParseAnimeFilename()` — single method, complex regex, high edge-case risk
- Priority: **TIER 2 HIGH**

**src/NoMercy.NmSystem/Extensions/Date.cs**
- All date parsing and formatting extensions
- Priority: **TIER 3 MEDIUM**

**src/NoMercy.NmSystem/Extensions/NumberConverter.cs**, **Url.cs**, **XmlHelper.cs**, **Culture.cs**
- Utility conversions used across the codebase
- Priority: **TIER 4 LOW**

**src/NoMercy.NmSystem/FileSystem/** (FileAttributes, FilePermissions, Folders, Lock)
- File system operation helpers; risk of incorrect permissions or data loss
- Priority: **TIER 3 MEDIUM**

**src/NoMercy.NmSystem/FFProbe.cs**
- FFprobe wrapper for media analysis; failure silently breaks library scanning
- Priority: **TIER 2 HIGH**

**src/NoMercy.NmSystem/Checksum.cs**, **AppProcessManager.cs**, **AutoStartupManager.cs**
- Priority: **TIER 3 MEDIUM**

---

### 2. NoMercy.Data.Repositories — CRITICAL: 16 of 22 Repositories Untested

**Completely Untested Repositories:**

| Repository | Methods | Criticality | Risk |
|---|---|---|---|
| `RecommendationRepository.cs` | 9 | **TIER 1** | Home page recommendations broken silently |
| `SpecialRepository.cs` | 8+ | **TIER 2** | Complex DTO projections; null ref risk |
| `CollectionRepository.cs` | 5+ | **TIER 3** | Collection display incorrect |
| `DeviceRepository.cs` | 5+ | **TIER 3** | Device management failures |
| `EncoderRepository.cs` | 5+ | **TIER 3** | Encoder profile persistence |
| `FolderRepository.cs` | 8+ | **TIER 2** | Library management; file org breakage |
| `LanguageRepository.cs` | 3+ | **TIER 4** | Translation fetching |

**RecommendationRepository** — 9 Methods, All Untested:
```
GetUnownedMovieRecommendationsAsync()    — filters by user library access
GetUnownedTvRecommendationsAsync()       — TV-specific filtering
GetUnownedAnimeRecommendationsAsync()    — anime-specific filtering
GetUnownedMovieSimilarAsync()            — similar item projection
GetUnownedTvSimilarAsync()
GetUnownedAnimeSimilarAsync()
GetUserMovieAffinityDataAsync()          — multi-join aggregation query
GetUserTvAffinityDataAsync()
GetGenresForMovieIdsAsync() / GetGenresForTvIdsAsync()
```
All use 2-step EF Core pattern to avoid SQLite APPLY. Verify the pattern holds under edge cases (empty results, no user data, null genre entries).

**SpecialRepository** — Complex Projections:
- `GetSpecialsByIdAsync()` — nested collection loading
- `GetSpecialsAsync()` — pagination + filtering
- `GetSpecialCardsAsync()` — certification/genre/cast projection

---

### 3. NoMercy.MediaProcessing — CRITICAL: 97 Source Files, ~8 Tests

**src/NoMercy.MediaProcessing/Common/FileNameParsers.cs** (129 lines, 6 methods):
- `CreateTitleSort()` — regex replacement + year parsing; wrong output breaks sort order
- `CreateBaseFolder()` — title + year concatenation; wrong output misplaces files
- `CreateEpisodeFolder()` — season/episode format; wrong output breaks episode linking
- `CreateFileName()` — media naming; wrong output breaks playback
- Priority: **TIER 1 CRITICAL**

**Completely Untested Managers:**

| Manager | Criticality |
|---|---|
| `Artists/ArtistManager.cs` | TIER 2 |
| `Collections/CollectionManager.cs` | TIER 2 |
| `Episodes/EpisodeManager.cs` | TIER 2 |
| `Seasons/SeasonManager.cs` | TIER 2 |
| `Specials/SpecialManager.cs` | TIER 2 |
| `Libraries/LibraryManager.cs` | TIER 2 |

**File Analysis:**
- `File Analysis/Audio.cs`, `VideoAudioFile.cs`, `VideoFile.cs` — property extraction from FFprobe output
- Silent failures here result in missing metadata across the entire library
- Priority: **TIER 2 HIGH**

---

### 4. NoMercy.Api — HIGH: Services and Controller Logic Untested

**Currently tested via snapshot only** — no business logic unit tests for most controllers.

**src/NoMercy.Api/Services/HomeService.cs** (580 lines, 7 public methods):
- `GetHomeData()`, `GetHomePageContent()`, `GetHomeTvContent()`, `GetHomeContinueContent()`, `GetSetupScreensaverContent()`
- Uses parallel queries against multiple repositories; race conditions and null references possible
- Priority: **TIER 1 CRITICAL**

**src/NoMercy.Api/Services/RecommendationService.cs**:
- Recommendation scoring and filtering logic
- Priority: **TIER 1 CRITICAL**

**Untested Controllers (no direct unit tests):**

| Controller Group | Criticality |
|---|---|
| MoviesController, TvShowsController | TIER 2 HIGH |
| SearchController — filtering/ranking logic | TIER 2 HIGH |
| RecommendationsController | TIER 2 HIGH |
| Dashboard: DevicesController, EncoderController, LibrariesController, LogController, ServerController | TIER 3 MEDIUM |
| Dashboard: OpticalMediaController, PluginController, ServerActivityController, TasksController | TIER 3 MEDIUM |
| GenresController, CollectionsController, SpecialController | TIER 3 MEDIUM |
| PeopleController, AlbumsController, ArtistsController, PlaylistsController, TracksController | TIER 3 MEDIUM |

**Music/Video Playback Services (10+ methods each):**
- `MusicPlaybackService`, `MusicPlaylistManager`, `MusicDeviceManager`
- `VideoPlaybackService`, `VideoPlaylistManager`, `VideoDeviceManager`
- State management, SignalR coordination, progress tracking
- Priority: **TIER 2 HIGH** (40–50 tests combined)

---

### 5. NoMercy.Networking — CRITICAL: Security Infrastructure Untested

**src/NoMercy.Networking/Certificate.cs** (200+ lines):
- SSL cert generation, validation, renewal
- Failure = server inaccessible; security vulnerability
- Priority: **TIER 2 HIGH**

**Untested Connectivity Strategies:**

| Class | Criticality |
|---|---|
| `Strategies/CloudflareTunnelStrategy.cs` | TIER 2 HIGH |
| `Strategies/PortForwardStrategy.cs` | TIER 2 HIGH |
| `Strategies/StunHolePunchStrategy.cs` | TIER 2 HIGH |
| `ChromeCast.cs` — device discovery | TIER 2 HIGH |
| `Client.cs` — registration and pairing | TIER 2 HIGH |
| `ConnectionHub.cs` — real-time management | TIER 3 MEDIUM |
| `ConnectivityManager.cs` | TIER 3 MEDIUM |
| `NetworkDiscovery.cs` | TIER 3 MEDIUM |
| `NetworkChangeMonitor.cs` | TIER 3 MEDIUM |
| `ClientMessenger.cs` | TIER 3 MEDIUM |
| `ConnectedClients.cs` | TIER 3 MEDIUM |
| `IpcClient.cs` | TIER 3 MEDIUM |

---

### 6. NoMercy.Helpers — CRITICAL: Security Utilities Untested

**src/NoMercy.Helpers/CredentialManager.cs**:
- Secret and credential storage — any bug is a security issue
- Priority: **TIER 2 CRITICAL**

**src/NoMercy.Helpers/UserPass.cs**:
- Password hashing and validation
- Priority: **TIER 2 CRITICAL**

**src/NoMercy.Helpers/Images.cs**, **ImageConvertArguments.cs**:
- Image manipulation; failures cause broken thumbnails
- Priority: **TIER 3 MEDIUM**

**src/NoMercy.Helpers/Monitoring/** (CPU, GPU, Memory, Storage, ResourceMonitor):
- System metrics; silent failures cause incorrect dashboard data
- Priority: **TIER 3 MEDIUM**

---

### 7. NoMercy.Encoder — MEDIUM: Codec Selection Untested

**Currently well-tested for:** pipeline building, regex, HLS playlist generation, progress throttle.

**Gaps:**

| Class | Methods | Criticality |
|---|---|---|
| `CodecSelector.cs` | 6+ | TIER 2 |
| `FFmpegHardwareConfig.cs` | 5+ | TIER 2 |
| `SubtitleParser.cs` | 3+ | TIER 2 |
| `VideoFile.cs` | property extraction | TIER 2 |
| `AudioFile.cs` | stream analysis | TIER 2 |
| `IsoLanguageMapper.cs` | language code mapping | TIER 4 |
| `Chapters.cs` | chapter detection | TIER 4 |
| `TesseractLanguageDownloader.cs` | OCR language packs | TIER 4 |

---

### 8. NoMercy.Queue — GOOD but Minor Gaps

26 test files provide strong coverage. Remaining gaps:
- Media processing job-specific processors (not the queue plumbing, but the job payloads)
- Cron expression edge cases (leap years, DST transitions)
- Retry backoff strategies (jitter distribution, exponential backoff with cancellation)
- Priority: **TIER 3 MEDIUM**

---

### 9. NoMercy.Providers — GOOD but Edge Cases Missing

38 test files cover API clients broadly. Remaining gaps:
- Rate limiting and circuit breaker behavior under stress
- Timeout and partial response handling
- Cache invalidation timing
- Null/malformed response field mapping
- Priority: **TIER 3 MEDIUM**

---

### 10. NoMercy.Database — BASIC Coverage

9 test files cover model initialization, JSON serialization, foreign key constraints. Gaps:
- Migration compatibility testing (forward + backward)
- Concurrent write patterns (multiple contexts writing same row)
- Data corruption recovery paths
- Priority: **TIER 3 MEDIUM**

---

## Prioritized Implementation Roadmap

### TIER 1 — Critical (Start Here)

| # | Target | Test Type | Estimated Tests |
|---|---|---|---|
| 1 | `RecommendationRepository` (9 methods) | Integration | 15–20 |
| 2 | `NmSystem/Extensions/Str.cs` (50+ methods) | Unit | 30–40 |
| 3 | `MediaProcessing/Common/FileNameParsers.cs` (6 methods) | Unit | 20–25 |
| 4 | `Api/Services/HomeService.cs` (7 methods) | Integration | 25–30 |
| 5 | `Api/Services/RecommendationService.cs` | Integration | 10–15 |

**Total Tier 1: ~100–130 tests**

### TIER 2 — High

| # | Target | Test Type | Estimated Tests |
|---|---|---|---|
| 6 | `Networking/Certificate.cs` | Integration/Unit | 15–20 |
| 7 | `Helpers/CredentialManager.cs` + `UserPass.cs` | Unit (mocked crypto) | 15–20 |
| 8 | `Data/Repositories/SpecialRepository.cs` | Integration | 20–25 |
| 9 | `Data/Repositories/FolderRepository.cs` | Integration | 15–20 |
| 10 | `NmSystem/AnimeParser.cs` | Unit | 20–25 |
| 11 | `NmSystem/FFProbe.cs` | Unit/Integration | 15–20 |
| 12 | `MediaProcessing/File Analysis/*` | Unit | 20–25 |
| 13 | `MediaProcessing` Managers (Artists, Collections, Episodes) | Integration | 30–40 |
| 14 | Playback Services (Music + Video) | Integration | 40–50 |
| 15 | `Encoder/CodecSelector.cs` + `FFmpegHardwareConfig.cs` | Unit | 15–20 |
| 16 | Controllers: MoviesController, TvShowsController, SearchController | Integration | 30–40 |
| 17 | Networking Strategies (3 strategies) | Integration | 30–40 |

**Total Tier 2: ~265–345 tests**

### TIER 3 — Medium

| # | Target | Estimated Tests |
|---|---|---|
| 18 | Dashboard Controllers (9 controllers) | 40–60 |
| 19 | Remaining Media Controllers (Genres, Collections, People, etc.) | 40–60 |
| 20 | `NmSystem/FileSystem/*` | 20–25 |
| 21 | `Helpers/Monitoring/*` | 20–25 |
| 22 | `Helpers/Images.cs` | 15–20 |
| 23 | Networking (ChromeCast, Client, ConnectionHub) | 30–40 |
| 24 | Queue: Job processor edge cases | 15–20 |
| 25 | Provider edge cases (rate limit, timeout) | 20–30 |
| 26 | Database: Migration + concurrency | 15–20 |

**Total Tier 3: ~215–300 tests**

### TIER 4 — Low (Completeness)

| # | Target | Estimated Tests |
|---|---|---|
| 27 | `NmSystem` utility extensions (Date, NumberConverter, Url, Xml, Culture) | 30–40 |
| 28 | `Encoder/IsoLanguageMapper.cs`, `Chapters.cs`, `Fonts.cs` | 15–20 |
| 29 | `Cli` + `Service` initialization coverage | 15–20 |
| 30 | `AutoStartupManager.cs`, `AppProcessManager.cs`, `UpdateChecker.cs` | 10–15 |

**Total Tier 4: ~70–95 tests**

---

## Grand Total Estimate

| Tier | Tests | Effort |
|---|---|---|
| Tier 1 (Critical) | ~100–130 | 4–5 weeks |
| Tier 2 (High) | ~265–345 | 8–10 weeks |
| Tier 3 (Medium) | ~215–300 | 7–9 weeks |
| Tier 4 (Low) | ~70–95 | 3–4 weeks |
| **Total** | **~650–870 tests** | **~22–28 weeks** |

---

## Highest-Risk Files (Quick Reference)

| File | Lines | Methods Untested | Priority |
|---|---|---|---|
| `src/NoMercy.Data/Repositories/RecommendationRepository.cs` | 438 | 9 | TIER 1 |
| `src/NoMercy.NmSystem/Extensions/Str.cs` | 300+ | 50+ | TIER 1 |
| `src/NoMercy.MediaProcessing/Common/FileNameParsers.cs` | 129 | 6 | TIER 1 |
| `src/NoMercy.Api/Services/HomeService.cs` | 580 | 7 | TIER 1 |
| `src/NoMercy.Api/Services/RecommendationService.cs` | — | all | TIER 1 |
| `src/NoMercy.Data/Repositories/SpecialRepository.cs` | 450+ | 8+ | TIER 2 |
| `src/NoMercy.Networking/Certificate.cs` | 200+ | 8+ | TIER 2 |
| `src/NoMercy.Helpers/CredentialManager.cs` | 50+ | 4+ | TIER 2 |
| `src/NoMercy.Helpers/UserPass.cs` | — | all | TIER 2 |
| `src/NoMercy.NmSystem/AnimeParser.cs` | 47 | 1 | TIER 2 |
| `src/NoMercy.Encoder/Core/CodecSelector.cs` | 100+ | 6+ | TIER 2 |
| `src/NoMercy.NmSystem/FFProbe.cs` | — | all | TIER 2 |

---

## Testing Guidelines for This Codebase

### DbContext Thread Safety
- Use `IDbContextFactory<MediaContext>` for any parallel queries
- Never share a scoped `MediaContext` across `Task.WhenAll()`
- Already-fixed controllers (HomeController, SearchController, SpecialController) serve as reference

### SQLite APPLY Restriction
- All `GroupBy().Select()` tests must verify 2-step pattern:
  1. Flat server-side projection
  2. Client-side grouping
- RecommendationRepository tests are most likely to surface regression here

### Null Handling Edge Cases to Test
- Empty library (0 items)
- User with no watch history
- Items with no translations
- Images/artwork missing
- Genres not populated

### Regex and String Parsing
- Non-ASCII filenames (accents, CJK, emoji)
- Very long filenames
- Filenames with no year
- False-positive year matches (e.g. "1080p")
- Anime episode patterns: `[Group] Title - 01 [720p]`, `Title S01E01`, etc.
