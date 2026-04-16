# NoMercy Encoder — Complete Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** A consumer-grade video archiver. Drop a movie, disc, or folder of files → get a clean, Netflix-quality library on your own hardware. Professional internals (DI, testability, plugin extensibility) but the features prioritized here are what a home user actually wants: automatic encoding, great playback on every device, intro/outro skip, disc ripping, HDR preservation.

Pro / B2B features (multi-server distributed encoding, DRM encryption, CENC packaging) are deferred — they're not on the consumer path and shouldn't gate consumer quality.

**Architecture:** Strategy-owns-the-pipeline. An orchestrator analyzes source media and resolves an encoding strategy. Each strategy (HLS, MP4, MKV, DASH, Live, Audio, Plugin) composes injectable building blocks (FFmpeg executor, codec resolver, filter builder, etc.) to implement its full encode lifecycle. Plugins provide custom strategies and replace building blocks via DI.

**Tech Stack:** C# / .NET 10, FFmpeg (custom nomercy-ffmpeg build), Entity Framework Core (SQLite), SignalR, xUnit + FluentAssertions + Moq

**Spec:** `.claude/prd/2026-04-15-encoder-strategy-architecture.md`

**Branch:** `feat/encoder-v3`

---

## Current Status — 2026-04-16

**Build:** 182 commits ahead of `master` · 0 errors · 0 warnings · 720 encoder tests + 6 repository tests passing (+136 added this session).

**What already works today (outside the strategy pattern):**
- 6-stage pipeline (`Analyze → Validate → Plan → Build → Execute → Finalize`) in [`Pipeline/Encoder.cs`](../../src/NoMercy.Encoder/Pipeline/Encoder.cs) delivering production-grade HLS single-pass
- V1 profile format (`VideoProfiles` / `AudioProfiles` / `SubtitleProfiles`) wired via [`ProfileMapper.FromV1()`](../../src/NoMercy.Encoder/Profiles/ProfileMapper.cs)
- Hardware-aware codec resolution with GPU session tracking, cascade deletes
- Apple-compliant HLS, measured bandwidth, dual audio, ASS subtitle variants (sign/full/alt)
- Font extraction, chapter writing, sprite/thumbnail generation (sprites run as a separate command due to spritevtt `EAGAIN` bug in nomercy-ffmpeg)
- HDR → SDR tonemap via `ITonemapSelector`
- Preview encoding (`IEncoder.PreviewAsync`) with real metrics
- EventBus progress channel, V1-compatible dashboard shape, audio-only encodes, subtitle variant detection

**Phase-by-phase status:**

| Phase | Status | Evidence |
|-------|--------|----------|
| 1. Foundation — DI + Building Blocks | ✅ done | All 4 tasks shipped: interfaces (1.1), DI registration (1.2), stage injection (1.3), EncodeMode enum + parser (1.4). Pending sub-step: `IOutputStrategyFactory` for format selection via DI. |
| 2. Strategy Pattern | ✅ MVP | `Orchestration/` has `IEncodingOrchestrator` + `EncodingOrchestrator` + `IStrategyResolver` + `StrategyResolver`. `Strategies/IEncodingStrategy` defines the contract. `VideoEncodeJob` / `MusicEncodeJob` call the orchestrator, not `IEncoder` directly. Task 2.2 (full HLS stage extraction) deferred — strategies currently wrap `IEncoder` as a seam; format-specific logic can peel out later. |
| 3. Additional File Strategies | ✅ all formats, single + two-pass | `HlsSinglePassStrategy`, `HlsTwoPassStrategy`, `MkvStrategy`, `Mp4SinglePassStrategy`, `Mp4TwoPassStrategy`, `DashSinglePassStrategy`, `DashTwoPassStrategy` all registered. 2-pass orchestration shared via `TwoPassStrategyBase` — subclasses override only `Format`. Pass-1 / pass-2 command layout format-agnostic in `BuildStage`. Pending: multi-variant 2-pass (requires per-variant stats). |
| 4. Checkpoint & Resume | ✅ done (for 2-pass) | `HlsTwoPassStrategy` loads checkpoint on start; if `Pass1Completed` and the stats file still exists on disk, pass 1 is skipped. Pass 1 success saves checkpoint; pass 2 success deletes checkpoint and cleans stats files. Single-pass doesn't need checkpoint granularity (one FFmpeg command, resume semantics don't apply). |
| 5. Live Transcode Strategy | ❌ scaffolded only | `LiveEncoder.StartAsync` creates a session object but does not spawn FFmpeg. Session manager + decision engine work. No strategy wrapper, no transport impl. |
| 6. Distribution | ❌ not started | No `Distribution/` dir. `IJobDispatcher` / `IRemoteWorker` / `IHardwareBenchmark` interfaces exist with zero implementations. |
| 7. Plugin Integration | ✅ implicit | `StrategyResolver` iterates `IEnumerable<IEncodingStrategy>` and walks in reverse so later (plugin) registrations override built-ins. The existing `IPluginServiceRegistrator` pattern lets plugins register additional strategies with zero core changes. `StrategyResolverTests.Resolve_LastRegistrationWins_PluginsOverrideBuiltIn` proves it. |
| 8. Queue & History | ✅ done | `EncodingHistory` DB model + migration + `EncodingHistoryRepository`. `VideoEncodeJob` writes one row per successful encode. `GET /dashboard/encoding/history` paginated endpoint (1–500 per page). Queue-level `pause-queue` / `resume-queue` endpoints via `QueueRunner.Stop/Start("encoder")`. `GET /dashboard/tasks/queue/eta` rolling-average ETA from 50 most-recent history samples × current queue depth. |
| 9. Content Intelligence | ⚠️ 3 of 4 | Crop detection ✅ (9.1), subtitle OCR ✅ (9.2 with auto-download Tesseract model manager), Whisper transcription ✅ (9.3). Intro/outro content detection (9.4) still pending. New nomercy-ffmpeg Windows build provides libtesseract + whisper filters. |
| 10. Format Capabilities | ⚠️ 4 of 7 done | HDR→SDR tonemap ✅, HDR→HDR passthrough ✅ (step 2), burn-in filter ✅ (10.1), loudnorm ✅ (10.4 partial), ABR ladder ✅ (10.3). Pending: DV passthrough (10.2 step 3), explicit downmix/`pan`/`amerge` (10.4 step 1 remainder), wire ABR generator into profile-load path. |
| 11. DRM & Encryption | ❌ not started | No `IDrmProcessor`. No AES-128 HLS, no CENC DASH. |
| 12. Presets & Automation | ⚠️ webhooks done | `INotificationDispatcher` + `WebhookNotificationDispatcher` (POST with exponential-backoff retries) + `EncodingNotificationSubscriber` hosted service wiring the event bus; configurable via `EncoderOptions.NotificationWebhookUrls`. Phase 12.1 preset CRUD + 12.2 watch-folder auto-encode still pending. |
| 13. Audio Strategies | ⚠️ m4a via Mp4 strategy | `ProfileMapper` maps `m4a` / `aac` containers → `OutputFormat.Mp4`. `Mp4OutputStrategy.FinalizeAsync` renames output to `.m4a` when the plan has no video tracks (V1 parity for music). Dedicated mp3 / flac / ogg output strategies still pending — only common case (m4a) landed. |
| 14. Disc Ripping | ❌ data only | `DiscDrive`, `DiscInfo`, `RipRequest`, `MetadataMatch`, `OpticalDiscType` records exist. Zero scanning/ripping logic. |

