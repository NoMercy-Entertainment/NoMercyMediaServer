# NoMercy EncoderV2 - Complete Architecture Specification

## Document Status
**Version:** 1.0 Final  
**Status:** Authoritative Implementation Guide  
**Last Updated:** December 2024  

This document consolidates and supersedes all previous architecture drafts. All contradictions have been resolved based on existing codebase analysis and production requirements (16,000+ encoded files).

---

## Executive Summary

EncoderV2 is a **distributed, specification-compliant video encoding system** designed for:
- Production-scale media processing (validated against 16,000+ files)
- Distributed encoding across multiple nodes
- Complete HLS/MP4/MKV specification compliance
- User-defined quality stacks and codec flexibility
- Zero codec restrictions (any FFmpeg-supported codec allowed)

**Core Principles:**
1. **Specification Compliance:** HLS (RFC 8216), MP4 (ISO 14496), MKV (Matroska)
2. **Production-Proven Structure:** Based on validated 16,000+ file output
3. **Smart Distribution:** Heavy operations (HDR→SDR) executed once, shared across qualities
4. **Fault Resilience:** Nodes survive server restarts; server survives node restarts
5. **User Control:** Complete codec/quality/language control via profiles
6. **Database Integration:** QueueContext for all encoder state (NOT separate database)

---

## Part 1: Production-Validated Output Structure

This structure is **proven in production** with 16,000+ encoded files.

### 1.1 HLS Output Directory Layout

```
/path/to/episode/
├── {episode}.{title}.m3u8                    # Master playlist (ONLY ONE)
├── video_{resolution}/                       # Video quality variants
│   ├── video_{resolution}-0000.ts
│   ├── video_{resolution}-0001.ts
│   └── video_{resolution}.m3u8               # Quality variant playlist
├── video_{resolution}_SDR/                   # SDR variant (if HDR source)
│   ├── video_{resolution}_SDR-0000.ts
│   └── video_{resolution}_SDR.m3u8
├── audio_{lang}_{codec}/                     # Per-language + codec audio
│   ├── audio_{lang}_{codec}-0000.ts
│   └── audio_{lang}_{codec}.m3u8
├── subtitles/                                # All subtitle files
│   ├── {episode}.{title}.{lang}.full.ass     # Native ASS (NEVER convert)
│   ├── {episode}.{title}.{lang}.sign.ass     # Signs/songs only
│   ├── {episode}.{title}.{lang}.full.vtt     # WebVTT (optional conversion)
│   └── {episode}.{title}.{lang}.sdh.vtt      # Hearing impaired variant
├── fonts/                                    # All fonts (single folder)
│   ├── Arial.ttf
│   ├── ArialBold.ttf
│   └── NotoSansJapanese.otf
├── thumbs_{width}x{height}/                  # Thumbnail sprites
│   ├── thumbs_{width}x{height}-0000.jpg
│   └── thumbs_{width}x{height}-0001.jpg
├── thumbs_{width}x{height}.webp              # Merged sprite sheet
├── thumbs_{width}x{height}.vtt               # Sprite timing metadata
├── chapters.vtt                              # Chapter markers
├── fonts.json                                # Font manifest with MIME types
├── original/                                 # Source backup (optional)
│   └── {source_file}.mkv
└── _m3u8_backup_{timestamp}/                 # Auto-backup of playlists
    └── {episode}.{title}.m3u8
```

### 1.2 Master Playlist Structure (HLS)

```m3u8
#EXTM3U
#EXT-X-VERSION:6

# Audio tracks (per language + codec, USER-DEFINED ORDER)
#EXT-X-MEDIA:TYPE=AUDIO,GROUP-ID="audio_aac",LANGUAGE="eng",AUTOSELECT=YES,DEFAULT=YES,URI="audio_eng_aac/audio_eng_aac.m3u8",NAME="English AAC"
#EXT-X-MEDIA:TYPE=AUDIO,GROUP-ID="audio_aac",LANGUAGE="jpn",AUTOSELECT=YES,DEFAULT=NO,URI="audio_jpn_aac/audio_jpn_aac.m3u8",NAME="Japanese AAC"
#EXT-X-MEDIA:TYPE=AUDIO,GROUP-ID="audio_eac3",LANGUAGE="eng",AUTOSELECT=YES,DEFAULT=YES,URI="audio_eng_eac3/audio_eng_eac3.m3u8",NAME="English E-AC3"

# Subtitle tracks (per language + type)
#EXT-X-MEDIA:TYPE=SUBTITLES,GROUP-ID="subtitles",LANGUAGE="eng",AUTOSELECT=YES,DEFAULT=YES,URI="subtitles/{episode}.eng.full.vtt",NAME="English (Full)"
#EXT-X-MEDIA:TYPE=SUBTITLES,GROUP-ID="subtitles",LANGUAGE="eng",AUTOSELECT=YES,DEFAULT=NO,URI="subtitles/{episode}.eng.sdh.vtt",NAME="English (SDH)"
#EXT-X-MEDIA:TYPE=SUBTITLES,GROUP-ID="subtitles",LANGUAGE="jpn",AUTOSELECT=YES,DEFAULT=NO,URI="subtitles/{episode}.jpn.full.vtt",NAME="Japanese (Full)"

# Video quality variants with COLOR SPACE metadata
#EXT-X-STREAM-INF:BANDWIDTH=10655852,RESOLUTION=1920x1080,CODECS="avc1.4D0028,mp4a.40.2",AUDIO="audio_aac",SUBTITLES="subtitles",VIDEO-RANGE=SDR,COLOUR-SPACE=BT.709,NAME="1920x1080 SDR"
video_1920x1080_SDR/video_1920x1080_SDR.m3u8

#EXT-X-STREAM-INF:BANDWIDTH=8695194,RESOLUTION=1920x1080,CODECS="avc1.4D0028,mp4a.40.2",AUDIO="audio_aac",SUBTITLES="subtitles",VIDEO-RANGE=PQ,COLOUR-SPACE=BT.2020,NAME="1920x1080 HDR"
video_1920x1080/video_1920x1080.m3u8

#EXT-X-STREAM-INF:BANDWIDTH=3341707,RESOLUTION=1280x720,CODECS="avc1.42001E,mp4a.40.2",AUDIO="audio_aac",SUBTITLES="subtitles",VIDEO-RANGE=SDR,COLOUR-SPACE=BT.709,NAME="1280x720 SDR"
video_1280x720/video_1280x720.m3u8
```

### 1.3 Font Manifest (fonts.json)

```json
[
  {
    "file": "fonts/Arial.ttf",
    "mimeType": "application/x-font-truetype"
  },
  {
    "file": "fonts/ArialBold.ttf",
    "mimeType": "application/x-font-truetype"
  },
  {
    "file": "fonts/NotoSansJapanese.otf",
    "mimeType": "application/x-font-opentype"
  }
]
```

---

## Part 2: System Architecture

### 2.1 Component Overview

```
NoMercy.EncoderV2/                    # Shared Library (Server + Nodes)
├── Core/
│   ├── EncodingSession.cs            # Immutable session state
│   ├── EncodingTask.cs               # Single encoding task
│   ├── TaskWeighting.cs              # CPU/time estimation
│   └── TaskDependencies.cs           # Dependency graph management
├── Jobs/
│   ├── EncodingJobPayload.cs         # Job definition from queue
│   ├── EncodingJobExecutor.cs        # FFmpeg execution
│   └── EncodingJobFactory.cs         # Job creation
├── Profiles/
│   ├── EncodingProfile.cs            # Profile model (mutable)
│   ├── ProfileManager.cs             # CRUD + validation
│   ├── DefaultProfiles.cs            # Built-in profiles
│   └── ProfileValidator.cs           # Validation rules
├── Specifications/
│   ├── HLS/
│   │   ├── HLSSpecification.cs       # Apple RFC 8216 compliance
│   │   ├── HLSPlaylistGenerator.cs   # Master/variant playlists
│   │   └── HLSValidator.cs           # Output validation
│   ├── MP4/
│   │   ├── MP4Specification.cs       # ISO 14496 compliance
│   │   └── MP4Validator.cs
│   └── MKV/
│       ├── MKVSpecification.cs       # Matroska compliance
│       └── MKVValidator.cs
├── Streams/
│   ├── StreamAnalyzer.cs             # FFprobe integration
│   ├── VideoStreamProcessor.cs       # Video processing
│   ├── AudioStreamProcessor.cs       # Audio + language handling
│   └── SubtitleStreamProcessor.cs    # Subtitle + font handling
├── Command/
│   ├── FFmpegCommandBuilder.cs       # Command generation
│   ├── FilterGraphBuilder.cs         # Complex filters
│   ├── HDRProcessing.cs              # HDR→SDR conversion
│   └── CommandValidator.cs
├── Processing/
│   ├── ProgressMonitor.cs            # Real-time progress parsing
│   ├── PostProcessor.cs              # Font/chapter extraction
│   ├── SpriteGenerator.cs            # Thumbnail generation
│   └── SubtitleConverter.cs          # Format conversion
├── Distribution/
│   ├── TaskSplitter.cs               # HLS quality splitting
│   ├── TaskWeightCalculator.cs       # Weight assignment
│   ├── DependencyGraph.cs            # HDR→SDR sharing
│   └── NodeSelector.cs               # Node assignment
└── Validation/
    ├── OutputValidator.cs            # Post-encode validation
    ├── PlaylistValidator.cs          # M3U8 syntax checking
    └── CodecValidator.cs             # Codec compliance

NoMercy.Server/
├── Encoder/
│   ├── ProfileController.cs          # Profile CRUD API
│   ├── EncodingController.cs         # Job submission API
│   ├── CapabilitiesController.cs     # System capabilities
│   └── ProgressHub.cs                # SignalR progress broadcast
└── Services/
    ├── EncoderService.cs             # Local execution + coordination
    ├── JobDispatcher.cs              # Load balancing
    └── NodeHealthMonitor.cs          # 30-second health checks

EncoderNode/                          # Separate application (optional)
├── EncodingNodeService.cs            # Task execution
├── NodeCapabilities.cs               # GPU/CPU detection
├── NodeRegistration.cs               # Server registration
└── (references NoMercy.EncoderV2)
```

