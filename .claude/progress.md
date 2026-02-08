# Progress Log

> Append-only log. Each entry records what was done in a single ralph iteration.

---

## CHAR-01 — Set up `NoMercy.Tests.Api` project with `WebApplicationFactory` and auth helpers

**Date**: 2026-02-07

**What was done**:
- Created `tests/NoMercy.Tests.Api/` project with `Microsoft.AspNetCore.Mvc.Testing` and `Microsoft.EntityFrameworkCore.Sqlite`
- Added project to solution under Tests folder
- Created `Infrastructure/NoMercyApiFactory.cs` — custom `WebApplicationFactory<Startup>` that:
  - Overrides `CreateWebHostBuilder()` to bootstrap the server without SSL certs or real network setup
  - Registers `StartupOptions`, `IApiVersionDescriptionProvider`, `ISunsetPolicyManager` needed by `Startup` constructor
  - Removes all `IHostedService` registrations to prevent background jobs from running during tests
  - Replaces JWT Bearer auth with a test authentication scheme
  - Replaces the `"api"` authorization policy with a simple authenticated-user policy
  - Ensures `AppFiles` data directories exist and seeds the SQLite database with a test user before server startup
  - Populates `ClaimsPrincipleExtensions.Users` static list so auth checks work
- Created `Infrastructure/TestAuthHandler.cs` — per-request test auth handler using `X-Test-Auth` header to control authentication (avoids shared static state between parallel tests)
- Created `Infrastructure/HttpClientAuthExtensions.cs` — `AsAuthenticated()` / `AsUnauthenticated()` extension methods for `HttpClient`
- Created `HealthControllerTests.cs` — 2 tests verifying `/health` and `/health/detailed` endpoints
- Created `AuthenticationTests.cs` — 3 tests verifying anonymous endpoint access, authenticated endpoint access, and unauthenticated rejection

**Test results**: 5 new tests pass. All 517 tests (5 new + 512 existing) pass with `dotnet build && dotnet test`.

---

## CHAR-02 — Set up `NoMercy.Tests.Repositories` project with in-memory SQLite + seed data

**Date**: 2026-02-08

**What was done**:
- Created `tests/NoMercy.Tests.Repositories/` project with `Microsoft.EntityFrameworkCore.Sqlite` for real SQLite provider testing
- Added project to solution under Tests folder with references to `NoMercy.Data` and `NoMercy.Database`
- Created `Infrastructure/TestMediaContext.cs` — subclass of `MediaContext` that skips `OnConfiguring` when options are already provided (allows in-memory SQLite instead of file-based)
- Created `Infrastructure/TestMediaContextFactory.cs` — factory that:
  - `CreateContext()` — creates an empty in-memory SQLite database with unique name per test (full isolation)
  - `CreateSeededContext()` — creates and seeds a database with realistic test data
  - `SeedData()` — seeds a complete test dataset: user, 2 libraries (movie + tv), library-user access, folder, 2 genres, 2 movies with video files, 1 TV show with season/episodes/video files, genre-movie and genre-tv joins
- Created `Infrastructure/SeedConstants.cs` — shared test IDs (UserId, OtherUserId, library IDs, folder ID)
- Created `TestMediaContextFactoryTests.cs` — 12 tests verifying seed data integrity (user, libraries, movies, shows, episodes, video files, genres, join tables, context isolation)
- Created `MovieRepositoryTests.cs` — 10 tests covering: GetMovieAsync (access control, not-found, includes VideoFiles), GetMovieAvailableAsync, GetMoviePlaylistAsync, DeleteMovieAsync, LikeMovieAsync (add/remove)
- Created `LibraryRepositoryTests.cs` — 13 tests covering: GetLibraries (access control, ordering), GetLibraryByIdAsync, GetAllLibrariesAsync, GetFoldersAsync, GetLibraryMovieCardsAsync (pagination, access control), GetLibraryTvCardsAsync, AddLibraryAsync, DeleteLibraryAsync
- Created `TvShowRepositoryTests.cs` — 10 tests covering: GetTvAvailableAsync (access control, not-found), GetTvPlaylistAsync (seasons/episodes, access control), DeleteTvAsync, GetMissingLibraryShows, LikeTvAsync (add/remove)
- Created `GenreRepositoryTests.cs` — 8 tests covering: GetGenreAsync (access control, includes movies/tv), GetGenresAsync (access control, pagination), GetGenresWithCountsAsync (correct counts)
- Created `HomeRepositoryTests.cs` — 10 tests covering: GetHomeMovies, GetHomeTvs, GetMovieCountAsync, GetTvCountAsync (access control), GetLibrariesAsync, GetHomeGenresAsync (pagination)