**External dependency:** Phase 9 depends on the `nomercy-ffmpeg` build (libtesseract + whisper filters both verified present in the 8.0-NoMercy-MediaServer Windows build). Phase 11 (CENC DASH) depends on `mp4box` / `shaka-packager` integration — not in nomercy-ffmpeg.

**V1 pause/resume/cancel parity** — shipped in a separate track alongside the plan. `EncoderProcessRegistry` (singleton, thread-safe) tracks live FFmpeg PIDs per job id; `EventBusProgressObserver` registers PIDs on first progress tick; `TasksController` pause/{id}, resume/{id}, and delete queue/{id} all operate via registry + `ProcessThrottle.Suspend/Resume` / `Process.Kill(entireProcessTree:true)`.

---

## Consumer-first roadmap (reprioritized 2026-04-16)

The phased plan below kept the original phase numbers for continuity. **Execution order** is now driven by what a home user actually experiences, not by phase number. Pro-tier features (distribution, DRM) are parked at the bottom until consumer parity is complete.

### Tier 1 — core consumer workflow (ship first)

1. ✅ **Phase 12.2 — Watch-folder auto-encode** — shipped (`6328cc09`). `MediaFilesScannedEvent` fires after library rescan; `AutoEncodeSubscriber` hosted service dispatches `VideoEncodeJob` per file in folders that have an `EncoderProfileFolder` assignment. Idempotent via sibling `.NoMercy` / `*.m3u8` check.
2. ✅ **Phase 14 / Tier 1.2 — Disc ripping** — shipped (`600b1b39`). `DiscScanner` (ffprobe → `DiscInfo`), `DriveMonitor` (polling CDRom DriveInfo, emits DriveEvents), `DiscRipper` (FFmpeg stream-copy per title, bluray `-playlist` support, opt-in audio/subtitle track mapping). 16 new tests, 747 total passing.
3. ✅ **Tier 1.3 — Multi-variant HLS 2-pass** — shipped (`1eec5e14`). `BuildInput.Pass1VariantIndex` threaded through `Encoder` → `BuildStage`; `TwoPassStrategyBase` loops pass 1 per variant with `{base}_v{i}` stats files; resume requires all variant stats present (partials invalid).
4. ✅ **Tier 1.4 — Auto ABR ladder wire-up** — shipped (`3a1b2c35`). `EncodingProfile.AutoLadder` opt-in flag; `PlanStage.ExpandAutoLadder` injects `IAbrLadderGenerator` and replaces user variants with source-analyzed ladder when the flag is set.

### Tier 2 — polish that matters for the "Netflix-on-your-own-server" pitch

5. **Phase 9.4 — Intro / outro detection.** Skip-intro button in the player. Requires `IAudioFingerprinter` implementation (chromaprint CLI is in the FFmpeg build).
6. **Phase 5 — Live transcode.** When a client can't direct-play a file, transcode on demand. Core of "plays on every device".
7. **Phase 10.2 step 3 — Dolby Vision passthrough.** HEVC → HEVC DV metadata preservation for 4K movie collectors.
8. ✅ **Phase 13 remaining — mp3 / flac / ogg single-file audio** — shipped (`65850740`). `OutputFormat.Mp3/Flac/Ogg` added; `SingleFileAudioOutputStrategy` base with three thin subclasses; `Mp3Strategy`/`FlacStrategy`/`OggStrategy` single-pass resolvers; `ProfileValidator` enforces container↔codec pairing (mp3→lame, flac→flac, ogg→vorbis/opus/flac) and rejects video/subtitle outputs. 26 new tests.

### Tier 3 — nice-to-have, low blast radius

9. **Phase 10.4 — Pan matrix / `amerge` downmix.** `-ac N` covers the common case; explicit matrices are niche.
10. **Phase 12.1 — Preset library.** `EncoderProfile` already serves as a preset — would need a product design pass before adding richer metadata.
11. Multi-variant 2-pass for MP4 / DASH (same generalization as the HLS one).

### Tier 4 — pro / B2B (deferred)

- **Phase 6 — Distribution** (remote workers, quality / time-split encoding across servers).
- **Phase 11 — DRM & encryption** (AES-128 HLS, CENC DASH). Only required for paid tiers or businesses.
- **Phase 7 — Plugin-contributed strategies beyond the existing resolver pattern** (the resolver already discovers plugin strategies; pro-tier work here would be hardening the plugin contract + marketplace).

---

## Phase 1: Foundation (DI + Building Blocks)

Extract all hardcoded `new()` instances into interfaces. Register everything in DI. No behavior change — existing 590 tests must still pass.

### Task 1.1: Extract Building Block Interfaces

**Files:**
- Create: `src/NoMercy.Encoder/BuildingBlocks/IFilterGraphBuilder.cs`
- Create: `src/NoMercy.Encoder/BuildingBlocks/IPlaylistGenerator.cs`
- Create: `src/NoMercy.Encoder/BuildingBlocks/ISubtitleExtractor.cs`
- Create: `src/NoMercy.Encoder/BuildingBlocks/IFontExtractor.cs`
- Create: `src/NoMercy.Encoder/BuildingBlocks/IHlsVariantAnalyzer.cs`
- Create: `src/NoMercy.Encoder/BuildingBlocks/IChapterWriter.cs`
- Create: `src/NoMercy.Encoder/BuildingBlocks/IThumbnailGenerator.cs`
- Modify: `src/NoMercy.Encoder/Commands/FilterGraphBuilder.cs` — implement `IFilterGraphBuilder`
- Modify: `src/NoMercy.Encoder/Output/PlaylistGenerator.cs` — implement `IPlaylistGenerator`
- Modify: `src/NoMercy.Encoder/PostProcess/SubtitleExtractor.cs` — implement `ISubtitleExtractor`
- Modify: `src/NoMercy.Encoder/PostProcess/FontExtractor.cs` — implement `IFontExtractor`
- Modify: `src/NoMercy.Encoder/PostProcess/ChapterWriter.cs` — implement `IChapterWriter`
- Modify: `src/NoMercy.Encoder/PostProcess/ThumbnailGenerator.cs` — implement `IThumbnailGenerator`
- Modify: `src/NoMercy.Encoder/Output/HlsVariantAnalyzer.cs` — implement `IHlsVariantAnalyzer`