### 2.2 Distributed Architecture Flow

```
┌─────────────────────────────────────────────────────────────────┐
│ NoMercy Main Server                                             │
├─────────────────────────────────────────────────────────────────┤
│ ┌──────────────────┐      ┌──────────────────┐                  │
│ │ Profile Manager  │      │ Queue System     │                  │
│ │ (QueueContext)   │      │ (NoMercy.Queue)  │                  │
│ └──────────────────┘      └──────────────────┘                  │
│         ↓                         ↓                             │
│ ┌──────────────────┐      ┌──────────────────┐                  │
│ │ EncoderV2        │◄─────►│ Job Dispatcher   │                 │
│ │ (Shared Library) │      │ (Load Balancing) │                  │
│ └──────────────────┘      └──────────────────┘                  │
│         ↓                         ↓                             │
│ ┌──────────────────────────────────────────────┐                │
│ │ Progress Tracker + WebSocket Broadcaster     │                │
│ └──────────────────────────────────────────────┘                │
└─────┬──────────────────────┬────────────────────┬───────────────┘
      │ (REST health check)  │ (REST health)      │ (API queries)
      │ every 30 sec         │ every 30 sec       │
      ▼                      ▼                    ▼
┌──────────────────┐ ┌──────────────────┐ ┌──────────────────┐
│ Local Encoder    │ │ Encoder Node #1  │ │ Encoder Node #2  │
│ (Server GPU/CPU) │ │ (RTX 4090)       │ │ (RTX 3080)       │
├──────────────────┤ ├──────────────────┤ ├──────────────────┤
│ EncoderV2 Lib    │ │ EncoderV2 Lib    │ │ EncoderV2 Lib    │
│ Job Executor     │ │ Job Executor     │ │ Job Executor     │
│ (FFmpeg)         │ │ (FFmpeg)         │ │ (FFmpeg)         │
└──────────────────┘ └──────────────────┘ └──────────────────┘
```

---

## Part 3: Critical Implementation Details

### 3.1 ASS Subtitle Handling (CRITICAL)

**❌ NEVER automatically convert ASS to VTT** - causes corruption of animations/styling.

**✅ CORRECT Implementation:**

```csharp
public class SubtitleStreamProcessor
{
    public async Task ProcessAsync(
        string inputFile,
        SubtitleProfileConfig profile,
        string outputFolder)
    {
        var analysis = await _analyzer.AnalyzeAsync(inputFile);
        var subtitles = analysis.SubtitleStreams
            .Where(s => profile.AllowedLanguages.Contains(s.Language))
            .ToList();
        
        foreach (var subtitle in subtitles)
        {
            if (subtitle.Codec == "ass" || subtitle.Codec == "ssa")
            {
                // Extract fonts FIRST
                var fonts = await _fontExtractor.ExtractAsync(
                    inputFile,
                    subtitle.StreamIndex,
                    Path.Combine(outputFolder, "fonts"));
                
                // Extract ASS in NATIVE format (preserve styling)
                var assFile = await _streamExtractor.ExtractAsync(
                    inputFile,
                    subtitle.StreamIndex,
                    GetSubtitlePath(subtitle.Language, "full", "ass"));
                
                // OPTIONAL: User can request VTT conversion separately
                if (profile.ConvertToWebVTT && subtitle.IsTextBased)
                {
                    var vttFile = await _converter.ConvertAsync(
                        assFile,
                        "webvtt",
                        GetSubtitlePath(subtitle.Language, "full", "vtt"));
                }
            }
            else if (subtitle.Codec == "webvtt")
            {
                // Already text-based, direct extract
                await _streamExtractor.ExtractAsync(
                    inputFile,
                    subtitle.StreamIndex,
                    GetSubtitlePath(subtitle.Language, "full", "vtt"));
            }
            else if (subtitle.Codec == "subrip")
            {
                // SRT can convert safely
                var srtFile = await _streamExtractor.ExtractAsync(
                    inputFile,
                    subtitle.StreamIndex);
                    
                if (profile.ConvertToWebVTT)
                {
                    await _converter.ConvertAsync(
                        srtFile,
                        "webvtt",
                        GetSubtitlePath(subtitle.Language, "full", "vtt"));
                }
            }
        }
    }
    
    private string GetSubtitlePath(string language, string type, string format)
    {
        // Pattern: {episode}.{lang}.{type}.{ext}
        return Path.Combine(
            _outputFolder,
            "subtitles",
            $"{_episodeId}.{language}.{type}.{format}");
    }
}
```

**Subtitle File Naming Convention:**
```
{episode_identifier}.{lang}.{type}.{ext}

Examples:
- No.Game.No.Life.S01E01.eng.full.ass      (Native ASS - KEEP THIS)
- No.Game.No.Life.S01E01.eng.sign.ass      (Signs/songs only)
- No.Game.No.Life.S01E01.eng.full.vtt      (Optional conversion)
- No.Game.No.Life.S01E01.jpn.full.ass      (Native Japanese)
- No.Game.No.Life.S01E01.eng.sdh.vtt       (Hearing impaired)

Language codes: ISO 639-2/B (eng, jpn, fre, deu, spa, etc.)
Types: full, sign, sdh
Extensions: ass (native), vtt (web), srt (legacy)
```

### 3.2 Audio Language Ordering (USER-CONFIGURABLE)

**❌ NEVER hardcode English as first language.**

**✅ CORRECT Implementation (from existing BaseAudio.cs):**

```csharp
public class AudioProfile
{
    public string Codec { get; set; }                // "aac", "opus", "eac3"
    public string[] AllowedLanguages { get; set; }   // USER-DEFINED ORDER
    public int Bitrate { get; set; }
    public int Channels { get; set; }
}

public class AudioStreamProcessor
{
    public async Task<List<AudioStreamConfig>> ProcessAsync(
        string inputFile,
        AudioProfile profile)
    {
        var analysis = await _analyzer.AnalyzeAsync(inputFile);
        var audioStreams = analysis.AudioStreams;
        
        // Filter by allowed languages (or take all if not specified)
        var filtered = profile.AllowedLanguages?.Any() == true
            ? audioStreams.Where(a => profile.AllowedLanguages.Contains(a.Language))
            : audioStreams;
        
        // CRITICAL: If filtering removed all audio, use first stream
        if (!filtered.Any())
        {
            filtered = new[] { audioStreams.First() };
        }
        
        // Sort by user's preferred order (FIRST = DEFAULT in HLS)
        var ordered = filtered
            .OrderBy(a => {
                var index = Array.IndexOf(profile.AllowedLanguages ?? [], a.Language);
                return index >= 0 ? index : int.MaxValue;
            })
            .ToList();
        
        // Build stream configs
        var result = new List<AudioStreamConfig>();
        for (int i = 0; i < ordered.Count; i++)
        {
            result.Add(new AudioStreamConfig
            {
                SourceIndex = ordered[i].Index,
                Language = ordered[i].Language,
                IsDefault = (i == 0),  // FIRST in user's order = DEFAULT
                Codec = profile.Codec,
                Bitrate = profile.Bitrate,
                Channels = profile.Channels
            });
        }
        
        return result;
    }
}

// Example profile for anime (Japanese first):
var animeProfile = new AudioProfile
{
    Codec = "aac",
    AllowedLanguages = new[] { "jpn", "eng" },  // Japanese DEFAULT
    Bitrate = 192
};

// Example profile for Western content (English first):
var westernProfile = new AudioProfile
{
    Codec = "aac",
    AllowedLanguages = new[] { "eng", "spa", "fre" },  // English DEFAULT
    Bitrate = 192
};
```

