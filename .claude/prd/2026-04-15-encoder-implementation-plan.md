# NoMercy Encoder — Complete Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Transform the V3 encoder from a working HLS single-pass encoder into a professional, plugin-extensible encoding engine supporting multiple formats, 2-pass encoding, live transcoding, distributed encoding, content intelligence, and DRM.

**Architecture:** Strategy-owns-the-pipeline. An orchestrator analyzes source media and resolves an encoding strategy. Each strategy (HLS, MP4, MKV, DASH, Live, Audio, Plugin) composes injectable building blocks (FFmpeg executor, codec resolver, filter builder, etc.) to implement its full encode lifecycle. Plugins provide custom strategies and replace building blocks via DI.

**Tech Stack:** C# / .NET 10, FFmpeg (custom nomercy-ffmpeg build), Entity Framework Core (SQLite), SignalR, xUnit + FluentAssertions + Moq

**Spec:** `.claude/prd/2026-04-15-encoder-strategy-architecture.md`

**Branch:** `feat/encoder-v3`

---

## Current Status — 2026-04-16

**Build:** 156 commits ahead of `master` · 0 errors · 0 warnings · 590 encoder tests passing.

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
| 1. Foundation — DI + Building Blocks | ⚠️ 1 of 4 tasks | Interfaces exist (1.1 ✅). Not registered in DI, stages still `new()` (1.2, 1.3). No `EncodeMode` enum (1.4). |
| 2. Strategy Pattern | ❌ not started | No `Orchestration/` or `Strategies/` dir. `Encoder.cs` is still the top-level. Jobs call `IEncoder`. |
| 3. Additional File Strategies | ❌ not started | `Output/` has HLS/MKV/MP4/DASH `IOutputStrategy` impls, but not the plan's full-lifecycle `IEncodingStrategy`. |
| 4. Checkpoint & Resume | ⚠️ partial | `JobCheckpoint` record exists with 5 fields. Not extended with `StatsFilePath`, `Pass1Completed`, `LastCompletedSegment`, `EncodeMode`. No `ICheckpointStore`. |
| 5. Live Transcode Strategy | ❌ scaffolded only | `LiveEncoder.StartAsync` creates a session object but does not spawn FFmpeg. Session manager + decision engine work. No strategy wrapper, no transport impl. |
| 6. Distribution | ❌ not started | No `Distribution/` dir. `IJobDispatcher` / `IRemoteWorker` / `IHardwareBenchmark` interfaces exist with zero implementations. |
| 7. Plugin Integration | ❌ not started | `IEncoderPlugin` exists in `SystemFeatures/`. No plugin strategy resolver, no DI override tests. |
| 8. Queue & History | ❌ not started | No `EncodingHistory` model. `TasksController` has pause/resume per-id and failed-retry but no reorder, queue pause, or ETA. |
| 9. Content Intelligence | ❌ interfaces only | `ICropDetector`, `IContentDetector`, `ISubtitleOcrEngine`, `IWhisperTranscriber` defined. Zero implementations. **Depends on new nomercy-ffmpeg build (libtesseract filter, whisper filter).** |
| 10. Format Capabilities | ⚠️ 1 of 7 steps | HDR→SDR tonemap done. HDR→HDR passthrough, DV passthrough, burn-in filter, ABR ladder, loudnorm, downmix all not done. |
| 11. DRM & Encryption | ❌ not started | No `IDrmProcessor`. No AES-128 HLS, no CENC DASH. |
| 12. Presets & Automation | ❌ not started | No `EncodingPreset` model. `FolderWatcher` exists but does not auto-encode. No webhook dispatcher. |
| 13. Audio Strategies | ❌ not started | No `AudioStrategy`. `MusicEncodeJob` uses the current pipeline directly. |
| 14. Disc Ripping | ❌ data only | `DiscDrive`, `DiscInfo`, `RipRequest`, `MetadataMatch`, `OpticalDiscType` records exist. Zero scanning/ripping logic. |

**External dependency:** Phase 9 (OCR, Whisper) and the spritevtt single-command fix depend on the new `nomercy-ffmpeg` build currently in progress. Phase 11 (CENC DASH) depends on `mp4box` / `shaka-packager` integration — not in nomercy-ffmpeg.

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

- [ ] **Step 1:** Add transient registrations for each building block interface → implementation:
```csharp
services.AddTransient<IFilterGraphBuilder, FilterGraphBuilder>();
services.AddTransient<IPlaylistGenerator, PlaylistGenerator>();
services.AddTransient<ISubtitleExtractor, SubtitleExtractor>();
services.AddTransient<IFontExtractor, FontExtractor>();
services.AddTransient<IChapterWriter, ChapterWriter>();
services.AddTransient<IThumbnailGenerator, ThumbnailGenerator>();
services.AddTransient<IHlsVariantAnalyzer, HlsVariantAnalyzer>();
```
- [ ] **Step 2:** Run full test suite — 590 tests pass.
- [ ] **Step 3:** Commit: `refactor(encoder): register building blocks in DI`