- [x] **Step 1:** Create each interface file extracting the public methods from the concrete class. Each interface lives in `BuildingBlocks/` — the implementations stay in their current folders.
- [x] **Step 2:** Add `: IInterfaceName` to each concrete class.
- [x] **Step 3:** Run `dotnet build` — verify 0 errors. No test changes needed since tests use concrete types.
- [x] **Step 4:** Commit: `refactor(encoder): extract building block interfaces` — merged as `36dca39`

### Task 1.2: Register Building Blocks in DI

**Files:**
- Modify: `src/NoMercy.Encoder/Composition/ServiceCollectionExtensions.cs`

- [x] **Step 1:** Add transient registrations for each building block interface → implementation (shipped in `ServiceCollectionExtensions.cs`).
- [x] **Step 2:** Run full test suite — 590 tests pass.
- [x] **Step 3:** Commit: `refactor(encoder): register building blocks in DI, inject into stages` — merged as `dc325cc`

### Task 1.3: Replace new() with Injected Building Blocks

**Files:**
- Modify: `src/NoMercy.Encoder/Pipeline/Stages/BuildStage.cs` — inject `ISubtitleExtractor`, `IFontExtractor`, `IThumbnailGenerator`
- Modify: `src/NoMercy.Encoder/Pipeline/Stages/FinalizeStage.cs` — inject `IChapterWriter`, `IFontExtractor`
- Modify: `src/NoMercy.Encoder/Output/HlsOutputStrategy.cs` — inject via method parameter or factory

- [x] **Step 1:** Change `BuildStage` constructor to accept injected `IFontExtractor` + `ISubtitleExtractor`. `ThumbnailGenerator` is unused by BuildStage (spritevtt muxer replaces it); left out intentionally.
- [x] **Step 2:** Change `FinalizeStage` to accept injected `IChapterWriter` and `IFontExtractor`.
- [ ] **Step 3:** For `HlsOutputStrategy` (created via `new` in `GetStrategy()`): register `IOutputStrategy` implementations in DI and resolve by format. Create `IOutputStrategyFactory` that resolves from DI by `OutputFormat`. **(Still pending — `GetStrategy` is a static switch in `BuildStage`/`FinalizeStage`.)**
- [x] **Step 4:** Update test mocks where constructors changed.
- [x] **Step 5:** Run full test suite — all pass (590).
- [x] **Step 6:** CSharpier format, commit: `refactor(encoder): register building blocks in DI, inject into stages` — merged as `dc325cc`

### Task 1.4: Add EncodeMode to Profile

**Files:**
- Create: `src/NoMercy.Encoder/Codecs/EncodeMode.cs`
- Modify: `src/NoMercy.Encoder/Profiles/EncodingProfile.cs`
- Modify: `src/NoMercy.Encoder/Profiles/ProfileMapper.cs`
- Modify: `src/NoMercy.Encoder/Profiles/V1ProfileTypes.cs`

- [x] **Step 1:** Create `EncodeMode` enum: `SinglePass`, `TwoPass`.
- [x] **Step 2:** Add `EncodeMode EncodeMode = EncodeMode.SinglePass` to `EncodingProfile`.
- [x] **Step 3:** Added `string? encodeMode` parameter to `ProfileMapper.FromV1` with case-insensitive parsing (`2pass`/`twopass`/`two_pass`/`two-pass` → TwoPass, everything else → SinglePass).
- [x] **Step 4:** Run tests — all pass (601, including 11 new EncodeMode parser tests).
- [x] **Step 5:** Commit: `feat(encoder): add EncodeMode to profile schema` — merged as `8520d59`

---

## Phase 2: Strategy Pattern

Create the core strategy architecture. Extract current HLS logic. Replace `Encoder` class with `EncodingOrchestrator`.

### Task 2.1: Create Strategy Interfaces and Orchestrator

**Files:**
- Create: `src/NoMercy.Encoder/Orchestration/IEncodingOrchestrator.cs`
- Create: `src/NoMercy.Encoder/Orchestration/EncodingOrchestrator.cs`
- Create: `src/NoMercy.Encoder/Orchestration/IStrategyResolver.cs`
- Create: `src/NoMercy.Encoder/Orchestration/StrategyResolver.cs`
- Create: `src/NoMercy.Encoder/Orchestration/EncodingContext.cs` (move from Pipeline/)
- Create: `src/NoMercy.Encoder/Strategies/IEncodingStrategy.cs`
- Create: `tests/NoMercy.Tests.Encoder/Orchestration/OrchestratorTests.cs`
- Create: `tests/NoMercy.Tests.Encoder/Orchestration/StrategyResolverTests.cs`

- [ ] **Step 1:** Write `IEncodingStrategy` interface:
```csharp
public interface IEncodingStrategy
{
    string Format { get; }
    string EncodeMode { get; }
    Task<EncodingResult> EncodeAsync(EncodingContext context, IProgressObserver? progress, CancellationToken ct);
    ValidationResult ValidateProfile(EncodingProfile profile, MediaInfo mediaInfo);
}
```
- [ ] **Step 2:** Write `IStrategyResolver` and `StrategyResolver` — resolves from `IEnumerable<IEncodingStrategy>` injected via DI (all registered strategies). Matches on `Format` + `EncodeMode`.
- [ ] **Step 3:** Write `IEncodingOrchestrator` and `EncodingOrchestrator`:
  - Injects `IMediaAnalyzer`, `IStrategyResolver`, `IProfileValidator`, `ILogger`
  - `EncodeAsync`: analyze → validate → resolve strategy → hand off
  - Wraps progress observer with stage notifications (Analyze, Validate, hand off to strategy)
- [ ] **Step 4:** Write tests: resolver finds correct strategy, orchestrator calls analyze then strategy.
- [ ] **Step 5:** Register in DI. `IEncodingOrchestrator` replaces `IEncoder`.
- [ ] **Step 6:** Run full test suite.
- [ ] **Step 7:** Commit: `feat(encoder): orchestrator + strategy resolver`

