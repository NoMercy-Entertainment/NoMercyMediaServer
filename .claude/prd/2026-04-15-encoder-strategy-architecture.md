# NoMercy Encoder — Complete Architecture

**Date:** 2026-04-15
**Status:** Design approved, pending implementation plan
**Branch:** feat/encoder-v3

## Vision

A professional, plugin-extensible encoding engine. Handbrake on steroids — embedded in a media server but capable of standing alone as a product. Users control what they want. We provide the ability to do whatever they wish.

## Solution

Strategy-owns-the-pipeline architecture. The core provides an orchestrator and injectable building blocks. Each output format/mode is a self-contained strategy that composes building blocks however it needs. Plugins provide custom strategies through the same interface.

## Architecture

### Orchestrator

Replaces the current `Encoder` class. Three responsibilities:

1. Analyze source via `IMediaAnalyzer` → `MediaInfo`
2. Resolve strategy via `IStrategyResolver` (matches `OutputFormat` + `EncodeMode` to an `IEncodingStrategy`)
3. Hand off to the strategy with an `EncodingContext`

```csharp
public interface IEncodingOrchestrator
{
    Task<EncodingResult> EncodeAsync(EncodingRequest request, IProgressObserver? progress, CancellationToken ct);
}
```

The orchestrator does NOT build FFmpeg commands, execute processes, or write playlists. That's the strategy's job.

### Strategies

Each strategy implements `IEncodingStrategy` and owns the full encode lifecycle for its format:

```csharp
public interface IEncodingStrategy
{
    string Format { get; }                    // "hls", "mp4", "mkv", "live", "plugin:x"
    string EncodeMode { get; }                // "single-pass", "two-pass", "live"
    Task<EncodingResult> EncodeAsync(EncodingContext context, IProgressObserver? progress, CancellationToken ct);
    ValidationResult ValidateProfile(EncodingProfile profile, MediaInfo mediaInfo);
}
```

`EncodingContext` contains: `MediaInfo`, `EncodingProfile`, `OutputDirectory`, `MediaTitle`, `CorrelationId`.

### Built-in Strategies

| Strategy | Format | Mode | Description |
|----------|--------|------|-------------|
| `HlsSinglePassStrategy` | HLS | SinglePass | Current working encoder extracted into a strategy |
| `HlsTwoPassStrategy` | HLS | TwoPass | Pass 1 analyzes complexity, pass 2 encodes with optimal bitrate allocation. Software encoders only. |
| `Mp4SinglePassStrategy` | MP4 | SinglePass | Single-file MP4 output |
| `Mp4TwoPassStrategy` | MP4 | TwoPass | Archival quality MP4 |
| `MkvStrategy` | MKV | SinglePass | Matroska container, stream copy where possible |
| `LiveTranscodeStrategy` | Live | Live | Streams segments to a client session, no disk output |

### Strategy Resolution

`IStrategyResolver` maps `(OutputFormat, EncodeMode)` to a strategy:

1. Check plugin-registered strategies first (plugins can override built-in)
2. Fall back to built-in strategies
3. Throw if no strategy matches

### Building Blocks

Injectable services that strategies compose. Not a pipeline — a toolbox.

| Service | Interface | Purpose |
|---------|-----------|---------|
| Media analysis | `IMediaAnalyzer` | ffprobe source analysis |
| Codec resolution | `ICodecResolver` | Pick best encoder for codec + hardware + preference |
| Filter graph | `IFilterGraphBuilder` | Construct FFmpeg filter_complex strings |
| FFmpeg execution | `IFfmpegExecutor` | Run commands with progress and kill signal |
| Playlist generation | `IPlaylistGenerator` | Write HLS/DASH master playlists with measured bandwidth |
| Subtitle extraction | `ISubtitleExtractor` | Resolve output paths, codecs, variants (sign/full/sdh/alt) |
| Font extraction | `IFontExtractor` | Extract embedded fonts and write manifest |
| Variant analysis | `IHlsVariantAnalyzer` | Measure actual bitrate from encoded segments |
| Work dispatch | `IWorkerDispatcher` | Assign encode tasks to local or remote workers |
| Hardware detection | `IHardwareCapabilities` | GPU info, encoder session limits, resource budget |

