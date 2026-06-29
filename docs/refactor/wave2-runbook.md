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