### Task 2.2: Extract HlsSinglePassStrategy

**Files:**
- Create: `src/NoMercy.Encoder/Strategies/Hls/HlsSinglePassStrategy.cs`
- Modify: `src/NoMercy.Encoder/Composition/ServiceCollectionExtensions.cs`
- Create: `tests/NoMercy.Tests.Encoder/Strategies/Hls/HlsSinglePassStrategyTests.cs`

- [ ] **Step 1:** Move logic from `BuildStage`, `ExecuteStage`, `FinalizeStage`, and `PlanStage` into `HlsSinglePassStrategy.EncodeAsync()`. The strategy injects building blocks directly — no more stages. Flow:
  1. Resolve codecs via `ICodecResolver`
  2. Build output plan
  3. Build filter graph via `IFilterGraphBuilder`
  4. Configure HLS output (segment naming, keyframes, etc.)
  5. Add subtitle outputs via `ISubtitleExtractor`
  6. Add sprite output (spritevtt)
  7. Execute main command via `IFfmpegExecutor`
  8. Execute bitmap subtitle extraction
  9. Execute font extraction via `IFontExtractor`
  10. Finalize: master playlist via `IPlaylistGenerator` + `IHlsVariantAnalyzer`, chapters via `IChapterWriter`, fonts manifest
- [ ] **Step 2:** Register as `IEncodingStrategy` in DI.
- [ ] **Step 3:** Write strategy tests with mocked building blocks.
- [ ] **Step 4:** Update `VideoEncodeJob` to call `IEncodingOrchestrator` instead of `IEncoder`.
- [ ] **Step 5:** Run real encode test — output must be identical to current encoder.
- [ ] **Step 6:** Remove old `Encoder.cs` and stage classes (or mark obsolete for gradual migration).
- [ ] **Step 7:** Run full test suite, update broken tests.
- [ ] **Step 8:** CSharpier, commit: `feat(encoder): extract HlsSinglePassStrategy, replace Encoder class`

---

## Phase 3: Additional File Strategies

### Task 3.1: HlsTwoPassStrategy

**Files:**
- Create: `src/NoMercy.Encoder/Strategies/Hls/HlsTwoPassStrategy.cs`
- Create: `tests/NoMercy.Tests.Encoder/Strategies/Hls/HlsTwoPassStrategyTests.cs`

- [ ] **Step 1:** Copy `HlsSinglePassStrategy` as base. Modify `EncodeAsync`:
  - Build pass 1 command: same filters but `-pass 1 -passlogfile {statsPath} -f null /dev/null`
  - Execute pass 1 (progress 0-50%)
  - Build pass 2 command: same filters + `-pass 2 -passlogfile {statsPath}` + normal HLS output
  - Execute pass 2 (progress 50-100%)
  - Same finalization as single-pass
  - Clean up stats file on success
- [ ] **Step 2:** Validate that profile uses software encoder — NVENC doesn't benefit from 2-pass. Return error if hardware encoder + 2-pass.
- [ ] **Step 3:** Write tests with mocked executor verifying two commands are run with correct pass flags.
- [ ] **Step 4:** Register in DI.
- [ ] **Step 5:** Test with real encode (software x264, CRF disabled, target bitrate set).
- [ ] **Step 6:** Commit: `feat(encoder): HLS two-pass strategy`

### Task 3.2: Mp4SinglePassStrategy

**Files:**
- Create: `src/NoMercy.Encoder/Strategies/Mp4/Mp4SinglePassStrategy.cs`
- Create: `tests/NoMercy.Tests.Encoder/Strategies/Mp4/Mp4SinglePassStrategyTests.cs`

- [ ] **Step 1:** Strategy for single-file MP4 output. Uses same building blocks but:
  - No HLS segmentation (no `-f hls`, no segment filenames)
  - Single output file: `{mediaTitle}.mp4`
  - Subtitles as separate files (MP4 has limited subtitle support)
  - Audio muxed into the same file
  - No master playlist
- [ ] **Step 2:** Tests, DI registration, real encode test.
- [ ] **Step 3:** Commit: `feat(encoder): MP4 single-pass strategy`

### Task 3.3: Mp4TwoPassStrategy

**Files:**
- Create: `src/NoMercy.Encoder/Strategies/Mp4/Mp4TwoPassStrategy.cs`
- Create: `tests/NoMercy.Tests.Encoder/Strategies/Mp4/Mp4TwoPassStrategyTests.cs`

- [ ] **Step 1:** Same 2-pass pattern as HLS but single-file output.
- [ ] **Step 2:** Tests, DI registration.
- [ ] **Step 3:** Commit: `feat(encoder): MP4 two-pass strategy`

### Task 3.4: MkvStrategy

**Files:**
- Create: `src/NoMercy.Encoder/Strategies/Mkv/MkvStrategy.cs`
- Create: `tests/NoMercy.Tests.Encoder/Strategies/Mkv/MkvStrategyTests.cs`

- [ ] **Step 1:** Matroska output. Key difference: stream copy where codecs are compatible (no re-encode for matching codecs). All streams in one file including subtitles and fonts.
- [ ] **Step 2:** Tests, DI registration.
- [ ] **Step 3:** Commit: `feat(encoder): MKV strategy`

### Task 3.5: DashSinglePassStrategy and DashTwoPassStrategy

**Files:**
- Create: `src/NoMercy.Encoder/Strategies/Dash/DashSinglePassStrategy.cs`
- Create: `src/NoMercy.Encoder/Strategies/Dash/DashTwoPassStrategy.cs`
- Create: `tests/NoMercy.Tests.Encoder/Strategies/Dash/DashStrategyTests.cs`

- [ ] **Step 1:** DASH output using `-f dash` with MPD manifest. Similar to HLS but different segmentation and manifest format. Required for Widevine DRM later.
- [ ] **Step 2:** Tests, DI registration, real encode test.
- [ ] **Step 3:** Commit: `feat(encoder): DASH strategies (single-pass + two-pass)`

---

## Phase 4: Checkpoint & Resume

### Task 4.1: Extend JobCheckpoint for Encode State

**Files:**
- Modify: `src/NoMercy.Encoder/Jobs/JobCheckpoint.cs`
- Create: `src/NoMercy.Encoder/Jobs/ICheckpointStore.cs`
- Create: `src/NoMercy.Encoder/Jobs/CheckpointStore.cs`
- Create: `tests/NoMercy.Tests.Encoder/Jobs/CheckpointStoreTests.cs`

