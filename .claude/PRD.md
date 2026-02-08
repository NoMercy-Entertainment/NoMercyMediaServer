# NoMercy MediaServer - Performance & Architecture PRD

## Document Info
- **Date**: 2026-02-07
- **Scope**: Comprehensive performance audit, architectural improvements, and feature roadmap
- **Server**: `https://85-144-244-49.75f26c31-97d3-e6d4-eb13-65fbe4bb799e.nomercy.tv:7626`
- **Startup**: `dotnet run --project src/NoMercy.Server -- --dev --internal-ip 192.168.2.201`
- **Access Token**: `C:\Users\patri\AppData\Local\NoMercy_dev\config\token.json`
- **Platforms**: Windows, macOS, Linux Desktop, Linux Server

### Developer Context
- **Experience level**: Self-taught, learning by trial and error — many patterns are intentional problem-solving
- **Readability**: Developer has dyslexia — code style prioritizes visual clarity, generous spacing, short lines (max 120 chars)
- **Design philosophy**: Pragmatic solutions for a self-hosted media server on modest hardware; performance and reliability over textbook patterns
- **Guiding principle for all changes**: Understand WHY code was written a certain way before changing it. If a pattern solves a real problem, the fix must preserve that solution while improving the implementation.

---

## Ralph Loop Instructions

Each iteration you must:
1. Read this file and `progress.md` to find the next unchecked `[ ]` task
2. Read the linked section file for that task's full details (problem, context, fix approach)
3. Implement the fix
4. Write or update tests that prove the fix works and existing behavior is preserved:
   - If a characterization test exists for the affected code, verify it still passes
   - If no test exists yet, write one that covers the changed behavior
   - For API changes: verify response shape is unchanged (snapshot tests)
   - For query changes: verify SQL output via `ToQueryString()`
   - For async/concurrency changes: verify no race conditions introduced
   - **NO SHORTCUTS**: No `Assert.True(true)`, no empty test bodies, no mocking everything to avoid real assertions, no skipping failing tests, no catching exceptions to force green. Tests must assert real behavior against real code.
5. Run `dotnet build && dotnet test` — both must pass with zero failures
6. Mark the task `[x]` in this file and update the `Next up` line
7. Append what you did to `progress.md`
8. Commit your changes
9. ONLY DO ONE TASK AT A TIME

If all tasks in all sections are checked off, output `<promise>COMPLETE</promise>`.

---

## Progress

**Next up**: Phase 1-2, item DBMOD-CRIT-01

### Phase 0: Test Harness & Build Foundation
Details: [02-testing-strategy.md](prd/02-testing-strategy.md) | [03-package-management.md](prd/03-package-management.md)

- [x] CHAR-01 — Set up `NoMercy.Tests.Api` project with `WebApplicationFactory` and auth helpers
- [x] CHAR-02 — Set up `NoMercy.Tests.Repositories` project with in-memory SQLite + seed data
- [x] CHAR-03 — Snapshot tests for all `/api/v1/` Media endpoints
- [x] CHAR-04 — Snapshot tests for all `/api/v1/` Music endpoints
- [x] CHAR-05 — Snapshot tests for all `/api/v1/` Dashboard endpoints
- [x] CHAR-06 — Query output tests for every repository method via `ToQueryString()`
- [x] CHAR-07 — SignalR hub connection tests
- [x] CHAR-08 — Queue behavior tests (enqueue, reserve, execute, fail, retry)
- [x] CHAR-09 — Encoder command-building tests (capture FFmpeg CLI args)
- [x] CHAR-10 — CI pipeline that runs all characterization tests
- [x] CPM-01 — Create `Directory.Packages.props` with all package versions
- [x] CPM-02 — Remove `Version` from all `.csproj` PackageReference entries
- [x] CPM-03 — Verify full build + test suite passes after CPM migration

### Phase 1-2: Fix What's Broken
Details: [04-bugs.md](prd/04-bugs.md) | [05-stability.md](prd/05-stability.md) | [06-performance.md](prd/06-performance.md) | [07-security.md](prd/07-security.md) | [08-code-quality.md](prd/08-code-quality.md)

