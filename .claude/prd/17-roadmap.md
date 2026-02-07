## 17. Implementation Roadmap

### Phase 0: Characterization Tests — MUST COME FIRST (Weeks 1-3)
> **GOLDEN RULE: Write tests BEFORE changing any code.** These tests capture current behavior as-is (even if buggy) so we can verify that intentional changes don't cause unintended regressions. No code is modified during this phase.

**Methodology — "Golden Master" Testing:**
1. Hit every API endpoint on the live server (`https://192-168-2-201...nomercy.tv:7626`)
2. Capture the exact JSON response as a snapshot/golden master
3. Write tests that assert the current response matches the snapshot
4. After any future code change, re-run to detect unintended changes

| Task | Description | Effort | Risk |
|------|-------------|--------|------|
| CHAR-01 | Set up `NoMercy.Tests.Api` project with `WebApplicationFactory` and auth helpers | Medium | None |
| CHAR-02 | Set up `NoMercy.Tests.Repositories` project with in-memory SQLite + seed data | Medium | None |
| CHAR-03 | Write snapshot tests for ALL `/api/v1/` Media endpoints (Home, Libraries, Movies, TvShows, Genres, Search, People, Special, UserData) | Large | None |
| CHAR-04 | Write snapshot tests for ALL `/api/v1/` Music endpoints (Artists, Albums, Tracks, Playlists, Genres) | Large | None |
| CHAR-05 | Write snapshot tests for ALL `/api/v1/` Dashboard endpoints (Libraries, Devices, Encoder, ServerInfo, Users, Config, Activity) | Medium | None |
| CHAR-06 | Write query output tests for every repository method — capture SQL via `ToQueryString()` | Large | None |
| CHAR-07 | Write SignalR hub connection tests (connect, subscribe, receive events) | Medium | None |
| CHAR-08 | Write queue behavior tests (enqueue, reserve, execute, fail, retry) | Small | None (mostly exists) |
| CHAR-09 | Write encoder command-building tests (capture FFmpeg CLI args for known inputs) | Medium | None |
| CHAR-10 | Create CI pipeline that runs all characterization tests | Small | None |
| CPM-01 | Create `Directory.Packages.props` with all package versions | Medium | None |
| CPM-02 | Remove `Version` from all `.csproj` PackageReference entries | Medium | Low — build verification |
| CPM-03 | Verify full build + test suite passes after CPM migration | Small | None |

**Exit criteria for Phase 0:** Every endpoint and query that will be modified has a passing test capturing its current behavior. Only then do we proceed to Phase 1.

---

### Phase 1: Foundation Fixes (Weeks 4-5)
> Get the basics right before building new features. Every fix re-runs Phase 0 tests to verify no regressions.

| Task | Ref | Effort | Risk |
|------|-----|--------|------|
| Remove all `new MediaContext()` — use DI | CRIT-01 | Large | High - test every endpoint |
| Remove `AddTransient<MediaContext>()` line | CRIT-13 | Small | Medium - verify DI still works |
| Fix `.Wait()` / `.Result` → `await` | CRIT-04 | Medium | Medium - requires async propagation |
| Fix ReserveJob missing return | CRIT-09 | Small | Low |
| Remove `EnableSensitiveDataLogging` | HIGH-03 | Small | Low |
| Fix image controller `\| true` bug | HIGH-15 | Small | Low |
| Fix static ClaimsPrincipleExtensions | CRIT-05 | Medium | High - auth system change |
| Remove duplicate DbContext registrations | CRIT-13 | Small | Medium |
| Simplify queue dispatch (no more generics) | QDS-01→06 | Medium | Low - cleaner API |
| Remove all 60+ GC.Collect calls, fix root disposal issues | MED-05 | Medium | Medium - audit disposals first |

### Phase 1: Critical Performance (Weeks 3-5)
> Fix issues that cause crashes or data corruption.