- [x] **Step 1:** Extended `JobCheckpoint` with `StatsFilePath`, `Pass1Completed`, `LastCompletedSegment`, `EncodeMode` (all default-valued for JSON back-compat).
- [x] **Step 2:** `ICheckpointStore` (Save, Load, Delete) is keyed by output directory instead of jobId — the checkpoint lives next to its encode output (`{OutputDirectory}/.checkpoint.json`). `JsonCheckpointStore` handles corrupt files by returning null and logging.
- [x] **Step 3:** 8 new `JsonCheckpointStoreTests` cover round-trip, missing file, corrupt file, timestamp refresh, nested-directory creation.
- [x] **Step 4:** Commit: `feat(encoder): checkpoint store for encode state persistence` — merged as `7637c41`

### Task 4.2: Wire Checkpoint into Strategies

**Files:**
- Modify: `src/NoMercy.Encoder/Strategies/Hls/HlsTwoPassStrategy.cs`
- Modify: `src/NoMercy.Encoder/Strategies/Hls/HlsSinglePassStrategy.cs`

- [ ] **Step 1:** At start of `EncodeAsync`, check `ICheckpointStore` for existing checkpoint. If found and `ResumeFromCheckpoint` is true:
  - 2-pass: skip pass 1 if `Pass1Completed` and stats file exists on disk
  - HLS: skip segments before `LastCompletedSegment`
- [ ] **Step 2:** After each pass/milestone, save checkpoint.
- [ ] **Step 3:** On successful completion, delete checkpoint.
- [ ] **Step 4:** Tests: mock checkpoint store, verify resume skips completed work.
- [ ] **Step 5:** Commit: `feat(encoder): checkpoint resume in HLS strategies`

---

## Phase 5: Live Transcode Strategy

### Task 5.1: LiveTranscodeStrategy

**Files:**
- Create: `src/NoMercy.Encoder/Strategies/Live/LiveTranscodeStrategy.cs`
- Create: `src/NoMercy.Encoder/LiveTranscode/ILiveSessionTransport.cs` (implementation)
- Create: `src/NoMercy.Encoder/LiveTranscode/HttpSegmentTransport.cs`
- Create: `tests/NoMercy.Tests.Encoder/Strategies/Live/LiveTranscodeStrategyTests.cs`

- [ ] **Step 1:** Strategy that wraps existing `ILiveEncoder`, `ISessionManager`, `IPlaybackDecisionEngine`. The `EncodeAsync` method:
  1. Create session via `ISessionManager`
  2. `IPlaybackDecisionEngine.Decide()` for transcode decision
  3. If DirectPlay/Remux — return immediately with path to source
  4. If TranscodeVideo/Audio — start `ILiveEncoder` session
  5. Return with session ID (the hub reads segments from the session channel)
- [ ] **Step 2:** Implement `ILiveSessionTransport` — HTTP chunked transfer or SignalR binary frames.
- [ ] **Step 3:** Wire into the SignalR streaming hub (separate from encode job queue — triggered by playback).
- [ ] **Step 4:** Tests: mock session lifecycle, verify decision engine routing.
- [ ] **Step 5:** Commit: `feat(encoder): live transcode strategy with session transport`

---

## Phase 6: Distribution

### Task 6.1: IWorkerDispatcher and Local Implementation

**Files:**
- Create: `src/NoMercy.Encoder/Distribution/IWorkerDispatcher.cs`
- Create: `src/NoMercy.Encoder/Distribution/LocalWorkerDispatcher.cs`
- Create: `src/NoMercy.Encoder/Distribution/EncodeTask.cs`
- Create: `src/NoMercy.Encoder/Distribution/DispatchResult.cs`
- Create: `tests/NoMercy.Tests.Encoder/Distribution/LocalWorkerDispatcherTests.cs`

- [ ] **Step 1:** Define interfaces:
```csharp
public record EncodeTask(string TaskId, FfmpegCommand Command, string OutputPath, TaskType Type);
public enum TaskType { QualityVariant, TimeChunk }
public record DispatchResult(string TaskId, bool Success, string? Error);

public interface IWorkerDispatcher
{
    Task<DispatchResult[]> DispatchAsync(EncodeTask[] tasks, CancellationToken ct);
}
```
- [ ] **Step 2:** `LocalWorkerDispatcher` runs all tasks sequentially on the local machine via `IFfmpegExecutor`. This is the default — same behavior as today.
- [ ] **Step 3:** Tests, DI registration.
- [ ] **Step 4:** Commit: `feat(encoder): local worker dispatcher`

### Task 6.2: Remote Worker Dispatcher

**Files:**
- Create: `src/NoMercy.Encoder/Distribution/RemoteWorkerDispatcher.cs`
- Create: `src/NoMercy.Encoder/Distribution/WorkerAssigner.cs`
- Modify: `src/NoMercy.Encoder/Jobs/IRemoteWorker.cs` (implement if stub)
- Create: `tests/NoMercy.Tests.Encoder/Distribution/RemoteWorkerDispatcherTests.cs`

- [ ] **Step 1:** `WorkerAssigner` uses `IResourceBudget` and `SpeedIndex` to assign tasks to workers by weight.
- [ ] **Step 2:** `RemoteWorkerDispatcher` sends tasks to `IRemoteWorker` instances. Collects results. Feature-flagged — falls back to local if not enabled.
- [ ] **Step 3:** Tests with mocked workers.
- [ ] **Step 4:** Commit: `feat(encoder): remote worker dispatcher with resource-based assignment`

### Task 6.3: Quality Split and Time Split in HLS Strategy

**Files:**
- Modify: `src/NoMercy.Encoder/Strategies/Hls/HlsSinglePassStrategy.cs`

- [ ] **Step 1:** When `IWorkerDispatcher` has multiple workers available:
  - **Quality split:** Create one `EncodeTask` per video variant (resolution). Each task is a complete encode for that variant. Dispatcher assigns to workers. No stitching needed.
  - **Time split:** Analyze source keyframes, split into N time ranges. Each task encodes the same variant for a time range. After all complete, concatenate playlists and renumber segments.
- [ ] **Step 2:** When only one worker (or feature disabled): produce one task per variant, all local. Identical to current behavior.
- [ ] **Step 3:** Tests for task splitting logic.
- [ ] **Step 4:** Commit: `feat(encoder): quality and time split in HLS strategy`

### Task 6.4: Hardware Benchmark

**Files:**
- Create: `src/NoMercy.Encoder/Hardware/HardwareBenchmark.cs`
- Create: `tests/NoMercy.Tests.Encoder/Hardware/HardwareBenchmarkTests.cs`