- [x] CRIT-09 — Fix missing job retry return value
- [x] CRIT-13 — Remove duplicate DbContext registration (scoped + transient)
- [x] HIGH-15 — Fix image controller `| true` logic bug
- [x] PROV-CRIT-03 — Fix `.Result` on `SendAsync` in TvdbBaseClient
- [x] PROV-CRIT-04 — Fix inverted client-key condition in FanArt
- [x] PROV-H06 — Remove dead code (contradictory while loop) in AcoustId
- [x] PROV-H10 — Fix stream consumed then reused in FanArt Image
- [x] PROV-H12 — Fix stream consumed then reused in CoverArt Image
- [x] PROV-H16 — Fix response content read 3 times in NoMercy Image
- [x] PMOD-CRIT-01 — Fix MusixMatch AlbumName typed as `long`
- [x] PMOD-CRIT-02 — Fix TVDB properties missing setters
- [x] PMOD-CRIT-03 — Fix TMDB `video` field typed as `string?`
- [ ] DBMOD-CRIT-01 — Fix Track.MetadataId type mismatch
- [ ] DBMOD-CRIT-02 — Fix Library.cs JsonProperty names all shifted
- [ ] DBMOD-CRIT-03 — Fix QueueJob.Payload limited to 256 characters
- [ ] DBMOD-CRIT-04 — Fix Cast.cs initializes nullable navigation to new()
- [ ] DBMOD-H01 — Fix UserData.TvId wrong JsonProperty
- [ ] DBMOD-H02 — Fix Network.cs duplicate JsonProperty
- [ ] SYS-H14 — Fix macOS cloudflared architectures swapped
- [ ] AUTH-BUG — Fix inverted expiration check in auth
- [ ] CRIT-01 — Replace all `new MediaContext()` with DI
- [ ] CRIT-04 — Fix `.Wait()` / `.Result` deadlock patterns
- [ ] CRIT-05 — Fix static ClaimsPrincipleExtensions (not DI-friendly)
- [ ] CRIT-06 — Fix `lock(Context)` in JobQueue
- [ ] CRIT-07 — Implement HttpClientFactory for all providers
- [ ] CRIT-08 — Fix fire-and-forget tasks in QueueRunner
- [ ] CRIT-11 — Fix FFmpeg process resource leak
- [ ] HIGH-09 — Clean up temp files on encoding failure
- [ ] HIGH-10 — Fix async void in queue processor
- [ ] HIGH-16 — Fix DbContext lifetime in background services
- [ ] HIGH-17 — Fix missing disposal in provider clients
- [ ] HIGH-18 — Fix ThreadPool.QueueUserWorkItem fire-and-forget
- [ ] HIGH-20 — Fix GC.Collect band-aids (60+ calls)
- [ ] MED-04 — Fix CancellationToken not propagated
- [ ] MED-05 — Remove manual GC.Collect calls, fix root disposal
- [ ] MED-15 — Fix missing using/dispose on streams
- [ ] CRIT-02 — Replace client-side filtering with DB queries
- [ ] CRIT-03 — Split 55+ Include chains into focused queries
- [ ] CRIT-12 — Fix unbounded memory growth in media processing
- [ ] HIGH-01 — Fix IQueryable returns (premature materialization)
- [ ] HIGH-04 — Add missing database indexes
- [ ] HIGH-05 — Enable response caching
- [ ] HIGH-08 — Rate-limit encoder progress updates
- [ ] MED-01 — Fix N+1 queries in library endpoints
- [ ] MED-02 — Replace string concatenation in hot paths
- [ ] MED-03 — Create `ForUser()` extension to eliminate repeated auth filtering
- [ ] MED-11 — Fix unnecessary ToList() before LINQ operations
- [ ] MED-12 — Fix blocking I/O on hot paths
- [ ] MED-16 — Fix image processing memory allocation
- [ ] CRIT-14 — Fix TypeNameHandling.All security vulnerability
- [ ] HIGH-03 — Remove EnableSensitiveDataLogging in production
- [ ] HIGH-06 — Fix middleware ordering issues
- [ ] HIGH-07 — Fix SignalR detailed errors in production
- [ ] HIGH-14 — Set Kestrel limits (currently unlimited)
- [ ] MED-07 — Reduce SignalR message limit from 100MB
- [ ] MED-17 — Remove hardcoded configuration in static properties
- [ ] MED-18 — Fix CORS configuration
- [ ] MED-20 — Fix memory cache configuration
- [ ] HIGH-02 — Fix pagination inside Include()
- [ ] HIGH-11 — Fix unbounded cache growth
- [ ] HIGH-13 — Fix cron jobs double registration
- [ ] HIGH-19 — Fix FFmpeg process termination without exception handling
- [ ] MED-08 — Fix dual JSON serializer configuration
- [ ] MED-19 — Fix duplicate RequestLocalization call
- [ ] LOW-01 through LOW-10 — Code quality cleanup items

### Phase 3: Restructure
Details: [09-code-organization.md](prd/09-code-organization.md)

- [ ] REORG-01 — Rename `services/` to `Services/` in NoMercy.Server
- [ ] REORG-02 — Rename `Socket/music/` → `Hubs/` + move services
- [ ] REORG-03 — Rename `Socket/video/` → same pattern
- [ ] REORG-04 — Consolidate all DTOs into `NoMercy.Api/DTOs/`
- [ ] REORG-05 — Remove duplicate FolderDto
- [ ] REORG-06 — Organize 96 database models into domain subfolders
- [ ] REORG-07 — Remove or complete NoMercy.EncoderV2
- [ ] REORG-09 — Rename `AppConfig/` to `Configuration/`
- [ ] REORG-10 — Create centralized `Extensions/` per project
- [ ] REORG-11 — Move Swagger config to dedicated folder

### Phase 4-6: Architecture
Details: [10-event-driven.md](prd/10-event-driven.md) | [11-plugin-system.md](prd/11-plugin-system.md) | [12-queue-decoupling.md](prd/12-queue-decoupling.md)