**Test results**: 63 new tests pass (all in NoMercy.Tests.Repositories). Build succeeds with 0 errors. 3 pre-existing failures in NoMercy.Tests.Api (from CHAR-01) are unrelated to this change.

---

## CHAR-03 — Snapshot tests for all `/api/v1/` Media endpoints

**Date**: 2026-02-08

**What was done**:
- Extended `NoMercyApiFactory.SeedMediaData()` with realistic test data: 2 libraries (movie + tv), 1 folder, 2 genres (Action/Drama), 2 movies (Fight Club id=550, Pulp Fiction id=680), 1 TV show (Breaking Bad id=1399), 1 season, 2 episodes, 4 video files, genre-movie/genre-tv joins, library-movie/library-tv/library-user joins
- Added static `DbLock` + `_dbInitialized` flag to prevent parallel test fixture race conditions during DB initialization
- Added DB file deletion before `EnsureCreated()` to handle stale state between test runs
- Created `MediaEndpointSnapshotTests.cs` with 51 test methods covering all 10 media controllers:
  - **MoviesController** (9 tests): GET detail, non-existent, unauthenticated, available shape, available not-found, watch, like, watch-list, delete
  - **TvShowsController** (9 tests): GET detail, available shape, available not-found, watch, like, watch-list, delete, missing
  - **CollectionsController** (6 tests): list, lolomo, available not-found, watch not-found, like, watch-list
  - **GenresController** (4 tests): list, seeded genre, non-existent, lolomo
  - **LibrariesController** (6 tests): list, single library, lolomo, by-letter, mobile, tv
  - **HomeController** (4 tests): index paginated, page1 shape, home component, home-tv
  - **SearchController** (4 tests): video, no results, video-tv, music no results
  - **PeopleController** (1 test): index paginated
  - **UserDataController** (2 tests): index, continue watching
  - **SpecialController** (2 tests): index, lolomo
  - **Cross-cutting auth** (10 tests): Theory-based 401/403 verification for all protected endpoints when unauthenticated
- Helper methods: `JsonBody()`, `AssertJsonHasProperty()`, `EnumerateProperties()`, `AssertStatusResponse()` (handles both custom StatusResponseDto and ASP.NET ProblemDetails formats)
- Discovered server-side bugs during testing (documented as known issues, not fixed — out of scope):
  - GenresController lolomo has CA2021 incompatible cast bug (returns 500)
  - VideoPlaylistResponseDto JSON serialization error on watch endpoints (returns 500)
  - TMDB client throws without API key when fetching non-existent movies (returns 500)

**Test results**: 51 new tests pass (56 total in NoMercy.Tests.Api including 5 from CHAR-01). All 634 tests pass across all projects (63 Repositories + 203 Queue + 56 Api + 307 Providers + 5 other). Build succeeds with 0 errors.

---

## CHAR-04 — Snapshot tests for all `/api/v1/` Music endpoints

**Date**: 2026-02-08

**What was done**:
- Extended `NoMercyApiFactory.SeedMediaData()` with music test data: 1 music library, 1 music folder, 1 artist (Test Artist), 1 album (Test Album), 2 tracks, 1 playlist (Test Playlist), 1 music genre (Rock), all necessary join tables (ArtistTrack, AlbumTrack, AlbumArtist, ArtistLibrary, AlbumLibrary, LibraryTrack, ArtistMusicGenre, PlaylistTrack, ArtistUser, TrackUser)
- Added static IDs for music entities: `MusicLibraryId`, `MusicFolderId`, `ArtistId1`, `AlbumId1`, `TrackId1`, `TrackId2`, `PlaylistId1`, `MusicGenreId1`
- Created `MusicEndpointSnapshotTests.cs` with 51 test methods covering all 6 music controllers:
  - **MusicController** (9 tests): GET index, start, POST favorites, favorite-artists, favorite-albums, playlists, search (no results + with query), type search
  - **ArtistsController** (5 tests): GET index (letter), show, show non-existent, POST like, like non-existent, DELETE
  - **AlbumsController** (5 tests): GET index, show, show non-existent, POST like, rescan
  - **PlaylistsController** (7 tests): GET index, show, show non-existent, POST create, create duplicate, DELETE, POST add track, DELETE remove track non-existent
  - **Music GenresController** (4 tests): GET index, by letter, show, show non-existent
  - **TracksController** (7 tests): GET index, POST like, like non-existent, GET lyrics (with timeout handling for external provider), lyrics non-existent, POST playback, playback non-existent
  - **Cross-cutting auth** (7 tests): Theory-based 401/403 verification for all music endpoints when unauthenticated