| Task | Ref | Effort | Risk |
|------|-----|--------|------|
| Replace client-side filtering with DB queries | CRIT-02 | Medium | High - verify search results |
| Split 55+ Include chains into focused queries | CRIT-03 | Large | High - verify all data returned |
| Fix lock(Context) in JobQueue | CRIT-06 | Medium | High - queue behavior change |
| Implement HttpClientFactory for all providers | CRIT-07 | Large | Medium |
| Fix fire-and-forget tasks in QueueRunner | CRIT-08 | Medium | Medium |
| Optional: Add type whitelist to serialization | CRIT-14 | Small | Low - only if desired |
| REMOVED — rate limiting already exists | CRIT-10 | N/A | INVALID |
| Fix FFmpeg process resource leak | CRIT-11 | Medium | Medium |
| Add missing database indexes | MED-06 | Small | Low |
| **Add repository tests for changed queries** | TEST-06 | Large | Required |

### Phase 2: High Priority Fixes (Weeks 6-9)
> Significant performance and correctness improvements.

| Task | Ref | Effort | Risk |
|------|-----|--------|------|
| Enable response caching | HIGH-05 | Medium | Medium |
| Fix middleware ordering | HIGH-06 | Small | Low |
| Fix SignalR detailed errors | HIGH-07 | Small | Low |
| Rate-limit encoder progress updates | HIGH-08 | Small | Low |
| Clean up temp files on encoding failure | HIGH-09 | Small | Low |
| Fix async void in queue processor | HIGH-10 | Small | Low |
| Fix captive dependency | HIGH-12 | Medium | Medium |
| Fix cron double registration | HIGH-13 | Small | Low |
| Set Kestrel limits | HIGH-14 | Small | Low |
| Fix IQueryable returns | HIGH-01 | Medium | High |
| Fix pagination inside Include | HIGH-02 | Medium | High |
| **Add API controller tests** | TEST-03,04 | Large | Required |

### Phase 3: Code Organization (Weeks 10-13)
> Restructure for navigability and maintainability.

| Task | Ref | Effort | Risk |
|------|-----|--------|------|
| Rename lowercase folders to PascalCase | REORG-01,03 | Small | Low |
| Move Socket services to Services/ folder | REORG-02 | Medium | Medium |
| Consolidate DTOs into centralized structure | REORG-04 | Large | Medium |
| Remove duplicate FolderDto classes | REORG-05 | Small | Low |
| Organize database models by domain | REORG-06 | Medium | Medium |
| Decide EncoderV2 fate | REORG-07 | Small | Decision |
| REMOVED — zip is bundled frontend app | REORG-08 | N/A | INVALID |
| Create `ForUser()` extension method | MED-03 | Small | Low |
| Replace Console.WriteLine with ILogger | HEAD-01 | Large | Medium |
| Create/update `.editorconfig` with readability rules | STYLE-01 | Small | Low |
| Run `dotnet format` across solution | STYLE-02 | Small | Low |

### Phase 4: Event-Driven Architecture (Weeks 14-17)
> Foundation for plugin system and loose coupling.

| Task | Ref | Effort | Risk |
|------|-----|--------|------|
| Create NoMercy.Events project | EVT-01 | Small | Low |
| Implement InMemoryEventBus | EVT-02 | Small | Low |
| Define all domain event classes | EVT-04 | Medium | Low |
| Add events to media scan pipeline | EVT-05 | Medium | Medium |
| Add events to encoding pipeline | EVT-06 | Medium | Medium |
| Add events to playback services | EVT-07 | Medium | Medium |
| Migrate SignalR broadcasting to events | EVT-09 | Medium | Medium |
| Add event logging/audit middleware | EVT-10 | Small | Low |

### Phase 5: Plugin System (Weeks 18-24)
> Extensibility framework.

| Task | Ref | Effort | Risk |
|------|-----|--------|------|
| Create NoMercy.Plugins.Abstractions | PLG-01 | Medium | Low |
| Implement PluginManager | PLG-02 | Large | Medium |
| Plugin manifest + lifecycle | PLG-03,04 | Medium | Low |
| Plugin DI integration | PLG-05 | Medium | Medium |
| Plugin configuration system | PLG-06 | Medium | Low |
| Plugin repository system | PLG-07 | Large | Medium |
| Plugin management API | PLG-08 | Medium | Low |
| Plugin template/NuGet | PLG-09 | Medium | Low |