- [ ] EVT-01 — Create NoMercy.Events project
- [ ] EVT-02 — Implement InMemoryEventBus
- [ ] EVT-04 — Define all domain event classes
- [ ] EVT-05 — Add events to media scan pipeline
- [ ] EVT-06 — Add events to encoding pipeline
- [ ] EVT-07 — Add events to playback services
- [ ] EVT-09 — Migrate SignalR broadcasting to events
- [ ] EVT-10 — Add event logging/audit middleware
- [ ] PLG-01 — Create NoMercy.Plugins.Abstractions
- [ ] PLG-02 — Implement PluginManager
- [ ] PLG-03 — Plugin manifest + lifecycle
- [ ] PLG-05 — Plugin DI integration
- [ ] PLG-06 — Plugin configuration system
- [ ] PLG-07 — Plugin repository system
- [ ] PLG-08 — Plugin management API
- [ ] PLG-09 — Plugin template/NuGet
- [ ] QDC-01 — Create Queue Core project + interfaces
- [ ] QDC-05 — Refactor JobQueue
- [ ] QDC-06 — Refactor JobDispatcher
- [ ] QDC-08 — Refactor QueueRunner
- [ ] QDC-10 — Create SQLite queue provider
- [ ] QDC-13 — Move job implementations
- [ ] QDC-17 — Comprehensive queue testing

### Phase 7: Service & Desktop
Details: [13-headless-server.md](prd/13-headless-server.md) | [14-server-setup.md](prd/14-server-setup.md)

- [ ] HEAD-03 — Create management API
- [ ] HEAD-04 — Implement IPC (named pipes)
- [ ] HEAD-05 — Windows Service host
- [ ] HEAD-06 — Verify macOS/Linux service
- [ ] HEAD-08 — Create Tray app (Avalonia)
- [ ] HEAD-09 — Tray icon + status
- [ ] HEAD-10 — Log viewer
- [ ] HEAD-11 — Server control UI
- [ ] HEAD-13 — CLI tool
- [ ] HEAD-15 — Platform packaging
- [ ] SETUP-01 — Create SetupState state machine
- [ ] SETUP-02 — Create SetupServer (minimal HTTP Kestrel)
- [ ] SETUP-03 — Build setup web UI
- [ ] SETUP-04 — Implement `/setup` page with OAuth + QR fallback
- [ ] SETUP-05 — Implement `/sso-callback` token exchange
- [ ] SETUP-06 — Implement `/setup/status` progress endpoint
- [ ] SETUP-07 — Add 503 middleware for non-setup routes
- [ ] SETUP-08 — Refactor Auth.cs — remove Console.* calls
- [ ] SETUP-09 — Add retry logic to Register + Certificate
- [ ] SETUP-11 — Implement HTTP → HTTPS restart after cert
- [ ] SETUP-12 — Remove Environment.Exit(1) from ApiInfo
- [ ] SETUP-17 — End-to-end setup flow testing

### Phase 8: Platform Features
Details: [15-wallpaper.md](prd/15-wallpaper.md) | [16-offline-boot.md](prd/16-offline-boot.md)

- [ ] WALL-01 — Implement cross-platform wallpaper strategy
- [ ] WALL-02 — Fix wallpaper performance issues
- [ ] WALL-03 — Update wallpaper controller
- [ ] BOOT-01 — Implement degraded mode startup
- [ ] BOOT-02 — Fix Cloudflare single point of failure
- [ ] BOOT-03 — Implement offline token validation
- [ ] BOOT-04 — Implement startup dependency reordering
- [ ] BOOT-05 — Implement health check endpoints

---

## Guardrails

### Validation Gate
```bash
dotnet build          # Must compile with zero errors
dotnet test           # All existing tests must pass
```
After every change, all existing characterization tests and unit tests must still pass. As Phase 0 adds snapshot tests, the gate gets stronger automatically — API response changes will be caught.

### Change Boundaries (Out-of-Scope Unless Explicitly Requested)
- No UI changes
- No database schema redesign beyond listed fixes
- No renaming public API endpoints
- No breaking changes to provider interfaces
- No changes to queue execution semantics
- No changes to FFmpeg command structure
- No changes to authentication flow unless in the Setup Mode section (Section 14)
- Never remove behavior unless explicitly instructed
- Never introduce new dependencies without approval

### Invariants (Must Be Preserved Across All Changes)
- SQLite single-writer — all DB writes serialize through one connection
- Queue job serialization — jobs execute in defined order with retry semantics
- Provider rate limits — MusicBrainz 1/sec, TMDB ~40/10sec, etc.
- Startup offline resilience — server must boot without network access
- Headless setup mode — first-time config must work without a browser
- Event-driven migration path — changes should move toward loose coupling, not away from it

### Engineering Process
- Keep changes small and isolated — one task per iteration
- Changes to lifetimes (DbContext, HttpClient, workers) may affect queue timing, provider behavior, or background processing. Evaluate ripple effects.
- SQLite's single-writer model imposes concurrency limits. Don't assume multi-writer scalability.
- When replacing `.Wait()` / `.Result`, preserve execution ordering. Avoid introducing race conditions.
- When removing GC.Collect calls, ensure unmanaged resources are deterministically disposed.
- Changes to queue serialization or locking must preserve current execution guarantees.