### Task 1.3: Replace new() with Injected Building Blocks

**Files:**
- Modify: `src/NoMercy.Encoder/Pipeline/Stages/BuildStage.cs` — inject `ISubtitleExtractor`, `IFontExtractor`, `IThumbnailGenerator`
- Modify: `src/NoMercy.Encoder/Pipeline/Stages/FinalizeStage.cs` — inject `IChapterWriter`, `IFontExtractor`
- Modify: `src/NoMercy.Encoder/Output/HlsOutputStrategy.cs` — inject via method parameter or factory

- [ ] **Step 1:** Change `BuildStage` constructor to accept injected building blocks instead of `new SubtitleExtractor()`, `new FontExtractor()`, `new ThumbnailGenerator()`. Static methods on `SubtitleExtractor` become instance methods.
- [ ] **Step 2:** Change `FinalizeStage` to accept injected `IChapterWriter` and `IFontExtractor`.
- [ ] **Step 3:** For `HlsOutputStrategy` (created via `new` in `GetStrategy()`): register `IOutputStrategy` implementations in DI and resolve by format. Create `IOutputStrategyFactory` that resolves from DI by `OutputFormat`.
- [ ] **Step 4:** Update test mocks where constructors changed.
- [ ] **Step 5:** Run full test suite — all pass.
- [ ] **Step 6:** CSharpier format, commit: `refactor(encoder): inject building blocks, remove hardcoded new()`

### Task 1.4: Add EncodeMode to Profile

**Files:**
- Create: `src/NoMercy.Encoder/Codecs/EncodeMode.cs`
- Modify: `src/NoMercy.Encoder/Profiles/EncodingProfile.cs`
- Modify: `src/NoMercy.Encoder/Profiles/ProfileMapper.cs`
- Modify: `src/NoMercy.Encoder/Profiles/V1ProfileTypes.cs`

- [ ] **Step 1:** Create `EncodeMode` enum: `SinglePass`, `TwoPass`.
- [ ] **Step 2:** Add `EncodeMode EncodeMode = EncodeMode.SinglePass` to `EncodingProfile`.
- [ ] **Step 3:** Add `string? EncodeMode` to `V1VideoProfile` or the top-level seed structure. `ProfileMapper` maps it.
- [ ] **Step 4:** Run tests — all pass (default is SinglePass, existing behavior unchanged).
- [ ] **Step 5:** Commit: `feat(encoder): add EncodeMode to profile schema`

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

- [ ] **Step 1:** Extend `JobCheckpoint` with encode-specific fields:
```csharp
public record JobCheckpoint(
    string JobId,
    string InputPath,
    string OutputDirectory,
    int[] CompletedGroupIndices,
    DateTime LastUpdated,
    string? StatsFilePath = null,
    bool Pass1Completed = false,
    int LastCompletedSegment = -1,
    string? EncodeMode = null
);
```
- [ ] **Step 2:** Create `ICheckpointStore` interface (Save, Load, Delete by JobId). Implementation persists to database or JSON file in output directory.
- [ ] **Step 3:** Write tests for checkpoint persistence and resume logic.
- [ ] **Step 4:** Commit: `feat(encoder): checkpoint store for encode state persistence`

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

- [ ] **Step 1:** Implement `ICropDetector`. Runs `ffmpeg -i input -vf cropdetect -f null /dev/null` and parses the detected crop from stderr. Returns `CropResult(int Width, int Height, int X, int Y)`.
- [ ] **Step 2:** Strategies optionally inject crop into filter graph: `crop={w}:{h}:{x}:{y}`.
- [ ] **Step 3:** Tests, DI registration.
- [ ] **Step 4:** Commit: `feat(encoder): crop detection building block`

### Task 9.2: Subtitle OCR

**Files:**
- Create: `src/NoMercy.Encoder/Subtitles/SubtitleOcrEngine.cs`
- Create: `tests/NoMercy.Tests.Encoder/Subtitles/SubtitleOcrEngineTests.cs`

- [ ] **Step 1:** Implement `ISubtitleOcrEngine`. Uses the nomercy-ffmpeg libtesseract filter to convert bitmap subtitles (PGS/VobSub) to text (SRT/VTT).
- [ ] **Step 2:** Can run as part of a strategy's subtitle processing or standalone.
- [ ] **Step 3:** Tests, DI registration.
- [ ] **Step 4:** Commit: `feat(encoder): OCR subtitle extraction via libtesseract`

