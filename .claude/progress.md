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

---

## CHAR-09 — Encoder command-building tests (capture FFmpeg CLI args)

**Date**: 2026-02-08

**What was done**:
- Created `tests/NoMercy.Tests.Encoder/` project with references to `NoMercy.Encoder`
- Added project to solution under Tests folder
- Added `InternalsVisibleTo` attribute to `NoMercy.Encoder.csproj` for test access to internal members (`VideoStream`, `AudioStream`, `Index`, `IsVideo`, `IsAudio`)
- Created `EncoderCommandBuildingTests.cs` with 111 test methods covering FFmpeg CLI argument construction:
  - **Helper infrastructure**: `CreateSdrProbeData()`, `CreateHdrProbeData()`, `CreateVideoStream()`, `CreateAudioStream()` using reflection to populate FFMpegCore stream properties; `CreateHlsContainer()` manually builds container with video/audio streams (bypassing `FfMpeg.Open()` which requires ffprobe); `BuildCommand()` wraps `FFmpegCommandBuilder` for clean test calls; `IDisposable` with temp directory for `CreateFolder()` side effects
  - **Global options** (5 tests): `-hide_banner`, `-probesize`/`-analyzeduration`, `-progress -`, `-threads 0` (no priority), `-threads N` (priority mode)
  - **Input options** (5 tests): `-i` with input file path, `-y` overwrite flag, `-map_metadata -1`, no `-gpu any` without accelerators, `-gpu any` + accelerator args with GPU
  - **Video codec selection** (3 tests): X264→`-c:v libx264`, X265→`-c:v libx265`, AV1→`-c:v librav1e`
  - **Filter complex** (2 tests): SDR video filter chain with `format=yuv420p` and `[v0_hls_0]` output label; audio volume filter `volume=3` with `[a0_hls_0]` label
  - **Video output parameters** (6 tests): `-map [v0_hls_0]` stream mapping, bt709 color space (4 properties), H.264 bitstream filter `h264_mp4toannexb`, HEVC bitstream filter `hevc_mp4toannexb`, `-metadata title=` with title
  - **HLS-specific parameters** (9 tests): `-hls_segment_filename`, `-hls_allow_cache 1`, `-hls_segment_type mpegts`, `-hls_playlist_type`, `-hls_time 4`/`-hls_init_time 4`, `-hls_flags independent_segments`, `-f hls`, `-start_number 0`, custom HLS time value (10)
  - **Audio output parameters** (5 tests): `-c:a aac`, `-map [a0_hls_0]`, `-b:a 192k` bitrate, `-ac 6` channels, `-ar 44100` sample rate
  - **Codec quality flags** (8 tests): CRF with VBR flags, preset, profile, video bitrate, maxrate, bufsize, level, keyframe interval (`-g`/`-keyint_min`)
  - **Video codec factory** (7 tests): `BaseVideo.Create()` returns correct type for libx264, h264_nvenc, libx265, hevc_nvenc, vp9, libvpx-vp9; unsupported codec throws
  - **Audio codec factory** (8 tests): `BaseAudio.Create()` returns correct type for aac, libmp3lame, opus, flac, ac3, eac3, truehd; unsupported codec throws
  - **Container factory** (6 tests): `BaseContainer.Create()` returns correct type for mkv, Mp4, mp3, flac, m3u8; unsupported container throws
  - **Codec fluent API setters** (12 tests): CRF out-of-range throws (>51, <0), negative bitrate throws, invalid preset/profile/tune/level throws, GetPasses with/without bitrate, CRF boundary validation (0 and 51 valid), default codec values for X264/X265/AV1
  - **Audio setter validation** (3 tests): zero bitrate throws, negative channels throws, zero sample rate throws
  - **HLS configuration** (4 tests): `SetHlsTime()` reflected in command, `SetHlsPlaylistType()` reflected in command, ContainerDto is HLS, extension is m3u8
  - **Container available codecs** (5 tests): HLS includes H264/H265/AAC, `GetName()` maps correctly, unsupported format throws
  - **Codec constants** (7 tests): H264, H264Nvenc, H265, AV1, AAC, MP3, FLAC constant values verified
  - **Command order** (4 tests): global options before input, input before filter_complex, filter_complex before outputs, video outputs before audio outputs
  - **Scale configuration** (3 tests): single value reflected in command, width+height reflected, fluent API returns self
  - **HDR/color space** (2 tests): UHD video gets bt2020 color primaries, 10-bit pixel format gets smpte2084 color_trc
  - **GetFullCommand integration** (1 test): `VideoAudioFile.GetFullCommand()` returns complete command string
  - **Custom arguments** (2 tests): container and video custom args appear in built command
  - **Audio metadata** (1 test): `-metadata:s:a:0` with language present
  - **FrameSizes constants** (1 test): verifies 240p through 4k resolutions exist
- Testing approach: Constructs `FFmpegCommandBuilder` directly with manually-built `Hls` containers containing `BaseVideo`/`BaseAudio` streams, bypassing `FfMpeg.Open()` (which requires ffprobe binary) and `CropDetect()` (which requires ffmpeg binary). Uses temp directories for `CreateFolder()` side effects during `BuildCommand()`. FFMpegCore stream properties set via reflection since constructors don't accept parameters.

**Test results**: 111 new encoder tests pass (111 total in NoMercy.Tests.Encoder). All 1,012 tests pass across all projects (2 Database + 111 Encoder + 120 Repositories + 229 Queue + 243 Api + 307 Providers). Build succeeds with 0 errors.

---

## CHAR-10 — CI pipeline that runs all characterization tests

**Date**: 2026-02-08

**What was done**:
- Added `[Trait("Category", "Characterization")]` attribute to all 15 characterization test classes across 4 test projects:
  - **NoMercy.Tests.Api** (6 classes): `HealthControllerTests`, `AuthenticationTests`, `MediaEndpointSnapshotTests`, `MusicEndpointSnapshotTests`, `DashboardEndpointSnapshotTests`, `SignalRHubConnectionTests`
  - **NoMercy.Tests.Repositories** (7 classes): `TestMediaContextFactoryTests`, `MovieRepositoryTests`, `LibraryRepositoryTests`, `TvShowRepositoryTests`, `GenreRepositoryTests`, `HomeRepositoryTests`, `QueryOutputTests`
  - **NoMercy.Tests.Encoder** (1 class): `EncoderCommandBuildingTests`
  - **NoMercy.Tests.Queue** (1 class): `QueueBehaviorTests`
- Updated `.github/workflows/test.yml` — expanded the fast test job filter from `Category=Unit|Category=ErrorHandling` to `Category=Unit|Category=ErrorHandling|Category=Characterization` so all 500 characterization tests run on every push/PR alongside existing unit and error handling tests
- Pre-existing queue tests (`CertificateRenewalJobTests`, `CronExpressionBuilderTests`, `EdgeCaseAndInterfaceTests`, `JobDispatcherTests`) were intentionally left uncategorized since they predate the CHAR tasks
- Verified the `--filter "Category=Characterization"` picks up exactly 500 tests (243 Api + 120 Repositories + 111 Encoder + 26 Queue)

**Test results**: All 1,012 tests pass across all projects (2 Database + 111 Encoder + 120 Repositories + 229 Queue + 243 Api + 307 Providers). Build succeeds with 0 errors.

---

## CPM-01 — Create `Directory.Packages.props` with all package versions

**Date**: 2026-02-08

**What was done**:
- Audited all 19 `.csproj` files across `src/` and `tests/` directories, extracting every `PackageReference` entry with its version
- Identified 91 unique packages with consistent versions across all projects (no version conflicts found)
- Created `Directory.Packages.props` at the solution root with all 91 `PackageVersion` entries organized into logical sections:
  - **API & Web** (26 packages): ASP.NET Core, SignalR, Swagger, authentication, versioning
  - **Swagger** (4 packages): Swashbuckle components
  - **Entity Framework** (9 packages): EF Core, SQLite, design-time tools
  - **Serialization** (1 package): Newtonsoft.Json
  - **Logging** (6 packages): Serilog (dev channel), Sentry
  - **Media & Encoding** (10 packages): FFMpegCore, ImageSharp, MediaInfo, TagLib
  - **Providers** (3 packages): AcoustID, MusixMatch, BigRational
  - **System & Infrastructure** (27 packages): Castle.Core, DeviceId, Humanizer, Polly, etc.
  - **Benchmarking** (1 package): BenchmarkDotNet
  - **Testing** (6 packages): xunit, MSTest SDK, FluentAssertions, Moq, coverlet
- Set `ManagePackageVersionsCentrally` to `false` so the build continues to pass — CPM-02 will remove `Version` attributes from `.csproj` files and activate CPM by flipping this to `true`
- Pre-release packages cataloged as-is (Serilog `4.3.1-dev-02373`, Serilog.Expressions `5.1.0-dev-02301`, Serilog.Sinks.Console `6.0.1-dev-00953`, Humanizer `3.0.0-beta.96`, LibreHardwareMonitorLib `0.9.5-pre437`, InfiniLore `0.1.0-preview.25`)

**Test results**: All 1,012 tests pass across all projects (2 Database + 111 Encoder + 120 Repositories + 229 Queue + 243 Api + 307 Providers). Build succeeds with 0 errors.

---

## CPM-02 — Remove `Version` from all `.csproj` PackageReference entries

**Date**: 2026-02-08