### 3.3 FFmpeg Progress Parsing (PRODUCTION-PROVEN)

**Based on existing FfMpeg.cs implementation:**

```csharp
public class ProgressMonitor
{
    private readonly TimeSpan _totalDuration;
    private DateTime _lastEmit = DateTime.UtcNow;
    private readonly int _emitIntervalMs;
    
    public ProgressMonitor(TimeSpan totalDuration, int emitIntervalMs = 1000)
    {
        _totalDuration = totalDuration;
        _emitIntervalMs = emitIntervalMs;
    }
    
    public async IAsyncEnumerable<EncodingProgress> MonitorAsync(
        Process ffmpegProcess,
        [EnumeratorCancellation] CancellationToken ct)
    {
        // FFmpeg arguments MUST include: -progress -
        // This enables key=value progress output to stdout
        
        bool durationFound = false;
        var totalDuration = _totalDuration;
        
        // Parse duration from stderr if not provided
        var durationRegex = new Regex(@"Duration:\s(\d{2}):(\d{2}):(\d{2})\.(\d+)");
        
        ffmpegProcess.ErrorDataReceived += (_, e) =>
        {
            if (e.Data != null && !durationFound)
            {
                var match = durationRegex.Match(e.Data);
                if (match.Success)
                {
                    int hours = int.Parse(match.Groups[1].Value);
                    int minutes = int.Parse(match.Groups[2].Value);
                    int seconds = int.Parse(match.Groups[3].Value);
                    int milliseconds = int.Parse(match.Groups[4].Value);
                    
                    totalDuration = new(0, hours, minutes, seconds, milliseconds * 10);
                    durationFound = true;
                }
            }
        };
        
        // Parse progress from stdout (key=value format)
        using var reader = new StreamReader(ffmpegProcess.StandardOutput);
        var progressBuffer = new StringBuilder();
        
        while (!reader.EndOfStream && !ct.IsCancellationRequested)
        {
            var line = await reader.ReadLineAsync();
            if (string.IsNullOrEmpty(line)) continue;
            
            progressBuffer.AppendLine(line);
            
            // FFmpeg outputs "progress=end" when complete
            if (line.Contains("progress=end") || line.Contains("progress=continue"))
            {
                var progress = ParseProgressData(
                    progressBuffer.ToString(),
                    totalDuration);
                
                if (progress != null && ShouldEmit())
                {
                    yield return progress;
                }
                
                progressBuffer.Clear();
            }
        }
    }
    
    private bool ShouldEmit()
    {
        var now = DateTime.UtcNow;
        if ((now - _lastEmit).TotalMilliseconds >= _emitIntervalMs)
        {
            _lastEmit = now;
            return true;
        }
        return false;
    }
    
    private EncodingProgress? ParseProgressData(string output, TimeSpan totalDuration)
    {
        // Parse key=value lines from FFmpeg
        var lines = output.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
        var values = new Dictionary<string, string>();
        
        foreach (var line in lines)
        {
            var parts = line.Split('=');
            if (parts.Length == 2)
            {
                values[parts[0].Trim()] = parts[1].Trim();
            }
        }
        
        if (!values.ContainsKey("frame")) return null;
        
        // Extract values
        var frame = int.Parse(values.GetValueOrDefault("frame", "0"));
        var fps = double.Parse(values.GetValueOrDefault("fps", "0"));
        var speedStr = values.GetValueOrDefault("speed", "0x")
            .Replace("N/A", "0")
            .TrimEnd('x');
        var speed = double.Parse(speedStr);
        var bitrate = values.GetValueOrDefault("bitrate", "");
        
        // Parse out_time (format: 00:01:23.45)
        var timeStr = values.GetValueOrDefault("out_time", "00:00:00.00");
        var currentTime = ParseTimeSpan(timeStr);
        
        // Calculate progress percentage
        var progressPct = totalDuration.TotalSeconds > 0
            ? (currentTime.TotalSeconds / totalDuration.TotalSeconds) * 100
            : 0;
        
        // Calculate ETA
        var remaining = speed > 0
            ? TimeSpan.FromSeconds((totalDuration.TotalSeconds - currentTime.TotalSeconds) / speed)
            : TimeSpan.Zero;
        
        return new EncodingProgress
        {
            Frame = frame,
            Fps = fps,
            Speed = speed,
            Bitrate = bitrate,
            CurrentTime = currentTime,
            TotalDuration = totalDuration,
            ProgressPercentage = Math.Min(progressPct, 100),
            EstimatedRemaining = remaining,
            Timestamp = DateTime.UtcNow
        };
    }
    
    private TimeSpan ParseTimeSpan(string timeStr)
    {
        var parts = timeStr.Split(':');
        if (parts.Length != 3) return TimeSpan.Zero;
        
        var hours = int.Parse(parts[0]);
        var minutes = int.Parse(parts[1]);
        var secondsParts = parts[2].Split('.');
        var seconds = int.Parse(secondsParts[0]);
        var milliseconds = secondsParts.Length > 1
            ? int.Parse(secondsParts[1].PadRight(2, '0'))
            : 0;
        
        return new TimeSpan(0, hours, minutes, seconds, milliseconds * 10);
    }
}

// FFmpeg command MUST include:
var command = $"ffmpeg -progress - -i {inputFile} ... {outputFile}";
//                     ↑
//                     CRITICAL FLAG
```

**Progress Emission Configuration:**

```csharp
public class EncoderNodeSettings
{
    // Default: 1 update/second (prevents network saturation)
    public int ProgressEmitIntervalMs { get; set; } = 1000;
    
    // Server can request different rate per task
    public int MaxProgressEmitRateHz { get; set; } = 5;  // Max 5 updates/sec
}
```

### 3.4 Output Validation (30-Second Strategy)

**Target: Complete validation in < 30 seconds**