All registered in DI. Strategies receive them via constructor injection. Plugins can replace any building block by registering their own implementation.

### Distribution (Paid Feature)

Two split modes, both optional and gated behind a feature flag:

**Quality split:** Each worker encodes a different variant (4K on beast machine, 1080p on GPU box, 720p on modest server). No stitching — each variant is a complete playlist.

**Time split:** Each worker encodes a time range of the same variant. Playlists concatenated and segments renumbered after all workers finish.

Both modes use the same interface:

```csharp
public interface IWorkerDispatcher
{
    Task<DispatchResult[]> DispatchAsync(EncodeTask[] tasks, CancellationToken ct);
}
```

Strategies produce a list of `EncodeTask` objects. The dispatcher assigns them to workers based on `IResourceBudget` weights. If no remote workers are available or the feature isn't enabled, all tasks run locally — same code path, single worker.

`IRemoteWorker` (already exists) handles communication with remote encoding nodes.

### Plugin System

Plugins provide custom strategies by implementing `IEncodingStrategy` and registering via `IPluginServiceRegistrator` during initialization. The strategy resolver discovers them through DI — plugin strategies and built-in strategies sit in the same registry.

Plugins get access to all building blocks through constructor injection. What they do with them is their business. A plugin could:

- Provide a WebM/VP9 strategy
- Provide a ProRes strategy for editing workflows
- Implement a custom distributed encoding protocol
- Add a preview strategy that encodes clips at multiple qualities for comparison

Plugins can also replace building blocks (e.g., a custom `ICodecResolver` that supports proprietary hardware).

### Profile Changes

`EncodingProfile` gets one new field:

```csharp
public record EncodingProfile(
    ...
    EncodeMode EncodeMode = EncodeMode.SinglePass,  // SinglePass, TwoPass
    ...
);
```

User-editable in the seed file. The strategy resolver uses `Format` + `EncodeMode` to pick the right strategy.

Live transcode is triggered by playback requests, not by encoding profiles — it's a separate entry point on the orchestrator or invoked directly by the streaming hub.

### 2-Pass Encoding Details

For strategies that support 2-pass:

- **Pass 1:** `ffmpeg -pass 1 -passlogfile {statsFile} -f null /dev/null` — analyzes source complexity, writes stats file, produces no output. Software encoders only (libx264, libx265, libsvtav1). NVENC/QSV/AMF ignore 2-pass (they have their own lookahead).
- **Pass 2:** `ffmpeg -pass 2 -passlogfile {statsFile} {normal output}` — uses stats for optimal bit allocation.
- Progress: pass 1 reports 0-50%, pass 2 reports 50-100%.
- If pass 1 fails, the whole encode fails — no partial output.

### Checkpoint & Resume

2-pass encodes (and long single-pass encodes) must survive system restarts. The `JobCheckpoint` system persists encode state to the database.

**What's checkpointed:**
- Pass 1 stats file path and completion flag
- Pass 2 progress (last completed segment for HLS, byte offset for single-file formats)
- Encode task assignment for distributed encodes (which worker had which task)