- [ ] **Step 1:** Implement `IHardwareBenchmark`. Encodes a short (5-second) test clip at each resolution tier with each available encoder. Records fps → populates `SpeedIndex`.
- [ ] **Step 2:** Runs on first startup or on-demand from dashboard API.
- [ ] **Step 3:** Tests, DI registration.
- [ ] **Step 4:** Commit: `feat(encoder): hardware benchmark for worker weighting`

---

## Phase 7: Plugin Integration

### Task 7.1: Wire Plugin Strategies into Resolver

**Files:**
- Modify: `src/NoMercy.Encoder/Orchestration/StrategyResolver.cs`
- Modify: `src/NoMercy.Encoder/Composition/ServiceCollectionExtensions.cs`
- Create: `tests/NoMercy.Tests.Encoder/Orchestration/PluginStrategyResolverTests.cs`

- [ ] **Step 1:** `StrategyResolver` queries `IEnumerable<IEncodingStrategy>` from DI — this automatically includes any plugin-registered strategies.
- [ ] **Step 2:** Plugin strategies checked first (plugin can override built-in format handling).
- [ ] **Step 3:** Write test: register a mock plugin strategy, verify resolver finds it for its format.
- [ ] **Step 4:** Commit: `feat(encoder): plugin strategies discoverable via DI`

### Task 7.2: Plugin Building Block Replacement

**Files:**
- Create: `tests/NoMercy.Tests.Encoder/Plugins/BuildingBlockReplacementTests.cs`

- [ ] **Step 1:** Test that a plugin can register its own `ICodecResolver` and the built-in one is replaced (last DI registration wins).
- [ ] **Step 2:** Document the pattern in the spec.
- [ ] **Step 3:** Commit: `feat(encoder): plugin building block replacement via DI`

---

## Phase 8: Queue & History

### Task 8.1: Encoding History

**Files:**
- Create: `src/NoMercy.Database/Models/Media/EncodingHistory.cs`
- Create migration
- Create: `src/NoMercy.Data/Repositories/EncodingHistoryRepository.cs`
- Create: `src/NoMercy.Api/Controllers/V1/Dashboard/EncodingHistoryController.cs`

- [ ] **Step 1:** `EncodingHistory` model: InputPath, OutputPath, ProfileName, EncoderUsed, Duration, InputSizeBytes, OutputSizeBytes, CompressionRatio, AverageSpeed, AverageBitrate, CreatedAt.
- [ ] **Step 2:** Orchestrator writes a history record after each successful encode.
- [ ] **Step 3:** API endpoint: GET `/api/v1/dashboard/encoding/history` with pagination.
- [ ] **Step 4:** Migration, tests, commit: `feat(encoder): encoding history table and API`

### Task 8.2: Queue Management API

**Files:**
- Modify: `src/NoMercy.Api/Controllers/V1/Dashboard/TasksController.cs`

- [ ] **Step 1:** Add endpoints:
  - POST `/dashboard/tasks/reorder` — change priority
  - POST `/dashboard/tasks/{id}/cancel` — kill process, clean up
  - POST `/dashboard/tasks/pause-queue` — stop dispatching new jobs
  - GET `/dashboard/tasks/queue/eta` — estimated completion based on history
- [ ] **Step 2:** Queue-level pause uses a flag in `QueueContext` — the job runner checks before dispatching.
- [ ] **Step 3:** Tests, commit: `feat(encoder): queue management API`

---

## Phase 9: Content Intelligence

### Task 9.1: Crop Detection

**Files:**
- Create: `src/NoMercy.Encoder/ContentAnalysis/CropDetector.cs`
- Create: `tests/NoMercy.Tests.Encoder/ContentAnalysis/CropDetectorTests.cs`

- [x] **Step 1:** `CropDetector` samples 60 s from the middle of the source (`-ss 120`), counts `crop=W:H:X:Y` observations from stderr, requires ≥ 5 matching observations before trusting the result. Returns `CropResult(Width, Height, X, Y, ShouldCrop)` with `ShouldCrop=false` when crop rectangle matches full frame.
- [ ] **Step 2:** Strategies optionally inject crop into filter graph: `crop={w}:{h}:{x}:{y}` (generator ready, wiring into `BuildBranchFilter` pending).
- [x] **Step 3:** 5 tests cover stable crop / full-frame / insufficient-observations / exit-code / most-frequent-wins. DI registered.
- [x] **Step 4:** Commit: `feat(encoder): V1 feature parity — OCR, Whisper, crop detection, detailed stages` — merged as `3600754`

### Task 9.2: Subtitle OCR

**Files:**
- Create: `src/NoMercy.Encoder/Subtitles/SubtitleOcrEngine.cs`
- Create: `tests/NoMercy.Tests.Encoder/Subtitles/SubtitleOcrEngineTests.cs`

- [x] **Step 1:** `SubtitleOcrEngine` runs the `ocr=language=X` filter with metadata=print output, parses pts_time + `lavfi.ocr.text=` blocks into cues, collapses identical consecutive texts (matches V1). Supports WebVTT or SRT output. `TesseractModelManager` streams missing *.traineddata files from the nomercy-tesseract repo via HttpClient, uses .tmp + rename so cancelled downloads leave no partial files.
- [x] **Step 2:** `VideoEncodeJob` post-process resolves `ISubtitleOcrEngine` via `EncoderProvider.ResolveService<T>()`, runs OCR for every bitmap subtitle in source MediaInfo. Individual language failures are logged and skipped, not fatal.
- [x] **Step 3:** 7 OCR parser tests + 7 Tesseract model manager tests. DI registered.
- [x] **Step 4:** Commit: `feat(encoder): V1 feature parity — OCR, Whisper, crop detection, detailed stages` — merged as `3600754`

### Task 9.3: Whisper Transcription

**Files:**
- Create: `src/NoMercy.Encoder/Subtitles/WhisperTranscriber.cs`
- Create: `tests/NoMercy.Tests.Encoder/Subtitles/WhisperTranscriberTests.cs`

- [x] **Step 1:** `WhisperTranscriber` emits the `whisper=model=...:language=...:queue=3:destination=...:format=srt` filter (matches V1 command exactly). Accepts optional `WhisperOptions` with `TranslateToEnglish` flag. Uses `EncoderOptions.WhisperModelPath` (defaults from `AppFiles.WhisperModelPath`).
- [x] **Step 2:** DI registered. (Integration tests deferred — component is a thin shell around FFmpeg; unit-testing the argument string is low value vs. running a real encode.)
- [x] **Step 3:** Commit: `feat(encoder): V1 feature parity — OCR, Whisper, crop detection, detailed stages` — merged as `3600754`