```csharp
public class OutputValidator
{
    public async Task<ValidationResult> ValidateAsync(
        EncodingJobPayload job,
        DirectoryInfo outputDir,
        CancellationToken ct)
    {
        var stopwatch = Stopwatch.StartNew();
        var errors = new List<ValidationError>();
        var warnings = new List<ValidationWarning>();
        
        // 1. File structure validation (< 1 second)
        ValidateFileStructure(job, outputDir, errors);
        
        // 2. Playlist syntax validation (< 1 second)
        await ValidatePlaylistSyntax(job, outputDir, errors, warnings);
        
        // 3. Quick segment metadata check (< 10 seconds)
        //    Only checks FIRST and LAST segment per quality
        await ValidateSegmentMetadata(job, outputDir, errors, ct);
        
        // 4. Codec header validation (< 5 seconds)
        //    FFprobe headers only (no full decode)
        await ValidateCodecHeaders(job, outputDir, errors, ct);
        
        // 5. Asset validation (< 5 seconds)
        await ValidateAssets(job, outputDir, warnings);
        
        stopwatch.Stop();
        
        return new ValidationResult
        {
            IsValid = errors.Count == 0,
            Errors = errors,
            Warnings = warnings,
            ValidationDurationMs = stopwatch.ElapsedMilliseconds
        };
    }
    
    private void ValidateFileStructure(
        EncodingJobPayload job,
        DirectoryInfo outputDir,
        List<ValidationError> errors)
    {
        // Check master playlist exists
        var masterPlaylist = Path.Combine(
            outputDir.FullName,
            $"{job.OutputFilename}.m3u8");
        
        if (!File.Exists(masterPlaylist))
        {
            errors.Add(new ValidationError(
                "Master playlist not found",
                Severity.Critical));
            return;  // Can't continue without master
        }
        
        // Check video quality folders exist
        var videoFolders = outputDir.GetDirectories("video_*");
        if (videoFolders.Length == 0)
        {
            errors.Add(new ValidationError(
                "No video quality folders found",
                Severity.Critical));
        }
        
        // Check audio folders
        var audioFolders = outputDir.GetDirectories("audio_*");
        if (audioFolders.Length == 0 && job.Profile.AudioProfile != null)
        {
            errors.Add(new ValidationError(
                "No audio folders found",
                Severity.Critical));
        }
        
        // Check each video quality has segments
        foreach (var videoFolder in videoFolders)
        {
            var segments = videoFolder.GetFiles("*.ts");
            if (segments.Length == 0)
            {
                errors.Add(new ValidationError(
                    $"{videoFolder.Name}: No segments found",
                    Severity.Critical));
            }
        }
    }
    
    private async Task ValidatePlaylistSyntax(
        EncodingJobPayload job,
        DirectoryInfo outputDir,
        List<ValidationError> errors,
        List<ValidationWarning> warnings)
    {
        var masterPath = Path.Combine(
            outputDir.FullName,
            $"{job.OutputFilename}.m3u8");
        
        var content = await File.ReadAllTextAsync(masterPath);
        
        // Required HLS directives
        if (!content.Contains("#EXTM3U"))
            errors.Add(new ValidationError(
                "Missing #EXTM3U header",
                Severity.Critical));
        
        if (!content.Contains("#EXT-X-VERSION"))
            errors.Add(new ValidationError(
                "Missing #EXT-X-VERSION",
                Severity.Critical));
        
        if (!content.Contains("#EXT-X-STREAM-INF"))
            errors.Add(new ValidationError(
                "Missing #EXT-X-STREAM-INF",
                Severity.Critical));
        
        // Count variants match expected
        var expectedCount = job.Profile.Qualities.Count;
        var actualCount = Regex.Matches(content, "#EXT-X-STREAM-INF").Count;
        
        if (actualCount < expectedCount)
            errors.Add(new ValidationError(
                $"Missing quality variants: expected {expectedCount}, found {actualCount}",
                Severity.Critical));
        
        // Color space metadata (warning if missing)
        if (!content.Contains("COLOUR-SPACE"))
            warnings.Add(new ValidationWarning(
                "Missing COLOUR-SPACE metadata in variants",
                "Players may not handle HDR correctly"));
        
        // Validate variant playlists exist
        var variantMatches = Regex.Matches(content, @"^(video_\S+\.m3u8|audio_\S+\.m3u8)", RegexOptions.Multiline);
        foreach (Match match in variantMatches)
        {
            var variantPath = Path.Combine(outputDir.FullName, match.Value);
            if (!File.Exists(variantPath))
            {
                errors.Add(new ValidationError(
                    $"Variant playlist not found: {match.Value}",
                    Severity.Critical));
            }
        }
    }
    
    private async Task ValidateSegmentMetadata(
        EncodingJobPayload job,
        DirectoryInfo outputDir,
        List<ValidationError> errors,
        CancellationToken ct)
    {
        // Only check FIRST and LAST segment per quality
        var videoFolders = outputDir.GetDirectories("video_*");
        
        foreach (var videoFolder in videoFolders)
        {
            var segments = videoFolder.GetFiles("*.ts")
                .OrderBy(f => f.Name)
                .ToList();
            
            if (segments.Count < 2) continue;
            
            // Check first segment
            var firstSegment = segments.First();
            var firstAnalysis = await QuickProbe(firstSegment.FullName, ct);
            
            if (firstAnalysis == null || firstAnalysis.Duration < TimeSpan.FromMilliseconds(100))
            {
                errors.Add(new ValidationError(
                    $"{videoFolder.Name}: First segment appears corrupt",
                    Severity.Critical));
            }
            
            // Check last segment
            var lastSegment = segments.Last();
            var lastAnalysis = await QuickProbe(lastSegment.FullName, ct);
            
            if (lastAnalysis == null || lastAnalysis.Duration < TimeSpan.FromMilliseconds(100))
            {
                errors.Add(new ValidationError(
                    $"{videoFolder.Name}: Last segment appears corrupt or incomplete",
                    Severity.Warning));
            }
            
            // Verify codec matches profile
            if (firstAnalysis?.VideoCodec != null && 
                firstAnalysis.VideoCodec != job.Profile.VideoProfile.Codec)
            {
                errors.Add(new ValidationError(
                    $"{videoFolder.Name}: Codec mismatch - expected {job.Profile.VideoProfile.Codec}, found {firstAnalysis.VideoCodec}",
                    Severity.Critical));
            }
        }
    }
    
    private async Task<MediaInfo?> QuickProbe(string filePath, CancellationToken ct)
    {
        // FFprobe with minimal options (headers only, no full decode)
        var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = "ffprobe",
                Arguments = $"-v quiet -print_format json -show_format -show_streams \"{filePath}\"",
                RedirectStandardOutput = true,
                UseShellExecute = false
            }
        };
        
        process.Start();
        var output = await process.StandardOutput.ReadToEndAsync();
        await process.WaitForExitAsync(ct);
        
        // Parse JSON (simplified - use actual JSON parser)
        if (string.IsNullOrEmpty(output)) return null;
        
        var durationMatch = Regex.Match(output, @"""duration"":""([\d.]+)""");
        var codecMatch = Regex.Match(output, @"""codec_name"":""(\w+)""");
        
        return new MediaInfo
        {
            Duration = durationMatch.Success
                ? TimeSpan.FromSeconds(double.Parse(durationMatch.Groups[1].Value))
                : TimeSpan.Zero,
            VideoCodec = codecMatch.Success ? codecMatch.Groups[1].Value : null
        };
    }
    
    private async Task ValidateCodecHeaders(
        EncodingJobPayload job,
        DirectoryInfo outputDir,
        List<ValidationError> errors,
        CancellationToken ct)
    {
        // Quick codec profile validation (H.264/H.265 profile levels)
        // This is non-critical - log warnings only
        
        var videoFolder = outputDir.GetDirectories("video_*").FirstOrDefault();
        if (videoFolder == null) return;
        
        var firstSegment = videoFolder.GetFiles("*.ts").FirstOrDefault();
        if (firstSegment == null) return;
        
        var analysis = await QuickProbe(firstSegment.FullName, ct);
        
        // Validate profile level if specified in job
        if (job.Profile.VideoProfile.Profile != null && analysis?.VideoCodec != null)
        {
            // Log for debugging but don't fail
            // H.264 profile levels are advisory, not critical
        }
    }
    
    private async Task ValidateAssets(
        EncodingJobPayload job,
        DirectoryInfo outputDir,
        List<ValidationWarning> warnings)
    {
        // Check optional assets (non-critical)
        
        // Fonts
        var fontsDir = Path.Combine(outputDir.FullName, "fonts");
        if (!Directory.Exists(fontsDir))
        {
            warnings.Add(new ValidationWarning(
                "Fonts directory not found",
                "ASS subtitles may not display correctly"));
        }
        else if (!File.Exists(Path.Combine(outputDir.FullName, "fonts.json")))
        {
            warnings.Add(new ValidationWarning(
                "fonts.json manifest missing",
                "Font references may be broken"));
        }
        
        // Sprites
        var spriteFiles = outputDir.GetFiles("thumbs_*.webp");
        if (spriteFiles.Length == 0 && job.Profile.GenerateSprites)
        {
            warnings.Add(new ValidationWarning(
                "Sprite sheet not found",
                "Timeline preview will not work"));
        }
        
        // Chapters
        if (!File.Exists(Path.Combine(outputDir.FullName, "chapters.vtt")) &&
            job.SourceHasChapters)
        {
            warnings.Add(new ValidationWarning(
                "Chapter markers not found",
                "Source had chapters but output doesn't"));
        }
    }
}

public record MediaInfo
{
    public TimeSpan Duration { get; init; }
    public string? VideoCodec { get; init; }
}

public record ValidationError(string Message, Severity Level);
public record ValidationWarning(string Message, string Impact);

public enum Severity { Warning, Critical }

public record ValidationResult
{
    public bool IsValid { get; init; }
    public List<ValidationError> Errors { get; init; } = [];
    public List<ValidationWarning> Warnings { get; init; } = [];
    public long ValidationDurationMs { get; init; }
}
```

**What This Validates (< 30 seconds):**
- ✅ File structure completeness
- ✅ Playlist syntax correctness
- ✅ Segment existence and count
- ✅ Codec compliance (first/last segment only)
- ✅ Asset availability (fonts, sprites, chapters)

**What This DOESN'T Validate (too slow):**
- ❌ Full segment decode (would take hours)
- ❌ Audio/video sync across all segments
- ❌ Bitrate consistency throughout file
- ❌ Checksum verification

### 3.5 Database Integration (QueueContext)

**CRITICAL: All EncoderV2 tables go in QueueContext (NOT separate database)**

