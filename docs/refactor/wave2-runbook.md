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

### Reusable transformer: scripts/migrate_logger.py  (committed de4db3a7)
String/char/verbatim/interpolation-aware paren matcher — correctly handles
multi-line calls, mixed levels, and parens INSIDE interpolated strings
(e.g. `$"(id={x})"`). Rewrites `Logger.<Category>(...)` → `<expr>.Log<Level>(...)`,
stripping a trailing `LogEventLevel.X` arg (else uses the category default level).
Management-API members (LogEmitted/GetLogs/SetLogLevel/LogTypes/LogType/WriteBanner/
GetColor/Write) are NOT in the category set, so they're left untouched.
- Dry report:  `python3 scripts/migrate_logger.py --dry <expr> <file>...`
- Apply:       `python3 scripts/migrate_logger.py --apply <expr> <file>...`
- In apply mode it SKIPS calls whose sole remaining arg is a bare identifier
  (`[IDENT-ARG]`, almost always an exception) — fix those by hand as
  `<expr>.LogError(e, e.Message)`.
- Import in a driver via `from migrate_logger import rewrite` (has an
  `if __name__=='__main__'` guard now — importing does NOT run main()).
- Driver does the ctor plumbing (add `ILogger<T>` first param; field `_logger`
  for classic ctors, param `logger` for primary ctors), then `rewrite`, then
  manual ident fixes. ALWAYS verify residual with regex `\bLogger\.` (NOT the
  substring "Logger." — that false-positives on `activityLogger.`).

### IMPORTANT: skip files that call the log-management API in the mechanical pass
ServerController calls `Logger.SetLogLevel(level)` → DEFERRED (mixes management
API with emit calls; can't fully convert until SetLogLevel is re-provided on the
new provider). Same family: LogController, LogBroadcastService, WebSockets/
ResourceMonitorService. Verify each file has no `\bLogger\.(SetLogLevel|GetLogs|
LogEmitted|LogTypes|LogType|WriteBanner|GetColor|Write)\b` before converting.

### More commits landed
- 8cd9169f media controllers; a133e880 music controllers; bcf432f3 config/userdata/filesystem
- b24b922c SignalR event handlers (+EventHandlerExtensions DI +tests, 17/17)
- 0ca9e60d encoder/recommendation/video services
- de4db3a7 drivers/image/libraries/home controllers + migrate_logger.py

### Api remaining (mechanical)
ManagementController (verify no mgmt-API first), middleware (GlobalExceptionHandler,
TokenParamAuth, AccessLog, DynamicStaticFiles, HubErrorLoggingFilter ×16,
EncoderRuntimeException), SignalRLibraryRefreshEventHandler, EncoderRuntimeException.
DEFER: ServerController, LogController, LogBroadcastService, ResourceMonitorService.
Then Networking → Service → Setup → Data → MediaProcessing using the transformer.

### Api status (end of this stretch)
DONE (ILogger<T>): 4 simple hubs (Cast/Drives/Dashboard/Ripper); media/music/
config/userdata/filesystem/drivers/image/libraries/home controllers; 5 SignalR
event handlers (+EventHandlerExtensions DI +tests); encoder/recommendation/video
services; all middleware (TokenParam/DynamicStaticFiles/GlobalException/AccessLog/
EncoderRuntime) + HubErrorLoggingFilter.
Commits: d149e019, 8cd9169f, a133e880, bcf432f3, b24b922c, 0ca9e60d, de4db3a7,
4c433abe, 2fae62f7, e546545a (+ migrate_logger.py).

REMAINING in Api (mechanical, use transformer):
- VideoHub (+VideoHub.Playback) and MusicHub (+.Devices/+.Playback) — partial
  classes: add `private readonly ILogger<XHub> _logger;` field + first ctor param
  in the MAIN partial only; other partials just use `_logger`. They have
  `Logger.App(ex)` (IDENT-ARG → manual `_logger.LogError(ex, ex.Message)`) and many
  multiline `Logger.Socket(`. Run transformer with expr `_logger`.
