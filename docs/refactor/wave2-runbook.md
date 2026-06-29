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
6. Commit (Conventional Commits, scope per slice). Push to refactor/wave-2 (retry on network err).
7. Tick the box below; go to next. No pausing.

## Baseline
- Branch: refactor/wave-2
- Build warnings baseline: SEE build-baseline below (filled on first run)

## Optimized order (adapt only for hard deps: C3<-B2, C4<-B3, E2<-E1, O1<-N2)
- [ ] L1 logging backbone (DI + Serilog provider)
- [x] L2 display-width engine (DONE)
- [ ] L3 console renderer (custom sink)
- [ ] L4 themes + palette (dark+light)
- [ ] L5 category mapping
- [ ] L6 scopes / correlation
- [ ] L7 file sink (JSON)
- [ ] L8 event bridge
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