**Resume behavior on restart:**
- Pass 1 completed, stats file exists on disk → skip to pass 2
- Pass 1 incomplete or stats file missing → re-run pass 1 from scratch
- Pass 2 incomplete, HLS segments on disk → resume from last completed segment
- Pass 2 incomplete, single-file output → re-run pass 2 (can't resume mid-file)

Stats files persist in the output directory (not temp) so they survive reboots. Cleaned up after successful pass 2 completion.

The `EncodingRequest.Options.ResumeFromCheckpoint` flag (already exists) controls this behavior. Strategies check for existing checkpoints at the start of `EncodeAsync` and skip completed work.

### Migration Path

The refactor preserves all current functionality:

1. Extract current `BuildStage` + `ExecuteStage` + `FinalizeStage` HLS logic into `HlsSinglePassStrategy`
2. The orchestrator replaces `Encoder` class, delegates to the strategy
3. `VideoEncodeJob` calls `IEncodingOrchestrator.EncodeAsync()` — same interface, different internals
4. All existing tests continue to work — strategy produces identical output
5. New strategies (TwoPass, Mp4, Live) added incrementally without touching existing code

### File Structure

```
src/NoMercy.Encoder/
  Orchestration/
    IEncodingOrchestrator.cs
    EncodingOrchestrator.cs
    IStrategyResolver.cs
    StrategyResolver.cs
    EncodingContext.cs
  Strategies/
    IEncodingStrategy.cs
    Hls/
      HlsSinglePassStrategy.cs
      HlsTwoPassStrategy.cs
    Mp4/
      Mp4SinglePassStrategy.cs
      Mp4TwoPassStrategy.cs
    Mkv/
      MkvStrategy.cs
    Live/
      LiveTranscodeStrategy.cs
  BuildingBlocks/
    IFilterGraphBuilder.cs      (extracted from FilterGraphBuilder)
    IPlaylistGenerator.cs       (extracted from PlaylistGenerator)
    ISubtitleExtractor.cs       (extracted from SubtitleExtractor)
    IFontExtractor.cs           (extracted from FontExtractor)
    IHlsVariantAnalyzer.cs      (extracted from HlsVariantAnalyzer)
  Distribution/
    IWorkerDispatcher.cs
    LocalWorkerDispatcher.cs
    RemoteWorkerDispatcher.cs
    EncodeTask.cs
    DispatchResult.cs
```

Existing folders (`Codecs/`, `Hardware/`, `Execution/`, `Analysis/`, `Infrastructure/`) stay unchanged — they're already building blocks.

## Content Intelligence

Pre-processing building blocks that run before encoding. Strategies can optionally use them. Each is an injectable service with an interface.

### Crop Detection (`ICropDetector`)

Interface exists. Analyzes black bars via FFmpeg `cropdetect` filter. Returns crop dimensions. Strategies inject the detected crop into their filter graph.

### Content Detection (`IContentDetector`)

Interface exists. Detects intro/outro/credits boundaries. Uses audio fingerprinting or scene detection. Returns timestamp ranges. Strategies can use these to:
- Skip intros during live transcode
- Chapter-mark intros in the output
- Encode intros at lower quality (saves bitrate)

### Audio Fingerprinting (`IAudioFingerprinter`)

Interface exists. Identifies content via audio signatures (Chromaprint/AcoustID). Used by content detection and for matching episodes across different releases.

### Whisper Transcription (`IWhisperTranscriber`)

Interface exists. The custom nomercy-ffmpeg has whisper built in. Generates subtitles from audio — full speech-to-text. Produces WebVTT/SRT output. Can run as a pre-processing step or as its own strategy.

### OCR Subtitles (`ISubtitleOcrEngine`)

Interface exists. The custom nomercy-ffmpeg has libtesseract. Converts bitmap subtitles (PGS/VobSub) to text (WebVTT/SRT). Can run during encoding (as a filter) or as post-processing.

## Format Capabilities

### HDR → SDR Tonemapping

`ITonemapSelector` and `TonemapSelector` exist. Support for:
- CPU tonemapping via zscale + tonemap filters
- GPU tonemapping via libplacebo (Vulkan)
- Strategies add tonemap to the filter graph when source is HDR and output is SDR

### Dolby Vision / HDR10+

Dynamic metadata passthrough when encoding HEVC→HEVC. When transcoding to SDR, the tonemap path handles conversion. Requires proper tagging (`-tag:v hvc1`, color metadata flags).

### Burn-in Subtitles

`SubtitleMode.BurnIn` exists in the enum but isn't implemented. The filter graph adds a `subtitles` or `ass` filter that renders text onto the video. Used when the output format doesn't support subtitle tracks (e.g., some MP4 players) or when the user explicitly wants hardcoded subs.

### Multi-Audio Handling

Current: each source audio stream matching AllowedLanguages is encoded as a separate track. Future: support audio mixing (commentary + main), downmixing (5.1 → stereo), and audio normalization (loudness mode already has `LoudnessMode` enum).

### Disc Ripping

Full interface set exists (`IDiscScanner`, `IDriveMonitor`, `IDiscMetadataResolver`). Separate workflow from file encoding — scans optical drives, rips to intermediate format, then feeds into the encoding pipeline as a source. The strategy pattern supports this naturally: the source could be a disc instead of a file.

## Live Transcode Details

The `LiveTranscodeStrategy` is fundamentally different from file-based strategies:

- **Entry point:** Triggered by a playback request from a client, not by an encoding job
- **Decision engine:** `IPlaybackDecisionEngine` decides: DirectPlay, Remux, TranscodeVideo, TranscodeAudio
- **Session management:** `ISessionManager` tracks active sessions, enforces per-user and global limits
- **Adaptive quality:** `ILiveQualitySelector` adjusts quality based on client buffer state (via `BufferManager`)
- **Output:** Segments pushed to a `Channel<Segment>` read by the SignalR/HTTP hub — no disk writes
- **Lifecycle:** Runs until client disconnects or session times out
- **Seeking:** Session state machine handles seek requests (Starting→Transcoding→Seeking→Transcoding)
- **Transport:** `ILiveSessionTransport` (interface exists, needs implementation) handles segment delivery protocol

All implementations exist and are DI-registered. Needs wiring into the strategy pattern and connecting to the streaming hub.

## Testing Strategy

| Layer | Approach | Runner |
|---|---|---|
| Building block unit tests | Mock FFmpeg, test codec resolution, filter building, playlist generation | ubuntu-latest |
| Strategy integration tests | Real FFmpeg, test each strategy produces correct output structure | self-hosted (needs NVENC for HW tests) |
| Live transcode tests | Mock client, test session lifecycle, quality switching, seek | ubuntu-latest |
| Distribution tests | Mock workers, test task splitting, assignment, result stitching | ubuntu-latest |
| Plugin tests | Test strategy registration, building block replacement | ubuntu-latest |
| End-to-end encode tests | Real files, real NAS, verify output matches V1 reference | self-hosted (Eagle) |

## Implementation Phases

### Phase 1: Foundation (DI + Building Blocks)
Extract all hardcoded `new()` instances into interfaces and DI registrations. Create the building block interfaces. No behavior change — existing tests pass.

### Phase 2: Strategy Pattern
Create `IEncodingStrategy`, `IStrategyResolver`, `EncodingOrchestrator`. Extract current HLS logic into `HlsSinglePassStrategy`. Replace `Encoder` class. Existing encodes produce identical output.

### Phase 3: Additional File Strategies
`HlsTwoPassStrategy`, `Mp4SinglePassStrategy`, `Mp4TwoPassStrategy`, `MkvStrategy`. Each with tests.

### Phase 4: Checkpoint & Resume
Persist encode state via `JobCheckpoint`. 2-pass stats file survival. HLS segment resume.

### Phase 5: Live Transcode Strategy
Wire existing `LiveTranscode/` implementations into `LiveTranscodeStrategy`. Connect to SignalR hub. Session lifecycle tests.

### Phase 6: Distribution
`IWorkerDispatcher`, quality split, time split. Local dispatcher first. Remote dispatcher with `IRemoteWorker`. Feature-flagged.

### Phase 7: Plugin Integration
Wire `IPluginServiceRegistrator` into strategy resolver. Plugin strategies discoverable via DI. Plugin building block replacement.

### Phase 8: Content Intelligence
Implement `ICropDetector`, `IContentDetector`, `ISubtitleOcrEngine`. Wire whisper transcription. These are independent building blocks — strategies opt in.

### Phase 9: Format Edge Cases
Burn-in subtitles, Dolby Vision passthrough, audio mixing/normalization, disc ripping pipeline.