### Task 9.3: Whisper Transcription

**Files:**
- Create: `src/NoMercy.Encoder/Subtitles/WhisperTranscriber.cs`
- Create: `tests/NoMercy.Tests.Encoder/Subtitles/WhisperTranscriberTests.cs`

- [ ] **Step 1:** Implement `IWhisperTranscriber`. Uses nomercy-ffmpeg's whisper filter for speech-to-text. Produces WebVTT/SRT from audio.
- [ ] **Step 2:** Tests, DI registration.
- [ ] **Step 3:** Commit: `feat(encoder): whisper transcription building block`

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

- [ ] **Step 1:** When `SubtitleMode.BurnIn` is set, add `subtitles` or `ass` filter to the video filter chain:
  - Text subs (ASS/SRT): `ass=filename` or `subtitles=filename` filter
  - Bitmap subs: `overlay` filter with PGS/VobSub decode
- [ ] **Step 2:** The subtitle is rendered onto the video — no separate subtitle track in output.
- [ ] **Step 3:** Tests, commit: `feat(encoder): burn-in subtitle support`

### Task 10.2: HDR Handling

**Files:**
- Modify: Strategy classes

- [x] **Step 1:** HDR → SDR: Use existing `ITonemapSelector` to add tonemap filters when source is HDR and output profile requests SDR. — `ITonemapSelector` injected in `PlanStage`, `FilterGraphBuilder` applies tonemap when HDR source + SDR target.
- [ ] **Step 2:** HDR → HDR passthrough: Preserve color metadata (`-color_primaries bt2020`, `-color_trc smpte2084`, etc.) and tag correctly.
- [ ] **Step 3:** Dolby Vision: When HEVC→HEVC, preserve DV metadata via stream copy of enhancement layer.
- [ ] **Step 4:** Tests with HDR test clips, commit: `feat(encoder): HDR tonemap and DV passthrough`

### Task 10.3: ABR Ladder Generator

**Files:**
- Create: `src/NoMercy.Encoder/BuildingBlocks/IAbrLadderGenerator.cs`
- Create: `src/NoMercy.Encoder/BuildingBlocks/AbrLadderGenerator.cs`
- Create: `tests/NoMercy.Tests.Encoder/BuildingBlocks/AbrLadderGeneratorTests.cs`

- [ ] **Step 1:** Analyzes source `MediaInfo` (resolution, bitrate, complexity estimate) and generates optimal `VideoOutput[]` quality ladder. Lower bitrate tiers for simple content (anime), more tiers for complex content (action).
- [ ] **Step 2:** Optional — users can still define manual profiles.
- [ ] **Step 3:** Tests, DI registration.
- [ ] **Step 4:** Commit: `feat(encoder): ABR ladder generator`

### Task 10.4: Audio Mixing and Normalization

**Files:**
- Modify: `src/NoMercy.Encoder/Commands/FilterGraphBuilder.cs`

- [ ] **Step 1:** Implement audio filter chains:
  - Downmix: `pan=stereo|...` filter for 5.1 → stereo
  - Loudness normalization: `loudnorm` filter using `LoudnessMode` enum (EBU R128, ReplayGain)
  - Audio mixing: `amerge` + `pan` for combining commentary + main audio
- [ ] **Step 2:** Tests, commit: `feat(encoder): audio mixing, downmix, loudness normalization`

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

## Phase 14: Disc Ripping

### Task 14.1: Disc Ripping Pipeline

**Files:**
- Create: `src/NoMercy.Encoder/DiscRipping/DiscRipStrategy.cs`
- Create: `src/NoMercy.Encoder/DiscRipping/DiscScanner.cs`
- Create: `src/NoMercy.Encoder/DiscRipping/DriveMonitor.cs`
- Create: `src/NoMercy.Encoder/DiscRipping/DiscMetadataResolver.cs`
- Create: `tests/NoMercy.Tests.Encoder/DiscRipping/DiscRipStrategyTests.cs`

- [ ] **Step 1:** Implement existing interfaces: `IDiscScanner` (scan drive for titles/tracks), `IDriveMonitor` (watch for disc insert/eject events), `IDiscMetadataResolver` (match disc to TMDB/TVDB metadata).
- [ ] **Step 2:** `DiscRipStrategy`: rip selected titles to intermediate MKV (via MakeMKV CLI or similar), then feed into the encoding pipeline as a regular file source.
- [ ] **Step 3:** Tests, DI registration.
- [ ] **Step 4:** Commit: `feat(encoder): disc ripping pipeline`

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
