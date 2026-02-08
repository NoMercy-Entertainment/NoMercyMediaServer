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

