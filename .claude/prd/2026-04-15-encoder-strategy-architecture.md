# Encoder Strategy Architecture

**Date:** 2026-04-15
**Status:** Design approved, pending implementation plan
**Branch:** feat/encoder-v3

## Problem

The V3 encoder works for single-pass HLS encoding. But it's a rigid pipeline — hardcoded stages, hardcoded output strategies, no 2-pass support, no live transcode integration, no plugin extensibility. To become a professional encoding engine (Handbrake-grade), the architecture needs to support arbitrary encoding workflows without code changes.

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
- Stats file lives in temp directory, cleaned up after encode.
- If pass 1 fails, the whole encode fails — no partial output.

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