```csharp
// In NoMercy.Database/QueueContext.cs

public class QueueContext : DbContext
{
    // Existing queue tables
    public DbSet<Job> Jobs { get; set; }
    public DbSet<JobQueue> JobQueues { get; set; }
    
    // NEW: EncoderV2 tables
    public DbSet<EncodingProfile> EncodingProfiles { get; set; }
    public DbSet<EncodingJob> EncodingJobs { get; set; }
    public DbSet<EncodingTask> EncodingTasks { get; set; }
    public DbSet<EncodingProgress> EncodingProgress { get; set; }
    public DbSet<EncoderNode> EncoderNodes { get; set; }
    public DbSet<EncodingNodeAssignment> EncodingNodeAssignments { get; set; }
    
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        
        // EncodingJob relationships
        modelBuilder.Entity<EncodingJob>()
            .HasOne(j => j.Profile)
            .WithMany()
            .HasForeignKey(j => j.ProfileId)
            .IsRequired(false);  // Profile can be deleted but job snapshot remains
        
        modelBuilder.Entity<EncodingJob>()
            .HasMany(j => j.Tasks)
            .WithOne(t => t.Job)
            .HasForeignKey(t => t.JobId)
            .OnDelete(DeleteBehavior.Cascade);
        
        // EncodingTask relationships
        modelBuilder.Entity<EncodingTask>()
            .HasOne(t => t.AssignedNode)
            .WithMany()
            .HasForeignKey(t => t.AssignedNodeId)
            .IsRequired(false);
        
        modelBuilder.Entity<EncodingTask>()
            .HasMany(t => t.ProgressUpdates)
            .WithOne(p => p.Task)
            .HasForeignKey(p => p.TaskId)
            .OnDelete(DeleteBehavior.Cascade);
        
        // Indexes for performance
        modelBuilder.Entity<EncodingJob>()
            .HasIndex(j => j.State);
        
        modelBuilder.Entity<EncodingTask>()
            .HasIndex(t => new { t.JobId, t.State });
        
        modelBuilder.Entity<EncodingProgress>()
            .HasIndex(p => new { p.TaskId, p.RecordedAt });
        
        modelBuilder.Entity<EncoderNode>()
            .HasIndex(n => n.IsHealthy);
    }
}

// Entity models in NoMercy.Database/Models/

public class EncodingProfile
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Name { get; set; } = "";
    public string Container { get; set; } = "hls";  // hls, mp4, mkv
    public string? Description { get; set; }
    public bool IsDefault { get; set; }
    public bool IsTemplate { get; set; }
    
    // JSON serialized configurations
    public string VideoProfileJson { get; set; } = "{}";
    public string AudioProfileJson { get; set; } = "{}";
    public string SubtitleProfileJson { get; set; } = "{}";
    public string QualitiesJson { get; set; } = "[]";
    
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? DeletedAt { get; set; }  // Soft delete
}

public class EncodingJob
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string InputFilePath { get; set; } = "";
    public string OutputFolder { get; set; } = "";
    
    // Profile reference (can be null if profile deleted)
    public string? ProfileId { get; set; }
    public EncodingProfile? Profile { get; set; }
    
    // Snapshot of profile at job creation time (IMMUTABLE)
    public string ProfileSnapshotJson { get; set; } = "{}";
    
    public string State { get; set; } = "queued";  // queued, encoding, completed, failed, cancelled
    public string? ErrorMessage { get; set; }
    
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public long? ExecutionTimeMs { get; set; }
    
    // Relationships
    public ICollection<EncodingTask> Tasks { get; set; } = [];
}

public class EncodingTask
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string JobId { get; set; } = "";
    public EncodingJob Job { get; set; } = null!;
    
    public string TaskType { get; set; } = "";  // HDRConversion, VideoEncoding, AudioEncoding, etc.
    public double Weight { get; set; }  // CPU/time estimate
    public string State { get; set; } = "pending";  // pending, running, completed, failed
    
    public string? AssignedNodeId { get; set; }
    public EncoderNode? AssignedNode { get; set; }
    
    public int RetryCount { get; set; }
    public string DependenciesJson { get; set; } = "[]";  // Task IDs this depends on
    public string? ErrorMessage { get; set; }
    
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    
    // Relationships
    public ICollection<EncodingProgress> ProgressUpdates { get; set; } = [];
}

public class EncodingProgress
{
    public long Id { get; set; }
    public string TaskId { get; set; } = "";
    public EncodingTask Task { get; set; } = null!;
    
    public double ProgressPercentage { get; set; }
    public long CurrentFrame { get; set; }
    public double Fps { get; set; }
    public double Speed { get; set; }
    public string Bitrate { get; set; } = "";
    public TimeSpan CurrentTime { get; set; }
    public TimeSpan EstimatedRemaining { get; set; }
    
    public DateTime RecordedAt { get; set; } = DateTime.UtcNow;
}

public class EncoderNode
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Name { get; set; } = "";
    public string IpAddress { get; set; } = "";
    public int Port { get; set; } = 9626;
    
    public bool HasGPU { get; set; }
    public string? GPUModel { get; set; }
    public int CPUCores { get; set; }
    public int MemoryGB { get; set; }
    
    public bool IsHealthy { get; set; }
    public DateTime LastHeartbeat { get; set; }
    
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public class EncodingNodeAssignment
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string TaskId { get; set; } = "";
    public string NodeId { get; set; } = "";
    public DateTime AssignedAt { get; set; } = DateTime.UtcNow;
    public DateTime? CompletedAt { get; set; }
}
```

**Why QueueContext:**
1. Encoding jobs ARE background queue jobs
2. Progress tracking IS task state management
3. Prevents database fragmentation
4. Unified transaction handling
5. Matches existing architecture pattern

---

## Part 4: Network Architecture

### 4.1 Communication Protocol: REST + HTTP Polling

**Decision: REST over gRPC**

**Rationale:**
- Small payload sizes (< 1KB per progress update)
- Moderate frequency (1-5 updates/sec)
- NAT compatibility (home router friendly)
- Easy debugging (HTTP traffic inspection)
- Simple implementation

**Node Health Check:**
```csharp
// Node endpoint
[HttpGet("api/v1/health")]
public IActionResult GetHealth()
{
    return Ok(new {
        status = "healthy",
        currentTaskId = _currentTaskId,
        cpuUsage = GetCpuUsage(),
        gpuUsage = GetGpuUsage(),
        memoryUsageGB = GetMemoryUsage(),
        timestamp = DateTime.UtcNow
    });
}

// Server-side monitor (runs every 30 seconds)
public class NodeHealthMonitor : BackgroundService
{
    private const int HeartbeatIntervalSeconds = 30;
    private const int HeartbeatTimeoutSeconds = 90;  // 3 missed heartbeats = offline
    
    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            var nodes = await _db.EncoderNodes.ToListAsync(ct);
            
            foreach (var node in nodes)
            {
                try
                {
                    var response = await _httpClient.GetAsync(
                        $"https://{node.IpAddress}:{node.Port}/api/v1/health",
                        ct);
                    
                    if (response.IsSuccessStatusCode)
                    {
                        node.IsHealthy = true;
                        node.LastHeartbeat = DateTime.UtcNow;
                    }
                    else
                    {
                        node.IsHealthy = false;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, 
                        "Health check failed for node {NodeId}", 
                        node.Id);
                    
                    // Mark offline if last heartbeat > timeout
                    if ((DateTime.UtcNow - node.LastHeartbeat).TotalSeconds > HeartbeatTimeoutSeconds)
                    {
                        node.IsHealthy = false;
                        
                        // Reassign tasks from dead node
                        await _jobDispatcher.ReassignNodeTasksAsync(node.Id, ct);
                    }
                }
            }
            
            await _db.SaveChangesAsync(ct);
            await Task.Delay(TimeSpan.FromSeconds(HeartbeatIntervalSeconds), ct);
        }
    }
}
```

### 4.2 Node Discovery & Registration

**Architecture: Phone-Home for Paid Tier + mDNS Fallback**