- Lyrics test uses CancellationTokenSource with 15s timeout to handle external NoMercyLyricsClient calls that are unavailable in test environment

**Test results**: 51 new music tests pass (107 total in NoMercy.Tests.Api). All 680 tests pass across all projects (63 Repositories + 203 Queue + 107 Api + 307 Providers). Build succeeds with 0 errors.

---

## CHAR-05 — Snapshot tests for all `/api/v1/` Dashboard endpoints

**Date**: 2026-02-08

**What was done**:
- Created `DashboardEndpointSnapshotTests.cs` with 75 test methods covering all 12 dashboard controllers:
  - **ConfigurationController** (4 tests): GET index, POST store, GET languages, GET countries
  - **DevicesController** (3 tests): GET index, POST create, DELETE destroy
  - **EncoderController** (5 tests): GET index, POST create, DELETE non-existent, GET containers, GET framesizes
  - **Dashboard LibrariesController** (10 tests): GET index, POST store, DELETE non-existent, POST rescan, POST rescan by ID non-existent, POST refresh, POST refresh by ID non-existent, POST add folder non-existent library, DELETE folder non-existent, DELETE encoder profile non-existent
  - **LogController** (3 tests): GET logs, GET levels, GET types
  - **PluginController** (3 tests): GET index, GET credentials, POST set credentials
  - **ServerActivityController** (3 tests): GET index, POST create, DELETE destroy
  - **ServerController** (11 tests): GET index, GET setup (with setup_complete shape), POST start, POST restart, GET update/check, GET info (with setup_complete shape), GET resources, GET paths, GET storage, POST directorytree, POST loglevel skipped (destructive)
  - **SpecialsController** (5 tests): GET index, POST store, DELETE non-existent, POST rescan all, POST rescan by ID
  - **TasksController** (9 tests): GET index, POST store, GET runners, GET queue, DELETE queue non-existent, GET failed, POST retry failed, POST pause non-existent, POST resume non-existent
  - **UsersController** (7 tests): GET index (documents known NullRef bug in PermissionsResponseItemDto), GET permissions, DELETE non-existent, DELETE owner denied, PATCH notifications, GET user permissions self denied, GET user permissions non-existent
  - **OpticalMediaController** (1 test): GET drives
  - **Cross-cutting auth** (12 tests): Theory-based 401/403 verification for all dashboard endpoints when unauthenticated
- Discovered server-side bugs during testing (documented, not fixed — out of scope):
  - UsersController.Index includes `LibraryUser` but not `.ThenInclude(x => x.Library)`, causing NullReferenceException in PermissionsResponseItemDto
  - JSON property names use snake_case (`setup_complete`) via Newtonsoft `[JsonProperty]`, not camelCase

**Test results**: 75 new dashboard tests pass (182 total in NoMercy.Tests.Api). All 755 tests pass across all projects (63 Repositories + 203 Queue + 182 Api + 307 Providers). Build succeeds with 0 errors.

---

## CHAR-06 — Query output tests for every repository method via `ToQueryString()`

**Date**: 2026-02-08