### Phase 6: Queue Decoupling (Weeks 18-24, parallel with Phase 5)
> Extract queue into standalone library.

| Task | Ref | Effort | Risk |
|------|-----|--------|------|
| Create Core project + interfaces | QDC-01,02,03,04 | Medium | Low |
| Refactor JobQueue | QDC-05 | Medium | High |
| Refactor JobDispatcher | QDC-06 | Medium | Medium |
| Refactor QueueRunner | QDC-08 | Large | High |
| Create SQLite provider | QDC-10,11 | Medium | Medium |
| Move job implementations | QDC-13 | Large | Medium |
| Comprehensive testing | QDC-17 | Large | Required |

### Phase 7: Headless Server, Setup Flow & Tray UI (Weeks 25-32)
> Service mode, first-time setup, and desktop experience.

**Phase 7a: HTTP Setup Flow (Critical for service mode)**

| Task | Ref | Effort | Risk |
|------|-----|--------|------|
| Create SetupState state machine | SETUP-01 | Small | Low |
| Create SetupServer (minimal HTTP Kestrel) | SETUP-02 | Medium | Medium |
| Build setup web UI (embedded HTML/JS/CSS) | SETUP-03 | Medium | Low |
| Implement `/setup` page with OAuth + QR fallback | SETUP-04 | Medium | Medium |
| Implement `/sso-callback` token exchange | SETUP-05 | Small | Low |
| Implement `/setup/status` progress endpoint | SETUP-06 | Small | Low |
| Add 503 middleware for non-setup routes | SETUP-07 | Small | Low |
| Refactor Auth.cs — remove Console.* calls | SETUP-08 | Medium | High |
| Add retry logic to Register + Certificate | SETUP-09,10 | Small | Low |
| Implement HTTP → HTTPS restart after cert | SETUP-11 | Medium | High |
| Remove Environment.Exit(1) from ApiInfo | SETUP-12 | Small | Low |
| End-to-end setup flow testing | SETUP-17 | Large | Required |

**Phase 7b: Service & Desktop**

| Task | Ref | Effort | Risk |
|------|-----|--------|------|
| Create management API | HEAD-03 | Medium | Low |
| Implement IPC (named pipes) | HEAD-04 | Medium | Medium |
| Windows Service host | HEAD-05 | Medium | Medium |
| Verify macOS/Linux service | HEAD-06,07 | Small | Low |
| Create Tray app (Avalonia) | HEAD-08 | Large | Medium |
| Tray icon + status | HEAD-09 | Medium | Low |
| Log viewer | HEAD-10 | Medium | Low |
| Server control UI | HEAD-11 | Medium | Low |
| CLI tool | HEAD-13,14 | Medium | Low |
| Platform packaging | HEAD-15,16,17 | Large | Medium |

### Phase 8: Deep Scan Cleanup (Ongoing)
> Address remaining deep scan findings now distributed across Sections 4-8.

**Provider Clients:** Fix blocking async patterns, stream reuse bugs, disposal gaps, rate limiting (MusicBrainz Concurrent=40). *(Items in Sections 5, 6, 7)*

**Provider Models:** Fix type mismatches (AlbumName, video field), remove duplicates, standardize naming, extract date parsing converter. *(Items in Sections 4, 8)*

**Database Models:** Fix Track.MetadataId mismatch, Library.cs shifted JsonProperty, QueueJob.Payload 256-char limit, remove test deps from production project. *(Items in Sections 4, 5)*

**System/Setup/Helpers:** Fix DriveMonitor spin loop, MediaScan GC.Collect, Auth.cs Thread.Sleep, int overflow on >2GB files, swapped macOS architectures. *(Items in Sections 5, 7, 8)*

All remaining MED-* and LOW-* issues from Sections 6 and 8.


---