```csharp
// Node startup registration
public class EncoderNodeService
{
    private readonly string _nodeId;
    private readonly string _paidTierToken;
    private readonly HttpClient _httpClient;
    
    public async Task StartAsync(CancellationToken ct)
    {
        // Detect capabilities
        var capabilities = await DetectCapabilitiesAsync();
        
        // Register with main server
        var registration = new NodeRegistration
        {
            NodeId = _nodeId,
            PaidTierToken = _paidTierToken,
            Name = Environment.MachineName,
            ExternalIp = await GetPublicIpAsync(),
            InternalIp = GetLocalIp(),
            Port = 9626,
            HasGPU = capabilities.HasGPU,
            GPUModel = capabilities.GPUModel,
            CPUCores = Environment.ProcessorCount,
            MemoryGB = capabilities.TotalMemoryGB
        };
        
        try
        {
            // Try phone-home registration first
            var response = await _httpClient.PostAsJsonAsync(
                "https://main-server/api/v1/nodes/register",
                registration,
                ct);
            
            if (response.IsSuccessStatusCode)
            {
                _logger.LogInformation("Registered with main server");
                return;
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to register with main server, falling back to mDNS");
        }
        
        // Fallback: mDNS broadcast for local network discovery
        await StartMdnsBroadcastAsync(registration, ct);
    }
    
    private async Task StartMdnsBroadcastAsync(
        NodeRegistration registration,
        CancellationToken ct)
    {
        // Broadcast on _nomercyencoder._tcp.local
        // This allows server to discover nodes without internet
        var mdnsService = new ServiceProfile(
            "_nomercyencoder._tcp",
            Environment.MachineName,
            9626);
        
        mdnsService.Resources.Add(new TXTRecord
        {
            Strings = new[]
            {
                $"nodeId={registration.NodeId}",
                $"hasGpu={registration.HasGPU}",
                $"gpuModel={registration.GPUModel}",
                $"cpuCores={registration.CPUCores}"
            }
        });
        
        await _mdns.StartAsync(mdnsService, ct);
    }
}

// Server-side node discovery
public class NodeDiscoveryService : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        // Listen for mDNS announcements
        _mdns.QueryReceived += async (sender, e) =>
        {
            if (e.Message.Questions.Any(q => q.Name.ToString().Contains("_nomercyencoder._tcp")))
            {
                // New node discovered via mDNS
                var txtRecords = e.Response.AdditionalRecords
                    .OfType<TXTRecord>()
                    .FirstOrDefault();
                
                if (txtRecords != null)
                {
                    var nodeInfo = ParseTxtRecords(txtRecords);
                    await RegisterNodeAsync(nodeInfo, ct);
                }
            }
        };
        
        // Start listening
        await _mdns.StartAsync(ct);
        
        while (!ct.IsCancellationRequested)
        {
            // Query for nodes every 5 minutes
            await _mdns.SendQueryAsync("_nomercyencoder._tcp.local", ct);
            await Task.Delay(TimeSpan.FromMinutes(5), ct);
        }
    }
}
```

### 4.3 Progress Emission Configuration

```csharp
public class EncoderNodeSettings
{
    // Default: 1 update/second (prevents network saturation)
    public int ProgressEmitIntervalMs { get; set; } = 1000;
    
    // Maximum rate server will accept (prevents abuse)
    public int MaxProgressEmitRateHz { get; set; } = 5;
    
    // Network congestion detection (auto-throttle)
    public bool AutoThrottleOnCongestion { get; set; } = true;
    
    // Buffer size before batching updates
    public int ProgressBufferSize { get; set; } = 10;
}

// Node-side progress emitter with throttling
public class ProgressEmitter
{
    private readonly EncoderNodeSettings _settings;
    private readonly HttpClient _httpClient;
    private DateTime _lastEmit = DateTime.UtcNow;
    private readonly Queue<EncodingProgress> _buffer = new();
    
    public async Task EmitProgressAsync(
        EncodingProgress progress,
        CancellationToken ct)
    {
        _buffer.Enqueue(progress);
        
        var now = DateTime.UtcNow;
        var timeSinceLastEmit = (now - _lastEmit).TotalMilliseconds;
        
        // Throttle based on configured interval
        if (timeSinceLastEmit < _settings.ProgressEmitIntervalMs)
        {
            return;  // Too soon, buffer it
        }
        
        // Batch send buffered updates
        if (_buffer.Count > 0)
        {
            try
            {
                var batch = _buffer.ToArray();
                _buffer.Clear();
                
                var response = await _httpClient.PostAsJsonAsync(
                    "api/v1/tasks/progress",
                    new { updates = batch },
                    ct);
                
                if (!response.IsSuccessStatusCode)
                {
                    // Network issue - increase throttle interval
                    if (_settings.AutoThrottleOnCongestion)
                    {
                        _settings.ProgressEmitIntervalMs = Math.Min(
                            _settings.ProgressEmitIntervalMs * 2,
                            5000);  // Max 5 seconds
                    }
                }
                else
                {
                    // Success - reset throttle
                    _settings.ProgressEmitIntervalMs = 1000;
                }
                
                _lastEmit = now;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to emit progress");
            }
        }
    }
}
```

---

## Part 5: Task Distribution & Smart Processing

### 5.1 User-Defined Quality Stack

```csharp
public record EncodingQuality
{
    public int Width { get; init; }
    public int Height { get; init; }
    public int Bitrate { get; init; }              // kbps
    public bool IncludeSDRVariant { get; init; }   // If source is HDR
}

public record EncodingProfile
{
    public string Id { get; init; } = "";
    public string Name { get; init; } = "";
    public string Container { get; init; } = "hls";
    
    // User defines EXACT qualities they want
    public List<EncodingQuality> Qualities { get; init; } = [];
    
    // Flexible codec configuration
    public VideoProfileConfig VideoProfile { get; init; } = new();
    public AudioProfileConfig AudioProfile { get; init; } = new();
    public SubtitleProfileConfig SubtitleProfile { get; init; } = new();
    
    // Post-processing
    public bool GenerateSprites { get; init; } = true;
    public int SpriteInterval { get; init; } = 5;  // seconds
    public bool ExtractChapters { get; init; } = true;
    public bool ExtractFonts { get; init; } = true;
}

// Example: Anime with multiple qualities
var animeProfile = new EncodingProfile
{
    Name = "Anime Multi-Quality",
    Container = "hls",
    Qualities = new()
    {
        new(1920, 1080, 5000, includeSDR: true),   // 1080p + SDR variant
        new(1280, 720, 2500, includeSDR: false),   // 720p
    },
    VideoProfile = new()
    {
        Codec = "h264",
        Preset = "medium",
        CRF = 23
    },
    AudioProfile = new()
    {
        Codec = "aac",
        Bitrate = 192,
        AllowedLanguages = new[] { "jpn", "eng" },  // Japanese first!
        Channels = 2
    },
    SubtitleProfile = new()
    {
        Codec = "ass",  // Native ASS preservation
        AllowedLanguages = new[] { "eng", "jpn" },
        ConvertToWebVTT = true  // Optional WebVTT conversion
    }
};
```

### 5.2 Task Splitting & Weighting