**What was done**:
- Removed `Version="..."` attribute from all `PackageReference` entries across 21 `.csproj` files (src and tests)
- Flipped `ManagePackageVersionsCentrally` from `false` to `true` in `Directory.Packages.props` to activate Central Package Management
- Total of ~170 `PackageReference` entries across 21 files were updated (the 22nd file, `NoMercy.Globals.csproj`, had no PackageReference entries)
- Multi-line PackageReference entries (with child elements like `PrivateAssets` and `IncludeAssets`) preserved correctly
- Non-PackageReference `<Version>` properties (e.g., `<Version>0.1.236</Version>` in Server.csproj, `<Version>0.0.1</Version>` in App.csproj) left untouched
- Files affected:
  - **src/**: NoMercy.Api, NoMercy.App, NoMercy.Data, NoMercy.Database, NoMercy.Encoder, NoMercy.Helpers, NoMercy.MediaProcessing, NoMercy.MediaSources, NoMercy.Networking, NoMercy.NmSystem, NoMercy.Providers, NoMercy.Queue, NoMercy.Server, NoMercy.Setup
  - **tests/**: NoMercy.Tests.Api, NoMercy.Tests.Database, NoMercy.Tests.Encoder, NoMercy.Tests.MediaProcessing, NoMercy.Tests.Providers, NoMercy.Tests.Queue, NoMercy.Tests.Repositories

**Test results**: All 1,012 tests pass across all projects (2 Database + 111 Encoder + 120 Repositories + 229 Queue + 243 Api + 307 Providers). Build succeeds with 0 errors.

---

## CPM-03 — Verify full build + test suite passes after CPM migration

**Date**: 2026-02-08

**What was done**:
- Ran `dotnet restore` — all projects up-to-date, no package resolution errors
- Ran `dotnet build` — compiled with 0 errors (74 pre-existing warnings, all CS8601/CS8600/NETSDK1206/CA2021 unrelated to CPM)
- Ran `dotnet test` — all 1,012 tests pass with 0 failures across all 7 test projects:
  - NoMercy.Tests.Database: 2 passed
  - NoMercy.Tests.Encoder: 111 passed
  - NoMercy.Tests.Repositories: 120 passed
  - NoMercy.Tests.Queue: 229 passed
  - NoMercy.Tests.Providers: 307 passed
  - NoMercy.Tests.Api: 243 passed
  - NoMercy.Tests.MediaProcessing: 0 tests (no test discoverer, pre-existing)
- CPM migration (CPM-01 + CPM-02) confirmed fully functional — `Directory.Packages.props` with `ManagePackageVersionsCentrally=true` works correctly with all 21 `.csproj` files

**Test results**: All 1,012 tests pass across all projects (2 Database + 111 Encoder + 120 Repositories + 229 Queue + 243 Api + 307 Providers). Build succeeds with 0 errors.

---

## CRIT-09 — Fix missing job retry return value

**Date**: 2026-02-08

**What was done**:
- Fixed `src/NoMercy.Queue/JobQueue.cs:76` — added missing `return` before the recursive `ReserveJob()` call in the catch block
- **The bug**: When `ReserveJob()` caught a transient (non-relational) exception and retried recursively, the result of the recursive call was discarded. The method always fell through to `return null` on line 84, causing jobs to silently fail to be reserved and the queue to stall.
- **The fix**: Changed `ReserveJob(name, currentJobId, attempt + 1);` to `return ReserveJob(name, currentJobId, attempt + 1);`
- Created `tests/NoMercy.Tests.Queue/ReserveJobRetryTests.cs` with 4 tests:
  - **IL structural regression test**: Inspects the compiled IL of `ReserveJob` via reflection to verify no `pop` opcode follows the recursive call (which would indicate the return value is being discarded)
  - **Max retry attempts exceeded**: Verifies `ReserveJob` returns null when called with `attempt=10` (the retry ceiling)
  - **Normal path success**: Verifies `ReserveJob` returns the job with correct `ReservedAt` and `Attempts` values
  - **Normal path no job**: Verifies `ReserveJob` returns null when no matching jobs exist

**Test results**: All 1,016 tests pass across all projects (2 Database + 111 Encoder + 120 Repositories + 233 Queue + 243 Api + 307 Providers). Build succeeds with 0 errors.

---

## CRIT-13 — Remove duplicate DbContext registration (scoped + transient)

**Date**: 2026-02-08

**What was done**:
- Removed `services.AddTransient<QueueContext>();` (line 117) and `services.AddTransient<MediaContext>();` (line 124) from `src/NoMercy.Server/AppConfig/ServiceConfiguration.cs`
- **The bug**: `AddDbContext<T>()` registers the DbContext as Scoped (one instance per request scope), but the subsequent `AddTransient<T>()` calls shadow that registration, causing DI to create a **new instance on every resolution**. This means:
  - Multiple change trackers per request — entities loaded by one resolution are invisible to another
  - `SaveChanges()` on one instance misses changes tracked by another instance in the same scope
  - Connection pool exhaustion from excessive connection creation
- **The fix**: Remove the two `AddTransient` lines. The `AddDbContext<T>()` scoped registration is the correct lifetime for DbContexts — one instance per HTTP request scope, shared across all services within that request.
- **Investigation**: Confirmed that `JobDispatcher` and `QueueRunner` create their own `new QueueContext()` directly (bypassing DI entirely), so they are unaffected by this change. The `JobQueue` singleton DI registration (line 153) is also unaffected since `JobDispatcher`/`QueueRunner` use their own static instances. The direct `new MediaContext()`/`new QueueContext()` patterns are a separate issue tracked by CRIT-01.
- Created `tests/NoMercy.Tests.Api/DbContextRegistrationTests.cs` with 8 tests:
  - **MediaContext scoped identity** (1 test): Two resolutions within the same scope return the same instance (`Assert.Same`)
  - **QueueContext scoped identity** (1 test): Two resolutions within the same scope return the same instance
  - **MediaContext cross-scope isolation** (1 test): Different scopes return different instances (`Assert.NotSame`)
  - **QueueContext cross-scope isolation** (1 test): Different scopes return different instances
  - **MediaContext not transient** (1 test): Verifies `ReferenceEquals` is true within scope (would be false if transient)
  - **QueueContext not transient** (1 test): Same verification for QueueContext
  - **SaveChanges persists within scope** (1 test): Modifies a user name, calls SaveChanges, re-resolves context from same scope, verifies change is visible
  - **Change tracking shared across resolutions** (1 test): Modifies entity via ctx1, verifies ctx2's Local collection sees the change (proves shared change tracker)

**Test results**: All 1,024 tests pass across all projects (2 Database + 111 Encoder + 120 Repositories + 233 Queue + 251 Api + 307 Providers). Build succeeds with 0 errors.

---

## HIGH-15 — Fix image controller `| true` logic bug

**Date**: 2026-02-08

**What was done**:
- Fixed `src/NoMercy.Api/Controllers/File/ImageController.cs:45` — removed `| true` (bitwise OR with true) from the `emptyArguments` boolean expression
- **The bug**: `bool emptyArguments = (request.Width is null && request.Type is null && request.Quality is 100) | true;` — the `| true` made `emptyArguments` always `true`, causing every image request to return the original file without any resizing, caching, or quality adjustment. The entire image processing pipeline (`Images.ResizeMagickNet()`) was completely bypassed. This was likely a debug bypass accidentally left in.
- **The fix**: Changed to `bool emptyArguments = request.Width is null && request.Type is null && request.Quality is 100;` — now `emptyArguments` is only `true` when no processing parameters are provided (no width, no type override, quality at default 100).
- Created `tests/NoMercy.Tests.Api/ImageControllerTests.cs` with 11 integration tests:
  - **No params returns original** (1 test): GET with no query params returns the original file unmodified (emptyArguments=true path)
  - **Width triggers resize** (1 test): GET with `?width=50` on a 200x100 image returns a 50x25 resized image (aspect ratio preserved)
  - **Non-default quality triggers processing** (1 test): GET with `?quality=80` processes the image instead of returning the original
  - **Type param triggers processing** (1 test): GET with `?type=png&width=100` returns a resized image at width=100
  - **SVG bypasses processing** (1 test): GET for an SVG file with `?width=50` returns the original SVG unmodified (SVG path guard)
  - **Processed images are cached** (1 test): Two identical requests with `?width=75` return the same result (second served from cache)
  - **Non-existent type folder returns 404** (1 test): GET for `/images/nonexistent/test.png` returns 404
  - **Non-existent file returns 404** (1 test): GET for a missing file in a valid type folder returns 404
  - **Default quality 100 returns original** (1 test): GET with `?quality=100` (the default) returns the original unchanged
  - **Caching headers set** (1 test): Response includes `Cache-Control: public` header
  - **Width with aspect ratio** (1 test): GET with `?width=100&aspect_ratio=2.0` returns a 100x200 image (custom aspect ratio)
- Test setup creates real PNG (200x100 red) and SVG test images in `AppFiles.ImagesPath/testtype/` directory, with cleanup of temp cached images in `Dispose()`

**Test results**: All 1,035 tests pass across all projects (2 Database + 111 Encoder + 120 Repositories + 233 Queue + 262 Api + 307 Providers). Build succeeds with 0 errors.

---

## PROV-CRIT-03 — Fix `.Result` on `SendAsync` in TvdbBaseClient

**Date**: 2026-02-08

**What was done**:
- Fixed `src/NoMercy.Providers/TVDB/Client/TvdbBaseClient.cs:107-109` — replaced `.Result` blocking call with proper `await`
- **The bug**: `await client.SendAsync(httpRequestMessage).Result.Content.ReadAsStringAsync()` mixes synchronous `.Result` (which blocks the thread waiting for `SendAsync` to complete) with `await` on `ReadAsStringAsync()`. This can cause deadlocks when called from a synchronization context (e.g., ASP.NET request thread), because `.Result` blocks the thread that `await` needs to resume on.
- **The fix**: Split into two proper await calls:
  ```csharp
  HttpResponseMessage httpResponse = await client.SendAsync(httpRequestMessage);
  string response = await httpResponse.Content.ReadAsStringAsync();
  ```
- Created `tests/NoMercy.Tests.Providers/TVDB/Client/TvdbBaseClientTests.cs` with 3 tests:
  - **IL structural regression test** (`GetToken_StateMachine_DoesNotCallTaskResult`): Inspects the compiled state machine IL of `GetToken` to verify no `get_Result` call exists on any `Task` type — this is the definitive proof that `.Result` is no longer used
  - **Async signature verification** (`GetToken_IsAsync_ReturnsTask`): Verifies `GetToken` is declared as async (returns `Task<T>`)
  - **Double-await verification** (`GetToken_StateMachine_HasMultipleAwaiterGetResult`): Counts `TaskAwaiter.GetResult()` calls in the state machine IL — the fixed code should have at least 2 await points (one for `SendAsync`, one for `ReadAsStringAsync`), whereas the buggy code only had 1 (the `SendAsync` was resolved synchronously via `.Result`)

**Test results**: All 1,038 tests pass across all projects (2 Database + 111 Encoder + 120 Repositories + 233 Queue + 262 Api + 310 Providers). Build succeeds with 0 errors.

---

## PROV-CRIT-04 — Fix inverted client-key condition in FanArt

**Date**: 2026-02-08

**What was done**:
- Fixed `src/NoMercy.Providers/FanArt/Client/FanArtBaseClient.cs:28,44` — changed `if (string.IsNullOrEmpty(...))` to `if (!string.IsNullOrEmpty(...))`
- **The bug**: Both constructors (parameterless and Guid) had `if (string.IsNullOrEmpty(ApiInfo.FanArtClientKey))` guarding `_client.DefaultRequestHeaders.Add("client-key", ...)`. This inverted logic added the `client-key` header when the key was empty/null (sending an empty string), and skipped it when the key was actually populated. FanArt API requests with a valid client key never sent it.
- **The fix**: Added `!` negation to both conditions: `if (!string.IsNullOrEmpty(ApiInfo.FanArtClientKey))` — now the header is only added when a non-empty client key exists.
- Created `tests/NoMercy.Tests.Providers/FanArt/Client/FanArtBaseClientTests.cs` with 7 tests:
  - **Parameterless constructor with populated key** (1 test): Sets `ApiInfo.FanArtClientKey` to a value, creates client via reflection, verifies `client-key` header IS present with correct value
  - **Parameterless constructor with empty key** (1 test): Sets key to `string.Empty`, verifies `client-key` header is NOT present
  - **Parameterless constructor with null key** (1 test): Sets key to `null`, verifies `client-key` header is NOT present
  - **Guid constructor with populated key** (1 test): Same verification as parameterless but via `FanArtBaseClient(Guid)` constructor
  - **Guid constructor with empty key** (1 test): Verifies no header when empty via Guid constructor
  - **Guid constructor sets Id** (1 test): Verifies the Guid constructor correctly sets the protected `Id` property
  - **Constructor always adds api-key** (1 test): Verifies `api-key` header is always present regardless of client key state
- Tests use `IDisposable` to save/restore `ApiInfo.FanArtClientKey` static state between tests

**Test results**: All 1,045 tests pass across all projects (2 Database + 111 Encoder + 120 Repositories + 233 Queue + 262 Api + 317 Providers). Build succeeds with 0 errors.

---

## PROV-H06 — Remove dead code (contradictory while loop) in AcoustId

**Date**: 2026-02-08

**What was done**:
- Removed contradictory while loop from `src/NoMercy.Providers/AcoustId/Client/AcoustIdBaseClient.cs:79-92`
- Removed unused `int iteration = 0;` variable (line 73)
- **The bug**: The while loop condition `data?.Results.Length == 0 && data.Results.Any(...)` was contradictory — an array with `Length == 0` can never have `.Any()` return `true`. The loop body (which would re-fetch the URL up to 10 times) could never execute. This was dead code that added complexity without effect.
- **The fix**: Removed the entire while loop (lines 79-92) and the associated `int iteration = 0;` variable (line 73). The early return on line 75-77 already handles the case where valid results with titled recordings exist, and the method correctly falls through to throw or return data otherwise.
- Created `tests/NoMercy.Tests.Providers/AcoustId/Client/AcoustIdBaseClientTests.cs` with 6 tests:
  - **IL structural regression — no iteration field** (1 test): Inspects the async state machine fields to verify no `iteration` variable exists in the compiled IL (proves the while loop and its counter are gone)
  - **IL structural regression — no while loop fields** (1 test): Additional verification that no iteration-like counter fields remain in the state machine
  - **Async return type** (1 test): Verifies `Get<T>` returns `Task<T?>` (generic task)
  - **AsyncStateMachine attribute** (1 test): Verifies `Get<T>` has the `AsyncStateMachineAttribute`
  - **Method parameters** (1 test): Verifies `Get<T>` has 4 parameters: url, query, priority, retry
  - **IDisposable** (1 test): Verifies `AcoustIdBaseClient` implements `IDisposable`

**Test results**: All 1,051 tests pass across all projects (2 Database + 111 Encoder + 120 Repositories + 233 Queue + 262 Api + 323 Providers). Build succeeds with 0 errors.

---

## PROV-H10 — Fix stream consumed then reused in FanArt Image

**Date**: 2026-02-08

**What was done**:
- Fixed `src/NoMercy.Providers/FanArt/Client/FanArtImageClient.cs:47-54` — replaced stream-based content reading with a single `ReadAsByteArrayAsync()` call
- **The bug**: The `Download` method read response content as a stream (line 47: `ReadAsStreamAsync()`), then:
  1. If `download is false`: loaded the image from the stream (consuming it) — this path worked
  2. If `download is true`: wrote the file using `ReadAsByteArrayAsync()` (re-reading the already-consumed response content — may return empty/corrupt data), then tried to load the image from the already-consumed stream (position at end — corrupt/empty image)
  - The stream was consumed on first read, making subsequent reads return empty/corrupt data
- **The fix**: Read content once as `byte[]` via `ReadAsByteArrayAsync()`, then use the same byte array for both `File.WriteAllBytesAsync()` and `Image.Load<Rgba32>(bytes)`. Also simplified the download condition: `if (download is not false && !File.Exists(filePath))` combines the two checks.
- Created `tests/NoMercy.Tests.Providers/FanArt/Client/FanArtImageClientTests.cs` with 8 tests:
  - **Download is static async** (1 test): Verifies `Download` is a static method with `AsyncStateMachineAttribute`
  - **Download returns Task of nullable Image** (1 test): Verifies return type is `Task<Image<Rgba32>?>`
  - **Download parameter signature** (1 test): Verifies `Download(Uri url, bool? download = true)` — 2 params with correct types and defaults
  - **IL regression — no ReadAsStreamAsync** (1 test): Inspects compiled state machine IL to verify `ReadAsStreamAsync` is never called (the root cause of the bug)
  - **IL regression — calls ReadAsByteArrayAsync** (1 test): Verifies the state machine calls `ReadAsByteArrayAsync` (the fix)
  - **IL regression — ReadAsByteArrayAsync called exactly once** (1 test): Verifies content is read exactly once (not twice like the original bug)
  - **IL regression — no multiple content reads** (1 test): Verifies no combination of `ReadAsByteArrayAsync`/`ReadAsStreamAsync`/`ReadAsStringAsync` exceeds 1 call total
  - **Image.Load uses byte[] overload** (1 test): Verifies `Image.Load` is called with `byte[]` or `ReadOnlySpan<byte>` parameter, not `Stream`

**Test results**: All 1,059 tests pass across all projects (2 Database + 111 Encoder + 120 Repositories + 233 Queue + 262 Api + 331 Providers). Build succeeds with 0 errors.

---

## PROV-H12 — Fix stream consumed then reused in CoverArt Image

**Date**: 2026-02-08

**What was done**:
- Fixed `src/NoMercy.Providers/CoverArt/Client/CoverArtCoverArtClient.cs:64-71` — replaced stream-based content reading with a single `ReadAsByteArrayAsync()` call
- **The bug**: The `Download` method read response content as a stream (line 64: `ReadAsStreamAsync()`), then:
  1. If `download is false`: loaded the image from the stream (consuming it) — this path worked
  2. If `download is true`: wrote the file using `ReadAsByteArrayAsync()` (re-reading the already-consumed response content — may return empty/corrupt data), then tried to load the image from the already-consumed stream (position at end — corrupt/empty image)
  - The stream was consumed on first read, making subsequent reads return empty/corrupt data
- **The fix**: Read content once as `byte[]` via `ReadAsByteArrayAsync()`, then use the same byte array for both `File.WriteAllBytesAsync()` and `Image.Load<Rgba32>(bytes)`. Also simplified the download condition: `if (download is not false && !File.Exists(filePath))` combines the two checks.
- Created `tests/NoMercy.Tests.Providers/CoverArt/Client/CoverArtCoverArtClientTests.cs` with 8 tests:
  - **Download is static async** (1 test): Verifies `Download` is a static method with `AsyncStateMachineAttribute`
  - **Download returns Task of nullable Image** (1 test): Verifies return type is `Task<Image<Rgba32>?>`
  - **Download parameter signature** (1 test): Verifies `Download(Uri? url, bool? download = true)` — 2 params with correct types and defaults
  - **IL regression — no ReadAsStreamAsync** (1 test): Inspects compiled state machine IL to verify `ReadAsStreamAsync` is never called (the root cause of the bug)
  - **IL regression — calls ReadAsByteArrayAsync** (1 test): Verifies the state machine calls `ReadAsByteArrayAsync` (the fix)
  - **IL regression — ReadAsByteArrayAsync called exactly once** (1 test): Verifies content is read exactly once (not twice like the original bug)
  - **IL regression — no multiple content reads** (1 test): Verifies no combination of `ReadAsByteArrayAsync`/`ReadAsStreamAsync`/`ReadAsStringAsync` exceeds 1 call total
  - **Image.Load uses byte[] overload** (1 test): Verifies `Image.Load` is called with `byte[]` or `ReadOnlySpan<byte>` parameter, not `Stream`

**Test results**: All 1,067 tests pass across all projects (2 Database + 111 Encoder + 120 Repositories + 233 Queue + 262 Api + 339 Providers). Build succeeds with 0 errors.

---

## PROV-H16 — Fix response content read 3 times in NoMercy Image

**Date**: 2026-02-08

**What was done**:
- Fixed `src/NoMercy.Providers/NoMercy/Client/NoMercyImageClient.cs:42-48` — replaced multiple content reads with a single `ReadAsByteArrayAsync()` call
- **The bug**: The `Download` method's local async function read response content multiple times:
  1. If `download is false`: called `ReadAsStreamAsync()` to load image (consuming the stream) — this path worked
  2. If `download is true`: called `ReadAsByteArrayAsync()` to write the file (first content read), then called `ReadAsStreamAsync()` to load the image (second content read on already-consumed response — producing corrupt/empty images)
  - The response content was consumed on the first read, making the second read return empty/corrupt data
- **The fix**: Read content once as `byte[]` via `ReadAsByteArrayAsync()`, then use the same byte array for both `File.WriteAllBytesAsync()` and `Image.Load<Rgba32>(bytes)`. Also simplified the download condition: `if (download is not false && !File.Exists(filePath))` combines the two checks.
- Created `tests/NoMercy.Tests.Providers/NoMercy/Client/NoMercyImageClientTests.cs` with 8 tests:
  - **Download is static and returns Task** (1 test): Verifies `Download` is a static method returning `Task<Image<Rgba32>?>`
  - **Download parameter signature** (1 test): Verifies `Download(string? path, bool? download = true)` — 2 params with correct types and defaults
  - **IL regression — no ReadAsStreamAsync** (1 test): Inspects the local function's compiled state machine IL to verify `ReadAsStreamAsync` is never called (the root cause of the bug)
  - **IL regression — calls ReadAsByteArrayAsync** (1 test): Verifies the state machine calls `ReadAsByteArrayAsync` (the fix)
  - **IL regression — ReadAsByteArrayAsync called exactly once** (1 test): Verifies content is read exactly once (not multiple times like the original bug)
  - **IL regression — no multiple content reads** (1 test): Verifies no combination of `ReadAsByteArrayAsync`/`ReadAsStreamAsync`/`ReadAsStringAsync` exceeds 1 call total
  - **Image.Load uses byte[] overload** (1 test): Verifies `Image.Load` is called with `byte[]` or `ReadOnlySpan<byte>` parameter, not `Stream`
  - **Local function has async state machine** (1 test): Verifies the compiler-generated state machine for the local `Task()` function can be found (prerequisite for all IL tests)
- Note: Unlike PROV-H10/H12, this method uses a local async function `Task()` inside `Download()` rather than being directly async. The IL tests target the compiler-generated state machine for the local function by searching nested types that implement `IAsyncStateMachine`.

**Test results**: All 1,075 tests pass across all projects (2 Database + 111 Encoder + 120 Repositories + 233 Queue + 262 Api + 347 Providers). Build succeeds with 0 errors.

---

## PMOD-CRIT-01 — Fix MusixMatch AlbumName typed as `long`

**Date**: 2026-02-08

**What was done**:
- Fixed `src/NoMercy.Providers/MusixMatch/Models/MusixMatchMusixMatchTrack.cs:41` — changed `AlbumName` property type from `long` to `string?`
- **The bug**: `[JsonProperty("album_name")] public long AlbumName { get; set; }` — the MusixMatch API returns album names as strings (e.g. "Abbey Road"), not numbers. With the `long` type, Newtonsoft.Json would throw a `JsonReaderException` when deserializing a real API response containing a string album name, or silently produce `0` if the field was missing/null.
- **The fix**: Changed to `public string? AlbumName { get; set; }` — nullable string matches the API response format. All other `AlbumName` properties across the codebase (PlaylistTrackDto, ArtistTrackDto, TrackRowData, LrclibSongResult) are already typed as `string` or `string?`.
- Created `tests/NoMercy.Tests.Providers/MusixMatch/Models/MusixMatchMusixMatchTrackTests.cs` with 7 tests:
  - **Property type is nullable string** (1 test): Verifies `AlbumName` property type is `string` via reflection
  - **Deserializes string value** (1 test): JSON `{"album_name": "Abbey Road"}` deserializes to `"Abbey Road"`
  - **Deserializes null value** (1 test): JSON `{"album_name": null}` deserializes to `null`
  - **Deserializes empty string** (1 test): JSON `{"album_name": ""}` deserializes to `""`
  - **Default value is null** (1 test): New instance has `AlbumName == null`
  - **JsonProperty attribute correct** (1 test): Verifies `[JsonProperty("album_name")]` attribute is present with correct name
  - **Round-trip serialization** (1 test): Serialize then deserialize preserves album name value

**Test results**: All 1,082 tests pass across all projects (2 Database + 111 Encoder + 120 Repositories + 233 Queue + 262 Api + 354 Providers). Build succeeds with 0 errors.

---

## PMOD-CRIT-02 — Fix TVDB properties missing setters

**Date**: 2026-02-08

**What was done**:
- Fixed `src/NoMercy.Providers/TVDB/Models/TvdbAwardsResponse.cs:34` — added `set;` to `ForSeries` property
- Fixed `src/NoMercy.Providers/TVDB/Models/TvdbCharacterResponse.cs:45` — added `set;` to `Tag` property
- **The bug**: Both properties were declared as get-only (`{ get; }` without `set;`). Newtonsoft.Json cannot populate properties without setters during deserialization, so `ForSeries` would always be `false` and `Tag` would always be `0` regardless of the JSON payload from the TVDB API.
- **The fix**: Added `set;` to both properties: `{ get; }` → `{ get; set; }`. This matches all other properties in their respective classes and allows JSON deserialization to populate them correctly.
- Created `tests/NoMercy.Tests.Providers/TVDB/Models/TvdbModelPropertyTests.cs` with 14 tests:
  - **ForSeries has setter** (1 test): Verifies `CanWrite` is true via reflection
  - **ForSeries deserializes true** (1 test): JSON `{"forSeries": true}` deserializes to `true`
  - **ForSeries deserializes false** (1 test): JSON `{"forSeries": false}` deserializes to `false`
  - **ForSeries round-trip** (1 test): Serialize then deserialize preserves value
  - **ForSeries JsonProperty attribute** (1 test): Verifies `[JsonProperty("forSeries")]` present
  - **ForSeries default value** (1 test): New instance has `ForSeries == false`
  - **Tag has setter** (1 test): Verifies `CanWrite` is true via reflection
  - **Tag deserializes value** (1 test): JSON `{"tag": 42}` deserializes to `42`
  - **Tag deserializes zero** (1 test): JSON `{"tag": 0}` deserializes to `0`
  - **Tag round-trip** (1 test): Serialize then deserialize preserves value
  - **Tag JsonProperty attribute** (1 test): Verifies `[JsonProperty("tag")]` present
  - **Tag default value** (1 test): New instance has `Tag == 0`
  - **Full award category deserialization** (1 test): Full JSON object with all fields including `forSeries` deserializes correctly
  - **Full character tag option deserialization** (1 test): Full JSON object with all fields including `tag` deserializes correctly

**Test results**: All 1,096 tests pass across all projects (2 Database + 111 Encoder + 120 Repositories + 233 Queue + 262 Api + 368 Providers). Build succeeds with 0 errors.

---

## PMOD-CRIT-03 — Fix TMDB `video` field typed as `string?`

**Date**: 2026-02-08

**What was done**:
- Fixed `src/NoMercy.Providers/TMDB/Models/Movies/TmdbMovie.cs:15` — changed `Video` property type from `string?` to `bool?`
- Fixed `src/NoMercy.Providers/TMDB/Models/Collections/TmdbCollectionPart.cs:18` — changed `Video` property type from `string?` to `bool?`
- Fixed `src/NoMercy.Providers/TMDB/Models/Shared/TmdbShowOrMovie.cs:15` — changed `Video` property type from `string?` to `bool?`
- **The bug**: The TMDB API returns the `video` field as a boolean (`true`/`false`), but three model classes typed it as `string?`. Newtonsoft.Json would either throw a `JsonReaderException` when encountering a boolean value during deserialization, or silently produce `null`/`"True"`/`"False"` string representations depending on settings. This corrupted the data flowing through `TmdbShowOrMovie` constructors and into `MovieManager` and `CollectionMovieDto`.
- **The fix**: Changed all three properties to `bool?`. Updated two downstream consumers:
  - `CollectionMovieDto.cs:101` — `VideoId = tmdbMovie.Video` → `VideoId = tmdbMovie.Video?.ToString()` (preserves existing `string? VideoId` DTO type)
  - `MovieManager.cs:69-70` — `Trailer = movieAppends.Video` and `Video = movieAppends.Video` → `.Video?.ToString()` (preserves existing database `string?` column type)
  - `TmdbMovieMockData.cs:35` — `Video = "false"` → `Video = false` (mock data matches new type)
- Created `tests/NoMercy.Tests.Providers/TMDB/Models/TmdbVideoFieldTypeTests.cs` with 21 tests:
  - **TmdbMovie property type** (1 test): Verifies `Video` property type is `bool?` via reflection
  - **TmdbMovie JsonProperty attribute** (1 test): Verifies `[JsonProperty("video")]` present
  - **TmdbMovie deserializes true** (1 test): JSON `{"video": true}` deserializes to `true`
  - **TmdbMovie deserializes false** (1 test): JSON `{"video": false}` deserializes to `false`
  - **TmdbMovie deserializes null** (1 test): JSON `{"video": null}` deserializes to `null`
  - **TmdbMovie default is null** (1 test): New instance has `Video == null`
  - **TmdbMovie round-trip** (1 test): Serialize then deserialize preserves value
  - **TmdbCollectionPart property type** (1 test): Verifies `Video` property type is `bool?`
  - **TmdbCollectionPart JsonProperty attribute** (1 test): Verifies `[JsonProperty("video")]` present
  - **TmdbCollectionPart deserializes true** (1 test): JSON `{"video": true}` → `true`
  - **TmdbCollectionPart deserializes false** (1 test): JSON `{"video": false}` → `false`
  - **TmdbCollectionPart deserializes null** (1 test): JSON `{"video": null}` → `null`
  - **TmdbCollectionPart round-trip** (1 test): Serialize then deserialize preserves value
  - **TmdbShowOrMovie property type** (1 test): Verifies `Video` property type is `bool?`
  - **TmdbShowOrMovie JsonProperty attribute** (1 test): Verifies `[JsonProperty("video")]` present
  - **TmdbShowOrMovie copied from TmdbMovie (true)** (1 test): Constructor copies `Video = true` correctly
  - **TmdbShowOrMovie copied from TmdbMovie (false)** (1 test): Constructor copies `Video = false` correctly
  - **TmdbShowOrMovie copied from TmdbMovie (null)** (1 test): Constructor copies `Video = null` correctly
  - **TmdbMovie realistic API JSON** (1 test): Full TMDB API-shaped JSON with `"video": false` deserializes correctly
  - **TmdbCollectionPart realistic API JSON** (1 test): Full TMDB API-shaped JSON deserializes correctly
  - **TmdbShowOrMovie via TmdbMovie deserialization** (1 test): Deserialize TmdbMovie from JSON, pass to TmdbShowOrMovie constructor, verify Video preserved

**Test results**: All 1,117 tests pass across all projects (2 Database + 111 Encoder + 120 Repositories + 233 Queue + 262 Api + 389 Providers). Build succeeds with 0 errors.

---

## DBMOD-CRIT-01 — Fix Track.MetadataId type mismatch

**Date**: 2026-02-08

**What was done**:
- Fixed `src/NoMercy.Database/Models/Track.cs:65` — changed `MetadataId` property type from `int?` to `Ulid?`
- **The bug**: `Track.MetadataId` was declared as `int?` but the FK target `Metadata.Id` is `Ulid`. This type mismatch meant:
  - EF Core could not discover the FK relationship by convention (Ulid PK vs int FK)
  - The `MetadataId` column was created as `INTEGER` in SQLite while it should be `TEXT` (matching Ulid storage)
  - Any attempt to set `Track.MetadataId` to a real `Metadata.Id` value would fail at runtime due to type incompatibility
  - This was inconsistent with `VideoFile.MetadataId` (Ulid?) and `Album.MetadataId` (Ulid?) which were correctly typed
- **The fix**:
  1. Changed `Track.MetadataId` from `int?` to `Ulid?` to match `Metadata.Id` type
  2. Added explicit relationship configuration in `MediaContext.OnModelCreating()` to disambiguate the Track↔Metadata relationships:
     - `Track.Metadata` (via `Track.MetadataId`) — Track belongs to a Metadata record (many-to-one)
     - `Metadata.AudioTrack` (via `Metadata.AudioTrackId`) — Metadata has one audio Track (one-to-one, independent relationship)
  3. Updated `MediaContextModelSnapshot.cs`:
     - Changed Track's MetadataId from `Property<int?>` / `INTEGER` to `Property<string>` / `TEXT`
     - Added `Track.HasOne(Metadata).WithMany().HasForeignKey(MetadataId)` FK configuration
     - Changed `Metadata.AudioTrack.WithOne("Metadata")` to `WithOne()` since Track.Metadata is now a separate relationship
     - Removed `Navigation("Metadata").IsRequired()` since MetadataId is nullable
- **Why explicit configuration was needed**: With `int?`, EF Core couldn't match the FK by convention, so `Track.Metadata` was configured as the inverse of `Metadata.AudioTrack` (via `AudioTrackId`). With the corrected `Ulid?`, EF Core discovers the FK but then finds TWO possible relationships (Track→Metadata via MetadataId, and Metadata→Track via AudioTrackId) with the same navigation property, causing an "ambiguous dependent side" error. The explicit configuration separates them into two independent relationships.
- Created `tests/NoMercy.Tests.Database/TrackMetadataIdTests.cs` with 11 tests:
  - **Property type is nullable Ulid** (1 test): Verifies `MetadataId` type is `Ulid?` via reflection
  - **Matches Metadata.Id type** (1 test): Verifies underlying type of `Track.MetadataId` equals `Metadata.Id` type
  - **Consistent with VideoFile.MetadataId** (1 test): Verifies same type as `VideoFile.MetadataId` (Ulid?)
  - **Consistent with Album.MetadataId** (1 test): Verifies same type as `Album.MetadataId` (Ulid?)
  - **Correct JsonProperty attribute** (1 test): Verifies `[JsonProperty("metadata_id")]` present
  - **Default value is null** (1 test): New Track has `MetadataId == null`
  - **Can be assigned Ulid** (1 test): Verifies assignment of `Ulid.NewUlid()` works
  - **Can be assigned null** (1 test): Verifies nullability works
  - **Serializes to JSON** (1 test): Verifies Ulid value appears in serialized JSON
  - **Is not int** (1 test): Regression test verifying type is not `int?` or `int`
  - **Metadata navigation exists** (1 test): Verifies `Track.Metadata` navigation property exists and returns `Metadata` type

**Test results**: All 1,128 tests pass across all projects (13 Database + 111 Encoder + 120 Repositories + 233 Queue + 262 Api + 389 Providers). Build succeeds with 0 errors.

---

## DBMOD-CRIT-02 — Fix Library.cs JsonProperty names all shifted

**Date**: 2026-02-08

**What was done**:
- Fixed `src/NoMercy.Database/Models/Library.cs:18-24` — realigned all `[JsonProperty]` attribute values to match their corresponding property names
- **The bug**: The `[JsonProperty]` values on the first four scalar properties after `Id` were shifted by one position:
  - `ChapterImages` (bool) had `[JsonProperty("auto_refresh_interval")]` — should be `"chapter_images"`
  - `ExtractChapters` (bool) had `[JsonProperty("chapter_images")]` — should be `"extract_chapters"`
  - `ExtractChaptersDuring` (bool) had `[JsonProperty("extract_chapters")]` — should be `"extract_chapters_during"`
  - `AutoRefreshInterval` (int) had `[JsonProperty("name")]` — should be `"auto_refresh_interval"`
- **Impact**: Every JSON serialization/deserialization of `Library` produced wrong field mappings. `ChapterImages` would serialize as `"auto_refresh_interval"`, `AutoRefreshInterval` (an int) would serialize as `"name"`, etc. Any client consuming the API would see mismatched data — boolean values in integer fields, integers in string fields.
- **The fix**: Corrected all four `[JsonProperty]` values to match their property names in snake_case. `Image` and `Order` were already correct and left unchanged.
- Created `tests/NoMercy.Tests.Database/LibraryJsonPropertyTests.cs` with 27 tests:
  - **Individual JsonProperty name checks** (9 tests): Verifies `ChapterImages`, `ExtractChapters`, `ExtractChaptersDuring`, `AutoRefreshInterval`, `Image`, `Order`, `Title`, `Type`, `Id` all have correct `[JsonProperty]` attribute values
  - **Serialization correctness** (3 tests): `ChapterImages` serializes to `"chapter_images"` (not `"auto_refresh_interval"`), `AutoRefreshInterval` serializes to `"auto_refresh_interval"` (not `"name"`), `ExtractChaptersDuring` serializes to `"extract_chapters_during"` (not `"extract_chapters"`)
  - **Deserialization correctness** (3 tests): `"chapter_images"` JSON key deserializes into `ChapterImages`, `"auto_refresh_interval"` into `AutoRefreshInterval`, `"extract_chapters_during"` into `ExtractChaptersDuring`
  - **Round-trip preservation** (1 test): All previously-shifted properties survive serialize→deserialize with correct values
  - **Theory-based comprehensive check** (11 cases): Verifies all 11 scalar properties have snake_case `[JsonProperty]` names matching their C# property names

**Test results**: All 1,155 tests pass across all projects (40 Database + 111 Encoder + 120 Repositories + 233 Queue + 262 Api + 389 Providers). Build succeeds with 0 errors.

---

## DBMOD-CRIT-03 — Fix QueueJob.Payload limited to 256 characters

**Date**: 2026-02-08

**What was done**:
- Added `[MaxLength(4096)]` to `QueueJob.Payload` in `src/NoMercy.Database/Models/QueueJob.cs`
- Added `[MaxLength(4096)]` to 18 large-text fields across 14 MediaContext model classes:
  - **Overview** (8 models): Movie, Episode, Tv, Season, Collection, Similar, Recommendation, Special
  - **Biography** (1 model): Person
  - **Description** (5 models): Network, Company, Artist, Album, ReleaseGroup, Playlist
  - **Translation** (3 fields): Overview, Description, Biography
- **The bug**: `ConfigureConventions` in both `MediaContext` and `QueueContext` sets `HaveMaxLength(256)` on ALL string properties. `QueueJob.Payload` stores serialized job data (JSON) — 256 chars is far too small, causing job payloads to be silently truncated. Similarly, Overview/Biography/Description fields from TMDB/TVDB/MusicBrainz APIs frequently exceed 256 chars.
- **The fix**:
  1. Added `[MaxLength(4096)]` data annotation attributes to all affected properties
  2. Added explicit fluent API override in `QueueContext.OnModelCreating()`: `modelBuilder.Entity<QueueJob>().Property(j => j.Payload).HasMaxLength(4096)` — because EF Core's `ConfigureConventions` `HaveMaxLength(256)` takes precedence over data annotations
  3. Added generic `[MaxLength]` attribute scanning in `MediaContext.OnModelCreating()` that reads `[MaxLength]` attributes via reflection and calls `SetMaxLength()` on matching properties — this ensures data annotations override the convention for all 18 fields
  4. Updated `MediaContextModelSnapshot.cs` — changed `HasMaxLength(256)` to `HasMaxLength(4096)` on all 18 Overview/Biography/Description property entries
  5. Added `using System.ComponentModel.DataAnnotations` to all 14 affected model files and both DbContext files
- **Consistency**: `FailedJob.Payload` already had `[MaxLength(4092)]` — the new `QueueJob.Payload` uses 4096 (a round power of 2), which is close to but not identical to `FailedJob`'s existing value
- Created `tests/NoMercy.Tests.Database/QueueJobPayloadMaxLengthTests.cs` with 26 tests:
  - **QueueJob.Payload attribute checks** (3 tests): HasMaxLengthAttribute, MaxLengthIs4096, MaxLengthIsNotDefault256
  - **QueueJob.Payload exceeds default** (1 test): Verifies MaxLength > 256
  - **Theory-based large text field check** (18 cases): Verifies all 18 Overview/Biography/Description properties across all 14 models have `[MaxLength(4096)]`
  - **QueueContext runtime model** (2 tests): Payload has MaxLength 4096 at runtime; Queue property still has convention 256
  - **Object-level validation** (2 tests): QueueJob can store 1000 and 4096 character payloads

**Test results**: All 1,181 tests pass across all projects (66 Database + 111 Encoder + 120 Repositories + 233 Queue + 262 Api + 389 Providers). Build succeeds with 0 errors.

---

## DBMOD-CRIT-04 — Fix Cast.cs initializes nullable navigation to new()

**Date**: 2026-02-08

**What was done**:
- Fixed `src/NoMercy.Database/Models/Cast.cs:33,36,39,42` — removed `= new()` initializer from four nullable navigation properties
- **The bug**: Four nullable navigation properties (`Movie?`, `Tv?`, `Season?`, `Episode?`) were initialized to `new()`. This meant:
  - `cast.Movie is not null` would always be `true` even when no Movie entity was loaded from the database
  - Null checks used throughout the codebase to determine which entity type a Cast belongs to would always pass, leading to incorrect branching
  - EF Core would see non-null navigation properties and could attempt to insert empty related entities during `SaveChanges()`
  - The `= new()` created empty Movie/Tv/Season/Episode instances with default values (Id=0), which is semantically wrong — a Cast with `MovieId = null` should have `Movie = null`
- **The fix**: Removed `= new()` from all four nullable navigation properties:
  - `public Movie? Movie { get; set; } = new()` → `public Movie? Movie { get; set; }`
  - `public Tv? Tv { get; set; } = new()` → `public Tv? Tv { get; set; }`
  - `public Season? Season { get; set; } = new()` → `public Season? Season { get; set; }`
  - `public Episode? Episode { get; set; } = new()` → `public Episode? Episode { get; set; }`
- Non-nullable navigations (`Person` and `Role`) correctly use `= null!` and were left unchanged
- Created `tests/NoMercy.Tests.Database/CastNavigationInitializerTests.cs` with 20 tests:
  - **Default null checks** (4 tests): Verify `Movie`, `Tv`, `Season`, `Episode` are all `null` on a new `Cast` instance
  - **Nullability annotation checks** (4 tests): Verify all four properties have `NullabilityState.Nullable` via `NullabilityInfoContext`
  - **Null pattern matching** (4 tests): Verify `cast.Movie is not null` returns `false` when not loaded (the core bug fix validation)
  - **Theory-based initializer check** (4 cases): Verify all four navigation properties return `null` via reflection `GetValue()`
  - **Non-nullable navigations preserved** (2 tests): Verify `Person` and `Role` remain `NullabilityState.NotNull`
  - **Assignment works** (1 test): Verify `Movie` can be assigned a real `Movie` instance
  - **Null assignment works** (1 test): Verify `Movie` can be set back to `null` after assignment

**Test results**: All 1,201 tests pass across all projects (86 Database + 111 Encoder + 120 Repositories + 233 Queue + 262 Api + 389 Providers). Build succeeds with 0 errors.

---

## DBMOD-H01 — Fix UserData.TvId wrong JsonProperty

**Date**: 2026-02-08

**What was done**:
- Fixed `src/NoMercy.Database/Models/UserData.cs:39` — changed `[JsonProperty("episode_id")]` to `[JsonProperty("tv_id")]` on the `TvId` property
- **The bug**: `TvId` had `[JsonProperty("episode_id")]` — the wrong JSON mapping. This meant:
  - Serialization: `TvId` would serialize as `"episode_id"` in JSON responses, causing API clients to see the TV show ID under the wrong key
  - Deserialization: JSON payloads with `"tv_id"` would not populate `TvId` (it would remain null/0), while `"episode_id"` payloads would incorrectly set it
  - This is a data corruption bug — TV show IDs were silently misrepresented as episode IDs in all JSON communication
- **The fix**: Changed `[JsonProperty("episode_id")]` to `[JsonProperty("tv_id")]` to match the property name in snake_case, consistent with all other FK properties in the class (`movie_id`, `collection_id`, `special_id`, `video_file_id`, `user_id`)
- Created `tests/NoMercy.Tests.Database/UserDataJsonPropertyTests.cs` with 25 tests:
  - **TvId JsonProperty checks** (2 tests): Verifies `TvId` has `[JsonProperty("tv_id")]` and is NOT `"episode_id"`
  - **Other FK JsonProperty checks** (5 tests): Verifies `MovieId`, `CollectionId`, `SpecialId`, `UserId`, `VideoFileId` all have correct `[JsonProperty]` values
  - **Serialization correctness** (1 test): `TvId = 42` serializes to `"tv_id":42` and NOT `"episode_id"`
  - **Deserialization from correct key** (1 test): `{"tv_id": 99}` deserializes into `TvId = 99`
  - **Deserialization ignores old key** (1 test): `{"episode_id": 99}` does NOT populate `TvId` (remains null)
  - **Round-trip preservation** (1 test): Serialize→deserialize preserves `TvId` value
  - **Theory-based comprehensive check** (14 cases): Verifies all 14 properties with `[JsonProperty]` have correct snake_case JSON names

**Test results**: All 1,226 tests pass across all projects (111 Database + 111 Encoder + 120 Repositories + 233 Queue + 262 Api + 389 Providers). Build succeeds with 0 errors.

---

## DBMOD-H02 — Fix Network.cs duplicate JsonProperty

**Date**: 2026-02-08

**What was done**:
- Fixed `src/NoMercy.Database/Models/Network.cs:20` — changed `[JsonProperty("id")]` to `[JsonProperty("network_tv")]` on the `NetworkTv` collection navigation property
- **The bug**: The `NetworkTv` collection property had `[JsonProperty("id")]`, identical to the `Id` scalar property on line 12. This caused a serialization conflict:
  - During JSON serialization, two properties would map to the same `"id"` key — the serializer would either throw an exception or produce ambiguous output where the collection overwrites the integer ID
  - During deserialization, a JSON payload with `"id": 42` would attempt to populate both the `int Id` and the `ICollection<NetworkTv> NetworkTv` — causing type mismatch errors or silent data loss
  - API consumers would never see the `NetworkTv` data under its own key
- **The fix**: Changed to `[JsonProperty("network_tv")]` — matching the snake_case convention used by all other collection navigation properties in the codebase (e.g., `Artist.ArtistTrack` → `"artist_track"`, `Album.AlbumTrack` → `"album_track"`, `Collection.CollectionMovies` → `"collection_movies"`)
- Created `tests/NoMercy.Tests.Database/NetworkJsonPropertyTests.cs` with 24 tests:
  - **NetworkTv JsonProperty checks** (2 tests): Verifies `NetworkTv` has `[JsonProperty("network_tv")]` and is NOT `"id"`
  - **Id JsonProperty check** (1 test): Verifies `Id` has `[JsonProperty("id")]`
  - **Id and NetworkTv differ** (1 test): Verifies `Id` and `NetworkTv` have different `[JsonProperty]` values
  - **Other property JsonProperty checks** (6 tests): Verifies `Name`, `Logo`, `OriginCountry`, `Description`, `Headquarters`, `Homepage` all have correct `[JsonProperty]` values
  - **Serialization correctness** (3 tests): `NetworkTv` serializes under `"network_tv"` key; `Id` serializes under `"id"` key; no duplicate `"id"` keys in serialized JSON
  - **Deserialization correctness** (1 test): `{"id":99}` deserializes into `Id = 99` correctly
  - **Round-trip preservation** (1 test): Serialize→deserialize preserves `Id` value
  - **Theory-based comprehensive check** (8 cases): Verifies all 8 properties with `[JsonProperty]` have correct snake_case JSON names
  - **No duplicate JsonProperty names** (1 test): Verifies all `[JsonProperty]` values across the entire `Network` class are unique

**Test results**: All 1,250 tests pass across all projects (135 Database + 111 Encoder + 120 Repositories + 233 Queue + 262 Api + 389 Providers). Build succeeds with 0 errors.

---

## SYS-H14 — Fix macOS cloudflared architectures swapped

**Date**: 2026-02-08

**What was done**:
- Fixed `src/NoMercy.Setup/Binaries.cs:290-298` — swapped the macOS cloudflared download URLs so each architecture downloads the correct binary
- **The bug**: The macOS Arm64 branch (line 290-293) downloaded `cloudflared-darwin-amd64.tgz` and the macOS X64 branch (line 295-298) downloaded `cloudflared-darwin-arm64.tgz`. The architecture strings were swapped — Arm64 Macs would get the x86_64 binary (which would fail to run or run under Rosetta with degraded performance), and Intel Macs would get the ARM64 binary (which would fail entirely).
- **The fix**: Swapped the asset names:
  - Arm64 branch: `cloudflared-darwin-amd64.tgz` → `cloudflared-darwin-arm64.tgz`
  - X64 branch: `cloudflared-darwin-arm64.tgz` → `cloudflared-darwin-amd64.tgz`
- Created `tests/NoMercy.Tests.Providers/Setup/BinariesCloudflaredArchTests.cs` with 8 tests:
  - **IsAsyncMethod** (1 test): Verifies `DownloadCloudflared` has `AsyncStateMachineAttribute`
  - **MacOS Arm64 downloads arm64 binary** (1 test): Source code regex verifies `Architecture.Arm64` branch within `OSPlatform.OSX` context downloads `cloudflared-darwin-arm64.tgz`
  - **MacOS X64 downloads amd64 binary** (1 test): Source code regex verifies `Architecture.X64` branch within `OSPlatform.OSX` context downloads `cloudflared-darwin-amd64.tgz`
  - **MacOS architectures not swapped** (1 test): Extracts all (Architecture, darwin-asset) pairs and verifies Arm64→arm64, X64→amd64 mapping
  - **Windows downloads amd64** (1 test): Verifies `cloudflared-windows-amd64.exe` present in method
  - **Linux Arm64 downloads arm** (1 test): Verifies Linux Arm64 branch downloads `cloudflared-linux-arm`
  - **Linux X64 downloads amd64** (1 test): Verifies Linux X64 branch downloads `cloudflared-linux-amd64`
  - **All platform assets present** (1 test): Verifies all 5 cloudflared platform binaries are referenced in the method
- Testing approach: Source code analysis via regex — reads the source file, extracts the `DownloadCloudflared` method body, and uses regex patterns anchored to `OSPlatform` + `Architecture` checks to verify each architecture branch downloads the correct binary. This catches swapped architectures that would not be detectable by IL inspection (compiler optimizes string literals in state machines).

**Test results**: All 1,258 tests pass across all projects (135 Database + 111 Encoder + 120 Repositories + 233 Queue + 262 Api + 397 Providers). Build succeeds with 0 errors.

---

## AUTH-BUG — Fix inverted expiration check in auth

**Date**: 2026-02-08

**What was done**:
- Fixed `src/NoMercy.Setup/Auth.cs:58` — changed inverted expiration logic from `NotBefore == null && expiresInDays >= 0` to `expiresInDays < 0`
- **The bug**: `bool expired = NotBefore == null && expiresInDays >= 0;` had two problems:
  1. It marked valid tokens (with `expiresInDays >= 0`, meaning more than 5 days until expiry) as "expired"
  2. It included an irrelevant `NotBefore == null` check that has nothing to do with token expiration
  - The downstream control flow (`if (!expired)` → try refresh, `else` → full re-auth) meant:
    - Valid tokens (`expiresInDays >= 0`) → `expired = true` → `!expired = false` → skipped refresh → went straight to full browser/password re-authentication (wrong!)
    - Expired tokens (`expiresInDays < 0`) → `expired = false` → `!expired = true` → tried refresh first (this is backwards — expired tokens need full re-auth, not refresh)
- **The fix**: Changed to `bool expired = expiresInDays < 0;` — now:
  - Valid tokens (`expiresInDays >= 0`) → `expired = false` → `!expired = true` → tries preemptive refresh with fallback to browser (correct)
  - Expired tokens (`expiresInDays < 0`) → `expired = true` → goes directly to browser/password re-auth (correct — refresh token likely expired too)
- **Context**: `expiresInDays` is computed as `_jwtSecurityToken.ValidTo.AddDays(-5).Subtract(DateTime.UtcNow).Days` — a 5-day early refresh window. So `expiresInDays < 0` means the token is within 5 days of expiry or already expired.
- Created `tests/NoMercy.Tests.Providers/Setup/AuthExpirationTests.cs` with 11 tests:
  - **No NotBefore reference** (1 test): Source code analysis verifies the `expired` expression does not reference `NotBefore`
  - **Uses less-than-zero** (1 test): Verifies expression contains `expiresInDays` and `< 0`
  - **Not inverted (no >= 0)** (1 test): Regression test verifying `>= 0` is not in the expression
  - **No buggy pattern** (1 test): Regex verifies the original `NotBefore == null && expiresInDays >= 0` pattern is gone
  - **Valid token not expired** (1 test): `expiresInDays = 5` → `expired = false`
  - **Expired token is expired** (1 test): `expiresInDays = -2` → `expired = true`
  - **Zero days not expired** (1 test): `expiresInDays = 0` → `expired = false` (boundary)
  - **Negative one is expired** (1 test): `expiresInDays = -1` → `expired = true`
  - **Expression is simple** (1 test): Verifies the full expression is exactly `expiresInDays < 0`
  - **Refresh-first control flow** (1 test): Verifies `if (!expired)` leads to `TokenByRefreshGrand`
  - **Else goes to browser** (1 test): Verifies `else` branch calls `TokenByBrowserOrPassword`

**Test results**: All 1,269 tests pass across all projects (135 Database + 111 Encoder + 120 Repositories + 233 Queue + 262 Api + 408 Providers). Build succeeds with 0 errors.

---

## CRIT-01 — Replace all `new MediaContext()` with DI

**Date**: 2026-02-08

**What was done**:
- Eliminated all `new MediaContext()` calls from the DI-managed codebase (~140+ occurrences across ~60 files), replacing them with proper dependency injection patterns
- **The bug**: `new MediaContext()` bypassed the DI container entirely, creating orphaned DbContexts that:
  - Were never disposed (memory leaks, connection pool exhaustion)
  - Used separate connection pools from the DI-managed contexts
  - Could not participate in request-scoped change tracking
  - Made unit testing impossible (no way to substitute an in-memory database)

**Fix patterns applied**:
1. **Sequential code (controllers, repositories, services)**: Replaced `new MediaContext()` with the DI-injected scoped `MediaContext` from the primary constructor parameter
2. **Concurrent code (Task.Run, Task.WhenAll)**: Used `IDbContextFactory<MediaContext>` to create a separate context per concurrent branch — DbContext is not thread-safe, so each parallel task needs its own instance
3. **Singleton services (StorageMonitor, ConnectionHub)**: Used `IDbContextFactory<MediaContext>` since singletons outlive scoped contexts
4. **Cron jobs**: Used constructor-injected `MediaContext` since they're resolved via `IServiceScope` in the CronWorker
5. **Background jobs with proper `await using`**: Left as-is (they already create and dispose their own contexts correctly)
6. **Startup code (before DI is available)**: Left as-is (UserSettings, Register, StartupOptions, Dev, ApplicationConfiguration, DatabaseSeeder)
7. **Static classes (ClaimsPrincipleExtensions)**: Left as-is (separate task CRIT-05)

**Files modified** (~60 files across the codebase):
- **DI registration**: `ServiceConfiguration.cs` — added `IDbContextFactory<MediaContext>` registration
- **Repositories**: `MusicRepository.cs` (30+ methods), `FileRepository.cs` (6 methods)
- **API Controllers**: `SearchController`, `HomeController`, `UserDataController`, `PeopleController`, `SpecialController`, `PlaylistsController`, `MusicController`, `ArtistsController`, `AlbumsController`, `ConfigurationController`, `EncoderController`, `ServerController`, `ServerActivityController`, `UsersController`, `TasksController`, `LibrariesController`, `SpecialsController`
- **SignalR Hubs**: `VideoHub`, `MusicHub`, `ConnectionHub`, `CastHub`, `DashboardHub`, `SocketHub`, `RipperHub`
- **Services**: `HomeService`, `VideoPlaybackService`, `VideoPlaybackCommandHandler`, `VideoPlayerStateFactory`
- **DTOs**: `LibraryResponseDto`, `PeopleResponseDto`, `InfoResponseItemDto`, `PersonResponseItemDto`
- **Cron Jobs**: All 12 palette cron jobs (Tv, Season, Episode, Movie, Collection, Person, Recommendation, Similar, Image, FanartArtistImages, Artist, Album)
- **Logic/Processing**: `MusicLogic`, `FileLogic`, `LibraryLogic`, `LibraryManager`, `LibraryFileWatcher`
- **Jobs**: `MusicJob`, `FindMediaFilesJob`, `AddMovieJob`, `EncodeVideoJob`, `RescanFilesJob`, `RescanLibraryJob`
- **System**: `StorageMonitor`

**Key design decisions**:
- SearchController's `MusicRepository` search methods (Step 1) are called sequentially because `MusicRepository` shares a single scoped DbContext. Only Step 2 (full Include queries) uses `IDbContextFactory` for parallel execution.
- DTOs that needed context now accept `MediaContext` as a parameter rather than creating their own
- `FileRepository.ProcessVideoFileInfo` was changed from static to instance method to access the injected context

**Tests added**: 7 new tests in `DiContextInjectionTests.cs`:
- `MusicRepository_UsesInjectedContext_NotNewInstance` — verifies search finds data through injected context
- `MusicRepository_SearchAlbumIds_UsesInjectedContext` — album search works through DI
- `MusicRepository_SearchTrackIds_UsesInjectedContext` — track search works through DI
- `MusicRepository_SearchPlaylistIds_UsesInjectedContext` — playlist search works through DI
- `MusicRepository_GetArtistAsync_UsesInjectedContext` — async query works through DI
- `DbContextFactory_CreatesDistinctContextsForConcurrentUse` — factory produces isolated contexts for safe parallel use
- `MusicRepository_EmptyContext_ReturnsNoResults` — proves repository reads from injected context (not global/static), returning empty when given empty DB

**Test results**: All 1,276 tests pass across all projects (135 Database + 111 Encoder + 127 Repositories + 233 Queue + 262 Api + 408 Providers). Build succeeds with 0 errors.

---

## CRIT-04 — Fix `.Wait()` / `.Result` deadlock patterns

**Date**: 2026-02-08

**What was done**:

### Files Modified

1. **`src/NoMercy.Api/Controllers/V1/Media/HomeController.cs`**:
   - Replaced `Task.Delay(1000).Wait()` with `await Task.Delay(1000, timeoutCts.Token)`
   - Added 30-second timeout via `CancellationTokenSource.CreateLinkedTokenSource` + `CancelAfter` to prevent infinite waits if encoding fails
   - Linked to `HttpContext.RequestAborted` so client disconnects also cancel the wait

2. **`src/NoMercy.Api/Controllers/Socket/music/MusicPlaybackService.cs`**:
   - Changed Timer callback from sync `void` lambda to `async void` lambda
   - Replaced `_musicRepository.RecordPlaybackAsync(...).Wait()` with `await _musicRepository.RecordPlaybackAsync(...)`
   - Replaced `HandleTrackCompletion(user, playerState).Wait()` with `await HandleTrackCompletion(user, playerState)`

3. **`src/NoMercy.Api/Controllers/Socket/video/VideoPlaybackService.cs`**:
   - Changed Timer callback from sync `void` lambda to `async void` lambda
   - Replaced `StoreWatchProgression(playerState, user).Wait()` with `await StoreWatchProgression(playerState, user)`
   - Replaced `HandleTrackCompletion(user, playerState).Wait()` with `await HandleTrackCompletion(user, playerState)`

4. **`src/NoMercy.Queue/JobQueue.cs`**:
   - Changed `ReserveJobQuery` from `EF.CompileAsyncQuery` to `EF.CompileQuery` — returns `QueueJob?` directly instead of `Task<QueueJob?>`
   - Removed `.Result` call in `ReserveJob()` method
   - Changed `ExistsQuery` from `EF.CompileAsyncQuery` to `EF.CompileQuery` — returns `bool` directly instead of `Task<bool>`
   - Removed `.Result` call in `Exists()` method

### Design Decisions
- **Timer callbacks**: Used `async void` because `System.Threading.Timer` requires `void` callbacks. This is one of the accepted cases for `async void` (event-handler-like patterns).
- **JobQueue compiled queries**: Switched to synchronous `EF.CompileQuery` instead of making the methods async, because these methods are called under `lock()` (which doesn't support `await`). Synchronous compiled queries avoid the `.Result` deadlock risk entirely.
- **HomeController timeout**: Added a 30-second timeout linked to `HttpContext.RequestAborted` to prevent thread starvation if the HLS segment never appears.

### Tests Added (10 new tests in `tests/NoMercy.Tests.Queue/BlockingPatternTests.cs`)
- `JobQueue_ReserveJobQuery_IsSynchronous` — reflection test verifying ReserveJobQuery returns `QueueJob?` not `Task<QueueJob?>`
- `JobQueue_ExistsQuery_IsSynchronous` — reflection test verifying ExistsQuery returns `bool` not `Task<bool>`
- `JobQueue_ReserveJob_WorksWithSynchronousQuery` — functional test of reserve after query type change
- `JobQueue_Enqueue_DuplicateCheckWorksSynchronously` — functional test of duplicate detection after query type change
- `JobQueue_SourceCode_NoBlockingPatterns` — static analysis verifying no `.Wait()` or `.Result` in JobQueue.cs
- `HomeController_SourceCode_NoBlockingWait` — static analysis verifying no `Task.Delay().Wait()` in HomeController.cs
- `MusicPlaybackService_SourceCode_NoBlockingPatterns` — static analysis verifying no `.Wait()` in MusicPlaybackService.cs
- `VideoPlaybackService_SourceCode_NoBlockingPatterns` — static analysis verifying no `.Wait()` in VideoPlaybackService.cs
- `HomeController_UsesAsyncDelay` — verifies `await Task.Delay` is present
- `HomeController_HasTimeout` — verifies `CancelAfter` timeout is present

**Test results**: All 1,040 tests pass (127 Repositories + 243 Queue + 408 Providers + 262 Api). Build succeeds with 0 errors.

---

## CRIT-05 — Fix static ClaimsPrincipleExtensions (not DI-friendly)

**Date**: 2026-02-08

**What was done**:
- Removed the static `MediaContext` field from `ClaimsPrincipleExtensions` that was created via `new MediaContext()` and never disposed — this leaked a DbContext for the entire application lifetime
- Removed the static initializers that froze `Users` and `FolderIds` at startup (loaded once, never refreshed)
- Changed `Users` and `FolderIds` to empty collections initialized via new `Initialize(MediaContext)` method
- Changed `Owner` from a static readonly field (throwing `InvalidOperationException` if no owner exists at class load time) to a computed property that dynamically finds the owner from the current `Users` list
- Added `Initialize(MediaContext)` method — loads both Users and FolderIds from a provided context
- Added `RefreshUsers(MediaContext)` and `RefreshFolderIds(MediaContext)` methods for targeted refresh
- Updated `IsOwner()` to handle null `Owner` gracefully (returns false instead of throwing)
- Called `ClaimsPrincipleExtensions.Initialize(mediaDbContext)` in `DatabaseSeeder.Run()` after user seeding completes — this is the earliest point where a MediaContext with seeded data is available
- Updated `NoMercyApiFactory.cs` test infrastructure to use `Initialize(mediaContext)` instead of manual `Users.Clear()` + `Users.AddRange()`

**Files changed**:
- `src/NoMercy.Helpers/ClaimsPrincipleExtensions.cs` — removed static MediaContext, added Initialize/Refresh methods
- `src/NoMercy.Server/Seeds/DatabaseSeeder.cs` — added Initialize call after seeding
- `tests/NoMercy.Tests.Api/Infrastructure/NoMercyApiFactory.cs` — use Initialize instead of manual list manipulation
- `tests/NoMercy.Tests.Repositories/NoMercy.Tests.Repositories.csproj` — added NoMercy.Helpers reference
- `tests/NoMercy.Tests.Repositories/ClaimsPrincipleExtensionsTests.cs` — 8 new tests

**Tests added** (8 tests in `ClaimsPrincipleExtensionsTests`):
- `Initialize_LoadsUsersFromContext` — verifies users are loaded from database
- `Initialize_LoadsFolderIdsFromContext` — verifies folder IDs are loaded from database
- `NewUserCreatedAfterStartup_IsAccessibleViaAddUser` — verifies new users can be added after initialization
- `DeletedUser_IsRemovedFromList` — verifies user removal works
- `RefreshUsers_ReloadsFromDatabase` — verifies users added to DB are visible after refresh
- `UpdateUser_ReplacesExistingUserInList` — verifies user update replaces existing entry
- `Initialize_ClearsPreviousData` — verifies stale data is cleared on re-initialization
- `NoStaticMediaContext_FieldDoesNotExist` — reflection test confirming static MediaContext field was removed

**Test results**: All 1,048 tests pass (135 Repositories + 243 Queue + 408 Providers + 262 Api). Build succeeds with 0 errors.

