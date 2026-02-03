# NoMercy EncoderV2 - Product Requirements Document

**Version:** 1.0
**Status:** Draft
**Last Updated:** February 2026
**Author:** NoMercy Development Team

---

## Table of Contents

1. [Executive Summary](#1-executive-summary)
2. [Problem Statement](#2-problem-statement)
3. [Goals & Non-Goals](#3-goals--non-goals)
4. [User Stories](#4-user-stories)
5. [Functional Requirements](#5-functional-requirements)
6. [Technical Requirements](#6-technical-requirements)
7. [Architecture Overview](#7-architecture-overview)
8. [Implementation Phases](#8-implementation-phases)
9. [Success Criteria](#9-success-criteria)
10. [Out of Scope / Future Roadmap](#10-out-of-scope--future-roadmap)

---

## 1. Executive Summary

### 1.1 Purpose

NoMercy EncoderV2 is a **complete architectural rewrite** of the media encoding system for NoMercy MediaServer. It addresses critical bugs, architectural debt, and scalability limitations in the original encoder while adding distributed encoding capabilities.

### 1.2 Vision

A **distributed, specification-compliant video encoding system** designed for:
- Production-scale media processing (validated against 16,000+ existing encoded files)
- Distributed encoding across multiple nodes
- Complete HLS/MP4/MKV specification compliance
- User-defined quality profiles and codec flexibility
- Zero codec restrictions (any FFmpeg-supported codec allowed)

### 1.3 Core Principles

| Principle | Description |
|-----------|-------------|
| **Specification Compliance** | HLS (RFC 8216), MP4 (ISO 14496), MKV (Matroska) |
| **Production-Proven Structure** | Output format validated against 16,000+ existing files |
| **Smart Distribution** | Heavy operations (HDR→SDR) executed once, shared across qualities |
| **Fault Resilience** | Nodes survive server restarts; server survives node restarts |
| **User Control** | Complete codec/quality/language control via profiles |
| **Database Integration** | QueueContext for all encoder state (unified with existing queue system) |
| **Complete Decoupling** | EncoderV2 must NOT reference or depend on the old NoMercy.Encoder project |

### 1.4 Decoupling Requirement

**CRITICAL:** The new EncoderV2 system must be **completely independent** from the legacy `NoMercy.Encoder` project.

- The old encoder (`src/NoMercy.Encoder/`) serves only as a **code reference** during development
- EncoderV2 must implement its own:
  - FFprobe wrapper for media analysis
  - FFmpeg command execution
  - Stream/format DTOs
  - All utility classes
- Once EncoderV2 is complete, the entire `NoMercy.Encoder` project will be **removed**
- Any shared functionality should be extracted to `NoMercy.NmSystem` or implemented fresh in EncoderV2

---

## 2. Problem Statement

### 2.1 Critical Bugs in Encoder V1

#### **BUG-001: Aspect Ratio Corruption (CRITICAL)**
- **Location:** `Classes.cs` line 58
- **Issue:** `AspectRatioValue => Crop.H / Crop.W;` performs integer division
- **Impact:** Aspect ratio always evaluates to 0 or 1, causing 480p videos to have distorted dimensions
- **Root Cause:** Missing explicit cast to `double`

#### **BUG-002: Scale Calculation Chaos (CRITICAL)**
- **Issue:** Scale values are calculated/overridden in **4+ different locations**:
  1. Profile stage in `BuildVideoStreams()`
  2. `VideoAudioFile.AddContainer()` after crop detection
  3. `GetFullCommand()` lines 140-148 (upscaling prevention)
  4. `GetFullCommand()` lines 150-156 (second upscaling check)
  5. `GetFullCommand()` lines 269-275 (third upscaling check)
  6. `GetFullCommand()` lines 278-283 (downscaling threshold)
- **Impact:** Unpredictable output dimensions, especially at lower resolutions

### 2.2 Architectural Issues

| Issue | Severity | Description |
|-------|----------|-------------|
| **Tangled Responsibilities** | HIGH | `VideoAudioFile.cs` handles video, audio, image, AND subtitle processing |
| **500+ Line Methods** | HIGH | `GetFullCommand()` is unmaintainable spaghetti code |
| **No Testability** | MEDIUM | Static methods, tight coupling, no dependency injection |
| **Inconsistent Error Handling** | MEDIUM | Mix of exceptions, null returns, and silent failures |
| **Poor Extensibility** | MEDIUM | Adding a new codec requires changes in 5+ files |

### 2.3 Business Impact

- **Encoding failures** require manual intervention and re-encoding
- **Quality inconsistencies** across video library
- **Developer time wasted** debugging scale/aspect ratio issues
- **No distributed scaling** capability for large media libraries

---

## 3. Goals & Non-Goals

### 3.1 Goals

| ID | Goal | Priority |
|----|------|----------|
| G1 | Fix all aspect ratio and scale calculation bugs | P0 |
| G2 | Produce output matching existing 16,000+ file structure | P0 |
| G3 | Support HLS, MP4, and MKV container formats | P0 |
| G4 | Enable user-configurable quality profiles | P1 |
| G5 | Support distributed encoding across multiple nodes | P1 |
| G6 | Preserve ASS subtitles natively (never auto-convert) | P1 |
| G7 | Support user-defined audio language ordering | P1 |
| G8 | Perform HDR→SDR conversion once, share across qualities | P1 |
| G9 | Validate output in < 30 seconds | P2 |
| G10 | Support hardware acceleration (NVENC, QSV, VideoToolbox) | P2 |

### 3.2 Non-Goals

| ID | Non-Goal | Rationale |
|----|----------|-----------|
| NG1 | Real-time streaming/transcoding | Focus is on file-based encoding |
| NG2 | Cloud-based encoding (AWS, GCP) | Self-hosted focus |
| NG3 | DRM/encryption | Not in current scope |
| NG4 | Machine learning quality optimization | Future roadmap item |
| NG5 | Automatic profile recommendation | Future roadmap item |

---

## 4. User Stories

### 4.1 Media Administrator

| ID | User Story | Acceptance Criteria |
|----|------------|---------------------|
| US1 | As an admin, I want to encode videos in multiple quality levels so users can stream at their connection speed | Multiple quality variants (1080p, 720p, 480p) generated from single source |
| US2 | As an admin, I want to set Japanese as the default audio language for anime libraries | Audio language ordering is configurable per profile |
| US3 | As an admin, I want to preserve anime subtitle styling | ASS subtitles extracted natively, not converted to VTT |
| US4 | As an admin, I want to encode HDR content with SDR fallback | SDR variants generated automatically for HDR sources |
| US5 | As an admin, I want to distribute encoding across multiple machines | Tasks distributed to available nodes based on capabilities |

### 4.2 End User

| ID | User Story | Acceptance Criteria |
|----|------------|---------------------|
| US6 | As a user, I want videos to play without aspect ratio distortion | All encoded videos maintain correct aspect ratio |
| US7 | As a user, I want to see encoding progress in real-time | Progress updates displayed via SignalR hub |
| US8 | As a user, I want thumbnail previews when seeking | Sprite sheets generated with timeline timing |

### 4.3 Developer

| ID | User Story | Acceptance Criteria |
|----|------------|---------------------|
| US9 | As a developer, I want to add new codecs easily | Adding a codec requires only one new class file |
| US10 | As a developer, I want to unit test encoding logic | All components mockable via interfaces |

---

## 5. Functional Requirements

### 5.1 Profile Management

| ID | Requirement | Priority |
|----|-------------|----------|
| FR1 | System SHALL support user-defined encoding profiles | P0 |
| FR2 | Profiles SHALL specify container format (HLS, MP4, MKV) | P0 |
| FR3 | Profiles SHALL specify video codec and quality settings | P0 |
| FR4 | Profiles SHALL specify audio codec, bitrate, and allowed languages | P0 |
| FR5 | Profiles SHALL specify subtitle handling (preserve ASS, convert to VTT) | P1 |
| FR6 | System SHALL provide default profiles for common use cases | P1 |
| FR7 | Profile changes SHALL NOT affect in-progress encoding jobs | P0 |

### 5.2 Video Encoding

| ID | Requirement | Priority |
|----|-------------|----------|
| FR8 | System SHALL support H.264, H.265, AV1, and VP9 codecs | P0 |
| FR9 | System SHALL support hardware acceleration (NVENC, QSV, VideoToolbox) | P2 |
| FR10 | System SHALL preserve source aspect ratio in all outputs | P0 |
| FR11 | System SHALL NOT upscale video (only downscale or copy) | P0 |
| FR12 | System SHALL detect and preserve HDR metadata | P1 |
| FR13 | System SHALL convert HDR→SDR when requested (one-time, shared) | P1 |

### 5.3 Audio Encoding

| ID | Requirement | Priority |
|----|-------------|----------|
| FR14 | System SHALL support AAC, Opus, AC3, E-AC3, FLAC codecs | P0 |
| FR15 | System SHALL respect user-defined language ordering | P0 |
| FR16 | First language in user's list SHALL be marked as DEFAULT in HLS | P0 |
| FR17 | System SHALL extract all audio tracks matching allowed languages | P1 |

### 5.4 Subtitle Handling

| ID | Requirement | Priority |
|----|-------------|----------|
| FR18 | System SHALL extract ASS/SSA subtitles in NATIVE format | P0 |
| FR19 | System SHALL NEVER automatically convert ASS to VTT | P0 |
| FR20 | System SHALL optionally convert SRT to WebVTT | P1 |
| FR21 | System SHALL extract all fonts from ASS/SSA streams | P1 |
| FR22 | System SHALL generate fonts.json manifest with MIME types | P1 |

### 5.5 Output Structure

| ID | Requirement | Priority |
|----|-------------|----------|
| FR23 | HLS output SHALL comply with RFC 8216 | P0 |
| FR24 | MP4 output SHALL comply with ISO 14496 | P0 |
| FR25 | MKV output SHALL comply with Matroska specification | P0 |
| FR26 | Output directory structure SHALL match production format | P0 |
| FR27 | System SHALL generate master playlist with all variants | P0 |
| FR28 | System SHALL include COLOR-SPACE metadata in HLS variants | P1 |

### 5.6 Post-Processing

| ID | Requirement | Priority |
|----|-------------|----------|
| FR29 | System SHALL generate thumbnail sprite sheets | P1 |
| FR30 | System SHALL generate sprite timing VTT file | P1 |
| FR31 | System SHALL extract chapter markers to chapters.vtt | P1 |
| FR32 | System SHALL validate output within 30 seconds | P2 |

### 5.7 Distributed Encoding

| ID | Requirement | Priority |
|----|-------------|----------|
| FR33 | System SHALL support multiple encoder nodes | P1 |
| FR34 | System SHALL distribute tasks based on node capabilities | P1 |
| FR35 | System SHALL reassign tasks from failed/offline nodes | P1 |
| FR36 | System SHALL perform health checks every 30 seconds | P1 |
| FR37 | Nodes SHALL survive server restarts (continue encoding) | P1 |
| FR38 | Server SHALL survive node restarts (reassign tasks) | P1 |

---

## 6. Technical Requirements

### 6.1 Performance

| ID | Requirement | Target |
|----|-------------|--------|
| TR1 | 1080p 24-minute episode encoding time (single node, GPU) | < 30 minutes |
| TR2 | Progress update latency | < 1 second |
| TR3 | API response time | < 200ms |
| TR4 | Database query time | < 100ms |
| TR5 | Output validation time | < 30 seconds |
| TR6 | Task assignment latency | < 5 seconds |

### 6.2 Scalability

| ID | Requirement | Target |
|----|-------------|--------|
| TR7 | Simultaneous encoding jobs | 10+ |
| TR8 | Distributed encoder nodes | 5+ |
| TR9 | Queued jobs without degradation | 100+ |
| TR10 | Aggregate progress updates per second | 1,000+ |

### 6.3 Reliability

| ID | Requirement | Description |
|----|-------------|-------------|
| TR11 | Failed tasks SHALL retry up to 3 times | Automatic retry with backoff |
| TR12 | Task failures SHALL NOT crash entire job | Fault isolation |
| TR13 | Network interruptions SHALL be handled gracefully | Reconnection logic |
| TR14 | Zombie processes SHALL be cleaned up automatically | Process monitoring |

### 6.4 Code Quality

| ID | Requirement | Target |
|----|-------------|--------|
| TR15 | Unit test coverage | > 80% |
| TR16 | No static methods for business logic | 0 static business methods |
| TR17 | All services injectable via DI | 100% injectable |
| TR18 | All public APIs documented | 100% documented |

---

## 7. Architecture Overview

### 7.1 Component Diagram

```
NoMercy.EncoderV2/                    # Shared Library (Server + Nodes)
├── Abstractions/                     # Interfaces and contracts
│   ├── IEncodingPipeline.cs         # Job submission and tracking
│   ├── IMediaAnalyzer.cs            # Media file analysis
│   ├── IFFmpegExecutor.cs           # FFmpeg process execution
│   ├── ICodec.cs                    # Codec interfaces (Video/Audio/Subtitle)
│   ├── IContainer.cs                # Container format interfaces
│   └── IEncodingProfile.cs          # Profile configuration
├── Codecs/                          # Codec implementations
│   ├── Video/                       # H264, H265, AV1, VP9 + hardware variants
│   ├── Audio/                       # AAC, Opus, AC3, FLAC
│   └── Subtitle/                    # ASS, VTT, SRT
├── Containers/                      # Container implementations
│   ├── HlsContainer.cs              # HLS with segmentation
│   ├── Mp4Container.cs              # MP4 with FastStart
│   └── MkvContainer.cs              # Matroska
├── Services/                        # Core services
│   ├── FFmpegExecutor.cs            # Process execution with progress
│   ├── MediaAnalyzer.cs             # FFprobe-based analysis
│   └── HardwareAccelerationDetector.cs
├── Pipeline/                        # Job orchestration
│   └── EncodingPipeline.cs          # Async job management
├── Profiles/                        # Profile system
│   ├── EncodingProfile.cs           # Profile implementation
│   ├── ProfileRegistry.cs           # Provider registry
│   └── SystemProfiles.cs            # Built-in profiles
├── Factories/                       # Object creation
│   ├── CodecFactory.cs              # Codec instantiation
│   └── ContainerFactory.cs          # Container instantiation
└── DependencyInjection/             # DI registration
    └── ServiceCollectionExtensions.cs
```

### 7.2 Data Flow

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
      ▼                      ▼                    ▼
┌──────────────────┐ ┌──────────────────┐ ┌──────────────────┐
│ Local Encoder    │ │ Encoder Node #1  │ │ Encoder Node #2  │
│ (Server GPU/CPU) │ │ (RTX 4090)       │ │ (RTX 3080)       │
└──────────────────┘ └──────────────────┘ └──────────────────┘
```

### 7.3 Database Schema (QueueContext)

```
EncodingProfiles
├── Id (PK)
├── Name
├── Container
├── VideoProfileJson
├── AudioProfileJson
├── SubtitleProfileJson
├── QualitiesJson
├── IsDefault
├── CreatedAt / UpdatedAt / DeletedAt

EncodingJobs
├── Id (PK)
├── ProfileId (FK → EncodingProfiles, nullable)
├── ProfileSnapshotJson (immutable at creation)
├── InputFilePath
├── OutputFolder
├── State (queued/encoding/completed/failed/cancelled)
├── ErrorMessage
├── CreatedAt / StartedAt / CompletedAt

EncodingTasks
├── Id (PK)
├── JobId (FK → EncodingJobs)
├── TaskType (HDRConversion/VideoEncoding/AudioEncoding/etc.)
├── Weight (CPU/time estimate)
├── State (pending/running/completed/failed)
├── AssignedNodeId (FK → EncoderNodes)
├── DependenciesJson
├── RetryCount
├── ErrorMessage

EncoderNodes
├── Id (PK)
├── Name
├── IpAddress / Port
├── HasGPU / GPUModel
├── CPUCores / MemoryGB
├── IsHealthy
├── LastHeartbeat

EncodingProgress
├── Id (PK)
├── TaskId (FK → EncodingTasks)
├── ProgressPercentage
├── Fps / Speed / Bitrate
├── CurrentTime / EstimatedRemaining
├── RecordedAt
```

---

## 8. Implementation Phases

### Phase 1: Foundation (Weeks 1-2)
**Goal:** Database + Core Models + Basic Profile Management

- [ ] Create QueueContext migration for EncoderV2 tables
- [ ] Implement EncodingProfile, EncodingJob, EncodingTask entities
- [ ] Build ProfileManager with CRUD operations
- [ ] Create default profiles (HLS, MP4, MKV variants)
- [ ] Implement profile validation
- [ ] Add StreamAnalyzer using existing FFprobe integration

**Deliverables:**
- Database tables operational
- Profile CRUD API functional
- Default profiles loadable
- Stream analysis produces correct metadata

### Phase 2: Specifications & Validation (Weeks 3-4)
**Goal:** Container specs + Codec configs + Validation

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

### Phase 3: Stream Processing (Weeks 5-6)
**Goal:** Video/Audio/Subtitle processing + Font extraction

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
- [ ] Implement ChapterProcessor
- [ ] Implement SpriteGenerator

**Deliverables:**
- Video scaling produces correct dimensions
- Audio language ordering works correctly
- ASS subtitles extracted natively (not converted)
- Fonts extracted and organized
- Sprites generated with timing metadata

### Phase 4: FFmpeg Integration (Weeks 7-8)
**Goal:** Command building + Execution + Progress tracking

- [ ] Implement FFmpegCommandBuilder
- [ ] Implement HDRProcessor (HDR→SDR tonemap)
- [ ] Implement ProgressMonitor (FFmpeg `-progress -` parsing)
- [ ] Implement EncodingJobExecutor
- [ ] Implement PostProcessor

**Deliverables:**
- FFmpeg commands are syntactically valid
- Progress parsing extracts all metrics
- HDR→SDR conversion shares output correctly
- Jobs complete successfully
- Output structure matches production format

### Phase 5: Task Distribution (Weeks 9-10)
**Goal:** Multi-node support + Load balancing

- [ ] Implement TaskSplitter (Job → Task decomposition)
- [ ] Implement NodeSelector (capability matching)
- [ ] Implement JobDispatcher (queue integration)
- [ ] Implement NodeHealthMonitor (30-second health checks)

**Deliverables:**
- Jobs split into correct tasks
- Tasks assigned to best available node
- Node failures handled gracefully
- Load balancing distributes work evenly

### Phase 6: Node Implementation (Weeks 11-12)
**Goal:** Standalone encoder node application

- [ ] Create EncoderNode project
- [ ] Implement NodeRegistration (phone-home + mDNS)
- [ ] Implement node-side ProgressEmitter
- [ ] Implement NodeCapabilities (GPU/CPU detection)

**Deliverables:**
- Node registers with server successfully
- Node executes tasks correctly
- Progress updates reach server
- Nodes and server survive mutual restarts

### Phase 7: API & Integration (Weeks 13-14)
**Goal:** REST API + SignalR + UI integration

- [ ] Create ProfileController (CRUD endpoints)
- [ ] Create EncodingController (job submission)
- [ ] Create ProgressHub (SignalR real-time updates)
- [ ] Integrate with existing Queue system
- [ ] Add API documentation (Swagger)

**Deliverables:**
- All API endpoints functional
- SignalR progress updates work
- Queue integration seamless
- API documentation complete

### Phase 8: Testing & Validation (Weeks 15-16)
**Goal:** Comprehensive testing + Production validation

- [ ] Unit tests (profile validation, stream processing, command generation)
- [ ] Integration tests (end-to-end encoding, multi-node distribution)
- [ ] Production validation (encode test files from 16,000+ library)
- [ ] Performance benchmarking
- [ ] Stress testing (100+ concurrent jobs)

**Deliverables:**
- 80%+ code coverage
- All critical paths tested
- Production validation successful
- Documentation complete

---

## 9. Success Criteria

### 9.1 Functional Success Criteria

| ID | Criterion | Validation Method |
|----|-----------|-------------------|
| SC1 | Encodes match exact production output structure | Compare against 16,000+ existing files |
| SC2 | HLS playlists comply with RFC 8216 | HLS validator tool |
| SC3 | MP4 files comply with ISO 14496 | MP4Box validation |
| SC4 | MKV files comply with Matroska specification | MKVToolNix validation |
| SC5 | ASS subtitles preserved natively | Manual inspection |
| SC6 | Audio language ordering respects user preferences | Automated test |
| SC7 | HDR→SDR conversion executes once per job | Task count verification |
| SC8 | Fonts extracted with manifest | File existence check |
| SC9 | Sprites generated with timing | VTT parsing |
| SC10 | Validation completes in < 30 seconds | Timer measurement |
| SC11 | Profile changes don't break queued jobs | Integration test |

### 9.2 Performance Success Criteria

| ID | Criterion | Target | Measurement |
|----|-----------|--------|-------------|
| SC12 | 1080p 24-min episode encoding | < 30 min (GPU) | Stopwatch |
| SC13 | Progress update latency | < 1 second | Network trace |
| SC14 | API response time | < 200ms | Load test |
| SC15 | Database queries | < 100ms | Query profiler |

### 9.3 Reliability Success Criteria

| ID | Criterion | Test Method |
|----|-----------|-------------|
| SC16 | Nodes survive server restarts | Kill server mid-encode, verify node continues |
| SC17 | Server survives node restarts | Kill node mid-encode, verify task reassignment |
| SC18 | Failed tasks retry 3 times | Inject failure, count retries |
| SC19 | Task failures isolated | Fail one task, verify others continue |

---

## 10. Out of Scope / Future Roadmap

### 10.1 Out of Scope for V2.0

| Item | Rationale |
|------|-----------|
| Real-time transcoding | Different use case, different architecture |
| Cloud encoding (AWS/GCP) | Self-hosted focus |
| DRM/encryption | Security feature for future |
| Dolby Vision | Complex HDR format, needs research |
| DASH output | HLS covers most use cases |

### 10.2 Future Roadmap

#### V2.1 (Short-term)
- Hardware acceleration profile presets
- Batch encoding with scheduling
- Encoding analytics dashboard
- Profile versioning + rollback
- Advanced filters (denoise, deinterlace, deband)

#### V2.2 (Medium-term)
- DASH (MPEG-DASH) output format
- Dolby Vision support
- AV1 hardware acceleration
- Encoding queue prioritization
- Resource limits per node

#### V3.0 (Long-term)
- ML-based quality optimization
- Automatic profile recommendation
- Streaming analytics integration
- Custom encoder plugin framework
- Multi-region encoding clusters

---

## Appendix A: Output Directory Structure

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
│   └── NotoSansJapanese.otf
├── thumbs_{width}x{height}/                  # Thumbnail sprites
│   ├── thumbs_{width}x{height}-0000.jpg
│   └── thumbs_{width}x{height}-0001.jpg
├── thumbs_{width}x{height}.webp              # Merged sprite sheet
├── thumbs_{width}x{height}.vtt               # Sprite timing metadata
├── chapters.vtt                              # Chapter markers
└── fonts.json                                # Font manifest with MIME types
```

---

## Appendix B: Current Implementation Status

As of February 2026, EncoderV2 is approximately **70% complete**:

### Fully Implemented
- Abstraction layer (7 interface files)
- Video codecs (H264, H265, AV1, VP9 + hardware variants)
- Audio codecs (AAC, Opus, AC3, FLAC)
- Containers (HLS, MP4, MKV)
- Services (FFmpegExecutor, MediaAnalyzer)
- Pipeline (EncodingPipeline with async job management)
- Profiles (EncodingProfile, ProfileRegistry, SystemProfiles)
- Factories (CodecFactory, ContainerFactory)
- Dependency Injection (ServiceCollectionExtensions)
- Unit tests (17 test files, 1525+ lines)

### Pending Implementation
- Database integration (QueueContext tables)
- Task execution system
- Output specification validation
- Stream processors
- Distributed node support
- REST API controllers
- SignalR progress hub

---

## Appendix C: Glossary

| Term | Definition |
|------|------------|
| **HLS** | HTTP Live Streaming (RFC 8216) - Apple's adaptive streaming protocol |
| **HDR** | High Dynamic Range - Video with extended color/brightness range |
| **SDR** | Standard Dynamic Range - Traditional video color range |
| **Tonemap** | Process of converting HDR content to SDR |
| **ASS/SSA** | Advanced SubStation Alpha - Rich subtitle format with styling |
| **VTT** | WebVTT - Web Video Text Tracks subtitle format |
| **CRF** | Constant Rate Factor - Quality-based encoding mode |
| **NVENC** | NVIDIA Video Encoder - GPU-accelerated encoding |
| **QSV** | Intel Quick Sync Video - Hardware video encoding |
| **Profile** | Collection of encoding settings (codec, quality, languages) |
| **Node** | Distributed encoding worker machine |

---

*Document End*