### Task 9.4: Content Detection (Intro/Outro)

**Files:**
- Create: `src/NoMercy.Encoder/ContentAnalysis/ContentDetector.cs`
- Create: `tests/NoMercy.Tests.Encoder/ContentAnalysis/ContentDetectorTests.cs`

- [ ] **Step 1:** Implement `IContentDetector`. Uses audio fingerprinting (`IAudioFingerprinter`) to detect recurring intro/outro patterns across episodes. Returns `ContentSegment[]` with timestamp ranges and type (Intro, Outro, Credits).
- [ ] **Step 2:** Tests, DI registration.
- [ ] **Step 3:** Commit: `feat(encoder): intro/outro content detection`

---

## Phase 10: Format Capabilities

### Task 10.1: Burn-in Subtitles

**Files:**
- Modify: `src/NoMercy.Encoder/Commands/FilterGraphBuilder.cs`
- Modify: Strategy classes that need burn-in support

- [x] **Step 1:** When `SubtitleMode.BurnIn` is set, `BuildStage.BuildFilterGraph` emits a `subtitles='<escaped-path>':si=<index>` filter on each video branch after scale/format. Colons in the source path are escaped. Text and bitmap subs both route through the same FFmpeg filter (decoded from source).
- [x] **Step 2:** `AddTextSubtitleOutputs` and `BuildBitmapSubtitleCommands` skip `SubtitleMode.BurnIn` entries so no sidecar is written. `SubtitleOutputPlan` carries `Mode` so downstream code can distinguish.
- [x] **Step 3:** 5 new `BuildStageBurnInTests` — commit: `feat(encoder): burn-in subtitle filter support` — merged as `3792ed9`

### Task 10.2: HDR Handling

**Files:**
- Modify: Strategy classes

- [x] **Step 1:** HDR → SDR: Use existing `ITonemapSelector` to add tonemap filters when source is HDR and output profile requests SDR. — `ITonemapSelector` injected in `PlanStage`, `FilterGraphBuilder` applies tonemap when HDR source + SDR target.
- [x] **Step 2:** HDR → HDR passthrough: `PlanStage` emits `-color_primaries`, `-color_trc`, `-colorspace`, `-color_range` flags when `sourceIsHdr && v.TenBit && !v.ConvertHdrToSdr`. Transfer is copied from source (smpte2084 / arib-std-b67). Merged as `a637559`.
- [ ] **Step 3:** Dolby Vision: When HEVC→HEVC, preserve DV metadata via stream copy of enhancement layer. **(Pending — requires reading DV side-data from source and encoder-side preservation.)**
- [x] **Step 4:** 5 new `PlanStageHdrPassthroughTests` cover HDR10 / HLG / SDR / explicit-SDR-conversion cases.

### Task 10.3: ABR Ladder Generator

**Files:**
- Create: `src/NoMercy.Encoder/BuildingBlocks/IAbrLadderGenerator.cs`
- Create: `src/NoMercy.Encoder/BuildingBlocks/AbrLadderGenerator.cs`
- Create: `tests/NoMercy.Tests.Encoder/BuildingBlocks/AbrLadderGeneratorTests.cs`

- [x] **Step 1:** `AbrLadderGenerator` analyzes `MediaInfo` (resolution + bitrate density in kbps/Mp) and emits a standard streaming ladder (360p/480p/720p/1080p/1440p/2160p) below the source. Complexity scale clamped to `[0.5, 1.2]` scales tier bitrates for simple/complex content. Non-standard heights get a native-resolution tier appended.
- [x] **Step 2:** Optional — the generator is invoked only when callers opt in. Manual profiles still work unchanged. Wiring into profile-load path (feature flag / opt-in on the profile itself) still pending.
- [x] **Step 3:** 8 new `AbrLadderGeneratorTests` cover tier selection, codec copy, bitrate scaling, even-width, audio-only empty, non-standard heights. DI registered.
- [x] **Step 4:** Commit: `feat(encoder): ABR ladder generator` — merged as `c26f444`

### Task 10.4: Audio Mixing and Normalization

**Files:**
- Modify: `src/NoMercy.Encoder/Commands/FilterGraphBuilder.cs`

- [x] **Step 1 (partial):** `PlanStage` maps `LoudnessMode` to `loudnorm` filter targets (EBU R128 → `I=-16:TP=-1.5:LRA=11`, ReplayGain → `I=-18:TP=-1.5:LRA=11`). All four output strategies emit `-af` when a filter is set and action is Transcode. Downmix is handled by existing `-ac N` emission (FFmpeg auto-downmix). **Pending:** explicit `pan=` matrix for custom downmix, `amerge` for commentary+main mixing.
- [x] **Step 2:** 4 new `PlanStageAudioFilterTests` cover each `LoudnessMode` branch. Commit: `feat(encoder): audio loudness normalization via loudnorm filter` — merged as `6f26c83`

---

## Phase 11: DRM & Encryption

### Task 11.1: IDrmProcessor and AES-128 HLS

**Files:**
- Create: `src/NoMercy.Encoder/BuildingBlocks/IDrmProcessor.cs`
- Create: `src/NoMercy.Encoder/BuildingBlocks/Aes128HlsDrmProcessor.cs`
- Create: `tests/NoMercy.Tests.Encoder/BuildingBlocks/Aes128DrmProcessorTests.cs`

- [ ] **Step 1:** `IDrmProcessor` interface: `ProcessAsync(outputDirectory, manifest, drmConfig)`. Encrypts segments and updates manifest with key URI.
- [ ] **Step 2:** AES-128 implementation: generates encryption key, encrypts `.ts` segments with `openssl` or FFmpeg's built-in `-hls_key_info_file`, updates playlist with `#EXT-X-KEY` tag.
- [ ] **Step 3:** Strategy calls `IDrmProcessor` after finalization if DRM is configured in profile.
- [ ] **Step 4:** Tests, DI registration.
- [ ] **Step 5:** Commit: `feat(encoder): AES-128 HLS encryption`

### Task 11.2: CENC for DASH (Widevine/PlayReady)

**Files:**
- Create: `src/NoMercy.Encoder/BuildingBlocks/CencDrmProcessor.cs`
- Create: `tests/NoMercy.Tests.Encoder/BuildingBlocks/CencDrmProcessorTests.cs`