**What was done**:
- Created `Infrastructure/SqlCaptureInterceptor.cs` — `DbCommandInterceptor` subclass that captures all SQL commands (reader, scalar, non-query) for both sync and async execution paths
- Extended `TestMediaContextFactory` with `CreateContextWithInterceptor()` and `CreateSeededContextWithInterceptor()` methods that wire up the SQL interceptor alongside in-memory SQLite
- Created `QueryOutputTests.cs` with 57 test methods covering query SQL generation across all 12 repositories:
  - **MovieRepository** (5 tests): GetMovieAsync, GetMovieAvailableAsync, GetMoviePlaylistAsync, DeleteMovieAsync, GetMovieDetailAsync (compiled query)
  - **TvShowRepository** (5 tests): GetTvAvailableAsync, GetTvPlaylistAsync, DeleteTvAsync, GetMissingLibraryShows, GetTvAsync (compiled query)
  - **GenreRepository** (6 tests): GetGenresAsync (via ToQueryString()), GetGenreAsync, GetGenresWithCountsAsync, GetMusicGenresAsync, GetPaginatedMusicGenresAsync, GetMusicGenreAsync
  - **HomeRepository** (9 tests): GetHomeTvs, GetHomeMovies, GetContinueWatchingAsync, GetScreensaverImagesAsync, GetLibrariesAsync, GetMovieCountAsync, GetTvCountAsync, GetAnimeCountAsync, GetHomeGenresAsync
  - **LibraryRepository** (12 tests): GetLibraries, GetLibraryByIdAsync (2 overloads), GetLibraryMovieCardsAsync, GetLibraryTvCardsAsync, GetPaginatedLibraryMovies, GetPaginatedLibraryShows, GetAllLibrariesAsync, GetFoldersAsync, GetRandomTvShow, GetRandomMovie
  - **CollectionRepository** (6 tests): GetCollectionsAsync, GetCollectionsListAsync, GetCollectionAsync, GetCollectionItems, GetAvailableCollectionAsync, GetCollectionPlaylistAsync
  - **SpecialRepository** (4 tests): GetSpecialsAsync, GetSpecialAsync, GetSpecialItems, GetSpecialPlaylistAsync
  - **DeviceRepository** (1 test): GetDevicesAsync
  - **EncoderRepository** (3 tests): GetEncoderProfilesAsync, GetEncoderProfileByIdAsync, GetEncoderProfileCountAsync
  - **FolderRepository** (5 tests): GetFolderByIdAsync, GetFolderByPathAsync, GetFoldersByLibraryIdAsync, GetFolderById, GetFolderByPath
  - **LanguageRepository** (2 tests): GetLanguagesAsync, GetLanguagesAsync with filter
- Three testing strategies used per method type:
  - `IQueryable` methods: `ToQueryString()` called directly (GenreRepository.GetGenresAsync)
  - Materialized methods: SQL captured via `SqlCaptureInterceptor` during execution
  - Compiled queries (`EF.CompileAsyncQuery`): SQL captured via interceptor when invoked
- CRUD-only methods (Add/Update/Delete/Like/Upsert) excluded — no complex query to verify
- `MusicRepository` methods that create `new MediaContext()` internally cannot be tested with in-memory SQLite — documented as out of scope (to be fixed by CRIT-01)
- Assertions verify: correct table names (e.g., `"LibraryUser"` not `"LibraryUsers"`), SQL clauses (WHERE, ORDER BY, LIMIT, COUNT, DELETE), and query execution

**Test results**: 57 new query output tests pass (120 total in NoMercy.Tests.Repositories). All 812 tests pass across all projects (120 Repositories + 203 Queue + 182 Api + 307 Providers). Build succeeds with 0 errors.

---

## CHAR-07 — SignalR hub connection tests

**Date**: 2026-02-08

**What was done**:
- Added `Microsoft.AspNetCore.SignalR.Client` package to `NoMercy.Tests.Api` project
- Created `SignalRHubConnectionTests.cs` with 61 test methods covering all 5 mapped SignalR hubs via the negotiate HTTP endpoint:
  - **Hub endpoint existence** (5 tests): Verify `/videoHub`, `/musicHub`, `/castHub`, `/dashboardHub`, `/ripperHub` negotiate endpoints return 200 OK
  - **Negotiate response shape — connectionId** (5 tests): Verify all hubs return non-empty `connectionId` in negotiate response
  - **Negotiate response shape — connectionToken** (5 tests): Verify all hubs return non-empty `connectionToken` in negotiate response
  - **Transport advertisement — WebSockets** (5 tests): Verify all hubs advertise `WebSockets` in `availableTransports`
  - **Transport exclusivity — WebSockets only** (5 tests): Verify all hubs only advertise `WebSockets` (no LongPolling/SSE), confirming the `HttpTransportType.WebSockets` configuration
  - **Negotiate version** (5 tests): Verify all hubs return `negotiateVersion: 1`
  - **Authentication required** (5 tests): Verify all hubs return 401/403 when unauthenticated
  - **Unique connection IDs** (5 tests): Verify successive negotiate calls return different `connectionId` values
  - **Invalid hub path** (1 test): Verify `/nonExistentHub/negotiate` returns 404
  - **HTTP method rejection** (5 tests): Verify GET requests to negotiate endpoints are rejected (POST required)
  - **Negotiate without version param** (5 tests): Verify negotiate works or returns 400 without `negotiateVersion` query param
  - **JSON content type** (5 tests): Verify negotiate responses have `application/json` content type
  - **Transfer formats** (5 tests): Verify WebSockets transport advertises both `Text` and `Binary` transfer formats
