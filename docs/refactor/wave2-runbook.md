# Wave-2 Autonomous Execution Runbook

STANDING ORDER (authorized by repo owner, 2026-06-29): implement ALL wave-2 refactor slices fully
autonomously, in the optimized order below, WITHOUT asking questions or waiting for confirmation.
After each task, proceed directly to the next. Do not stop for approval.

If context is summarized/reset: RE-READ this file + the spec gist
(https://gist.github.com/StoneyEagle/f153f8ffcef071d5b7f06069ab3c520d) and CONTINUE from the next
unchecked box. The standing order stays in force across resets.

## Per-task loop (non-negotiable)
1. Implement the next task from the spec.
2. dotnet build -> 0 errors AND warnings <= baseline. Never increase warnings.
3. CSharpier passes on every changed/new file.
4. Explicit types only (never var). Licence header on every new file. No Co-Authored-By.
   No model identifiers anywhere in commits/code/PRs.
5. Add/adjust tests for the task's Gate; run the relevant tests.
6. Commit (Conventional Commits, scope per slice). Push to refactor/slices (retry on network err).
7. Tick the box below; go to next. No pausing.

## Baseline
- Branch: refactor/slices
- Build warnings baseline: SEE build-baseline below (filled on first run)

## Optimized order (adapt only for hard deps: C3<-B2, C4<-B3, E2<-E1, O1<-N2)
- [x] L1 logging backbone (provider+logger+options) DONE
- [x] L2 display-width engine (DONE)
- [~] L3 console renderer: L3a categories + L3b line renderer DONE (sink wiring pending in L1)
- [x] L4 themes+palette + host wiring (CustomLogger->NoMercyLoggerProvider) DONE
- [x] L5 category mapping (ResolveSource) DONE
- [x] L6 scopes/correlation (scope suffix) DONE
- [x] L7 file sink (JSON) DONE
- [~] L8 record callback DONE (bus wiring with O)
- [ ] L9 banner (slant + colossal, width-adaptive)
- [ ] L10 alt-screen QR/auth
- [ ] L11 source-gen hot paths
- [ ] L12 big-bang migration + delete global Logger
- [ ] L13 guard analyzer
- [ ] N1..N6 globals dissolution
- [ ] O1..O6 event-driven adoption
- [ ] M1..M6 boot organizer
- [ ] E1 dedicated writer / E2 palette / E3 compiled queries / E4 ref cache / E5 token-bucket / A4
- [ ] A1 / A3 / A2 / A5 sync-over-async & concurrency
- [ ] B1..B5 coverage
- [ ] P1..P6 setup/registration consolidation
- [ ] C1 / C2 / C3 / C5 / C6 decompositions

## Tooling (IMPORTANT)
- CSharpier IS available. One-time: `bash scripts/bootstrap-csharpier.sh` (side-loads the
  pinned version; wrapper at /usr/local/bin/csharpier). Then `csharpier check <files>` and
  `csharpier format <files>`. Do NOT rely on `dotnet tool restore` (it fails in this container).
  Run `csharpier format` on every changed/new file before committing.
- Build flag for this container: `dotnet build -p:AllowMissingPrunePackageData=true`.

## L12 plan (static Logger -> ILogger<T> big-bang) — execute incrementally, green per step
Scope: ~965 calls in 12 projects (MediaProcessing 269, Api 184, Setup 155, Service 131,
Networking 92, NmSystem 74, Data 60, Providers 37, OpticalMedia 12, Encoder 6, Monitoring 3,
Storage 1). Methods are category helpers (App/Setup/MovieDb/Socket/System/Encoder/Auth/...).

Order (one project per commit, build+csharpier+test 0/0 each, push):
1. Providers (37) -> ILogger<T> via ctor where DI; smallest, isolates the pattern.
2. Encoder (6), Monitoring (3), Storage (1), OpticalMedia (12) -> quick wins.
3. Data (60), Networking (92), Providers remainder.
4. MediaProcessing (269) -> managers already take ctor deps; add ILogger<T>.
5. Setup (155), Api (184), Service (131).
Mechanics:
- DI-resolved classes: add `ILogger<T>` ctor param; map Logger.App->LogInformation,
  Logger.Warning->LogWarning, Logger.Error->LogError, category helpers (MovieDb/Queue/...) ->
  LogInformation (category comes from the type's namespace via LogCategories.ResolveSource).
- Genuinely static utilities: add a static source-generated `Log` (LoggerMessage) bound to the
  provider, OR thread an ILogger param. Decide per-site; document in the commit.
- Preserve the few API uses: GetLogs, SetLogLevel, LogEmitted, WriteBanner, LogTypes/GetColor ->
  reprovide on the new system before deleting the static Logger.
6. LAST: delete NoMercy.NmSystem/SystemCalls/Logger.cs + remove the BridgeLegacyLogger option/
   subscription + drop Serilog packages no longer used. Full-solution build 0/0.
Acceptance: `grep -rn "SystemCalls.Logger" src` -> 0 (except its own deletion); 0/0; csharpier clean.

## L12 DECISION (locked by owner): PURE ILogger<T> EVERYWHERE
- Convert ALL ~965 static Logger.* sites to ILogger<T>. No static Log facade.
- Static/non-DI classes (e.g. Monitoring.ResourceMonitor: static ctor+methods+static callers)
  must be RESTRUCTURED to instance + DI so ILogger<T> can be injected; update every construction
  site and DI registration accordingly.
- Each class: confirm construction sites first (new vs DI), add ILogger<T> ctor param (primary
  ctor where present), replace Logger.X(...) with the matching _logger.Log* call. Category derives
  from the type namespace via LogCategories.ResolveSource, so no manual category arg.
- Per-project, one project per commit, build + csharpier + tests 0/0, push. Order: Monitoring,
  Encoder, OpticalMedia, Storage, Providers, Data, Networking, MediaProcessing, Setup, Api, Service.
- Reprovide the few API surfaces (GetLogs, SetLogLevel, LogEmitted, WriteBanner, LogTypes/GetColor)
  on the new system, THEN delete NoMercy.NmSystem/SystemCalls/Logger.cs and remove the
  BridgeLegacyLogger option/subscription. Drop now-unused Serilog packages.
- Acceptance: grep -rn "SystemCalls.Logger" src -> 0; full-solution build 0/0; csharpier clean.
- NOTE: this is a multi-session migration; each session resumes here and continues the next project.

---

## L12 progress log (pure ILogger<T>)

### Accurate per-project `Logger.` site counts (grep `\bLogger\.`, excl bin/obj)
OpticalMedia 10 (DONE) · Providers 37 · Data 60 · NmSystem 76 · Networking 90 ·
Service 131 · Setup 155 · Api 167 · MediaProcessing 269.
(NOTE: earlier counts undercounted — the category/provider methods like
`MovieDb`, `MusicBrainz`, `Ping`, `Ripper`, `Socket`, `LogEmitted` were missing
from the regex. Repo-wide top categories: App 276, Setup 253, MovieDb 101,
Socket 69, System 56, Encoder 53, Auth 35, MusicBrainz 33.)

### KEY DECISION — what the mechanical L12 pass does and does NOT touch
The legacy `NoMercy.NmSystem.SystemCalls.Logger` is a static class with category
methods (App/Setup/Socket/Encoder/MovieDb/Ripper/…), each `(<T> message,
LogEventLevel level)`, plus Debug/Info/Warning/Error/Verbose, plus the
log-management API (`LogEmitted` event, `GetLogs`, `SetLogLevel`, `LogTypes`,
`LogType`, `WriteBanner`, `GetColor`). All legacy calls ALREADY render through the
new logging pipeline via `BridgeLegacyLogger`, so nothing is visually broken today.

- **Mechanical L12 pass = DI-lifecycle classes only** (controllers, SignalR hubs,
  middleware, hosted/DI-registered services, DI-registered managers). These get a
  ctor-injected `ILogger<T>` cleanly, and converting them upgrades generic
  categories (App/Socket) to proper per-type categories/colors (the console goal).
- **DEFER to the N/O boot/registration slices:** classes that are `new`'d ad-hoc
  (NOT DI-resolved). Chief example: **all NoMercy.Providers HTTP clients**
  (`BaseClient`/`ExternalApiClient` derivatives — parameterless / `(Guid id)` ctors,
  static `HttpClientProvider`/`Queue`, constructed throughout Data/MediaProcessing).
  Injecting `ILogger<T>` into them forces either a NEW global static factory seam
  (exactly the "global helper" the user wants killed) or a huge construction
  cascade. Correct fix = make them DI-resolved in N/O, THEN inject. Until then they
  keep legacy `Logger.<Provider>` calls (which route via the bridge). Queue jobs
  (`IShouldQueue`) similarly use service-locator logger (the established pattern,
  e.g. DiscRipJob/BundleSlugRenamer) — acceptable.
- **Log-management API consumers** (Api: LogController, LogBroadcastService,
  WebSockets, ResourceMonitorService, DashboardHub's ILogBroadcastService usage):
  do NOT convert `GetLogs/SetLogLevel/LogEmitted/LogTypes/LogType` to ILogger.
  These need equivalent query/subscribe APIs re-provided on NoMercyLoggerProvider
  first (a dedicated task). Skip those files in the mechanical pass.
- **Delete `Logger.cs` + remove BridgeLegacyLogger + drop Serilog packages = LAST**,
  only after providers/jobs are DI-ified and the management API is re-provided.

### Proven per-class pattern (used for hubs)
Add ctor param `ILogger<TSelf> logger` (insert as FIRST param to dodge trailing
optional params), `private readonly ILogger<TSelf> _logger;` field, assign in body.
Partial classes: field in main partial, other partials just use `_logger`. Then
rewrite calls: `Logger.Cat(msg)` → `_logger.LogInformation(msg)`;
`Logger.Cat(msg, LogEventLevel.X)` → `_logger.Log{Trace|Debug|Information|Warning|Error|Critical}(msg)`
(Verbose→Trace, Fatal→Critical). Most categories default to Information; Queue/Request
default Debug. Remove `using Serilog.Events;` when LogEventLevel drops out; add
`using Microsoft.Extensions.Logging;`. `Logger.X(ex)` (Exception arg) needs a template:
`_logger.LogError(ex, "...")` — handle manually, the green gate catches the miscompile.
Edits via /home/claude/edit_*.py with exact-string replace + count asserts; csharpier
format changed files; build `NoMercy.Server.sln -p:AllowMissingPrunePackageData=true`;
commit only if ERR=0 AND WARN=0; push refactor/slices.

### Commits landed this stretch
- 5f392962 refactor(encoder): BundleSlugRenamer uses ILogger<T> (L12)
- 8ab3f697 refactor(optical): drive backends use ILogger<T> (L12)
- d149e019 refactor(api): SignalR hubs use ILogger<T> (L12)  [Cast/Drives/Dashboard/Ripper]

### Next up (Api, mechanical)
VideoHub (+VideoHub.Playback, has `Logger.App(ex)` + multiline) ; MusicHub
(+Devices/+Playback) ; then simple controllers (App/Setup/Encoder/Http single-line
calls): TvShows/Albums/Artists/Collections/Movies/Playlists/Home/Libraries/Drivers/
Server/Management/Image/Filesystem/Configuration/UserData ; middleware
(GlobalExceptionHandler/TokenParamAuth/AccessLog/DynamicStaticFiles/
HubErrorLoggingFilter/EncoderRuntimeException) ; EventHandlers ; Services
(Encoder/Recommendation/VideoPlayback). SKIP LogController, LogBroadcastService,
WebSockets/ResourceMonitorService (management-API consumers).