- [ ] **Step 1:** CENC encryption for DASH output. Uses `mp4box` or `shaka-packager` for CENC packaging. Produces encrypted segments + PSSH boxes in MPD.
- [ ] **Step 2:** Key management: processor receives key ID + key from configuration. Key generation and licensing is server-level, not encoder-level.
- [ ] **Step 3:** Tests, DI registration.
- [ ] **Step 4:** Commit: `feat(encoder): CENC DRM for DASH (Widevine/PlayReady)`

---

## Phase 12: Presets & Automation

### Task 12.1: Preset System

**Files:**
- Create: `src/NoMercy.Database/Models/Media/EncodingPreset.cs`
- Create migration
- Create: `src/NoMercy.Data/Repositories/PresetRepository.cs`
- Create: `src/NoMercy.Api/Controllers/V1/Dashboard/PresetsController.cs`

- [ ] **Step 1:** `EncodingPreset` model: Name, Description, Author, Tags, ProfileJson (serialized `EncodingProfile`), ParentPresetId (for inheritance), IsBuiltIn.
- [ ] **Step 2:** CRUD API: create, list, get, update, delete, import (from URL or file), export.
- [ ] **Step 3:** Built-in presets seeded from JSON files (like current encoder profiles).
- [ ] **Step 4:** Migration, tests, commit: `feat(encoder): preset library with CRUD API`

### Task 12.2: Watch Folders

**Files:**
- Modify: `src/NoMercy.MediaProcessing/Files/FolderWatcher.cs`
- Modify: `src/NoMercy.MediaProcessing/Files/LibraryFileWatcher.cs`

- [ ] **Step 1:** Connect file watcher to auto-encoding: new file detected → check folder's assigned profiles → dispatch encode job.
- [ ] **Step 2:** Configurable settle time (wait for file write to complete). Extension filter. Size minimum.
- [ ] **Step 3:** Dedup: don't re-encode files that already have output in the target directory.
- [ ] **Step 4:** Tests, commit: `feat(encoder): watch folder auto-encoding`

### Task 12.3: Webhook Notifications

**Files:**
- Create: `src/NoMercy.Encoder/BuildingBlocks/INotificationDispatcher.cs`
- Create: `src/NoMercy.Encoder/BuildingBlocks/WebhookNotificationDispatcher.cs`
- Create: `tests/NoMercy.Tests.Encoder/BuildingBlocks/WebhookNotificationDispatcherTests.cs`

- [ ] **Step 1:** `INotificationDispatcher` interface: `NotifyAsync(event, payload)`. Plugin-replaceable (Discord, Slack, etc.).
- [ ] **Step 2:** Default `WebhookNotificationDispatcher`: HTTP POST to configured URLs with JSON payload. Retry with exponential backoff.
- [ ] **Step 3:** Orchestrator calls dispatcher at lifecycle points: started, completed, failed.
- [ ] **Step 4:** Configuration: webhook URLs per profile or global.
- [ ] **Step 5:** Tests, DI registration.
- [ ] **Step 6:** Commit: `feat(encoder): webhook notifications on encode events`

---

## Phase 13: Audio Strategies

### Task 13.1: AudioStrategy

**Files:**
- Create: `src/NoMercy.Encoder/Strategies/Audio/AudioStrategy.cs`
- Create: `tests/NoMercy.Tests.Encoder/Strategies/Audio/AudioStrategyTests.cs`

- [ ] **Step 1:** Single-file audio output (M4A, OGG, FLAC). No video track. Loudness normalization via `LoudnessMode`. Metadata preservation.
- [ ] **Step 2:** Wire into `MusicEncodeJob`.
- [ ] **Step 3:** Tests, DI registration.
- [ ] **Step 4:** Commit: `feat(encoder): audio-only encoding strategy`

### Task 13.2: AudioHlsStrategy

**Files:**
- Create: `src/NoMercy.Encoder/Strategies/Audio/AudioHlsStrategy.cs`
- Create: `tests/NoMercy.Tests.Encoder/Strategies/Audio/AudioHlsStrategyTests.cs`

- [ ] **Step 1:** HLS audio-only streaming for the music player. Segments + playlist, no video.
- [ ] **Step 2:** Tests, DI registration.
- [ ] **Step 3:** Commit: `feat(encoder): audio HLS strategy for music streaming`

---

## Phase 14: Disc Ripping — shipped `600b1b39`

### Task 14.1: Disc Ripping Pipeline

**Files:**
- Created: `src/NoMercy.Encoder/DiscRipping/DiscScanner.cs`
- Created: `src/NoMercy.Encoder/DiscRipping/DriveMonitor.cs`
- Created: `src/NoMercy.Encoder/DiscRipping/DiscRipper.cs` + `IDiscRipper.cs`
- Created: `tests/NoMercy.Tests.Encoder/DiscRipping/DiscRipperTests.cs`, `DiscScannerParseTests.cs`, `DriveMonitorTests.cs`

- [x] **Step 1:** `DiscScanner` implements `IDiscScanner` — runs ffprobe against libbluray/libdvdread pseudo-URL and parses the JSON envelope into `DiscInfo` (titles, video/audio/subtitle streams, chapters).
- [x] **Step 2:** `DriveMonitor` implements `IDriveMonitor` — polls `DriveInfo.GetDrives()` filtered to `DriveType.CDRom` every 3 seconds, emits `DriveEvent`s for disc insert/eject/drive add/remove. Singleton DI scope preserves state across `MonitorAsync` enumerations.
- [x] **Step 3:** `DiscRipper` implements the new `IDiscRipper` — builds FFmpeg stream-copy command per selected title (`-playlist {i}` for bluray protocol, `-map` entries for user-opted audio/subtitle streams), writes `{outputDir}/title_NN.mkv` intermediates the regular encoding pipeline can pick up.
- [x] **Step 4:** 16 new tests (42 total under `DiscRipping/`), DI registration in `ServiceCollectionExtensions`. `IDiscMetadataResolver` implementation deferred — scaffold still present; resolver integration happens when a disc rip UI lands.
- [x] **Step 5:** Commit: `feat(encoder): Tier 1.2 — disc ripping scanner, drive monitor, ripper` — merged as `600b1b39`.

---

## Verification

After each phase:
- [ ] All existing tests pass
- [ ] CSharpier format on all changed files
- [ ] Commit and push
- [ ] Real encode test where applicable (HLS output matches V1 reference)

After all phases:
- [ ] Full test suite green
- [ ] Encode test matrix: Darkwing Duck (480p SD), No Game No Life (1080p anime), HDR content, audio-only
- [ ] Dashboard shows correct progress for all strategy types
- [ ] Plugin test: register mock strategy, verify it's called
- [ ] Distribution test: quality split across 2 workers (can be local processes)