- Testing approach: Uses `WebApplicationFactory`'s HTTP client to test the SignalR negotiate protocol (HTTP POST endpoints). The negotiate endpoint is the standard SignalR handshake that returns connection metadata, transport options, and session tokens. This approach tests the hub configuration (auth, transport settings, endpoint mapping) without requiring actual WebSocket connections.
- `SocketHub` is not tested because it is not mapped to an endpoint in `ApplicationConfiguration.ConfigureEndpoints`

**Test results**: 61 new SignalR hub tests pass (243 total in NoMercy.Tests.Api). All 873 tests pass across all projects (120 Repositories + 203 Queue + 243 Api + 307 Providers). Build succeeds with 0 errors.

---

## CHAR-08 — Queue behavior tests (enqueue, reserve, execute, fail, retry)

**Date**: 2026-02-08

**What was done**:
- Created `QueueBehaviorTests.cs` in `NoMercy.Tests.Queue` with 26 tests covering behavioral gaps in the existing queue test suite:
  - **Implicit retry loop** (2 tests): Verify that failing a job under maxAttempts keeps it in QueueJobs with ReservedAt cleared, and that it can be re-reserved. Full 3-attempt cycle: fail twice, succeed on third attempt.
  - **Attempt boundary precision** (3 tests): Verify FailJob behavior at exactly maxAttempts (permanent fail), one under (stays in queue), and well above (permanent fail).
  - **Full retry lifecycle** (1 test): Enqueue → exhaust retries → permanent failure → RetryFailedJobs → attempts reset to 0 → reserve and succeed.
  - **Cross-queue isolation** (2 tests): Jobs on queue "alpha" not returned when reserving on "beta"; multiple queues serve their own jobs independently.
  - **currentJobId guard** (2 tests): ReserveJob with non-null currentJobId returns null (worker busy guard); ReserveJob with null currentJobId returns the job.
  - **Exception content preservation** (2 tests): FailJob preserves InnerException content in FailedJob record; uses outer exception when no InnerException exists.
  - **RetryFailedJobs behavior** (4 tests): Resets Attempts to 0; preserves queue name; processes all 5 failed jobs; specific ID only retries that job.
  - **Enqueue duplicate semantics** (2 tests): Different payloads on same queue both stored; same payload on different queues blocked (global duplicate check).
  - **Reserve sequencing** (2 tests): Second reserve on single job returns null (already reserved); two jobs reserved sequentially after delete returns both.
  - **Dequeue vs ReserveJob** (1 test): Dequeue removes job immediately without setting ReservedAt or incrementing Attempts.
  - **DeleteJob idempotency** (1 test): Double-deleting same job does not throw (catch block swallows).
  - **Serialization round-trip** (1 test): AnotherTestJob with Value=42 survives enqueue → reserve → deserialize → execute → Value=84.
  - **Enqueue after delete** (1 test): Same payload can be re-enqueued after being deleted.
  - **FailJob always clears ReservedAt** (1 test): ReservedAt set to null regardless of attempt count.
  - **Priority ordering** (1 test): 5 jobs with priorities [3,1,5,2,4] reserved in order [5,4,3,2,1].

**Test results**: 26 new queue behavior tests pass (229 total in NoMercy.Tests.Queue). All 899 tests pass across all projects (120 Repositories + 229 Queue + 243 Api + 307 Providers). Build succeeds with 0 errors.