DEFERRED (call the log-management API — need GetLogs/SetLogLevel/LogEmitted/
LogTypes re-provided on NoMercyLoggerProvider first):
  ManagementController (GetLogs/LogEmitted), ServerController (SetLogLevel),
  LogController, WebSockets/LogBroadcastService, WebSockets/ResourceMonitorService.

### Next projects (after Api): Networking(90) → Service(131) → Setup(155) →
Data(60) → MediaProcessing(269). For each: `migrate_logger.py --dry logger <files>`
first; primary-ctor classes use `logger`, classic-ctor use `_logger` field;
make any static method that logs an instance method (CS9105) or thread an
ILogger param; verify residual with `\bLogger\.`; skip mgmt-API consumers; build
NoMercy.Server.sln; commit-only-if ERR=0 && WARN=0. Provider HTTP clients & queue
jobs remain on legacy Logger (bridge) until DI-ified in N/O. Delete Logger.cs LAST.

### Transformer gotcha: conditional / non-trailing-simple level args
`Logger.X(msg, cond ? LogEventLevel.Warning : LogEventLevel.Debug)` is NOT stripped
by the transformer (its level regex only matches a simple trailing `LogEventLevel.X`).
The call gets rewritten to `_logger.LogInformation(msg, cond ? LogEventLevel... )`
which fails to compile (CS0103 once `using Serilog.Events;` is removed, and wrong
overload anyway). Fix by hand to MEL's runtime-level overload:
`_logger.Log(cond ? LogLevel.Warning : LogLevel.Debug, msg)`. The build catches
these; grep `\bLogEventLevel\b` in changed files after converting to find them.

### Api project: COMPLETE (mechanical L12)
All Api emit-call files now use ILogger<T> incl. Video/Music hubs (ccd0da17).
ONLY remaining Api = the deferred log-management-API consumers (ManagementController,
ServerController, LogController, LogBroadcastService, ResourceMonitorService) — these
wait on re-providing GetLogs/SetLogLevel/LogEmitted/LogTypes on NoMercyLoggerProvider.
Resume next at: Networking(90) → Service(131) → Setup(155) → Data(60) → MediaProcessing(269).

### Networking: DONE (d47bfd8e client-messenger/port-forward; 9b012495 the rest)
All Networking services use ILogger<T>. DEFERRED: NetworkProbe (`static class` with a
static method that logs — same bucket as provider clients/jobs; needs the seam or
stays on bridge until refactored). ConnectionHub's only "call" was a commented line.

### CRITICAL LESSON — services that are manually constructed (not pure DI)
Changing a service ctor to add `ILogger<T>` breaks EVERY manual construction site,
which the per-project file scan does NOT show. Before converting a service whose
instances are built explicitly, grep the WHOLE repo (src AND tests) for ALL forms:
  grep -rnE "new <Type>\(|\b<Type> +\w+ *= *new\(" src tests --include=*.cs
Networking hit sites in ServiceConfiguration.Core.cs (factory lambdas, target-typed
`X x = new(...)`) and 6 test files (NoMercyApiFactory, DegradedModeStartupTests,
BootOrchestratorTests, HttpsRestartTests, CertificateRenewalJobTests,
NetworkingExternalIpTests). Fix: logger is added as the FIRST ctor param, so PREPEND
it as the first argument at every site — `sp.GetRequiredService<ILogger<T>>()` inside
DI factory lambdas (sp in scope), `NullLogger<T>.Instance` in tests (+ using
Microsoft.Extensions.Logging.Abstractions). Do this in the SAME commit or the green
gate blocks (which is correct). The build's CS7036 errors enumerate remaining sites,
but grepping up front avoids multiple revert/retry cycles.

### Remaining order: Service(131) → Setup(155) → Data(60) → MediaProcessing(269).
Expect MANY manual construction sites for Data/MediaProcessing managers (they're
newed in jobs/Service wiring). Grep construction sites first. Provider HTTP clients,
queue jobs, and static utility classes (e.g. NetworkProbe) stay on the legacy bridge
until DI-ified in N/O. Delete Logger.cs LAST.