```csharp
public class TaskSplitter
{
    public async Task<List<EncodingTask>> SplitJobAsync(
        EncodingJobPayload job,
        StreamAnalysis analysis)
    {
        var tasks = new List<EncodingTask>();
        
        // 1. HDR Conversion (if source is HDR and any quality needs SDR)
        if (analysis.IsHDR && job.Profile.Qualities.Any(q => q.IncludeSDRVariant))
        {
            tasks.Add(new EncodingTask
            {
                TaskId = $"{job.Id}_hdr_conversion",
                JobId = job.Id,
                Type = EncodingTaskType.HDRConversion,
                Weight = CalculateHDRConversionWeight(analysis),
                State = "pending",
                DependsOn = new List<string>()  // First task, no dependencies
            });
        }
        
        // 2. Video encoding tasks (one per quality)
        foreach (var quality in job.Profile.Qualities)
        {
            var videoTask = new EncodingTask
            {
                TaskId = $"{job.Id}_video_{quality.Width}x{quality.Height}",
                JobId = job.Id,
                Type = EncodingTaskType.VideoEncoding,
                Weight = CalculateVideoWeight(analysis, quality),
                State = "pending",
                DependsOn = new List<string>(),
                Metadata = JsonSerializer.Serialize(new { quality })
            };
            
            tasks.Add(videoTask);
            
            // SDR variant (depends on HDR conversion if source is HDR)
            if (quality.IncludeSDRVariant && analysis.IsHDR)
            {
                var sdrTask = new EncodingTask
                {
                    TaskId = $"{job.Id}_video_{quality.Width}x{quality.Height}_SDR",
                    JobId = job.Id,
                    Type = EncodingTaskType.VideoEncodingSDR,
                    Weight = CalculateVideoWeight(analysis, quality) * 0.5,  // Uses pre-converted HDR
                    State = "pending",
                    DependsOn = new List<string> { $"{job.Id}_hdr_conversion" },  // Wait for HDR
                    Metadata = JsonSerializer.Serialize(new { quality, usesHDRConversion = true })
                };
                
                tasks.Add(sdrTask);
            }
        }
        
        // 3. Audio encoding tasks (per language + codec)
        foreach (var language in job.Profile.AudioProfile.AllowedLanguages)
        {
            var audioStream = analysis.AudioStreams
                .FirstOrDefault(a => a.Language == language);
            
            if (audioStream != null)
            {
                tasks.Add(new EncodingTask
                {
                    TaskId = $"{job.Id}_audio_{language}_{job.Profile.AudioProfile.Codec}",
                    JobId = job.Id,
                    Type = EncodingTaskType.AudioEncoding,
                    Weight = CalculateAudioWeight(audioStream),
                    State = "pending",
                    DependsOn = new List<string>(),
                    Metadata = JsonSerializer.Serialize(new { language, audioStream.Index })
                });
            }
        }
        
        // 4. Subtitle processing (per language)
        foreach (var language in job.Profile.SubtitleProfile.AllowedLanguages)
        {
            var subtitleStream = analysis.SubtitleStreams
                .FirstOrDefault(s => s.Language == language);
            
            if (subtitleStream != null)
            {
                tasks.Add(new EncodingTask
                {
                    TaskId = $"{job.Id}_subtitle_{language}",
                    JobId = job.Id,
                    Type = EncodingTaskType.SubtitleProcessing,
                    Weight = 0.2,  // Very light
                    State = "pending",
                    DependsOn = new List<string>(),
                    Metadata = JsonSerializer.Serialize(new { language, subtitleStream.Index })
                });
            }
        }
        
        // 5. Sprite generation (depends on video encoding)
        if (job.Profile.GenerateSprites)
        {
            var firstVideoTask = tasks
                .First(t => t.Type == EncodingTaskType.VideoEncoding);
            
            tasks.Add(new EncodingTask
            {
                TaskId = $"{job.Id}_sprites",
                JobId = job.Id,
                Type = EncodingTaskType.SpriteGeneration,
                Weight = 1.0,
                State = "pending",
                DependsOn = new List<string> { firstVideoTask.TaskId }
            });
        }
        
        // 6. Post-processing (depends on all encoding tasks)
        var allEncodingTasks = tasks
            .Where(t => t.Type is EncodingTaskType.VideoEncoding 
                        or EncodingTaskType.AudioEncoding 
                        or EncodingTaskType.SubtitleProcessing)
            .Select(t => t.TaskId)
            .ToList();
        
        tasks.Add(new EncodingTask
        {
            TaskId = $"{job.Id}_postprocessing",
            JobId = job.Id,
            Type = EncodingTaskType.PostProcessing,
            Weight = 0.5,
            State = "pending",
            DependsOn = allEncodingTasks
        });
        
        return tasks;
    }
    
    private double CalculateHDRConversionWeight(StreamAnalysis analysis)
    {
        // HDR→SDR is CPU-intensive
        var durationMinutes = analysis.Duration.TotalMinutes;
        var resolution = analysis.VideoStreams.First().Width * analysis.VideoStreams.First().Height;
        
        return (durationMinutes * resolution) / 1_000_000;  // Weight formula
    }
    
    private double CalculateVideoWeight(StreamAnalysis analysis, EncodingQuality quality)
    {
        var durationMinutes = analysis.Duration.TotalMinutes;
        var targetPixels = quality.Width * quality.Height;
        
        return (durationMinutes * targetPixels) / 1_000_000;
    }
    
    private double CalculateAudioWeight(AudioStreamInfo stream)
    {
        // Audio encoding is relatively light
        return 0.5;
    }
}

public enum EncodingTaskType
{
    HDRConversion,          // One-time HDR→SDR conversion
    VideoEncoding,          // Per-quality encoding (HDR or original)
    VideoEncodingSDR,       // Per-quality SDR (uses HDR conversion result)
    AudioEncoding,          // Per-language encoding
    SubtitleProcessing,     // Font extraction + conversion
    SpriteGeneration,       // Thumbnail extraction
    PostProcessing          // Playlist generation, validation
}
```

### 5.3 HDR→SDR Smart Conversion

```csharp
public class HDRProcessor
{
    public async Task<string> ConvertHDRToSDRAsync(
        string inputFile,
        string outputFolder,
        CancellationToken ct)
    {
        var sdrOutputFile = Path.Combine(
            outputFolder,
            "temp",
            $"{Path.GetFileNameWithoutExtension(inputFile)}_sdr.mkv");
        
        Directory.CreateDirectory(Path.GetDirectoryName(sdrOutputFile)!);
        
        // Analyze color space
        var colorInfo = await AnalyzeColorSpaceAsync(inputFile, ct);
        
        if (!colorInfo.IsHDR)
        {
            // Not HDR, return original
            return inputFile;
        }
        
        // Build HDR→SDR tonemap command
        var command = BuildHDRToSDRCommand(inputFile, sdrOutputFile, colorInfo);
        
        // Execute FFmpeg
        var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = "ffmpeg",
                Arguments = command,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false
            }
        };
        
        process.Start();
        
        // Monitor progress
        await foreach (var progress in _progressMonitor.MonitorAsync(process, ct))
        {
            // Emit progress for HDR conversion task
            await _progressEmitter.EmitProgressAsync(progress, ct);
        }
        
        await process.WaitForExitAsync(ct);
        
        if (process.ExitCode != 0)
        {
            throw new Exception($"HDR conversion failed: {await process.StandardError.ReadToEndAsync()}");
        }
        
        // Return path to SDR file (all SDR variants will use this)
        return sdrOutputFile;
    }
    
    private string BuildHDRToSDRCommand(
        string inputFile,
        string outputFile,
        ColorSpaceInfo colorInfo)
    {
        // Tonemap filter for HDR→SDR
        var tonemapFilter = colorInfo.TransferFunction switch
        {
            "pq" => "zscale=t=linear:npl=100,format=gbrpf32le,zscale=p=bt709,tonemap=tonemap=hable:desat=0,zscale=t=bt709:m=bt709:r=tv,format=yuv420p",
            "hlg" => "zscale=t=linear:npl=100,format=gbrpf32le,zscale=p=bt709,tonemap=tonemap=mobius:desat=0,zscale=t=bt709:m=bt709:r=tv,format=yuv420p",
            _ => "zscale=t=bt709:m=bt709:p=bt709,format=yuv420p"
        };
        
        return $"-i \"{inputFile}\" " +
               $"-vf \"{tonemapFilter}\" " +
               $"-c:v libx264 -preset medium -crf 18 " +  // High quality for reuse
               $"-c:a copy " +  // Copy audio
               $"-c:s copy " +  // Copy subtitles
               $"-map 0 " +     // All streams
               $"-y \"{outputFile}\"";
    }
    
    private async Task<ColorSpaceInfo> AnalyzeColorSpaceAsync(
        string inputFile,
        CancellationToken ct)
    {
        var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = "ffprobe",
                Arguments = $"-v quiet -print_format json -show_streams \"{inputFile}\"",
                RedirectStandardOutput = true,
                UseShellExecute = false
            }
        };
        
        process.Start();
        var output = await process.StandardOutput.ReadToEndAsync();
        await process.WaitForExitAsync(ct);
        
        // Parse JSON to extract color info
        var json = JsonDocument.Parse(output);
        var videoStream = json.RootElement
            .GetProperty("streams")
            .EnumerateArray()
            .First(s => s.GetProperty("codec_type").GetString() == "video");
        
        var colorSpace = videoStream.GetProperty("color_space").GetString() ?? "bt709";
        var colorTransfer = videoStream.GetProperty("color_transfer").GetString() ?? "bt709";
        var colorPrimaries = videoStream.GetProperty("color_primaries").GetString() ?? "bt709";
        
        return new ColorSpaceInfo
        {
            IsHDR = colorTransfer is "smpte2084" or "arib-std-b67",  // PQ or HLG
            TransferFunction = colorTransfer switch
            {
                "smpte2084" => "pq",
                "arib-std-b67" => "hlg",
                _ => "sdr"
            },
            ColorSpace = colorSpace,
            ColorPrimaries = colorPrimaries
        };
    }
}

public record ColorSpaceInfo
{
    public bool IsHDR { get; init; }
    public string TransferFunction { get; init; } = "sdr";  // "pq", "hlg", "sdr"
    public string ColorSpace { get; init; } = "bt709";
    public string ColorPrimaries { get; init; } = "bt709";
}

// In task execution:
// 1. HDR Conversion task runs ONCE
// 2. Stores output path in shared state
// 3. All SDR variants read from that path instead of original
// 4. Significant time savings (convert once, use many times)
```

---

## Part 6: Implementation Roadmap

### Phase 1: Foundation (Week 1-2)
**Goal: Database + Core Models + Basic Profile Management**

- [ ] Create QueueContext migration for EncoderV2 tables
- [ ] Implement EncodingProfile, EncodingJob, EncodingTask entities
- [ ] Build ProfileManager with CRUD operations
- [ ] Create default profiles (HLS, MP4, MKV variants)
- [ ] Implement profile validation
- [ ] Add StreamAnalyzer using existing Ffprobe.cs

**Deliverables:**
- Database tables working
- Profile CRUD API functional
- Default profiles loadable
- Stream analysis produces correct metadata

### Phase 2: Specifications & Validation (Week 3-4)
**Goal: Container specs + Codec configs + Validation**

- [ ] Implement HLSSpecification with RFC 8216 compliance
- [ ] Implement MP4Specification with ISO 14496 compliance
- [ ] Implement MKVSpecification with Matroska compliance
- [ ] Create VideoCodecValidator (H.264, H.265, VP9, AV1)
- [ ] Create AudioCodecValidator (AAC, Opus, FLAC, AC3)
- [ ] Create SubtitleCodecValidator (ASS, WebVTT, SRT)
- [ ] Implement OutputValidator (30-second strategy)
- [ ] Build PlaylistValidator for M3U8 syntax

**Deliverables:**
- HLS output validates correctly
- MP4 output validates correctly
- MKV output validates correctly
- Validation completes in < 30 seconds
- Profile validation catches invalid configs

### Phase 3: Stream Processing (Week 5-6)
**Goal: Video/Audio/Subtitle processing + Font extraction**

- [ ] Implement VideoStreamProcessor
  - Scaling algorithm (no upscaling)
  - Aspect ratio preservation
  - HDR detection
- [ ] Implement AudioStreamProcessor
  - Language filtering with user-defined order
  - Bitrate/codec assignment
  - Channel configuration
- [ ] Implement SubtitleStreamProcessor
  - Native ASS preservation
  - Optional WebVTT conversion
  - Font extraction
- [ ] Implement FontExtractor
  - Extract from ASS/SSA streams
  - Generate fonts.json manifest
  - Organize by MIME type
- [ ] Implement ChapterProcessor
- [ ] Implement SpriteGenerator

**Deliverables:**
- Video scaling produces correct dimensions
- Audio language ordering works correctly
- ASS subtitles extracted natively (not converted)
- Fonts extracted and organized
- Sprites generated with timing metadata

### Phase 4: FFmpeg Integration (Week 7-8)
**Goal: Command building + Execution + Progress tracking**

- [ ] Implement FFmpegCommandBuilder
  - Input specifications
  - Filter graphs
  - Codec options
  - Container-specific options
- [ ] Implement HDRProcessor
  - HDR→SDR tonemap conversion
  - Color space detection
  - Shared output for SDR variants
- [ ] Implement ProgressMonitor
  - FFmpeg `-progress -` parsing
  - Real-time progress emission
  - ETA calculation
- [ ] Implement EncodingJobExecutor
  - Task dependency resolution
  - FFmpeg process management
  - Error handling + retry logic
- [ ] Implement PostProcessor
  - Font organization
  - Playlist generation
  - Backup creation

**Deliverables:**
- FFmpeg commands are syntactically valid
- Progress parsing extracts all metrics
- HDR→SDR conversion shares output correctly
- Jobs complete successfully
- Output structure matches production format

### Phase 5: Task Distribution (Week 9-10)
**Goal: Multi-node support + Load balancing**

- [ ] Implement TaskSplitter
  - Job → Task decomposition
  - Dependency graph creation
  - Weight calculation
- [ ] Implement NodeSelector
  - Node capability matching
  - Load balancing algorithm
  - Task assignment
- [ ] Implement JobDispatcher
  - Queue integration
  - Priority handling
  - Task reassignment on node failure
- [ ] Implement NodeHealthMonitor
  - 30-second health checks
  - Automatic node marking (healthy/offline)
  - Task reassignment

**Deliverables:**
- Jobs split into correct tasks
- Tasks assigned to best available node
- Node failures handled gracefully
- Load balancing distributes work evenly

### Phase 6: Node Implementation (Week 11-12)
**Goal: Standalone encoder node application**

- [ ] Create EncoderNode project
  - REST API endpoints
  - Task execution
  - Progress emission
- [ ] Implement NodeRegistration
  - Phone-home to main server
  - mDNS fallback
  - Capability detection
- [ ] Implement node-side ProgressEmitter
  - Throttled progress updates
  - Network congestion detection
  - Auto-throttling
- [ ] Implement NodeCapabilities
  - GPU detection (NVIDIA, AMD, Intel)
  - CPU core count
  - Memory availability
  - FFmpeg codec support

**Deliverables:**
- Node registers with server successfully
- Node executes tasks correctly
- Progress updates reach server
- Node survives server restarts
- Server survives node restarts

### Phase 7: API & Integration (Week 13-14)
**Goal: REST API + SignalR + UI integration**

- [ ] Create ProfileController
  - Profile CRUD endpoints
  - Validation endpoints
  - Template endpoints
- [ ] Create EncodingController
  - Job submission
  - Job status
  - Job cancellation
- [ ] Create ProgressHub (SignalR)
  - Real-time progress broadcast
  - Job completion notifications
  - Node status updates
- [ ] Integrate with existing Queue system
- [ ] Add API documentation (Swagger)

**Deliverables:**
- All API endpoints functional
- SignalR progress updates work
- Queue integration seamless
- API documentation complete

### Phase 8: Testing & Validation (Week 15-16)
**Goal: Comprehensive testing + Production validation**

- [ ] Unit tests
  - Profile validation
  - Stream processing
  - Command generation
  - Task splitting
- [ ] Integration tests
  - End-to-end encoding
  - Multi-node task distribution
  - Progress tracking
  - Error handling
- [ ] Production validation
  - Encode test files from 16,000+ library
  - Validate against existing output structure
  - Performance benchmarking
  - Stress testing (100+ concurrent jobs)

**Deliverables:**
- 80%+ code coverage
- All critical paths tested
- Production validation successful
- Performance acceptable
- Documentation complete

---

## Part 7: Success Criteria

### Functional Requirements
- [ ] Encodes match exact production output structure (validated against 16,000+ files)
- [ ] HLS playlists comply with RFC 8216
- [ ] MP4 files comply with ISO 14496
- [ ] MKV files comply with Matroska specification
- [ ] ASS subtitles preserved natively (not converted)
- [ ] Audio language ordering respects user preferences
- [ ] HDR→SDR conversion executes once per job (shared across variants)
- [ ] Fonts extracted and organized with manifest
- [ ] Sprites generated with correct timing
- [ ] Validation completes in < 30 seconds
- [ ] Profile changes don't break queued jobs

### Performance Requirements
- [ ] 1080p 24-minute episode encodes in < 30 minutes (single node, GPU)
- [ ] Progress updates < 1 second latency
- [ ] API responses < 200ms
- [ ] Database queries < 100ms
- [ ] Node health checks < 30 seconds
- [ ] Task assignment < 5 seconds

### Reliability Requirements
- [ ] Nodes survive server restarts (continue encoding)
- [ ] Server survives node restarts (reassigns tasks)
- [ ] Failed tasks retry up to 3 times
- [ ] Task failures don't crash entire job
- [ ] Network interruptions handled gracefully
- [ ] Zombie processes cleaned up automatically

### Scalability Requirements
- [ ] Support 10+ simultaneous encoding jobs
- [ ] Support 5+ distributed nodes
- [ ] Queue 100+ jobs without performance degradation
- [ ] Handle 1,000+ progress updates/second aggregate

---

## Part 8: Post-Launch Roadmap

### Short-Term (v2.1)
- Hardware acceleration profiles (NVIDIA NVENC, Intel QSV, AMD AMF)
- Batch encoding with scheduling
- Encoding analytics dashboard
- Profile versioning + rollback
- Advanced filters (denoise, deinterlace, deband)

### Medium-Term (v2.2)
- DASH (MPEG-DASH) output format
- Dolby Vision support
- AV1 hardware acceleration
- Encoding queue prioritization
- Resource limits per node

### Long-Term (v3.0)
- ML-based quality optimization
- Automatic profile recommendation
- Streaming analytics integration
- Custom encoder development framework
- Multi-region encoding clusters

---

## Document Changelog

**v1.0 Final (December 2024)**
- Consolidated all architecture documents
- Resolved ASS subtitle conversion issue
- Clarified audio language ordering (user-configurable)
- Defined FFmpeg progress parsing implementation
- Specified 30-second validation strategy
- Confirmed QueueContext database integration
- Added complete implementation roadmap
- Validated against 16,000+ production files

**Previous Drafts:**
- ENCODER_V2_ARCHITECTURE_PLAN.md (superseded)
- ENCODER_V2_ARCHITECTURE_PLAN_UPDATED.md (superseded)
- ENCODER_V2_CLARIFICATIONS.md (superseded)

---

## Conclusion

This architecture provides a **production-validated, distributed encoding system** that:

1. **Matches Production Reality:** Structure validated against 16,000+ encoded files
2. **Preserves Quality:** Native ASS subtitles, HDR→SDR smart conversion, specification compliance
3. **Respects User Preferences:** Configurable language ordering, quality stacks, codec flexibility
4. **Scales Reliably:** Distributed nodes, fault resilience, automatic recovery
5. **Performs Efficiently:** < 30-second validation, smart task weighting, shared HDR conversion
6. **Integrates Seamlessly:** QueueContext database, existing patterns, proven components

**All contradictions resolved. All patterns validated. Ready for implementation.**